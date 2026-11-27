using System.Collections.Concurrent;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;

namespace CydLan.Core;

/// <summary>A peer endpoint. Every instance listens locally; there is no broker or relay.</summary>
public sealed partial class LanService : IAsyncDisposable
{
    private readonly AppSettings settings;
    private readonly DiagnosticLog? log;
    private readonly CancellationTokenSource stopping = new();
    private readonly RSA rsa = RSA.Create(2048);
    private readonly object rsaGate = new();
    private readonly object sessionGate = new();
    private readonly ConcurrentDictionary<string, Session> sessions = new();
    private readonly ConcurrentDictionary<string, byte> connecting = new();
    private readonly ConcurrentDictionary<string, (string Address, int Port, int Failures, long RetryAt)> retryDelays = new();
    private readonly ConcurrentDictionary<string, FileTransfer> transfers = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, ActiveFileTransfer> activeTransfers = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, TaskCompletionSource<bool>> pending = new();
    private readonly ConcurrentDictionary<string, byte> received = new();
    private readonly ConcurrentDictionary<TcpClient, byte> clients = new();
    private readonly List<IPAddress> interfaces = [];
    private readonly HashSet<IPAddress> joinedInterfaces = [];
    private readonly ConcurrentDictionary<string, (string Address, int Port)> knownPeers = new();
    private readonly SemaphoreSlim discoveryGate = new(1);
    private IPAddress? boundAddress;
    private bool multicastEnabled;
    private TcpListener? listener;
    private UdpClient? udp;
    private long sequence = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    public int ListeningPort { get; private set; }
    public int DiscoveryPort { get; private set; }
    public string LocalAddress { get; private set; } = "127.0.0.1";
    public event Action<Peer>? PeerOnline;
    public event Action<string>? PeerOffline;
    public event Action<ChatMessage>? MessageReceived;
    public event Action<string, string>? TypingChanged;
    public event Action<FileTransfer>? TransferChanged;
    public event Action<string>? Diagnostic;
    /// <summary>Legacy group chat: (sender id, thread id, operation request|join|message|leave, text).</summary>
    public event Action<string, string, string, string>? GroupMessageReceived;
    /// <summary>When set, ordinary file offers are accepted automatically into this folder (LAN Messenger's "accept files automatically").</summary>
    public string? AutoAcceptDirectory { get; set; }
    /// <summary>
    /// When set, small image offers from CydLan peers that carry the inline-image extension are accepted
    /// automatically into this folder. Everything else still waits for an explicit accept.
    /// </summary>
    public string? InlineImageDirectory { get; set; }
    /// <summary>When set, contacts' profile pictures are accepted automatically into this folder; otherwise they are declined.</summary>
    public string? AvatarDirectory { get; set; }
    public const long MaxAvatarBytes = 1024 * 1024;
    /// <summary>A contact's profile picture arrived: (peer id, local path).</summary>
    public event Action<string, string>? AvatarReceived;
    /// <summary>A contact's details arrived in reply to a query: (peer id, first name, last name, about).</summary>
    public event Action<string, string, string, string>? ProfileReceived;
    public event Action<string, string>? PetCodeReceived;
    /// <summary>Companion mini-game traffic from a CydLan peer: peer id and the message fields.</summary>
    public event Action<string, IReadOnlyDictionary<string, string>>? GameMessageReceived;
    /// <summary>First CydLan build that understands companion battles.</summary>
    public static bool SupportsPetBattles(string version)
    {
        if (!IsCydLanVersion(version)) return false;
        var parts = version["CydLan".Length..].Trim().Split('.');
        return parts.Length >= 2 && int.TryParse(parts[0], out int major) && int.TryParse(parts[1], out int minor) && (major > 0 || minor >= 2);
    }
    public const long MaxInlineImageBytes = 20 * 1024 * 1024;
    public static readonly string[] InlineImageExtensions = [".png", ".jpg", ".jpeg", ".gif", ".bmp", ".webp"];
    public static bool IsInlineImageName(string name) => InlineImageExtensions.Contains(System.IO.Path.GetExtension(name).ToLowerInvariant());
    public static bool IsCydLanVersion(string version) => version.StartsWith("CydLan", StringComparison.Ordinal);
    /// <summary>
    /// Version sent in userdata. LAN Messenger compares the first three dot-separated numbers (without bounds checks)
    /// and leaves anyone at 1.2.10 or below out of chat rooms, so the wire value leads with a current legacy version.
    /// </summary>
    public const string WireVersion = "1.2.39.CydLan.0.2";
    private const string CydLanMarker = ".CydLan.";
    /// <summary>Shows a CydLan wire version as "CydLan 0.1"; other versions are unchanged.</summary>
    public static string DisplayVersion(string wire)
    {
        int marker = wire.IndexOf(CydLanMarker, StringComparison.Ordinal);
        return marker >= 0 ? "CydLan " + wire[(marker + CydLanMarker.Length)..] : wire;
    }

    public LanService(AppSettings settings, IEnumerable<Peer>? savedPeers = null, DiagnosticLog? log = null)
    {
        if (!AppSettings.IsValidUserId(settings.UserId))
            throw new ArgumentException($"User IDs must be 1–{AppSettings.MaxUserIdLength} printable ASCII characters for LAN Messenger file transfers.", nameof(settings));
        this.settings = settings;
        this.log = log;
        foreach (var peer in savedPeers ?? [])
            if (IPAddress.TryParse(peer.Address, out var ip) && ip.AddressFamily == AddressFamily.InterNetwork && peer.Port is > 0 and <= 65535)
                knownPeers[peer.Id] = (ip.ToString(), peer.Port);
    }
    public async Task StartAsync(bool discovery = true, IPAddress? bindAddress = null)
    {
        if (listener is not null) throw new InvalidOperationException("Already started.");
        log?.Write("network.start", null, ("port", settings.Port), ("discovery", discovery));
        boundAddress = bindAddress;
        listener = CreateListener(bindAddress, settings.Port);
        try { listener.Start(); }
        catch (SocketException e) when (e.SocketErrorCode is SocketError.AddressAlreadyInUse or SocketError.AccessDenied && settings.Port != 0)
        {
            listener.Stop();
            listener = CreateListener(bindAddress, 0);
            listener.Start();
            log?.Write("network.port_fallback", e, ("requestedPort", settings.Port), ("actualPort", ((IPEndPoint)listener.LocalEndpoint).Port));
            Diagnostic?.Invoke($"TCP {settings.Port} is in use. Using TCP {((IPEndPoint)listener.LocalEndpoint).Port}; discovery remains on UDP {settings.Port}.");
        }
        ListeningPort = ((IPEndPoint)listener.LocalEndpoint).Port;
        RefreshInterfaces();
        _ = AcceptLoopAsync();
        if (discovery)
        {
            try
            {
                udp = new(AddressFamily.InterNetwork);
                udp.ExclusiveAddressUse = false;
                udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                udp.Client.Bind(new IPEndPoint(bindAddress ?? IPAddress.Any, settings.Port));
                DiscoveryPort = ((IPEndPoint)udp.Client.LocalEndPoint!).Port;
                udp.EnableBroadcast = true;
                udp.MulticastLoopback = false;
                udp.Client.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastTimeToLive, 1);
                multicastEnabled = bindAddress is null || !IPAddress.IsLoopback(bindAddress);
                RefreshInterfaces();
                _ = DiscoveryLoopAsync();
                await AnnounceAsync();
                _ = DiscoveryHeartbeatAsync();
                _ = ReconnectKnownAsync();
            }
            catch { await DisposeAsync(); throw; }
        }
        Diagnostic?.Invoke($"Listening on TCP {ListeningPort} · discovery UDP {DiscoveryPort}");
        log?.Write("network.listening", null, ("address", LocalAddress), ("tcpPort", ListeningPort), ("udpPort", DiscoveryPort));
        _ = HeartbeatLoopAsync();
    }

    // LAN Messenger listens on [::] (dual-stack). A separate 0.0.0.0 listener on the same port would
    // succeed on Windows and silently take every IPv4 connection meant for it, so bind dual-stack and exclusively:
    // an occupied port then fails to bind and the fallback port is used instead.
    private static TcpListener CreateListener(IPAddress? bindAddress, int port)
    {
        if (bindAddress is null && Socket.OSSupportsIPv6)
        {
            var dual = new TcpListener(IPAddress.IPv6Any, port) { ExclusiveAddressUse = true };
            dual.Server.DualMode = true;
            return dual;
        }
        return new TcpListener(bindAddress ?? IPAddress.Any, port) { ExclusiveAddressUse = true };
    }

    private static string RemoteIPv4(TcpClient client)
    {
        var address = ((IPEndPoint)client.Client.RemoteEndPoint!).Address;
        return (address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address).ToString();
    }

    public async Task AnnounceAsync()
    {
        if (udp is null || !multicastEnabled) return;
        await discoveryGate.WaitAsync(stopping.Token);
        try
        {
            RefreshInterfaces();
            log?.Write("discovery.announce", null, ("interfaces", interfaces.Count), ("port", settings.Port));
            var bytes = Encode("announce", null, new() { ["tcpport"] = ListeningPort.ToString() });
            foreach (var ip in interfaces.DefaultIfEmpty(IPAddress.Any))
            {
                try
                {
                    if (!ip.Equals(IPAddress.Any)) udp.Client.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastInterface, ip.GetAddressBytes());
                    await udp.SendAsync(bytes, new IPEndPoint(IPAddress.Parse(settings.Multicast), settings.Port), stopping.Token);
                }
                catch (SocketException e) { log?.Write("discovery.send_failed", e, ("interface", ip)); Diagnostic?.Invoke($"Discovery send failed: {e.Message}"); }
            }
        }
        finally { discoveryGate.Release(); }
    }

    public async Task RefreshAsync() { await AnnounceAsync(); await ReconnectKnownAsync(); }

    private Task ReconnectKnownAsync() => Task.WhenAll(knownPeers
        .Where(peer => !sessions.ContainsKey(peer.Key))
        .Select(peer => ConnectDiscoveredAsync(peer.Key, peer.Value.Address, peer.Value.Port)));

    // A directed legacy announcement also works when Wi-Fi blocks multicast.
    // The remote peer learns our identity and opens the usual encrypted session.
    public async Task DiscoverAsync(IPAddress address, int port)
    {
        if (address.AddressFamily != AddressFamily.InterNetwork || port is < 1 or > 65535)
            throw new ArgumentException("Enter an IPv4 address and a UDP port between 1 and 65535.");
        if (udp is null) throw new InvalidOperationException("Discovery has not started.");
        log?.Write("discovery.directed", null, ("address", address), ("port", port));
        await udp.SendAsync(Encode("announce", null, new() { ["tcpport"] = ListeningPort.ToString() }), new IPEndPoint(address, port), stopping.Token);
    }

    private void RefreshInterfaces()
    {
        var current = boundAddress is not null ? new[] { boundAddress } : NetworkInterface.GetAllNetworkInterfaces()
            .Where(n => n.OperationalStatus == OperationalStatus.Up && n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
            .SelectMany(n => n.GetIPProperties().UnicastAddresses)
            .Where(a => a.Address.AddressFamily == AddressFamily.InterNetwork).Select(a => a.Address).Distinct().ToArray();
        if (udp is not null && multicastEnabled)
        {
            var group = IPAddress.Parse(settings.Multicast);
            foreach (var ip in joinedInterfaces.Except(current).ToArray())
            {
                try { udp.Client.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.DropMembership, new MulticastOption(group, ip)); } catch (SocketException) { }
                joinedInterfaces.Remove(ip);
            }
            foreach (var ip in current.Except(joinedInterfaces))
            {
                try { udp.JoinMulticastGroup(group, ip); joinedInterfaces.Add(ip); }
                catch (SocketException e) { log?.Write("discovery.join_failed", e, ("interface", ip)); Diagnostic?.Invoke($"Discovery unavailable on {ip}: {e.Message}"); }
            }
        }
        if (log is not null && !current.ToHashSet().SetEquals(interfaces))
            log.Write("network.interfaces_changed", null, ("addresses", string.Join(", ", current.Select(ip => ip.ToString()))));
        interfaces.Clear();
        interfaces.AddRange(current);
        LocalAddress = interfaces.FirstOrDefault()?.ToString() ?? "127.0.0.1";
    }

    private async Task DiscoveryHeartbeatAsync()
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(15));
        try { while (await timer.WaitForNextTickAsync(stopping.Token)) await RefreshAsync(); }
        catch (OperationCanceledException) { }
        catch (ObjectDisposedException) { }
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
                if (message.Type == "depart")
                {
                    if (sessions.TryGetValue(message.From, out var departed))
                    {
                        log?.Write("peer.depart", null, ("peer", departed.Id), ("address", packet.RemoteEndPoint.Address));
                        departed.Client.Dispose();
                    }
                    continue;
                }
                if (message.Type != "announce" || sessions.ContainsKey(message.From)) continue;
                var port = int.TryParse(message.Get("tcpport"), out var p) && p is > 0 and <= 65535 ? p : settings.Port;
                _ = ConnectDiscoveredAsync(message.From, packet.RemoteEndPoint.Address.ToString(), port);
            }
            catch (OperationCanceledException) { break; }
            catch (ObjectDisposedException) { break; }
            catch (Exception e) when (e is SocketException or System.Xml.XmlException or InvalidDataException) { log?.Write("discovery.packet_rejected", e); Diagnostic?.Invoke($"Ignored discovery packet: {e.Message}"); }
        }
    }

    private async Task ConnectDiscoveredAsync(string id, string address, int port)
    {
        try { await ConnectAsync(id, address, port, automatic: true); }
        catch (Exception e) when (e is SocketException or IOException or OperationCanceledException or CryptographicException or ArgumentException)
        { if (!stopping.IsCancellationRequested) Diagnostic?.Invoke($"Could not connect to {address}: {e.Message}"); }
    }

    public Task ConnectAsync(string peerId, string address, int port) => ConnectAsync(peerId, address, port, automatic: false);

    private async Task ConnectAsync(string peerId, string address, int port, bool automatic)
    {
        if (peerId == settings.UserId || sessions.ContainsKey(peerId) || !connecting.TryAdd(peerId, 0)) return;
        retryDelays.TryGetValue(peerId, out var previous);
        bool sameEndpoint = previous.Address == address && previous.Port == port;
        if (automatic && sameEndpoint && Environment.TickCount64 < previous.RetryAt)
        {
            connecting.TryRemove(peerId, out _);
            return;
        }
        log?.Write("connection.attempt", null, ("peer", peerId), ("address", address), ("port", port));
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
            retryDelays.TryRemove(peerId, out _);
        }
        catch (Exception e)
        {
            if (!stopping.IsCancellationRequested && (sameEndpoint || retryDelays.Count < 4096))
            {
                int failures = sameEndpoint ? Math.Min(previous.Failures + 1, 5) : 1;
                int seconds = Math.Min(30 << (failures - 1), 300);
                retryDelays[peerId] = (address, port, failures, Environment.TickCount64 + seconds * 1000L);
            }
            log?.Write("connection.failed", e, ("peer", peerId), ("address", address), ("port", port));
            client.Dispose();
            clients.TryRemove(client, out _);
            throw;
        }
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
            catch (SocketException e) { if (!stopping.IsCancellationRequested) { log?.Write("connection.accept_failed", e); Diagnostic?.Invoke(e.Message); } }
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
                var session = new Session(id, RemoteIPv4(client), settings.Port, client, true);
                Session? spareFor = null;
                lock (sessionGate)
                {
                    if (sessions.TryGetValue(id, out var existing))
                    {
                        if (existing.Cipher is not null)
                        {
                            // Only a peer known to be LAN Messenger gets a held spare; another CydLan never dials twice.
                            if (existing.Version.Length == 0 || IsCydLanVersion(existing.Version) || existing.Address != session.Address) return;
                            spareFor = existing;
                        }
                        // Resolve simultaneous handshakes consistently: the lower ID initiates.
                        else if (existing.Incoming || string.CompareOrdinal(settings.UserId, id) < 0) return;
                        else
                        {
                            sessions[id] = session;
                            existing.Client.Dispose();
                        }
                    }
                    else if (!sessions.TryAdd(id, session)) return;
                }
                owned = false;
                if (spareFor is not null) await RunSpareChannelAsync(spareFor, client);
                else await RunSessionAsync(session, true);
            }
            else if (Encoding.ASCII.GetString(prefix) == "FIL")
            {
                var header = new byte[33];
                await stream.ReadExactlyAsync(header, deadline.Token);
                if (header[0] != (byte)'E')
                {
                    log?.Write("transfer.stream_rejected", null, ("reason", "invalid_header"));
                    return;
                }
                var id = Encoding.ASCII.GetString(header, 1, 32);
                var peer = await ReadIdentityAsync(stream, deadline.Token);
                var remote = RemoteIPv4(client);
                if (!sessions.TryGetValue(peer, out var session) || session.Address != remote)
                {
                    log?.Write("transfer.stream_rejected", null, ("peer", peer), ("id", id), ("reason", "session_mismatch"), ("address", remote), ("sessionAddress", session?.Address));
                    return;
                }
                if (await TryReceiveFolderFileAsync(session, id, stream)) return;
                if (!transfers.TryGetValue(peer + ":" + id, out var transfer) || transfer.Outgoing || transfer.State != "Accepted")
                {
                    log?.Write("transfer.stream_rejected", null, ("peer", peer), ("id", id), ("reason", "not_accepted"), ("state", transfer?.State), ("outgoing", transfer?.Outgoing));
                    return;
                }
                await ReceiveFileAsync(client, stream, transfer);
            }
        }
        catch (Exception e) when (e is IOException or SocketException or OperationCanceledException or CryptographicException or InvalidDataException or ObjectDisposedException)
        { if (!stopping.IsCancellationRequested) { log?.Write("connection.greeting_failed", e); Diagnostic?.Invoke($"Connection closed: {e.Message}"); } }
        finally { if (owned) { client.Dispose(); clients.TryRemove(client, out _); } }
    }

    private async Task<string> ReadIdentityAsync(NetworkStream stream, CancellationToken ct)
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
        var raw = bytes.ToArray();
        var id = Encoding.ASCII.GetString(raw);
        // Legacy LAN Messenger appends the Windows logon name, which can contain spaces.
        bool unsupportedBytes = raw.Any(value => value is < 32 or > 126);
        if (raw.Length > 61 || unsupportedBytes || string.IsNullOrWhiteSpace(id))
        {
            log?.Write("connection.identity_rejected", null, ("bytes", raw.Length), ("unsupportedBytes", unsupportedBytes));
            throw new InvalidDataException("Invalid peer identity.");
        }
        return id;
    }

    /// <summary>
    /// LAN Messenger 1.2.39 dials a contact again when our announcement arrives while it has not yet (or no longer)
    /// listed us. It removes the contact when any of its connections to us closes, keeps one key per contact, and
    /// sends through its newest connection. So a second connection is held open without a handshake, and frames
    /// arriving on it are read with the established session's key. It closes with that session.
    /// </summary>
    private async Task RunSpareChannelAsync(Session owner, TcpClient client)
    {
        bool added;
        lock (owner.Spares) { added = owner.Spares.Count < 8; if (added) owner.Spares.Add(client); }
        log?.Write("connection.spare_open", null, ("peer", owner.Id), ("address", owner.Address), ("accepted", added));
        try
        {
            if (!added || !sessions.TryGetValue(owner.Id, out var current) || current != owner) return;
            var stream = client.GetStream();
            while (!stopping.IsCancellationRequested)
            {
                var frame = await LegacyProtocol.ReadFrameAsync(stream, stopping.Token);
                if (frame.Type != "MESSAG" || owner.Cipher is not { } cipher) continue;
                var message = LegacyProtocol.Decode(cipher.Decrypt(frame.Payload));
                if (message.From != owner.Id) throw new InvalidDataException("Peer identity mismatch.");
                if (message.To.Length == 0 || message.To == settings.UserId) await HandleAsync(owner, message);
            }
        }
        catch (Exception e) when (e is IOException or SocketException or OperationCanceledException or CryptographicException or System.Xml.XmlException or InvalidDataException or ArgumentException or ObjectDisposedException)
        { if (!stopping.IsCancellationRequested) log?.Write("connection.spare_failed", e, ("peer", owner.Id)); }
        finally
        {
            lock (owner.Spares) owner.Spares.Remove(client);
            client.Dispose();
            clients.TryRemove(client, out _);
            log?.Write("connection.spare_closed", null, ("peer", owner.Id));
        }
    }

    private async Task RunSessionAsync(Session session, bool incoming)
    {
        log?.Write("connection.open", null, ("peer", session.Id), ("address", session.Address),
            ("localEndpoint", session.LocalEndpoint), ("incoming", incoming));
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
                    log?.Write("connection.encrypted", null, ("peer", session.Id));
                }
                else if (frame.Type == "HNDSHK" && incoming && session.Cipher is null)
                {
                    byte[] material;
                    lock (rsaGate) material = rsa.Decrypt(frame.Payload, RSAEncryptionPadding.OaepSHA1);
                    session.Cipher = new(material);
                    CryptographicOperations.ZeroMemory(material);
                    session.Ready.TrySetResult(true);
                    log?.Write("connection.encrypted", null, ("peer", session.Id));
                    await SendUserDataAsync(session, "get");
                }
                else if (frame.Type == "MESSAG" && session.Cipher is not null)
                {
                    var message = LegacyProtocol.Decode(session.Cipher.Decrypt(frame.Payload));
                    if (message.From != session.Id) throw new InvalidDataException("Peer identity mismatch.");
                    // A copy addressed to someone else is skipped; it does not end an otherwise valid session.
                    if (message.To.Length > 0 && message.To != settings.UserId) Diagnostic?.Invoke($"{session.Address}: ignored a message addressed to another user.");
                    else await HandleAsync(session, message);
                }
                else throw new InvalidDataException("Unexpected handshake frame.");
            }
        }
        catch (Exception e) when (e is IOException or SocketException or OperationCanceledException or CryptographicException or System.Xml.XmlException or InvalidDataException or ArgumentException or ObjectDisposedException)
        {
            session.Ready.TrySetException(new IOException("Peer handshake or connection failed.", e));
            if (!stopping.IsCancellationRequested) log?.Write("connection.session_failed", e, ("peer", session.Id), ("address", session.Address));
            if (!stopping.IsCancellationRequested) Diagnostic?.Invoke($"{session.Address}: {e.Message}");
        }
        finally
        {
            bool removed = sessions.TryRemove(new KeyValuePair<string, Session>(session.Id, session));
            log?.Write("connection.closed", null, ("peer", session.Id), ("address", session.Address), ("shutdown", stopping.IsCancellationRequested), ("superseded", !removed));
            if (removed) PeerOffline?.Invoke(session.Id);
            StopFolderTransfers(session, "Interrupted");
            if (removed) StopFileTransfers(session.Id, "Interrupted");
            lock (session.Spares) foreach (var spare in session.Spares) spare.Dispose();
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
                if (int.TryParse(message.Get("tcpport"), out int peerPort) && peerPort is > 0 and <= 65535) session.Port = peerPort;
                knownPeers[session.Id] = (session.Address, session.Port);
                retryDelays.TryRemove(session.Id, out _);
                session.Version = DisplayVersion(Limit(message.Get("version"), 80));
                session.SupportsFolders = uint.TryParse(message.Get("usercaps"), out uint capabilities) && (capabilities & 4) != 0;
                if (message.Get("queryop") == "get") await SendUserDataAsync(session, "result");
                PeerOnline?.Invoke(new() { Id = session.Id, Name = Limit(message.Get("name"), 100), Address = session.Address,
                    Port = session.Port, Version = session.Version, Status = message.Get("status"), Online = true,
                    FirstName = Limit(message.Get("firstname"), 100), LastName = Limit(message.Get("lastname"), 100), About = Limit(message.Get("about"), 1000),
                    PetCode = IsCydLanVersion(session.Version) ? CompanionProfileCodec.SanitizeWireCode(message.Get("cydpet")) : "" });
                if (!session.Introduced)
                {
                    // LAN Messenger only shares first name, last name and About in reply to a query, and
                    // exchanges profile pictures as avatar offers once a connection is up.
                    session.Introduced = true;
                    if (!IsCydLanVersion(session.Version)) EnableTcpKeepAlive(session);
                    log?.Write("peer.online", null, ("peer", session.Id), ("address", session.Address), ("version", session.Version));
                    _ = IntroduceAsync(session.Id);
                }
                break;
            case "ping":
            case "message":
                await SendAsync(session.Id, "acknowledge", new() { ["messageid"] = message.Id });
                if (message.Type == "message" && received.TryAdd(session.Id + ":" + message.Id, 0))
                {
                    if (received.Count > 10000) received.Clear();
                    MessageReceived?.Invoke(new() { Id = message.Id, PeerId = session.Id, Text = Limit(message.Get("message"), MaxMessageChars) });
                }
                break;
            case "acknowledge":
                if (pending.TryRemove(session.Id + ":" + message.Get("messageid"), out var completion)) completion.TrySetResult(true);
                break;
            case "broadcast":
                if (received.TryAdd(session.Id + ":broadcast:" + message.Id, 0))
                    MessageReceived?.Invoke(new() { Id = message.Id, PeerId = session.Id, Text = Limit(message.Get("broadcast"), MaxMessageChars), Broadcast = true });
                break;
            case "status":
            case "name":
                // Re-fetch user data to keep updates in one normalized event.
                await SendUserDataAsync(session, "get");
                break;
            case "chatstate": TypingChanged?.Invoke(session.Id, message.Get("chatstate") == "composing" ? "Typing…" : ""); break;
            case "file":
                if (message.Get("filetype") == "folder") HandleFolderFile(session, message);
                else await HandleFileAsync(session, message);
                break;
            case "groupmessage":
                // Group chat is not acknowledged by LAN Messenger, so neither side waits for a receipt.
                var thread = message.Get("thread"); var op = message.Get("groupmsgop");
                if (thread.Length is > 0 and <= 200 && !thread.Any(char.IsControl) && op is "request" or "join" or "message" or "leave"
                    && (op != "message" || received.TryAdd(session.Id + ":group:" + message.Id, 0)))
                    GroupMessageReceived?.Invoke(session.Id, thread, op, op == "message" ? Limit(message.Get("message"), MaxMessageChars) : "");
                break;
            case "avatar": await HandleAvatarAsync(session, message); break;
            case "cydgame":
                if (IsCydLanVersion(session.Version)) HandleGame(session, message);
                break;
            case "folder": await HandleFolderAsync(session, message); break;
            case "query":
                // LAN Messenger's "Get Information" window reads every one of these fields, including status.
                if (message.Get("queryop") == "get") await SendAsync(session.Id, "query", new() {
                    ["queryop"] = "result", ["messageid"] = message.Id, ["userid"] = settings.UserId, ["name"] = settings.DisplayName,
                    ["address"] = LocalAddress, ["version"] = WireVersion, ["status"] = settings.Status, ["note"] = "",
                    ["logon"] = Environment.UserName, ["host"] = Environment.MachineName, ["os"] = "Windows",
                    ["firstname"] = settings.FirstName, ["lastname"] = settings.LastName, ["about"] = settings.About,
                    ["cydpet"] = CompanionProfileCodec.Encode(settings) });
                else if (message.Get("queryop") == "result")
                {
                    // LAN Messenger fills blank fields with "N/A".
                    static string Field(WireMessage m, string name, int max) { var v = m.Get(name).Trim(); return v == "N/A" ? "" : (v.Length > max ? v[..max] : v); }
                    ProfileReceived?.Invoke(session.Id, Field(message, "firstname", 100), Field(message, "lastname", 100), Field(message, "about", 1000));
                    if (IsCydLanVersion(session.Version))
                        PetCodeReceived?.Invoke(session.Id, CompanionProfileCodec.SanitizeWireCode(message.Get("cydpet")));
                }
                break;
        }
    }

    public const int MaxMessageChars = 14_000_000;

    public async Task<bool> SendChatAsync(string peerId, string text, string id)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Length > MaxMessageChars) throw new ArgumentException("Messages must contain 1–14,000,000 characters.");
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var key = peerId + ":" + id;
        if (!pending.TryAdd(key, completion)) throw new InvalidOperationException("Duplicate message identifier.");
        try
        {
            // Retry with the SAME ID so lost receipts do not duplicate messages.
            for (int attempt = 0; attempt < 3; attempt++)
            {
                log?.Write(attempt == 0 ? "message.send" : "message.retry", null, ("peer", peerId), ("id", id), ("attempt", attempt + 1));
                await SendAsync(peerId, "message", new() { ["message"] = text }, id);
                try {
                    bool delivered = await completion.Task.WaitAsync(TimeSpan.FromSeconds(3), stopping.Token);
                    log?.Write(delivered ? "message.delivered" : "message.unconfirmed", null, ("peer", peerId), ("id", id));
                    return delivered;
                }
                catch (TimeoutException) when (attempt < 2) { }
            }
            return false;
        }
        catch (TimeoutException) { log?.Write("message.unconfirmed", null, ("peer", peerId), ("id", id)); return false; }
        finally { pending.TryRemove(key, out _); }
    }

    /// <summary>Sends one legacy group-chat operation to one participant (LAN Messenger addresses group messages per person).</summary>
    public Task SendGroupAsync(string peerId, string thread, string operation, string text = "")
    {
        var body = new Dictionary<string, string> { ["thread"] = thread, ["groupmsgop"] = operation };
        if (operation == "message") body["message"] = text;
        return SendAsync(peerId, "groupmessage", body);
    }
    public Task SendTypingAsync(string peerId, bool composing) => SendAsync(peerId, "chatstate", new() { ["chatstate"] = composing ? "composing" : "paused" });
    public async Task<int> BroadcastAsync(string text)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Length > MaxMessageChars) throw new ArgumentException("Announcements must contain 1–14,000,000 characters.");
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
                await CheckPeersAsync();
        }
        catch (OperationCanceledException) { }
    }
    internal Task CheckPeersAsync() => Task.WhenAll(sessions.Values
        .Where(s => s.Cipher is not null && IsCydLanVersion(s.Version))
        .Select(CheckSessionAsync));

    private void EnableTcpKeepAlive(Session session)
    {
        // Legacy peers may omit protocol ping receipts on otherwise usable connections.
        // Let TCP detect a dead transport while still handling departures and socket closes normally.
        try
        {
            var socket = session.Client.Client;
            socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
            socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveTime, 60);
            socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveInterval, 10);
            socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveRetryCount, 3);
        }
        catch (Exception e) when (e is SocketException or ObjectDisposedException or PlatformNotSupportedException)
        {
            log?.Write("connection.keepalive_configuration_failed", e, ("peer", session.Id));
        }
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
        catch (Exception e) when (e is TimeoutException or IOException or SocketException or OperationCanceledException) { if (!stopping.IsCancellationRequested) log?.Write("heartbeat.failed", e, ("peer", session.Id)); session.Client.Dispose(); }
        finally { pending.TryRemove(key, out _); }
    }
    private Task SendUserDataAsync(Session session, string operation) => SendAsync(session.Id, "userdata", new() {
        ["userid"] = settings.UserId, ["name"] = settings.DisplayName, ["address"] = LocalAddress,
        // UC_File | UC_GroupMessage | UC_Folder
        ["version"] = WireVersion, ["status"] = settings.Status, ["note"] = "", ["usercaps"] = "7", ["queryop"] = operation, ["tcpport"] = ListeningPort.ToString(),
        ["firstname"] = settings.FirstName, ["lastname"] = settings.LastName, ["about"] = settings.About,
        ["cydpet"] = CompanionProfileCodec.Encode(settings) });
    private byte[] Encode(string type, string? to, Dictionary<string, string> body, string? id = null) =>
        LegacyProtocol.Encode(type, settings.UserId, to, id ?? Interlocked.Increment(ref sequence).ToString(), body);
    private Task SendAsync(string id, string type, Dictionary<string, string> body, string? messageId = null)
    {
        if (!sessions.TryGetValue(id, out var session)) throw new IOException("This person is offline.");
        return SendAsync(session, type, body, messageId);
    }
    private async Task SendAsync(Session session, string type, Dictionary<string, string> body, string? messageId = null)
    {
        if (session.Cipher is not { } cipher || !sessions.TryGetValue(session.Id, out var current) || current != session)
            throw new IOException("This person is offline.");
        // Serialize encryption and frame writes together so concurrent sends cannot interleave.
        await session.WriteGate.WaitAsync(stopping.Token);
        try { await LegacyProtocol.WriteFrameAsync(session.Stream, "MESSAG", cipher.Encrypt(Encode(type, session.Id, body, messageId)), stopping.Token); }
        catch (ObjectDisposedException e) { throw new IOException("The peer disconnected.", e); }
        finally { session.WriteGate.Release(); }
    }
    private static string Limit(string value, int max) => value.Length > max ? value[..max] : value;

    /// <param name="inlineImage">Ask a CydLan receiver to show this image in the conversation. Legacy clients ignore the hint.</param>
    public async Task<FileTransfer> OfferFileAsync(string peerId, string path, bool inlineImage = false)
    {
        var info = new FileInfo(path);
        if (!info.Exists) throw new FileNotFoundException("File does not exist.", path);
        inlineImage = inlineImage && info.Length <= MaxInlineImageBytes && IsInlineImageName(info.Name);
        var transfer = new FileTransfer { Id = Guid.NewGuid().ToString("N").ToUpperInvariant(), PeerId = peerId, Name = info.Name, Size = info.Length, Outgoing = true, Path = path, Inline = inlineImage };
        return await OfferTransferAsync(transfer);
    }

    public async Task<FileTransfer> OfferFolderAsync(string peerId, string path)
    {
        if (!sessions.TryGetValue(peerId, out var session)) throw new IOException("This person is offline.");
        var source = new DirectoryInfo(path);
        if (session.SupportsFolders)
        {
            var entries = await Task.Run(() => ReadFolder(source, stopping.Token), stopping.Token);
            if (entries.Count > 0) return await OfferNativeFolderAsync(session, source, entries);
        }
        var archive = await Task.Run(() => FolderArchive.CreateAsync(source, stopping.Token), stopping.Token);
        try
        {
            var info = new FileInfo(archive.Path);
            return await OfferTransferAsync(new FileTransfer {
                Id = Guid.NewGuid().ToString("N").ToUpperInvariant(), PeerId = peerId, Name = info.Name,
                Size = info.Length, Outgoing = true, Path = source.FullName, Archive = archive
            });
        }
        catch
        {
            archive.Dispose();
            throw;
        }
    }

    private async Task<FileTransfer> OfferTransferAsync(FileTransfer transfer)
    {
        string key = transfer.PeerId + ":" + transfer.Id;
        transfers[key] = transfer;
        try { await SendAsync(transfer.PeerId, "file", FileBody(transfer, "request")); }
        catch { transfers.TryRemove(key, out _); throw; }
        TransferChanged?.Invoke(transfer);
        return transfer;
    }
    public async Task AcceptFileAsync(FileTransfer transfer, string destination)
    {
        if (transfer.Outgoing || transfer.State != "Waiting for approval") throw new InvalidOperationException("This offer is no longer available.");
        if (transfer.Folder)
        {
            AcceptNativeFolder(transfer, destination);
            return;
        }
        string key = transfer.PeerId + ":" + transfer.Id;
        if (!transfers.TryGetValue(key, out var active) || !ReferenceEquals(active, transfer))
            throw new InvalidOperationException("This offer is no longer available.");
        transfer.Path = destination; transfer.State = "Accepted";
        var body = FileBody(transfer, "accept"); body["tcpport"] = ListeningPort.ToString();
        try { await SendAsync(transfer.PeerId, "file", body); }
        catch { transfer.State = "Waiting for approval"; throw; }
        TransferChanged?.Invoke(transfer);
        _ = ExpireAcceptedAsync(key, transfer);
    }
    /// <summary>Withdraws a pending offer or cancels an active transfer.</summary>
    public async Task CancelFileAsync(FileTransfer transfer)
    {
        if (transfer.Folder) { await CancelNativeFolderAsync(transfer); return; }
        string key = transfer.PeerId + ":" + transfer.Id;
        if (transfer.Outgoing && transfer.State == "Waiting for approval")
        {
            if (!transfers.TryRemove(new KeyValuePair<string, FileTransfer>(key, transfer)))
                throw new InvalidOperationException("Only a pending or active transfer can be cancelled.");
            try { await SendAsync(transfer.PeerId, "file", FileBody(transfer, "cancel")); }
            catch (Exception e) when (e is IOException or SocketException or OperationCanceledException) { }
            transfer.State = "Cancelled"; Report(transfer);
            ReleaseFolderArchive(transfer);
            return;
        }
        if (transfer.State is not ("Accepted" or "Connecting" or "Transferring"))
            throw new InvalidOperationException("Only a pending or active transfer can be cancelled.");

        transfers.TryRemove(new KeyValuePair<string, FileTransfer>(key, transfer));
        activeTransfers.TryGetValue(key, out var control);
        if (control is null)
        {
            transfer.State = "Cancelled";
            Report(transfer);
            ReleaseFolderArchive(transfer);
            try { await SendAsync(transfer.PeerId, "file", FileBody(transfer, "abort")).WaitAsync(TimeSpan.FromSeconds(1)); }
            catch (Exception e) when (e is IOException or SocketException or OperationCanceledException or TimeoutException) { }
            return;
        }
        control.MarkStopping("Cancelled");
        try
        {
            try { await SendAsync(transfer.PeerId, "file", FileBody(transfer, "abort")).WaitAsync(TimeSpan.FromMilliseconds(300)); }
            catch (Exception e) when (e is IOException or SocketException or OperationCanceledException or TimeoutException) { }
        }
        finally { control.Cancel(); }
        await control.Finished.Task;
    }
    public async Task DeclineFileAsync(FileTransfer transfer)
    {
        if (transfer.Outgoing || transfer.State != "Waiting for approval") return;
        if (transfer.Folder) { await AnswerNativeFolderAsync(transfer, "decline", "Declined"); return; }
        string key = transfer.PeerId + ":" + transfer.Id;
        transfers.TryRemove(key, out _);
        try { await SendAsync(transfer.PeerId, "file", FileBody(transfer, "decline")); }
        catch (Exception e) when (e is IOException or SocketException or OperationCanceledException) { }
        transfer.State = "Declined"; TransferChanged?.Invoke(transfer);
    }
    private async Task ExpireAcceptedAsync(string key, FileTransfer transfer)
    {
        try { await Task.Delay(TimeSpan.FromSeconds(30), stopping.Token); }
        catch (OperationCanceledException) { return; }
        if (transfer.State == "Accepted" && transfers.TryRemove(new KeyValuePair<string, FileTransfer>(key, transfer)))
        {
            transfer.State = "Failed: Transfer timed out";
            Report(transfer);
        }
    }
    private void StopFileTransfers(string peerId, string reason)
    {
        foreach (var pair in transfers.ToArray())
        {
            var transfer = pair.Value;
            if (transfer.PeerId != peerId) continue;
            if (transfer.Avatar) { transfers.TryRemove(pair); continue; }
            if (transfer.State is not ("Waiting for approval" or "Accepted" or "Connecting")) continue;
            if (!transfers.TryRemove(pair)) continue;
            if (activeTransfers.TryGetValue(pair.Key, out var control)) control.TryStop(reason);
            ReleaseFolderArchive(transfer);
            transfer.State = reason;
            TransferChanged?.Invoke(transfer);
        }
    }
    // ---------------- Profile pictures (LAN Messenger "avatar" messages) ----------------
    // Offer: type avatar, filetype avatar, fileop request, mode send. The receiver accepts automatically and the
    // picture then travels over the normal FILE stream. Received pictures are stored under our own name.
    private async Task IntroduceAsync(string peerId)
    {
        try
        {
            await SendAsync(peerId, "query", new() { ["queryop"] = "get" });
            await SendAvatarAsync(peerId);
        }
        catch (Exception e) when (e is IOException or OperationCanceledException or ObjectDisposedException) { }
    }
    /// <summary>Offers your profile picture to one contact, or to everyone connected when <paramref name="peerId"/> is null.</summary>
    public async Task SendAvatarAsync(string? peerId = null)
    {
        var path = settings.AvatarPath;
        if (path.Length == 0 || !File.Exists(path)) return;
        long size = new FileInfo(path).Length;
        if (size is <= 0 or > MaxAvatarBytes) return;
        var targets = peerId is not null ? [peerId] : sessions.Values.Where(s => s.Cipher is not null).Select(s => s.Id).ToArray();
        foreach (var id in targets)
        {
            var transfer = new FileTransfer { Id = Guid.NewGuid().ToString("N"), PeerId = id, Name = "avt_local.png", Size = size, Outgoing = true, Path = path, Avatar = true };
            transfers[id + ":" + transfer.Id] = transfer;
            try
            {
                await SendAsync(id, "avatar", new() { ["filetype"] = "avatar", ["fileop"] = "request", ["filepath"] = path, ["fileid"] = transfer.Id,
                    ["mode"] = "send", ["filename"] = transfer.Name, ["filesize"] = size.ToString() });
            }
            catch (IOException) { transfers.TryRemove(id + ":" + transfer.Id, out _); }
        }
    }
    private async Task HandleAvatarAsync(Session session, WireMessage message)
    {
        var id = message.Get("fileid");
        if (id.Length != 32 || !id.All(Uri.IsHexDigit)) return;
        var key = session.Id + ":" + id;
        switch (message.Get("fileop"))
        {
            case "request":
                if (AvatarDirectory is null || !long.TryParse(message.Get("filesize"), out var size) || size is <= 0 or > MaxAvatarBytes
                    || transfers.Values.Count(t => t.Avatar && !t.Outgoing) >= 50)
                {
                    await SendAsync(session.Id, "avatar", new() { ["fileop"] = "decline", ["fileid"] = id, ["filetype"] = "avatar", ["mode"] = "receive" });
                    return;
                }
                Directory.CreateDirectory(AvatarDirectory);
                // Legacy LAN Messenger ignores tcpport and dials its own configured port, which on a shared computer
                // is its own listener. A peer on this computer names its picture file, so copy it and decline the stream.
                if (IsThisComputer(session.Address) && CopyLocalAvatar(message.Get("filepath"), id) is { } copied)
                {
                    await SendAsync(session.Id, "avatar", new() { ["fileop"] = "decline", ["fileid"] = id, ["filetype"] = "avatar", ["mode"] = "receive" });
                    AvatarReceived?.Invoke(session.Id, copied);
                    return;
                }
                var incoming =new FileTransfer { Id = id, PeerId = session.Id, Name = "avatar", Size = size, Avatar = true,
                    Path = System.IO.Path.Combine(AvatarDirectory, id.ToLowerInvariant() + ".img") };
                if (!transfers.TryAdd(key, incoming)) return;
                incoming.State = "Accepted";
                await SendAsync(session.Id, "avatar", new() { ["mode"] = "receive", ["filetype"] = "avatar", ["fileop"] = "accept", ["fileid"] = id,
                    ["filesize"] = size.ToString(), ["tcpport"] = ListeningPort.ToString() });
                break;
            case "accept":
                if (transfers.TryGetValue(key, out var outgoing) && outgoing.Avatar && outgoing.Outgoing && outgoing.State == "Waiting for approval")
                {
                    outgoing.State = "Connecting";
                    var port = int.TryParse(message.Get("tcpport"), out var p) && p is > 0 and <= 65535 ? p : session.Port;
                    _ = SendFileAsync(session, outgoing, port);
                }
                break;
            case "decline" or "cancel" or "abort":
                transfers.TryRemove(key, out _);
                break;
        }
    }
    /// <summary>True when the address is one of this computer's own.</summary>
    public static bool IsThisComputer(string address)
    {
        if (!IPAddress.TryParse(address, out var ip)) return false;
        if (IPAddress.IsLoopback(ip)) return true;
        try { return NetworkInterface.GetAllNetworkInterfaces().SelectMany(n => n.GetIPProperties().UnicastAddresses).Any(u => u.Address.Equals(ip)); }
        catch (NetworkInformationException) { return false; }
    }
    private string? CopyLocalAvatar(string source, string id)
    {
        if (AvatarDirectory is null || source.Length == 0 || !System.IO.Path.IsPathFullyQualified(source)) return null;
        try
        {
            var info = new FileInfo(source);
            if (!info.Exists || info.Length is <= 0 or > MaxAvatarBytes || !IsImageFile(source)) return null;
            var target = System.IO.Path.Combine(AvatarDirectory, id.ToLowerInvariant() + ".img");
            File.Copy(source, target, true);
            return target;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException) { return null; }
    }
    /// <summary>Profile pictures finish silently; everything else is reported as a file transfer.</summary>
    private void Report(FileTransfer transfer)
    {
        if (!transfer.Avatar) { TransferChanged?.Invoke(transfer); return; }
        if (transfer.State is "Waiting for approval" or "Accepted" or "Connecting" or "Transferring") return;
        transfers.TryRemove(transfer.PeerId + ":" + transfer.Id, out _);
        if (!transfer.Outgoing && transfer.State == "Received")
        {
            if (IsImageFile(transfer.Path)) AvatarReceived?.Invoke(transfer.PeerId, transfer.Path);
            else try { File.Delete(transfer.Path); } catch (IOException) { }
        }
    }
    /// <summary>PNG, JPEG, GIF or BMP signature.</summary>
    public static bool IsImageFile(string path)
    {
        try
        {
            Span<byte> head = stackalloc byte[8];
            using var file = File.OpenRead(path);
            int read = file.Read(head);
            return read >= 4 && (head[..4].SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47 }) || head[..3].SequenceEqual(new byte[] { 0xFF, 0xD8, 0xFF })
                || head[..4].SequenceEqual("GIF8"u8) || head[..2].SequenceEqual("BM"u8));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return false; }
    }

    private static Dictionary<string, string> FileBody(FileTransfer t, string operation)
    {
        var body = new Dictionary<string, string> {
            ["fileid"] = t.Id, ["mode"] = t.Outgoing ? "send" : "receive", ["filetype"] = "normal",
            ["fileop"] = operation, ["filename"] = t.Name, ["filesize"] = t.Size.ToString() };
        // CydLan extension: an extra body element that legacy LAN Messenger parsers skip.
        if (t.Inline) body["cydlaninline"] = "image";
        return body;
    }
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
            bool inline = message.Get("cydlaninline") == "image" && InlineImageDirectory is not null && IsCydLanVersion(session.Version)
                && size <= MaxInlineImageBytes && IsInlineImageName(name);
            var transfer = new FileTransfer { Id = id, PeerId = session.Id, Name = name, Size = size, Inline = inline };
            if (!transfers.TryAdd(key, transfer)) return;
            TransferChanged?.Invoke(transfer);
            if (AutoAcceptDirectory is { } folder)
            {
                try { await AcceptFileIntoAsync(transfer, folder); }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException or SocketException or InvalidOperationException or OperationCanceledException)
                {
                    transfers.TryRemove(key, out _);
                    transfer.State = "Failed: " + e.Message;
                    TransferChanged?.Invoke(transfer);
                }
            }
        }
        else if (transfers.TryGetValue(key, out var transfer))
        {
            if (message.Get("fileop") == "accept" && transfer.Outgoing && transfer.State == "Waiting for approval")
            {
                transfer.State = "Connecting"; TransferChanged?.Invoke(transfer);
                var port = int.TryParse(message.Get("tcpport"), out var p) && p is > 0 and <= 65535 ? p : session.Port;
                _ = SendFileAsync(session, transfer, port);
            }
            // Keep LAN Messenger's distinct outcomes: the other side declined the offer, cancelled it, or aborted mid-transfer.
            else if (message.Get("fileop") is "decline" or "cancel" or "abort" && transfer.State is not ("Received" or "Sent · receipt unavailable"))
            {
                string nextState = message.Get("fileop") switch { "decline" => "Declined", "cancel" => "Cancelled", _ => "Aborted" };
                transfers.TryRemove(new KeyValuePair<string, FileTransfer>(key, transfer));
                if (activeTransfers.TryGetValue(key, out var control) && control.TryStop(nextState)) return;
                transfer.State = nextState;
                Report(transfer);
                ReleaseFolderArchive(transfer);
            }
        }
        await Task.CompletedTask;
    }
    /// <summary>"name.ext", then "name [1].ext", "name [2].ext", … like LAN Messenger, so an existing file is never overwritten.</summary>
    public static string UniquePath(string folder, string name, IEnumerable<string>? reserved = null)
    {
        var taken = new HashSet<string>(reserved ?? [], StringComparer.OrdinalIgnoreCase);
        var path = System.IO.Path.Combine(folder, name);
        var stem = System.IO.Path.GetFileNameWithoutExtension(name); var ext = System.IO.Path.GetExtension(name);
        for (int n = 1; File.Exists(path) || Directory.Exists(path) || taken.Contains(path); n++) path = System.IO.Path.Combine(folder, $"{stem} [{n}]{ext}");
        return path;
    }
    /// <summary>Accepts an offer into <paramref name="folder"/> under a free name, as LAN Messenger does (or into <see cref="InlineImageDirectory"/> for inline images).</summary>
    public Task AcceptFileIntoAsync(FileTransfer transfer, string folder)
    {
        if (transfer.Inline && InlineImageDirectory is { } images)
        {
            Directory.CreateDirectory(images);
            var destination = System.IO.Path.Combine(images, transfer.Id + System.IO.Path.GetExtension(transfer.Name).ToLowerInvariant());
            return AcceptFileAsync(transfer, destination);
        }
        Directory.CreateDirectory(folder);
        // Files still arriving have no file yet, so their names are reserved too.
        var pending = transfers.Values.Where(t => !t.Outgoing && !t.Avatar && t.State is "Accepted" or "Transferring").Select(t => t.Path);
        return AcceptFileAsync(transfer, UniquePath(folder, transfer.Name, pending));
    }
    private async Task SendFileAsync(Session session, FileTransfer transfer, int port)
    {
        string key = transfer.PeerId + ":" + transfer.Id;
        var control = new ActiveFileTransfer(stopping.Token);
        activeTransfers[key] = control;
        bool started = false;
        try
        {
            await SendFilePayloadAsync(session, port, transfer.Id, transfer.Archive?.Path ?? transfer.Path, transfer.Size,
                bytes =>
                {
                    if (control.StopState is not null) return;
                    transfer.State = "Transferring"; transfer.TransferredBytes = bytes; transfer.Progress = TransferPercent(bytes, transfer.Size); Report(transfer);
                },
                control.Token,
                () => started = true);
            if (control.StopState is { } stopped)
            {
                transfer.State = stopped; Report(transfer);
                return;
            }
            transfer.TransferredBytes = transfer.Size; transfer.Progress = 100; transfer.State = "Sent · receipt unavailable"; Report(transfer);
        }
        catch (Exception e) when (e is IOException or SocketException or OperationCanceledException or UnauthorizedAccessException or InvalidDataException or ObjectDisposedException)
        {
            if (control.StopState is { } stopped)
            {
                transfer.State = stopped; Report(transfer);
                return;
            }
            log?.Write("transfer.send_failed", e, ("peer", transfer.PeerId), ("id", transfer.Id), ("bytes", transfer.Size));
            transfer.State = "Failed: " + e.Message;
            Report(transfer);
            if (!started && !transfer.Avatar)
            {
                try { await SendAsync(session, "file", FileBody(transfer, "abort")); }
                catch (Exception abortError) when (abortError is IOException or SocketException or OperationCanceledException) { }
            }
        }
        finally
        {
            activeTransfers.TryRemove(new KeyValuePair<string, ActiveFileTransfer>(key, control));
            control.Complete();
            ReleaseFolderArchive(transfer);
            transfers.TryRemove(new KeyValuePair<string, FileTransfer>(key, transfer));
        }
    }
    private async Task ReceiveFileAsync(TcpClient client, NetworkStream stream, FileTransfer transfer)
    {
        string key = transfer.PeerId + ":" + transfer.Id;
        var control = new ActiveFileTransfer(stopping.Token);
        activeTransfers[key] = control;
        using var cancelClient = control.Token.Register(() => { try { client.Dispose(); } catch (ObjectDisposedException) { } });
        try
        {
            await ReceiveFilePayloadAsync(stream, transfer.Id, transfer.Path, transfer.Size,
                bytes =>
                {
                    if (control.StopState is not null) return;
                    transfer.State = "Transferring"; transfer.TransferredBytes = bytes; transfer.Progress = TransferPercent(bytes, transfer.Size); Report(transfer);
                },
                control.Token);
            if (control.StopState is { } stopped)
            {
                transfer.State = stopped; Report(transfer);
                return;
            }
            transfer.TransferredBytes = transfer.Size; transfer.Progress = 100; transfer.State = "Received"; Report(transfer);
        }
        catch (Exception e) when (e is IOException or SocketException or OperationCanceledException or UnauthorizedAccessException or ObjectDisposedException)
        {
            if (control.StopState is { } stopped)
            {
                transfer.State = stopped; Report(transfer);
                return;
            }
            log?.Write("transfer.receive_failed", e, ("peer", transfer.PeerId), ("id", transfer.Id), ("bytes", transfer.Size));
            transfer.State = "Failed: " + e.Message; Report(transfer);
        }
        finally
        {
            activeTransfers.TryRemove(new KeyValuePair<string, ActiveFileTransfer>(key, control));
            control.Complete();
            transfers.TryRemove(new KeyValuePair<string, FileTransfer>(key, transfer));
        }
    }

    private static double TransferPercent(long bytes, long total) => total <= 0 ? 0 : Math.Min(99.9, bytes * 100d / total);

    private const int TransferBufferSize = 262144;

    private async Task SendFilePayloadAsync(Session session, int port, string id, string path, long size, Action<long> progress, CancellationToken cancellationToken, Action? onStarted = null)
    {
        using var client = new TcpClient { NoDelay = true };
        using var cancelSocket = cancellationToken.Register(() => { try { client.Dispose(); } catch (ObjectDisposedException) { } });
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(30));
        await client.ConnectAsync(session.Address, port, deadline.Token);
        var stream = client.GetStream();
        await stream.WriteAsync(Encoding.ASCII.GetBytes("FILE" + id + settings.UserId), deadline.Token);
        var start = new byte[5];
        try { await stream.ReadExactlyAsync(start, deadline.Token); }
        catch (EndOfStreamException) { throw new EndOfStreamException("The peer closed the transfer before starting."); }
        if (!start.AsSpan().SequenceEqual("START"u8)) throw new InvalidDataException("Invalid transfer response.");
        onStarted?.Invoke();
        await using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, TransferBufferSize, FileOptions.Asynchronous | FileOptions.SequentialScan);
        if (file.Length != size) throw new IOException("The file changed after it was offered.");
        progress(0);
        var buffer = new byte[TransferBufferSize];
        long sent = 0;
        long lastReport = Environment.TickCount64;
        while (sent < size)
        {
            deadline.CancelAfter(TimeSpan.FromSeconds(30));
            int read = await file.ReadAsync(buffer.AsMemory(0, (int)Math.Min(buffer.Length, size - sent)), deadline.Token);
            if (read == 0) throw new EndOfStreamException("The file changed during transfer.");
            await stream.WriteAsync(buffer.AsMemory(0, read), deadline.Token);
            sent += read;
            long now = Environment.TickCount64;
            if (sent == size || now - lastReport >= 50)
            {
                lastReport = now;
                progress(sent);
            }
        }
        // Legacy file streams have no application-level completion receipt.
        client.Client.Shutdown(SocketShutdown.Send);
    }

    private static async Task ReceiveFilePayloadAsync(NetworkStream stream, string id, string path, long size, Action<long> progress, CancellationToken cancellationToken)
    {
        // A received file is visible only after the exact advertised byte count.
        string partial = path + "." + id + ".partial";
        bool created = false;
        using var cancelStream = cancellationToken.Register(() => { try { stream.Dispose(); } catch (ObjectDisposedException) { } });
        try
        {
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            deadline.CancelAfter(TimeSpan.FromSeconds(30));
            await using (var file = new FileStream(partial, FileMode.CreateNew, FileAccess.Write, FileShare.None, TransferBufferSize, FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                created = true;
                await stream.WriteAsync("START"u8.ToArray(), deadline.Token);
                progress(0);
                var buffer = new byte[TransferBufferSize];
                long received = 0;
                long lastReport = Environment.TickCount64;
                while (received < size)
                {
                    deadline.CancelAfter(TimeSpan.FromSeconds(30));
                    int read = await stream.ReadAsync(buffer.AsMemory(0, (int)Math.Min(buffer.Length, size - received)), deadline.Token);
                    if (read == 0) throw new EndOfStreamException("The transfer ended early.");
                    await file.WriteAsync(buffer.AsMemory(0, read), deadline.Token);
                    received += read;
                    long now = Environment.TickCount64;
                    if (received == size || now - lastReport >= 50)
                    {
                        lastReport = now;
                        progress(received);
                    }
                }
                await file.FlushAsync(deadline.Token);
            }
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(partial, path, false);
        }
        finally
        {
            if (created) try { File.Delete(partial); } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (stopping.IsCancellationRequested) return;
        log?.Write("network.stop");
        if (udp is not null && multicastEnabled)
        {
            try { await udp.SendAsync(Encode("depart", null, []), new IPEndPoint(IPAddress.Parse(settings.Multicast), settings.Port)); }
            catch (Exception e) when (e is SocketException or ObjectDisposedException) { }
        }
        foreach (var active in activeTransfers.Values) active.TryStop("Interrupted");
        await stopping.CancelAsync();
        udp?.Dispose(); listener?.Stop();
        foreach (var client in clients.Keys) client.Dispose();
        foreach (var value in pending.Values) value.TrySetResult(false);
        foreach (var transfer in transfers.Values) ReleaseFolderArchive(transfer);
        await Task.WhenAll(nativeFolders.Values.Select(folder => folder.Finished.Task));
        // The gate also protects any handshake that was already processing.
        lock (rsaGate) rsa.Dispose();
    }

    private void ReleaseFolderArchive(FileTransfer transfer)
    {
        if (transfer.Archive is not { } archive) return;
        archive.Dispose();
        transfers.TryRemove(transfer.PeerId + ":" + transfer.Id, out _);
    }

    private sealed class ActiveFileTransfer
    {
        private readonly CancellationTokenSource cancellation;
        private readonly object gate = new();
        private string? stopState;
        private bool completed;

        public ActiveFileTransfer(CancellationToken stopping)
        {
            cancellation = CancellationTokenSource.CreateLinkedTokenSource(stopping);
            Token = cancellation.Token;
        }

        public CancellationToken Token { get; }
        public TaskCompletionSource Finished { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public string? StopState { get { lock (gate) return stopState; } }

        public void MarkStopping(string state)
        {
            lock (gate)
            {
                if (!completed && stopState is null) stopState = state;
            }
        }

        public void Cancel()
        {
            lock (gate)
            {
                if (!completed) cancellation.Cancel();
            }
        }

        public bool TryStop(string state)
        {
            lock (gate)
            {
                if (completed || stopState is not null) return false;
                stopState = state;
                cancellation.Cancel();
                return true;
            }
        }

        public void Complete()
        {
            lock (gate)
            {
                completed = true;
                cancellation.Dispose();
                Finished.TrySetResult();
            }
        }
    }

    private sealed class Session(string id, string address, int port, TcpClient client, bool incoming)
    {
        public string Id { get; } = id;
        public string Address { get; } = address;
        public int Port { get; set; } = port;
        public TcpClient Client { get; } = client;
        public EndPoint? LocalEndpoint { get; } = client.Client.LocalEndPoint;
        public bool Incoming { get; } = incoming;
        public NetworkStream Stream { get; } = client.GetStream();
        public LegacyCipher? Cipher { get; set; }
        public string Version { get; set; } = "";
        public bool SupportsFolders { get; set; }
        /// <summary>Profile query and picture already sent for this connection.</summary>
        public bool Introduced { get; set; }
        /// <summary>Extra LAN Messenger connections held open for this session; see RunSpareChannelAsync.</summary>
        public List<TcpClient> Spares { get; } = [];
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
