using System.Collections.Concurrent;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;

namespace CydLan.Core;

/// <summary>A peer endpoint. Every instance listens locally; there is no broker or relay.</summary>
public sealed class LanService : IAsyncDisposable
{
    private readonly AppSettings settings;
    private readonly CancellationTokenSource stopping = new();
    private readonly RSA rsa = RSA.Create(2048);
    private readonly object rsaGate = new();
    private readonly object sessionGate = new();
    private readonly ConcurrentDictionary<string, Session> sessions = new();
    private readonly ConcurrentDictionary<string, byte> connecting = new();
    private readonly ConcurrentDictionary<string, FileTransfer> transfers = new();
    private readonly ConcurrentDictionary<string, TaskCompletionSource<bool>> pending = new();
    private readonly ConcurrentDictionary<string, byte> received = new();
    private readonly ConcurrentDictionary<TcpClient, byte> clients = new();
    private readonly List<IPAddress> interfaces = [];
    private TcpListener? listener;
    private UdpClient? udp;
    private long sequence = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    public int ListeningPort { get; private set; }
    public string LocalAddress { get; private set; } = "127.0.0.1";
    public event Action<Peer>? PeerOnline;
    public event Action<string>? PeerOffline;
    public event Action<ChatMessage>? MessageReceived;
    public event Action<string, string>? TypingChanged;
    public event Action<FileTransfer>? TransferChanged;
    public event Action<string>? Diagnostic;

    public LanService(AppSettings settings) => this.settings = settings;
    public async Task StartAsync(bool discovery = true, IPAddress? bindAddress = null)
    {
        if (listener is not null) throw new InvalidOperationException("Already started.");
        listener = new(bindAddress ?? IPAddress.Any, settings.Port);
        listener.Start();
        ListeningPort = ((IPEndPoint)listener.LocalEndpoint).Port;
        interfaces.AddRange(NetworkInterface.GetAllNetworkInterfaces()
            .Where(n => n.OperationalStatus == OperationalStatus.Up && n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
            .SelectMany(n => n.GetIPProperties().UnicastAddresses)
            .Where(a => a.Address.AddressFamily == AddressFamily.InterNetwork).Select(a => a.Address));
        LocalAddress = interfaces.FirstOrDefault()?.ToString() ?? "127.0.0.1";
        _ = AcceptLoopAsync();
        if (discovery)
        {
            try
            {
                udp = new(AddressFamily.InterNetwork);
                udp.ExclusiveAddressUse = false;
                udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                udp.Client.Bind(new IPEndPoint(IPAddress.Any, settings.Port));
                udp.EnableBroadcast = true;
                udp.MulticastLoopback = false;
                udp.Client.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastTimeToLive, 1);
                var group = IPAddress.Parse(settings.Multicast);
                foreach (var ip in interfaces)
                {
                    try { udp.JoinMulticastGroup(group, ip); }
                    catch (SocketException e) { Diagnostic?.Invoke($"Discovery unavailable on {ip}: {e.Message}"); }
                }
                _ = DiscoveryLoopAsync();
                await AnnounceAsync();
            }
            catch { await DisposeAsync(); throw; }
        }
        Diagnostic?.Invoke($"Listening on TCP {ListeningPort} · discovery UDP {settings.Port}");
        _ = HeartbeatLoopAsync();
    }

    public async Task AnnounceAsync()
    {
        if (udp is null) return;
        var bytes = Encode("announce", null, new() { ["tcpport"] = ListeningPort.ToString() });
        foreach (var ip in interfaces.DefaultIfEmpty(IPAddress.Any))
        {
            try
            {
                if (!ip.Equals(IPAddress.Any)) udp.Client.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastInterface, ip.GetAddressBytes());
                await udp.SendAsync(bytes, new IPEndPoint(IPAddress.Parse(settings.Multicast), settings.Port), stopping.Token);
            }
            catch (SocketException e) { Diagnostic?.Invoke($"Discovery send failed: {e.Message}"); }
        }
    }

    private async Task DiscoveryLoopAsync()
    {
        while (!stopping.IsCancellationRequested)
        {
            try
            {
                var packet = await udp!.ReceiveAsync(stopping.Token);
                var message = LegacyProtocol.Decode(packet.Buffer);
                if (message.From == settings.UserId || (message.To.Length > 0 && message.To != settings.UserId)) continue;
                if (message.Type == "depart") { if (sessions.TryGetValue(message.From, out var departed)) departed.Client.Dispose(); continue; }
                if (message.Type != "announce" || sessions.ContainsKey(message.From)) continue;
                var port = int.TryParse(message.Get("tcpport"), out var p) && p is > 0 and <= 65535 ? p : settings.Port;
                _ = ConnectDiscoveredAsync(message.From, packet.RemoteEndPoint.Address.ToString(), port);
            }
            catch (OperationCanceledException) { break; }
            catch (ObjectDisposedException) { break; }
            catch (Exception e) when (e is SocketException or System.Xml.XmlException or InvalidDataException) { Diagnostic?.Invoke($"Ignored discovery packet: {e.Message}"); }
        }
    }

    private async Task ConnectDiscoveredAsync(string id, string address, int port)
    {
        try { await ConnectAsync(id, address, port); }
        catch (Exception e) when (e is SocketException or IOException or OperationCanceledException or CryptographicException)
        { if (!stopping.IsCancellationRequested) Diagnostic?.Invoke($"Could not connect to {address}: {e.Message}"); }
    }

    public async Task ConnectAsync(string peerId, string address, int port)
    {
        if (peerId == settings.UserId || sessions.ContainsKey(peerId) || !connecting.TryAdd(peerId, 0)) return;
        var client = new TcpClient { NoDelay = true };
        clients.TryAdd(client, 0);
        try
        {
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(stopping.Token);
            deadline.CancelAfter(TimeSpan.FromSeconds(10));
            await client.ConnectAsync(address, port, deadline.Token);
            await client.GetStream().WriteAsync(Encoding.ASCII.GetBytes("MSG" + settings.UserId), deadline.Token);
            var session = new Session(peerId, address, port, client, false);
            lock (sessionGate)
            {
                if (!sessions.TryAdd(peerId, session)) { client.Dispose(); clients.TryRemove(client, out _); return; }
            }
            _ = RunSessionAsync(session, false);
            await session.Ready.Task.WaitAsync(deadline.Token);
        }
        catch { client.Dispose(); clients.TryRemove(client, out _); throw; }
        finally { connecting.TryRemove(peerId, out _); }
    }

    private async Task AcceptLoopAsync()
    {
        while (!stopping.IsCancellationRequested)
        {
            try
            {
                var client = await listener!.AcceptTcpClientAsync(stopping.Token);
                client.NoDelay = true;
                if (clients.Count >= 128) { client.Dispose(); continue; }
                clients.TryAdd(client, 0);
                _ = AcceptClientAsync(client);
            }
            catch (OperationCanceledException) { break; }
            catch (ObjectDisposedException) { break; }
            catch (SocketException e) { if (!stopping.IsCancellationRequested) Diagnostic?.Invoke(e.Message); }
        }
    }

    private async Task AcceptClientAsync(TcpClient client)
    {
        bool owned = true;
        try
        {
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(stopping.Token);
            deadline.CancelAfter(TimeSpan.FromSeconds(10));
            var stream = client.GetStream();
            var prefix = new byte[3];
            await stream.ReadExactlyAsync(prefix, deadline.Token);
            if (Encoding.ASCII.GetString(prefix) == "MSG")
            {
                var id = await ReadIdentityAsync(stream, deadline.Token);
                if (id == settings.UserId) return;
                var session = new Session(id, ((IPEndPoint)client.Client.RemoteEndPoint!).Address.ToString(), settings.Port, client, true);
                lock (sessionGate)
                {
                    if (sessions.TryGetValue(id, out var existing))
                    {
                        // Resolve simultaneous connections consistently: the lower ID
                        // initiates. Never replace a session that is already encrypted.
                        if (existing.Cipher is not null || existing.Incoming || string.CompareOrdinal(settings.UserId, id) < 0) return;
                        sessions[id] = session;
                        existing.Client.Dispose();
                    }
                    else if (!sessions.TryAdd(id, session)) return;
                }
                owned = false;
                await RunSessionAsync(session, true);
            }
            else if (Encoding.ASCII.GetString(prefix) == "FIL")
            {
                var header = new byte[33];
                await stream.ReadExactlyAsync(header, deadline.Token);
                if (header[0] != (byte)'E') return;
                var id = Encoding.ASCII.GetString(header, 1, 32);
                var peer = await ReadIdentityAsync(stream, deadline.Token);
                if (!transfers.TryGetValue(peer + ":" + id, out var transfer) || transfer.Outgoing || transfer.State != "Accepted") return;
                var remote = ((IPEndPoint)client.Client.RemoteEndPoint!).Address.ToString();
                if (!sessions.TryGetValue(peer, out var session) || session.Address != remote) return;
                await ReceiveFileAsync(stream, transfer);
            }
        }
        catch (Exception e) when (e is IOException or SocketException or OperationCanceledException or CryptographicException or InvalidDataException or ObjectDisposedException)
        { if (!stopping.IsCancellationRequested) Diagnostic?.Invoke($"Connection closed: {e.Message}"); }
        finally { if (owned) { client.Dispose(); clients.TryRemove(client, out _); } }
    }

    private static async Task<string> ReadIdentityAsync(NetworkStream stream, CancellationToken ct)
    {
        // The legacy identity has no length or terminator. Peers wait for PUBKEY
        // after writing it. An idle interval is the only available delimiter.
        using var bytes = new MemoryStream();
        var buffer = new byte[62];
        while (bytes.Length < 62)
        {
            using var idle = CancellationTokenSource.CreateLinkedTokenSource(ct);
            if (bytes.Length > 0) idle.CancelAfter(120);
            int count;
            try { count = await stream.ReadAsync(buffer.AsMemory(0, 62 - (int)bytes.Length), idle.Token); }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested && bytes.Length > 0) { break; }
            if (count == 0) throw new EndOfStreamException();
            bytes.Write(buffer, 0, count);
        }
        var id = Encoding.ASCII.GetString(bytes.ToArray());
        if (id.Length is < 1 or > 61 || id.Any(c => c < 33 || c > 126)) throw new InvalidDataException("Invalid peer identity.");
        return id;
    }

    private async Task RunSessionAsync(Session session, bool incoming)
    {
        using var handshake = CancellationTokenSource.CreateLinkedTokenSource(stopping.Token);
        handshake.CancelAfter(TimeSpan.FromSeconds(10));
        try
        {
            if (incoming)
            {
                string publicKey;
                lock (rsaGate) publicKey = rsa.ExportRSAPublicKeyPem();
                await session.WriteAsync("PUBKEY", Encoding.ASCII.GetBytes(publicKey + "\n"), handshake.Token);
            }
            while (!stopping.IsCancellationRequested)
            {
                var frame = await LegacyProtocol.ReadFrameAsync(session.Stream, session.Cipher is null ? handshake.Token : stopping.Token);
                if (frame.Type == "PUBKEY" && !incoming && session.Cipher is null)
                {
                    using var remoteKey = RSA.Create();
                    remoteKey.ImportFromPem(Encoding.ASCII.GetString(frame.Payload));
                    var material = RandomNumberGenerator.GetBytes(48);
                    var sealedMaterial = remoteKey.Encrypt(material, RSAEncryptionPadding.OaepSHA1);
                    session.Cipher = new(material);
                    CryptographicOperations.ZeroMemory(material);
                    await session.WriteAsync("HNDSHK", sealedMaterial, handshake.Token);
                    session.Ready.TrySetResult(true);
                }
                else if (frame.Type == "HNDSHK" && incoming && session.Cipher is null)
                {
                    byte[] material;
                    lock (rsaGate) material = rsa.Decrypt(frame.Payload, RSAEncryptionPadding.OaepSHA1);
                    session.Cipher = new(material);
                    CryptographicOperations.ZeroMemory(material);
                    session.Ready.TrySetResult(true);
                    await SendUserDataAsync(session, "get");
                }
                else if (frame.Type == "MESSAG" && session.Cipher is not null)
                {
                    var message = LegacyProtocol.Decode(session.Cipher.Decrypt(frame.Payload));
                    if (message.From != session.Id || (message.To.Length > 0 && message.To != settings.UserId)) throw new InvalidDataException("Peer identity mismatch.");
                    await HandleAsync(session, message);
                }
                else throw new InvalidDataException("Unexpected handshake frame.");
            }
        }
        catch (Exception e) when (e is IOException or SocketException or OperationCanceledException or CryptographicException or System.Xml.XmlException or InvalidDataException or ArgumentException or ObjectDisposedException)
        {
            session.Ready.TrySetException(new IOException("Peer handshake or connection failed.", e));
            if (!stopping.IsCancellationRequested) Diagnostic?.Invoke($"{session.Address}: {e.Message}");
        }
        finally
        {
            bool removed = sessions.TryRemove(new KeyValuePair<string, Session>(session.Id, session));
            if (removed) PeerOffline?.Invoke(session.Id);
            session.Client.Dispose(); session.Cipher?.Dispose();
            clients.TryRemove(session.Client, out _);
            if (removed)
                foreach (var item in pending.Where(p => p.Key.StartsWith(session.Id + ":", StringComparison.Ordinal)))
                    if (pending.TryRemove(item.Key, out var completion)) completion.TrySetResult(false);
        }
    }

    private async Task HandleAsync(Session session, WireMessage message)
    {
        switch (message.Type)
        {
            case "userdata":
                if (message.Get("userid") != session.Id) throw new InvalidDataException("User data identity mismatch.");
                if (message.Get("queryop") == "get") await SendUserDataAsync(session, "result");
                PeerOnline?.Invoke(new() { Id = session.Id, Name = Limit(message.Get("name"), 100), Address = session.Address,
                    Port = session.Port, Version = Limit(message.Get("version"), 80), Status = message.Get("status"), Online = true });
                break;
            case "ping":
            case "message":
                await SendAsync(session.Id, "acknowledge", new() { ["messageid"] = message.Id });
                if (message.Type == "message" && received.TryAdd(session.Id + ":" + message.Id, 0))
                {
                    if (received.Count > 10000) received.Clear();
                    MessageReceived?.Invoke(new() { Id = message.Id, PeerId = session.Id, Text = Limit(message.Get("message"), 32000) });
                }
                break;
            case "acknowledge":
                if (pending.TryRemove(session.Id + ":" + message.Get("messageid"), out var completion)) completion.TrySetResult(true);
                break;
            case "broadcast":
                MessageReceived?.Invoke(new() { Id = message.Id, PeerId = session.Id, Text = Limit(message.Get("broadcast"), 32000), Broadcast = true });
                break;
            case "status":
            case "name":
                // Re-fetch user data to keep updates in one normalized event.
                await SendUserDataAsync(session, "get");
                break;
            case "chatstate": TypingChanged?.Invoke(session.Id, message.Get("chatstate") == "composing" ? "Typing…" : ""); break;
            case "file": await HandleFileAsync(session, message); break;
            case "avatar":
            case "folder":
                if (message.Get("fileop") == "request") await SendAsync(session.Id, message.Type, new() {
                    ["fileop"] = "decline", ["fileid"] = message.Get("fileid"), ["filetype"] = message.Get("filetype"), ["mode"] = "receive" });
                break;
            case "query":
                if (message.Get("queryop") == "get") await SendAsync(session.Id, "query", new() {
                    ["queryop"] = "result", ["messageid"] = message.Id, ["name"] = settings.DisplayName,
                    ["host"] = Environment.MachineName, ["os"] = "Windows", ["logon"] = Environment.UserName });
                break;
        }
    }

    public async Task<bool> SendChatAsync(string peerId, string text, string id)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Length > 32000) throw new ArgumentException("Messages must contain 1–32,000 characters.");
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var key = peerId + ":" + id;
        if (!pending.TryAdd(key, completion)) throw new InvalidOperationException("Duplicate message identifier.");
        try
        {
            await SendAsync(peerId, "message", new() { ["message"] = text }, id);
            return await completion.Task.WaitAsync(TimeSpan.FromSeconds(10), stopping.Token);
        }
        catch (TimeoutException) { return false; }
        finally { pending.TryRemove(key, out _); }
    }

    public Task SendTypingAsync(string peerId, bool composing) => SendAsync(peerId, "chatstate", new() { ["chatstate"] = composing ? "composing" : "paused" });
    public async Task<int> BroadcastAsync(string text)
    {
        int count = 0;
        foreach (var session in sessions.Values.Where(s => s.Cipher is not null))
        {
            try { await SendAsync(session.Id, "broadcast", new() { ["broadcast"] = text }); count++; }
            catch (Exception e) when (e is IOException or SocketException) { Diagnostic?.Invoke($"Broadcast to {session.Address} failed: {e.Message}"); }
        }
        return count;
    }
    public async Task UpdateProfileAsync()
    {
        foreach (var session in sessions.Values.Where(s => s.Cipher is not null))
        {
            await SendAsync(session.Id, "name", new() { ["name"] = settings.DisplayName });
            await SendAsync(session.Id, "status", new() { ["status"] = settings.Status });
        }
    }
    private async Task HeartbeatLoopAsync()
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        try
        {
            while (await timer.WaitForNextTickAsync(stopping.Token))
                await Task.WhenAll(sessions.Values.Where(s => s.Cipher is not null).Select(CheckSessionAsync));
        }
        catch (OperationCanceledException) { }
    }
    private async Task CheckSessionAsync(Session session)
    {
        var id = Interlocked.Increment(ref sequence).ToString();
        var key = session.Id + ":" + id;
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        pending[key] = completion;
        try
        {
            await SendAsync(session.Id, "ping", [], id);
            if (!await completion.Task.WaitAsync(TimeSpan.FromSeconds(10), stopping.Token)) session.Client.Dispose();
        }
        catch (Exception e) when (e is TimeoutException or IOException or SocketException or OperationCanceledException) { session.Client.Dispose(); }
        finally { pending.TryRemove(key, out _); }
    }
    private Task SendUserDataAsync(Session session, string operation) => SendAsync(session.Id, "userdata", new() {
        ["userid"] = settings.UserId, ["name"] = settings.DisplayName, ["address"] = LocalAddress,
        ["version"] = "CydLan 0.1", ["status"] = settings.Status, ["note"] = "", ["usercaps"] = "1", ["queryop"] = operation });
    private byte[] Encode(string type, string? to, Dictionary<string, string> body, string? id = null) =>
        LegacyProtocol.Encode(type, settings.UserId, to, id ?? Interlocked.Increment(ref sequence).ToString(), body);
    private async Task SendAsync(string id, string type, Dictionary<string, string> body, string? messageId = null)
    {
        if (!sessions.TryGetValue(id, out var session) || session.Cipher is null) throw new IOException("This person is offline.");
        // Serialize encryption and frame writes together so concurrent sends cannot interleave.
        await session.WriteGate.WaitAsync(stopping.Token);
        try { await LegacyProtocol.WriteFrameAsync(session.Stream, "MESSAG", session.Cipher.Encrypt(Encode(type, id, body, messageId)), stopping.Token); }
        catch (ObjectDisposedException e) { throw new IOException("The peer disconnected.", e); }
        finally { session.WriteGate.Release(); }
    }
    private static string Limit(string value, int max) => value.Length > max ? value[..max] : value;

    public async Task<FileTransfer> OfferFileAsync(string peerId, string path)
    {
        var info = new FileInfo(path);
        if (!info.Exists) throw new FileNotFoundException("File does not exist.", path);
        var transfer = new FileTransfer { Id = Guid.NewGuid().ToString("N").ToUpperInvariant(), PeerId = peerId, Name = info.Name, Size = info.Length, Outgoing = true, Path = path };
        transfers[peerId + ":" + transfer.Id] = transfer;
        await SendAsync(peerId, "file", FileBody(transfer, "request"));
        TransferChanged?.Invoke(transfer);
        return transfer;
    }
    public async Task AcceptFileAsync(FileTransfer transfer, string destination)
    {
        if (transfer.Outgoing || transfer.State != "Waiting for approval") throw new InvalidOperationException("This offer is no longer available.");
        transfer.Path = destination; transfer.State = "Accepted";
        var body = FileBody(transfer, "accept"); body["tcpport"] = ListeningPort.ToString();
        await SendAsync(transfer.PeerId, "file", body);
        TransferChanged?.Invoke(transfer);
    }
    public async Task DeclineFileAsync(FileTransfer transfer)
    {
        await SendAsync(transfer.PeerId, "file", FileBody(transfer, "decline"));
        transfer.State = "Declined"; TransferChanged?.Invoke(transfer);
    }
    private static Dictionary<string, string> FileBody(FileTransfer t, string operation) => new() {
        ["fileid"] = t.Id, ["mode"] = t.Outgoing ? "send" : "receive", ["filetype"] = "normal",
        ["fileop"] = operation, ["filename"] = t.Name, ["filesize"] = t.Size.ToString() };
    private async Task HandleFileAsync(Session session, WireMessage message)
    {
        var id = message.Get("fileid");
        if (id.Length != 32 || !id.All(Uri.IsHexDigit)) return;
        var key = session.Id + ":" + id;
        if (message.Get("fileop") == "request")
        {
            if (message.Get("filetype") != "normal" || !long.TryParse(message.Get("filesize"), out var size) || size < 0 || transfers.Count >= 1000) return;
            var name = System.IO.Path.GetFileName(message.Get("filename").Replace('\\', '/'));
            if (string.IsNullOrWhiteSpace(name) || name.Length > 240 || name.IndexOfAny(System.IO.Path.GetInvalidFileNameChars()) >= 0) return;
            var transfer = new FileTransfer { Id = id, PeerId = session.Id, Name = name, Size = size };
            if (transfers.TryAdd(key, transfer)) TransferChanged?.Invoke(transfer);
        }
        else if (transfers.TryGetValue(key, out var transfer))
        {
            if (message.Get("fileop") == "accept" && transfer.Outgoing && transfer.State == "Waiting for approval")
            {
                transfer.State = "Connecting"; TransferChanged?.Invoke(transfer);
                var port = int.TryParse(message.Get("tcpport"), out var p) && p is > 0 and <= 65535 ? p : session.Port;
                _ = SendFileAsync(session, transfer, port);
            }
            else if (message.Get("fileop") is "decline" or "cancel" or "abort")
            { transfer.State = "Declined / cancelled"; TransferChanged?.Invoke(transfer); }
        }
        await Task.CompletedTask;
    }
    private async Task SendFileAsync(Session session, FileTransfer transfer, int port)
    {
        using var client = new TcpClient { NoDelay = true };
        try
        {
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(stopping.Token);
            deadline.CancelAfter(TimeSpan.FromSeconds(30));
            await client.ConnectAsync(session.Address, port, deadline.Token);
            var stream = client.GetStream();
            await stream.WriteAsync(Encoding.ASCII.GetBytes("FILE" + transfer.Id + settings.UserId), deadline.Token);
            var start = new byte[5]; await stream.ReadExactlyAsync(start, deadline.Token);
            if (Encoding.ASCII.GetString(start) != "START") throw new InvalidDataException("Invalid transfer response.");
            transfer.State = "Transferring";
            using var file = File.OpenRead(transfer.Path);
            if (file.Length != transfer.Size) throw new IOException("The file changed after it was offered.");
            var buffer = new byte[65536]; long sent = 0; int read;
            while ((read = await file.ReadAsync(buffer, stopping.Token)) > 0)
            {
                deadline.CancelAfter(TimeSpan.FromSeconds(30));
                await stream.WriteAsync(buffer.AsMemory(0, read), deadline.Token);
                sent += read; transfer.Progress = transfer.Size == 0 ? 100 : sent * 100d / transfer.Size;
                TransferChanged?.Invoke(transfer);
            }
            // Legacy file streams have no application-level completion receipt.
            client.Client.Shutdown(SocketShutdown.Send);
            transfer.Progress = 100; transfer.State = "Sent · receipt unavailable"; TransferChanged?.Invoke(transfer);
        }
        catch (Exception e) when (e is IOException or SocketException or OperationCanceledException or UnauthorizedAccessException)
        { transfer.State = "Failed: " + e.Message; TransferChanged?.Invoke(transfer); }
    }
    private async Task ReceiveFileAsync(NetworkStream stream, FileTransfer transfer)
    {
        // Write to a unique sibling and move only after the exact advertised size.
        var partial = transfer.Path + "." + transfer.Id + ".partial";
        try
        {
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(stopping.Token);
            deadline.CancelAfter(TimeSpan.FromSeconds(30));
            await using (var file = new FileStream(partial, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, true))
            {
                await stream.WriteAsync("START"u8.ToArray(), deadline.Token);
                transfer.State = "Transferring";
                var buffer = new byte[65536]; long remaining = transfer.Size;
                while (remaining > 0)
                {
                    deadline.CancelAfter(TimeSpan.FromSeconds(30));
                    int read = await stream.ReadAsync(buffer.AsMemory(0, (int)Math.Min(buffer.Length, remaining)), deadline.Token);
                    if (read == 0) throw new EndOfStreamException("The transfer ended early.");
                    await file.WriteAsync(buffer.AsMemory(0, read), deadline.Token);
                    remaining -= read; transfer.Progress = (transfer.Size - remaining) * 100d / transfer.Size;
                    TransferChanged?.Invoke(transfer);
                }
            }
            File.Move(partial, transfer.Path, false);
            transfer.Progress = 100; transfer.State = "Received"; TransferChanged?.Invoke(transfer);
        }
        catch (Exception e) when (e is IOException or SocketException or OperationCanceledException or UnauthorizedAccessException)
        {
            if (File.Exists(partial)) File.Delete(partial);
            transfer.State = "Failed: " + e.Message; TransferChanged?.Invoke(transfer);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (stopping.IsCancellationRequested) return;
        if (udp is not null)
        {
            try { await udp.SendAsync(Encode("depart", null, []), new IPEndPoint(IPAddress.Parse(settings.Multicast), settings.Port)); }
            catch (Exception e) when (e is SocketException or ObjectDisposedException) { }
        }
        await stopping.CancelAsync();
        udp?.Dispose(); listener?.Stop();
        foreach (var client in clients.Keys) client.Dispose();
        foreach (var value in pending.Values) value.TrySetResult(false);
        // The gate also protects any handshake that was already processing.
        lock (rsaGate) rsa.Dispose();
    }

    private sealed class Session(string id, string address, int port, TcpClient client, bool incoming)
    {
        public string Id { get; } = id;
        public string Address { get; } = address;
        public int Port { get; } = port;
        public TcpClient Client { get; } = client;
        public bool Incoming { get; } = incoming;
        public NetworkStream Stream { get; } = client.GetStream();
        public LegacyCipher? Cipher { get; set; }
        public SemaphoreSlim WriteGate { get; } = new(1);
        public TaskCompletionSource<bool> Ready { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task WriteAsync(string type, byte[] payload, CancellationToken ct)
        {
            await WriteGate.WaitAsync(ct);
            try { await LegacyProtocol.WriteFrameAsync(Stream, type, payload, ct); }
            finally { WriteGate.Release(); }
        }
    }
}
