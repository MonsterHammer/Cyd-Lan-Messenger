using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using CydLan.Core;

if (args.Contains("--experiment-peer")) return await NetworkExperiment.RunPeerAsync(args);
if (args.Contains("--sandbox-legacy"))
{
    int[] ports = args.SkipWhile(arg => arg != "--sandbox-legacy").Skip(1).Select(int.Parse).ToArray();
    await TestOriginalLegacyAsync(ports.Length > 0 ? ports : [50000]).WaitAsync(TimeSpan.FromMinutes(3));
    return 0;
}
if (args.Contains("--isolated-mixed"))
{
    await NetworkExperiment.RunAsync();
    await TestMixedPeers().WaitAsync(TimeSpan.FromSeconds(25));
    Console.WriteLine("PASS  Three CydLan services and two legacy fixture connections on loopback");
    return 0;
}

var tests = new (string Name, Func<Task> Run)[] {
    ("Persistent diagnostics: rotation, concurrency, privacy and disk failures", DiagnosticLogTests.RunAsync),
    ("Legacy XML fixtures, escaping and Unicode", TestXml),
    ("LAN Messenger emoticon codes", TestSmileys),
    ("Reject external entities and malformed packets", TestInvalidXml),
    ("Fragmented and coalesced TCP frames", TestFraming),
    ("Reject oversized and truncated frames", TestInvalidFrames),
    ("OpenSSL AES-256-CBC known-answer vector", TestCipher),
    ("OpenSSL RSA-OAEP-SHA1 legacy 1024-bit envelope", TestEnvelope),
    ("Real sockets: peer handshake, chat, receipts, typing and broadcast", TestPeers),
    ("Legacy fixture peer: inbound handshake, deduplication, numeric IDs", TestLegacyPeer),
    ("Legacy fixture acceptor: outbound 1024-bit RSA session and receipt", TestLegacyAcceptor),
    ("Second LAN Messenger connection is held open, not closed or re-keyed", TestLegacySpareConnection),
    ("Mixed loopback: three CydLan services and two legacy fixture connections", TestMixedPeers),
    ("Simultaneous peer connections converge without a relay", TestSimultaneous),
    ("Directed UDP discovery and reconnect after a peer restarts", TestDiscoveryReconnect),
    ("Occupied legacy TCP port falls back to an advertised port", TestPortCoexistence),
    ("Dual-stack legacy listener keeps its IPv4 connections", TestLegacyDualStackPort),
    ("User IDs fit LAN Messenger's 64-byte file greeting", TestLegacyFileGreeting),
    ("Version passes LAN Messenger's chat-room check", TestLegacyRoomVersion),
    ("Profile details and pictures travel like LAN Messenger avatars", TestProfileAndAvatar),
    ("Saved contact reconnects automatically without multicast", TestSavedContactRecovery),
    ("Repeated recovery attempts back off for an unreachable contact", TestRecoveryBackoff),
    ("Concurrent bidirectional chat and duplicate suppression", TestConcurrentChat),
    ("Separate-process peers: Unicode replies and binary files both ways", NetworkExperiment.RunAsync),
    ("File offers, approval, binary transfer and empty files", TestFiles),
    ("Active file and folder transfer cancellation and pre-start EOF handling", TestCancelTransfers),
    ("Local persistence, stable identity and corrupt history recovery", TestStore)
};
int failures = 0;
foreach (var (name, run) in tests)
{
    try { await run().WaitAsync(TimeSpan.FromSeconds(25)); Console.WriteLine("PASS  " + name); }
    catch (Exception e) { failures++; Console.WriteLine("FAIL  " + name + "\n" + e); }
}
Console.WriteLine($"\n{tests.Length - failures}/{tests.Length} test groups passed. Connections use loopback only (one listener briefly binds all interfaces); no LAN announcements were sent.");
return failures == 0 ? 0 : 1;

static void Check(bool condition, string description) { if (!condition) throw new Exception(description); }
static async Task Until(Func<bool> predicate) { using var deadline = new CancellationTokenSource(8000); while (!predicate()) await Task.Delay(15, deadline.Token); }
static AppSettings Settings(string name) => new() { DisplayName = name, Port = 0 };
static Task TestSmileys()
{
    string Render(string text) => string.Concat(Smileys.Split(text).Select(s => s.Picture > 0 ? $"[{s.Picture}]" : s.Text));
    Check(Smileys.PickerCodes.Count == 70 && Smileys.PickerCodes[0] == ":-)" && Smileys.PickerCodes[69] == "(M)", "Picker codes must follow LAN Messenger's order.");
    Check(Render("hi :-) and :) (h)") == "hi [1] and [1] [6]", "Codes and aliases must match case-insensitively.");
    Check(Render("8-)") == "[23]", "Earlier codes in the table win, as in LAN Messenger.");
    Check(Render(":-(:'(") == "[10][11]", "Adjacent codes must both render.");
    Check(Render("<:o) & (&)") == "[17] & [29]", "Codes with markup characters must match the raw text.");
    Check(Render("plain text 😊") == "plain text 😊" && !Smileys.HasAny("plain") && Smileys.HasAny("ok (Y)"), "Plain text must be left alone.");
    Check(Smileys.Split("").Count == 0, "Empty text has no segments.");
    return Task.CompletedTask;
}
static Task TestXml()
{
    var bytes = LegacyProtocol.Encode("message", "AABBCCalice", "DDEEFFbob", "42", new Dictionary<string, string> { ["message"] = "<hello> & 你好\n🙂" });
    var message = LegacyProtocol.Decode(bytes);
    Check(message.Type == "message" && message.From == "AABBCCalice" && message.To == "DDEEFFbob" && message.Id == "42", "Legacy headers must survive encoding.");
    Check(message.Get("message") == "<hello> & 你好\n🙂", "Message text must survive XML escaping and UTF-8.");
    var announce = LegacyProtocol.Decode(Encoding.UTF8.GetBytes("<lmcmessage><head><from>001122334455OldUser</from><messageid>0</messageid><type>announce</type></head><body/></lmcmessage>"));
    Check(announce.Get("tcpport") == "", "Old clients announce without an advertised port.");
    // Captured from LAN Messenger 1.2.39: the second recipient of a group message gets the first recipient's header too.
    var group = LegacyProtocol.Decode(Encoding.UTF8.GetBytes("<lmcmessage>  <head>   <from>00155DC89F4Dmagba</from>   <to>b46b70b87f7046079525e6c3</to>   <messageid>155</messageid>   <type>groupmessage</type>   <from>00155DC89F4Dmagba</from>   <to>probe0123456789abcdef</to>   <messageid>155</messageid>   <type>groupmessage</type>  </head>  <body>   <thread>probe0123456789abcdef98f313c8bb9541049077604243d8711d</thread>   <groupmsgop>join</groupmsgop>  </body> </lmcmessage>"));
    Check(group.To == "probe0123456789abcdef" && group.From == "00155DC89F4Dmagba" && group.Type == "groupmessage" && group.Get("groupmsgop") == "join",
        "Repeated legacy group headers must resolve to this recipient's copy.");
    return Task.CompletedTask;
}
static Task TestInvalidXml()
{
    foreach (var xml in new[] { "<invalid/>", "<!DOCTYPE lmcmessage [<!ENTITY x SYSTEM 'file:///secret'>]><lmcmessage><head/><body>&x;</body></lmcmessage>", "<lmcmessage><head><from/></head><body/></lmcmessage>" })
    {
        bool rejected = false; try { LegacyProtocol.Decode(Encoding.UTF8.GetBytes(xml)); } catch (Exception e) when (e is System.Xml.XmlException or InvalidDataException) { rejected = true; }
        Check(rejected, "Malformed or entity-bearing XML must be rejected.");
    }
    return Task.CompletedTask;
}
static async Task TestFraming()
{
    using var output = new MemoryStream();
    await LegacyProtocol.WriteFrameAsync(output, "PUBKEY", [1, 2, 3], default);
    await LegacyProtocol.WriteFrameAsync(output, "MESSAG", [4, 5, 6, 7], default);
    Check(Convert.ToHexString(output.ToArray()[..4]) == "00000009", "Qt QDataStream uses big-endian lengths.");
    using var fragmented = new FragmentedStream(output.ToArray());
    var first = await LegacyProtocol.ReadFrameAsync(fragmented, default); var second = await LegacyProtocol.ReadFrameAsync(fragmented, default);
    Check(first.Type == "PUBKEY" && first.Payload.SequenceEqual(new byte[] { 1, 2, 3 }), "Partial length/header reads must be reassembled.");
    Check(second.Type == "MESSAG" && second.Payload.SequenceEqual(new byte[] { 4, 5, 6, 7 }), "Coalesced packets must stay separate.");
}
static async Task TestInvalidFrames()
{
    foreach (int size in new[] { -1, 5, LegacyProtocol.MaxFrame + 1, 32 })
    {
        var data = new byte[4]; BinaryPrimitives.WriteInt32BigEndian(data, size);
        bool rejected = false;
        try { await LegacyProtocol.ReadFrameAsync(new MemoryStream(data), default); } catch (Exception e) when (e is IOException or InvalidDataException) { rejected = true; }
        Check(rejected, "Invalid or truncated frames must fail closed.");
    }
}
static Task TestCipher()
{
    using var cipher = new LegacyCipher(Enumerable.Range(0, 48).Select(i => (byte)i).ToArray());
    var clear = Encoding.ASCII.GetBytes("Legacy LAN compatibility");
    var expected = Convert.FromHexString("540e21f8495a5d67d07f13305a3fa8e42b83b3a42850b694bb4842584109ec46");
    Check(cipher.Encrypt(clear).SequenceEqual(expected), "Encryption must match the independent OpenSSL fixture.");
    Check(cipher.Decrypt(expected).SequenceEqual(clear), "OpenSSL ciphertext must decrypt.");
    Check(cipher.Encrypt(clear).SequenceEqual(expected), "Legacy encryption reuses the negotiated IV per message.");
    bool rejected = false; try { cipher.Decrypt(expected[..^1]); } catch (CryptographicException) { rejected = true; }
    Check(rejected, "Truncated AES blocks must be rejected.");
    return Task.CompletedTask;
}
static Task TestEnvelope()
{
    using var rsa = RSA.Create();
    rsa.ImportFromPem(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures/legacy-test-private.pem")));
    var envelope = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures/openssl-envelope.bin"));
    var material = rsa.Decrypt(envelope, RSAEncryptionPadding.OaepSHA1);
    Check(material.SequenceEqual(Enumerable.Range(0, 48).Select(i => (byte)i)), "Released 1024-bit RSA peers use OAEP SHA1 and a 48-byte session envelope.");
    return Task.CompletedTask;
}
static async Task TestPeers()
{
    var a = Settings("Alex"); var b = Settings("Jamie");
    await using var one = new LanService(a); await using var two = new LanService(b);
    int online = 0, messages = 0; string typing = "", broadcast = "";
    one.PeerOnline += _ => Interlocked.Increment(ref online); two.PeerOnline += _ => Interlocked.Increment(ref online);
    two.MessageReceived += message => { if (message.Broadcast) broadcast = message.Text; else if (message.Text == "Hello 你好 🙂") Interlocked.Increment(ref messages); };
    two.TypingChanged += (_, value) => typing = value;
    await one.StartAsync(false, IPAddress.Loopback); await two.StartAsync(false, IPAddress.Loopback);
    await one.ConnectAsync(b.UserId, "127.0.0.1", two.ListeningPort); await Until(() => online == 2);
    Check(await one.SendChatAsync(b.UserId, "Hello 你好 🙂", "10001"), "Messages require an acknowledgment.");
    await Until(() => messages == 1);
    await one.SendTypingAsync(b.UserId, true); await Until(() => typing == "Typing…");
    await one.SendTypingAsync(b.UserId, false); await Until(() => typing == "");
    Check(await one.BroadcastAsync("Standup in 5") == 1, "Broadcast should target the directly connected peer."); await Until(() => broadcast == "Standup in 5");
    await two.DisposeAsync(); // Disposal is idempotent; pending writes may fail, never fabricate delivery.
    bool unavailable = false; try { unavailable = !await one.SendChatAsync(b.UserId, "after disconnect", "10002"); } catch (IOException) { unavailable = true; }
    Check(unavailable, "An offline peer must not produce a delivered receipt.");
}
static async Task TestLegacyPeer()
{
    var settings = Settings("CydLan test");
    await using var service = new LanService(settings);
    await service.StartAsync(false, IPAddress.Loopback);
    await ExerciseLegacyPeer(service, settings, testFolder: true);
}
static async Task ExerciseLegacyPeer(LanService service, AppSettings settings, Func<Task>? whileConnected = null, bool testFolder = false)
{
    int count = 0; string? name = null;
    const string id = "001122334455Legacy User";
    service.PeerOnline += peer => { if (peer.Id == id) name = peer.Name; };
    service.MessageReceived += message => { if (message.PeerId == id) Interlocked.Increment(ref count); };
    using var client = new TcpClient(); await client.ConnectAsync(IPAddress.Loopback, service.ListeningPort);
    var stream = client.GetStream();
    // An independent peer fixture deliberately fragments the unframed identity.
    foreach (byte value in Encoding.ASCII.GetBytes("MSG" + id)) { await stream.WriteAsync(new byte[] { value }); await Task.Delay(1); }
    var pub = await RawRead(stream); Check(pub.Type == "PUBKEY", "The acceptor sends its PKCS#1 public key first.");
    var pem = Encoding.ASCII.GetString(pub.Payload); Check(pem.StartsWith("-----BEGIN RSA PUBLIC KEY-----"), "Old peers require PKCS#1 PEM.");
    using var rsa = RSA.Create(); rsa.ImportFromPem(pem);
    var material = Enumerable.Range(0, 48).Select(i => (byte)i).ToArray();
    await RawWrite(stream, "HNDSHK", rsa.Encrypt(material, RSAEncryptionPadding.OaepSHA1));
    using var aes = Aes.Create(); aes.Key = material[..32]; aes.IV = material[32..];
    var userdata = await RawRead(stream);
    var xml = XElement.Parse(Encoding.UTF8.GetString(aes.DecryptCbc(userdata.Payload, aes.IV)));
    Check((string?)xml.Element("head")?.Element("type") == "userdata", "Handshake must request user data.");
    async Task Send(string type, string msgId, params XElement[] body)
    {
        var packet = new XElement("lmcmessage", new XElement("head", new XElement("type", type), new XElement("from", id), new XElement("to", settings.UserId), new XElement("messageid", msgId)), new XElement("body", body));
        await RawWrite(stream, "MESSAG", aes.EncryptCbc(Encoding.UTF8.GetBytes(packet.ToString()), aes.IV));
    }
    using var folderListener = new TcpListener(IPAddress.Loopback, 0);
    if (testFolder) folderListener.Start();
    int folderPort = testFolder ? ((IPEndPoint)folderListener.LocalEndpoint).Port : 50000;
    await Send("userdata", "1", new("userid", id), new("name", "Old LAN Messenger"), new("status", "chat"),
        new("queryop", "result"), new("version", testFolder ? "1.2.39" : "1.2.35"), new("usercaps", testFolder ? "7" : "3"), new("tcpport", folderPort));
    await Until(() => name == "Old LAN Messenger");
    await Send("message", "22", new XElement("message", "Legacy hello"));
    await Send("message", "22", new XElement("message", "Legacy hello"));
    await Until(() => count == 1);
    for (int i = 0; i < 2; i++)
    {
        var ackXml = await ReadMessage(stream, aes);
        Check((string?)ackXml.Element("body")?.Element("messageid") == "22", "Each retry is acknowledged with the original numeric ID.");
    }
    Check(count == 1, "A legacy retry must not duplicate the message.");

    // This independent legacy peer deliberately never answers application heartbeat pings.
    // A missing ping receipt must not take it offline while the TCP connection is usable.
    int wentOffline = 0;
    service.PeerOffline += _ => Interlocked.Increment(ref wentOffline);
    await service.CheckPeersAsync();
    await Send("message", "24", new XElement("message", "Chat after heartbeat"));
    var afterHeartbeat = await ReadMessage(stream, aes);
    Check((string?)afterHeartbeat.Element("body")?.Element("messageid") == "24" && wentOffline == 0,
        "An otherwise working legacy connection must survive without heartbeat receipts.");
    await Until(() => count == 2);
    if (whileConnected is not null) await whileConnected();

    // LAN Messenger group sends stack one header per recipient; a copy for someone else must not end the session.
    string? groupText = null; service.GroupMessageReceived += (_, _, op, text) => { if (op == "message") groupText = text; };
    async Task SendHeads(string[] recipients, params XElement[] body)
    {
        var head = new XElement("head", recipients.SelectMany(to => new[] { new XElement("from", id), new XElement("to", to), new XElement("messageid", "60"), new XElement("type", "groupmessage") }));
        await RawWrite(stream, "MESSAG", aes.EncryptCbc(Encoding.UTF8.GetBytes(new XElement("lmcmessage", head, new XElement("body", body)).ToString()), aes.IV));
    }
    await SendHeads(["someoneelse0123456789"], new XElement("thread", "room-1"), new XElement("groupmsgop", "message"), new XElement("message", "not for us"));
    await SendHeads(["someoneelse0123456789", settings.UserId], new XElement("thread", "room-1"), new XElement("groupmsgop", "message"), new XElement("message", "Room hello 🙂"));
    await Until(() => groupText is not null);
    Check(groupText == "Room hello 🙂", "A stacked-header group message addressed to us must be delivered.");
    await Send("message", "23", new XElement("message", "Still connected"));
    await Until(() => count == 3);
    var lastAck = await ReadMessage(stream, aes);
    Check((string?)lastAck.Element("body")?.Element("messageid") == "23", "The final legacy chat must be acknowledged.");
    if (testFolder)
    {
        var directory = Path.Combine(Path.GetTempPath(), "CydLan-tests-" + Guid.NewGuid().ToString("N"));
        var folderPath = Directory.CreateDirectory(Path.Combine(directory, "Project", "Nested")).Parent!.FullName;
        try
        {
            byte[] content = Encoding.UTF8.GetBytes("Legacy folder content");
            await File.WriteAllBytesAsync(Path.Combine(folderPath, "Nested", "report.txt"), content);
            var folder = await service.OfferFolderAsync(id, folderPath);
            var folderOffer = await ReadMessage(stream, aes);
            var body = folderOffer.Element("body");
            Check((string?)folderOffer.Element("head")?.Element("type") == "folder" &&
                (string?)body?.Element("folderid") == folder.Id && (string?)body?.Element("filecount") == "1" &&
                (string?)body?.Element("filename") == "Project", "The legacy peer should receive one native folder offer.");
            await Send("folder", "61", new("folderid", folder.Id), new("fileid", folder.Id),
                new("filetype", "folder"), new("fileop", "accept"), new("mode", "receive"));
            var fileOffer = await ReadMessage(stream, aes);
            body = fileOffer.Element("body");
            string fileId = (string)body!.Element("fileid")!;
            Check((string?)fileOffer.Element("head")?.Element("type") == "file" && (string?)body.Element("filetype") == "folder" &&
                (string?)body.Element("folderid") == folder.Id && (string?)body.Element("relpath") == "Nested/report.txt",
                "The legacy peer should receive the file with its native relative path.");
            await Send("file", "62", new("fileid", fileId), new("filetype", "folder"), new("fileop", "accept"),
                new("mode", "receive"), new("filesize", content.Length));
            using var transfer = await folderListener.AcceptTcpClientAsync().WaitAsync(TimeSpan.FromSeconds(5));
            var data = transfer.GetStream();
            byte[] greeting = new byte[36 + settings.UserId.Length];
            await data.ReadExactlyAsync(greeting);
            Check(Encoding.ASCII.GetString(greeting) == "FILE" + fileId + settings.UserId, "The folder file should use the regular FILE greeting.");
            await data.WriteAsync("START"u8.ToArray());
            byte[] received = new byte[content.Length];
            await data.ReadExactlyAsync(received);
            Check(received.SequenceEqual(content), "The legacy peer should receive the exact file contents.");
            await Until(() => folder.State == "Sent · receipt unavailable");
        }
        finally { DeleteTestDirectory(directory); }
    }
    client.Dispose();
    await Until(() => wentOffline == 1);
}
// LAN Messenger 1.2.39 dials a contact again when an announcement arrives before it lists that contact, and
// removes the contact when any connection to it closes. CydLan must keep that extra connection open without
// a new handshake and read it with the established key.
static async Task TestLegacySpareConnection()
{
    var settings = Settings("CydLan test");
    await using var service = new LanService(settings);
    await service.StartAsync(false, IPAddress.Loopback);
    const string id = "00155DBA6B94Spare User";
    int count = 0; bool online = false;
    service.PeerOnline += peer => { if (peer.Id == id) online = true; };
    service.MessageReceived += message => { if (message.PeerId == id && message.Text == "via spare") Interlocked.Increment(ref count); };
    using var main = new TcpClient(); await main.ConnectAsync(IPAddress.Loopback, service.ListeningPort);
    var stream = main.GetStream();
    await stream.WriteAsync(Encoding.ASCII.GetBytes("MSG" + id));
    var pub = await RawRead(stream);
    using var rsa = RSA.Create(); rsa.ImportFromPem(Encoding.ASCII.GetString(pub.Payload));
    var material = Enumerable.Range(0, 48).Select(i => (byte)(i * 3)).ToArray();
    await RawWrite(stream, "HNDSHK", rsa.Encrypt(material, RSAEncryptionPadding.OaepSHA1));
    using var aes = Aes.Create(); aes.Key = material[..32]; aes.IV = material[32..];
    await RawRead(stream); // userdata get
    byte[] Packet(string type, string msgId, params XElement[] body) => aes.EncryptCbc(Encoding.UTF8.GetBytes(new XElement("lmcmessage",
        new XElement("head", new XElement("type", type), new XElement("from", id), new XElement("to", settings.UserId), new XElement("messageid", msgId)), new XElement("body", body)).ToString()), aes.IV);
    await RawWrite(stream, "MESSAG", Packet("userdata", "1", new("userid", id), new("name", "Spare"), new("status", "chat"), new("queryop", "result"), new("version", "1.2.39")));
    await Until(() => online);

    using var spare = new TcpClient(); await spare.ConnectAsync(IPAddress.Loopback, service.ListeningPort);
    var spareStream = spare.GetStream();
    await spareStream.WriteAsync(Encoding.ASCII.GetBytes("MSG" + id));
    var probe = new byte[1];
    using (var quiet = new CancellationTokenSource(1500))
    {
        bool timedOut = false;
        int read = -1;
        try { read = await spareStream.ReadAsync(probe, quiet.Token); } catch (OperationCanceledException) { timedOut = true; }
        Check(timedOut, $"The second connection must stay open with no new handshake (no PUBKEY, no close); read {read} bytes.");
    }
    await RawWrite(spareStream, "MESSAG", Packet("message", "71", new XElement("message", "via spare")));
    await Until(() => count == 1);
    var ack = await ReadMessage(stream, aes);
    Check((string?)ack.Element("head")?.Element("type") == "acknowledge" && (string?)ack.Element("body")?.Element("messageid") == "71",
        "A message read from the second connection is acknowledged on the established one.");
    main.Dispose();
    using var closed = new CancellationTokenSource(8000);
    Check(await spareStream.ReadAsync(probe, closed.Token) == 0, "The second connection closes with the established session.");
}
static async Task TestMixedPeers()
{
    var a = Settings("Mixed A"); var b = Settings("Mixed B"); var c = Settings("Mixed C");
    await using var one = new LanService(a);
    await using var two = new LanService(b);
    await using var three = new LanService(c);
    var nodes = new[] { (Service: one, Settings: a), (Service: two, Settings: b), (Service: three, Settings: c) };
    await Task.WhenAll(nodes.Select(node => node.Service.StartAsync(false, IPAddress.Loopback)));
    var messages = new ConcurrentBag<(string Receiver, string Text)>();
    foreach (var node in nodes)
        node.Service.MessageReceived += message => messages.Add((node.Settings.UserId, message.Text));

    for (int i = 0; i < nodes.Length; i++)
        for (int j = i + 1; j < nodes.Length; j++)
            await nodes[i].Service.ConnectAsync(nodes[j].Settings.UserId, "127.0.0.1", nodes[j].Service.ListeningPort);

    var legacyReady = new SemaphoreSlim(0);
    using var done = new CancellationTokenSource(TimeSpan.FromSeconds(15));
    var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    async Task HoldLegacyAsync()
    {
        legacyReady.Release();
        await release.Task.WaitAsync(done.Token);
    }
    var legacy = Task.WhenAll(ExerciseLegacyPeer(one, a, HoldLegacyAsync), ExerciseLegacyPeer(two, b, HoldLegacyAsync));
    try
    {
        for (int i = 0; i < 2; i++) await legacyReady.WaitAsync(done.Token);
        await Task.WhenAll(nodes.SelectMany(sender => nodes
            .Where(receiver => receiver.Settings.UserId != sender.Settings.UserId)
            .Select(async receiver =>
            {
                string text = $"{sender.Settings.DisplayName} to {receiver.Settings.DisplayName}";
                Check(await sender.Service.SendChatAsync(receiver.Settings.UserId, text, "17701"),
                    "Missing receipt while legacy sessions were open: " + text);
                Check(messages.Count(message => message == (receiver.Settings.UserId, text)) == 1,
                    "Mixed chat was missing, duplicated or routed incorrectly: " + text);
            })));
    }
    finally { release.TrySetResult(); await legacy; legacyReady.Dispose(); }
}
static async Task<string> ProbeOriginalIdentityAsync(int legacyPort)
{
    using var probe = new TcpClient();
    await probe.ConnectAsync(IPAddress.Loopback, legacyPort);
    var stream = probe.GetStream();
    await stream.WriteAsync(Encoding.ASCII.GetBytes("MSG" + AppSettings.NewUserId()));
    var key = await RawRead(stream).WaitAsync(TimeSpan.FromSeconds(10));
    Check(key.Type == "PUBKEY", "Original client did not send its public key.");
    using var rsa = RSA.Create();
    rsa.ImportFromPem(Encoding.ASCII.GetString(key.Payload));
    byte[] material = RandomNumberGenerator.GetBytes(48);
    await RawWrite(stream, "HNDSHK", rsa.Encrypt(material, RSAEncryptionPadding.OaepSHA1));
    using var aes = Aes.Create();
    aes.Key = material[..32]; aes.IV = material[32..];
    var profile = await ReadMessage(stream, aes).WaitAsync(TimeSpan.FromSeconds(10));
    string legacyId = (string?)profile.Element("body")?.Element("userid") ?? "";
    Check(legacyId.Length > 0, "Original client returned an empty identity.");
    return legacyId;
}

static async Task TestOriginalLegacyAsync(int[] legacyPorts)
{
    var legacy = await Task.WhenAll(legacyPorts.Select(async port => (Port: port, Id: await ProbeOriginalIdentityAsync(port))));
    Check(legacy.Select(peer => peer.Id).Distinct().Count() == legacy.Length, "Original clients must have separate identities.");
    var first = Settings("Isolated CydLan A"); var second = Settings("Isolated CydLan B");
    await using var a = new LanService(first);
    await using var b = new LanService(second);
    int offline = 0;
    var legacyIds = legacy.Select(peer => peer.Id).ToHashSet();
    var legacyOnline = new ConcurrentDictionary<(string CydLan, string Legacy), string>();
    a.PeerOnline += peer => { if (legacyIds.Contains(peer.Id)) legacyOnline[(first.UserId, peer.Id)] = peer.Version; };
    b.PeerOnline += peer => { if (legacyIds.Contains(peer.Id)) legacyOnline[(second.UserId, peer.Id)] = peer.Version; };
    a.PeerOffline += id => { if (legacyIds.Contains(id)) Interlocked.Increment(ref offline); };
    b.PeerOffline += id => { if (legacyIds.Contains(id)) Interlocked.Increment(ref offline); };
    await a.StartAsync(false, IPAddress.Loopback);
    await b.StartAsync(false, IPAddress.Loopback);
    await a.ConnectAsync(second.UserId, "127.0.0.1", b.ListeningPort);
    await Task.WhenAll(legacy.SelectMany(peer => new[] {
        a.ConnectAsync(peer.Id, "127.0.0.1", peer.Port), b.ConnectAsync(peer.Id, "127.0.0.1", peer.Port)
    }));
    await Until(() => legacyOnline.Count == 2 * legacy.Length);
    Console.WriteLine($"Two CydLan clients connected to {legacy.Length} distinct original LAN Messenger client(s), version(s) {string.Join(", ", legacyOnline.Values.Distinct())}.");
    for (int i = 0; i < 4; i++)
    {
        foreach (var peer in legacy)
        {
            Check(await a.SendChatAsync(peer.Id, $"Isolated CydLan A {i}", (18000 + i).ToString()), $"Legacy port {peer.Port} did not acknowledge CydLan A.");
            Check(await b.SendChatAsync(peer.Id, $"Isolated CydLan B {i}", (19000 + i).ToString()), $"Legacy port {peer.Port} did not acknowledge CydLan B.");
        }
        Check(await a.SendChatAsync(second.UserId, $"Mixed topology {i}", (20000 + i).ToString()), "CydLan B did not acknowledge CydLan A.");
        if (i < 3) await Task.Delay(TimeSpan.FromSeconds(15));
    }
    Check(Volatile.Read(ref offline) == 0, "A legacy session disconnected during the stability check.");
    Console.WriteLine($"PASS  {8 * legacy.Length} mixed messages acknowledged; all {2 * legacy.Length} legacy connections survived 45 seconds of mixed traffic.");
}
static async Task TestLegacyAcceptor()
{
    var settings = Settings("Modern user"); const string oldId = "AA0011223344olduser";
    await using var service = new LanService(settings);
    bool online = false; service.PeerOnline += peer => online = peer.Id == oldId;
    await service.StartAsync(false, IPAddress.Loopback);
    var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
    try
    {
        var oldPeer = Task.Run(async () => {
            using var client = await listener.AcceptTcpClientAsync(); var stream = client.GetStream();
            var greeting = new byte[3 + settings.UserId.Length]; await stream.ReadExactlyAsync(greeting);
            Check(Encoding.ASCII.GetString(greeting) == "MSG" + settings.UserId, "Initiator greeting must use the legacy prefix.");
            using var rsa = RSA.Create(); rsa.ImportFromPem(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures/legacy-test-private.pem")));
            await RawWrite(stream, "PUBKEY", Encoding.ASCII.GetBytes(rsa.ExportRSAPublicKeyPem()));
            var handshake = await RawRead(stream); Check(handshake.Type == "HNDSHK", "Initiator must return a sealed session envelope.");
            var material = rsa.Decrypt(handshake.Payload, RSAEncryptionPadding.OaepSHA1); Check(material.Length == 48, "Session material is exactly 48 bytes.");
            using var aes = Aes.Create(); aes.Key = material[..32]; aes.IV = material[32..];
            async Task Send(string type, params XElement[] body) {
                var packet = new XElement("lmcmessage", new XElement("head", new XElement("from", oldId), new XElement("to", settings.UserId), new XElement("type", type), new XElement("messageid", "44")), new XElement("body", body));
                await RawWrite(stream, "MESSAG", aes.EncryptCbc(Encoding.UTF8.GetBytes(packet.ToString()), aes.IV));
            }
            await Send("userdata", new("userid", oldId), new("queryop", "get"), new("name", "Old acceptor"), new("status", "chat"), new("version", "1.2.35"));
            var profile = await RawRead(stream); var xml = XElement.Parse(Encoding.UTF8.GetString(aes.DecryptCbc(profile.Payload, aes.IV)));
            Check((string?)xml.Element("body")?.Element("queryop") == "result", "Initiator replies to the old acceptor's user-data request.");
            xml = await ReadMessage(stream, aes);
            Check((string?)xml.Element("body")?.Element("message") == "Hello from CydLan", "The old acceptor can decrypt a new message.");
            var id = (string?)xml.Element("head")?.Element("messageid"); Check(long.TryParse(id, out _), "New message IDs are decimal int64 values.");
            // Lose the first receipt. The retry must reuse its original identity.
            var retryXml = await ReadMessage(stream, aes);
            Check((string?)retryXml.Element("head")?.Element("messageid") == id, "A lost receipt must retry with the same message ID.");
            await Send("acknowledge", new XElement("messageid", id));
            await Task.Delay(100);
        });
        await service.ConnectAsync(oldId, "127.0.0.1", ((IPEndPoint)listener.LocalEndpoint).Port); await Until(() => online);
        Check(await service.SendChatAsync(oldId, "Hello from CydLan", "181234567890001"), "Independent legacy peer acknowledges outbound chat.");
        await oldPeer;
    }
    finally { listener.Stop(); }
}
static async Task TestSimultaneous()
{
    var a = Settings("A"); var b = Settings("B");
    await using var one = new LanService(a); await using var two = new LanService(b);
    int online = 0; one.PeerOnline += _ => Interlocked.Increment(ref online); two.PeerOnline += _ => Interlocked.Increment(ref online);
    await one.StartAsync(false, IPAddress.Loopback); await two.StartAsync(false, IPAddress.Loopback);
    async Task Attempt(LanService service, string id, int port) { try { await service.ConnectAsync(id, "127.0.0.1", port); } catch (Exception e) when (e is IOException or SocketException or OperationCanceledException) { } }
    await Task.WhenAll(Attempt(one, b.UserId, two.ListeningPort), Attempt(two, a.UserId, one.ListeningPort));
    await Until(() => online >= 2);
    Check(await one.SendChatAsync(b.UserId, "A to B", "501"), "Simultaneous discovery must settle on a working connection.");
    Check(await two.SendChatAsync(a.UserId, "B to A", "502"), "The resulting connection is bidirectional.");
}
static async Task TestFiles()
{
    var a = Settings("Sender"); var b = Settings("Receiver");
    await using var one = new LanService(a); await using var two = new LanService(b);
    var offers = new ConcurrentQueue<FileTransfer>(); int online = 0;
    one.PeerOnline += _ => Interlocked.Increment(ref online); two.PeerOnline += _ => Interlocked.Increment(ref online);
    two.TransferChanged += t => { if (t.State == "Waiting for approval") offers.Enqueue(t); };
    await one.StartAsync(false, IPAddress.Loopback); await two.StartAsync(false, IPAddress.Loopback);
    await one.ConnectAsync(b.UserId, "127.0.0.1", two.ListeningPort); await Until(() => online == 2);
    var directory = Path.Combine(Path.GetTempPath(), "CydLan-tests-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory);
    try
    {
        // Free names follow LAN Messenger: "name.ext", then "name [1].ext", "name [2].ext", skipping names reserved by files still arriving.
        File.WriteAllText(Path.Combine(directory, "report.pdf"), "");
        Check(LanService.UniquePath(directory, "report.pdf") == Path.Combine(directory, "report [1].pdf"), "A taken name gets \" [1]\" before the extension.");
        Check(LanService.UniquePath(directory, "report.pdf", [Path.Combine(directory, "report [1].pdf")]) == Path.Combine(directory, "report [2].pdf"), "Names of files still arriving are skipped.");
        Check(LanService.UniquePath(directory, "fresh.txt") == Path.Combine(directory, "fresh.txt"), "A free name is kept.");
        foreach (int size in new[] { 0, 190123 })
        {
            var source = Path.Combine(directory, $"source-{size}.bin"); var target = Path.Combine(directory, $"received-{size}.bin");
            var data = RandomNumberGenerator.GetBytes(size); await File.WriteAllBytesAsync(source, data);
            var outgoing = await one.OfferFileAsync(b.UserId, source); await Until(() => !offers.IsEmpty);
            Check(offers.TryDequeue(out var incoming), "A file must be offered for approval.");
            Check(!File.Exists(target), "No file is written before acceptance.");
            await two.AcceptFileAsync(incoming!, target); await Until(() => incoming!.State == "Received" || incoming.State.StartsWith("Failed"));
            Check(incoming!.State == "Received", incoming.State);
            Check((await File.ReadAllBytesAsync(target)).SequenceEqual(data), "File contents must match byte for byte, including empty files.");
            Check(!Directory.EnumerateFiles(directory, "*.partial").Any(), "Completed transfers leave no partial files.");
            await Until(() => outgoing.Progress == 100);
        }
        var declined = await one.OfferFileAsync(b.UserId, Path.Combine(directory, "source-0.bin")); await Until(() => !offers.IsEmpty); offers.TryDequeue(out var offer);
        await two.DeclineFileAsync(offer!); await Until(() => declined.State == "Declined");
        var sourceFolder = Directory.CreateDirectory(Path.Combine(directory, "Project"));
        Directory.CreateDirectory(Path.Combine(sourceFolder.FullName, "Empty"));
        Directory.CreateDirectory(Path.Combine(sourceFolder.FullName, "Nested"));
        byte[] folderData = RandomNumberGenerator.GetBytes(120_000);
        await File.WriteAllBytesAsync(Path.Combine(sourceFolder.FullName, "Nested", "report.bin"), folderData);
        await File.WriteAllTextAsync(Path.Combine(sourceFolder.FullName, "readme.txt"), "notes");
        var folder = await one.OfferFolderAsync(b.UserId, sourceFolder.FullName);
        await Until(() => !offers.IsEmpty);
        Check(offers.TryDequeue(out var folderOffer) && folderOffer is { Name: "Project", Folder: true },
            "A capable peer should receive a single native folder offer.");
        Check(!Directory.Exists(Path.Combine(directory, "Project [1]")), "Nothing should be saved before the folder is accepted.");
        await two.AcceptFileIntoAsync(folderOffer!, directory);
        await Until(() => folderOffer!.State == "Received" && folder.State == "Sent · receipt unavailable");
        Check(folderOffer!.Path == Path.Combine(directory, "Project [1]"), "Receiving a folder must not overwrite a directory.");
        Check((await File.ReadAllBytesAsync(Path.Combine(folderOffer.Path, "Nested", "report.bin"))).SequenceEqual(folderData) &&
            await File.ReadAllTextAsync(Path.Combine(folderOffer.Path, "readme.txt")) == "notes", "The native folder should preserve file contents and nesting.");
        Check(!Directory.EnumerateDirectories(directory, "*.partial").Any(), "Folder transfer should leave no staging directory.");

        var emptyFolder = await one.OfferFolderAsync(b.UserId, Path.Combine(sourceFolder.FullName, "Empty"));
        await Until(() => !offers.IsEmpty);
        Check(offers.TryDequeue(out var zipOffer) && zipOffer is { Name: "Empty.zip", Folder: false },
            "A completely empty folder should still be sendable as a ZIP.");
        await two.AcceptFileIntoAsync(zipOffer!, directory);
        await Until(() => zipOffer!.State == "Received" && !File.Exists(emptyFolder.Archive!.Path));
        using (var zip = ZipFile.OpenRead(zipOffer!.Path))
        {
            Check(zip.GetEntry("Empty/") is not null, "The ZIP fallback should preserve an empty directory.");
        }
        var declinedFolder = await one.OfferFolderAsync(b.UserId, sourceFolder.FullName);
        await Until(() => !offers.IsEmpty);
        Check(offers.TryDequeue(out var secondZipOffer), "The second folder should remain a separate offer.");
        await two.DeclineFileAsync(secondZipOffer!);
        await Until(() => declinedFolder.State == "Declined");
        Check(File.Exists(Path.Combine(sourceFolder.FullName, "Nested", "report.bin")), "Declining a folder must keep the original folder.");
        string existing = Path.Combine(directory, "existing.bin"); await File.WriteAllTextAsync(existing, "Keep this file");
        await one.OfferFileAsync(b.UserId, Path.Combine(directory, "source-190123.bin")); await Until(() => !offers.IsEmpty); offers.TryDequeue(out var overwrite);
        await two.AcceptFileAsync(overwrite!, existing); await Until(() => overwrite!.State.StartsWith("Failed"));
        Check(await File.ReadAllTextAsync(existing) == "Keep this file", "Even the transport must never overwrite an existing destination.");
        string changedPath = Path.Combine(directory, "changing.bin"); await File.WriteAllBytesAsync(changedPath, [1, 2, 3]);
        var changed = await one.OfferFileAsync(b.UserId, changedPath); await Until(() => !offers.IsEmpty); offers.TryDequeue(out var interrupted);
        await File.WriteAllBytesAsync(changedPath, [1]);
        string incomplete = Path.Combine(directory, "incomplete.bin"); await two.AcceptFileAsync(interrupted!, incomplete);
        await Until(() => changed.State.StartsWith("Failed") && interrupted!.State.StartsWith("Failed"));
        Check(!File.Exists(incomplete) && !Directory.EnumerateFiles(directory, "*.partial").Any(), "An interrupted transfer must not expose an incomplete destination or leave partial bytes.");

        // Large-file streaming & progress precision: GB formatting, monotonic progress, never reports 100% before completion, exact SHA-256 match.
        Check(FileTransfer.FormatBytes(2_684_354_560L) == "2.5 GB", $"Expected GB formatting, got {FileTransfer.FormatBytes(2_684_354_560L)}.");
        string largeSource = Path.Combine(directory, "large-64mb.bin");
        string largeTarget = Path.Combine(directory, "large-64mb-received.bin");
        const long largeSize = 64L * 1024 * 1024;
        var chunk = RandomNumberGenerator.GetBytes(1024 * 1024);
        byte[] sourceHash;
        using (var hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256))
        await using (var outStream = File.Create(largeSource))
        {
            for (int m = 0; m < 64; m++)
            {
                chunk[0] = (byte)m;
                await outStream.WriteAsync(chunk);
                hasher.AppendData(chunk);
            }
            sourceHash = hasher.GetHashAndReset();
        }
        var progressSamples = new List<(string State, long Bytes, double Progress, int DisplayPercent)>();
        void CaptureLargeProgress(FileTransfer t)
        {
            if (t.Name == "large-64mb.bin" && !t.Outgoing)
                lock (progressSamples) progressSamples.Add((t.State, t.TransferredBytes, t.Progress, t.DisplayPercent));
        }
        two.TransferChanged += CaptureLargeProgress;
        try
        {
            var largeOutgoing = await one.OfferFileAsync(b.UserId, largeSource);
            await Until(() => !offers.IsEmpty);
            Check(offers.TryDequeue(out var largeIncoming) && largeIncoming!.Size == largeSize, "Large file offer should preserve exact 64-bit byte length.");
            await two.AcceptFileAsync(largeIncoming!, largeTarget);
            await Until(() => largeIncoming!.State == "Received" && largeOutgoing.State == "Sent · receipt unavailable");
            Check(largeIncoming!.TransferredBytes == largeSize && largeIncoming.Progress == 100 && largeIncoming.DisplayPercent == 100,
                "Completed large transfer should finish at exact byte count and 100%.");
            lock (progressSamples)
            {
                var transferring = progressSamples.Where(s => s.State == "Transferring").ToList();
                Check(transferring.Count > 0 && transferring.All(s => s.Progress < 100 && s.DisplayPercent < 100),
                    "Progress must never show 100% while still transferring.");
                for (int i = 1; i < transferring.Count; i++)
                    Check(transferring[i].Bytes >= transferring[i - 1].Bytes && transferring[i].Progress >= transferring[i - 1].Progress,
                        "Transfer progress must advance monotonically.");
            }
            using var receivedFile = File.OpenRead(largeTarget);
            byte[] receivedHash = await SHA256.HashDataAsync(receivedFile);
            Check(receivedHash.SequenceEqual(sourceHash), "64 MB streamed file must match SHA-256 byte for byte.");
        }
        finally { two.TransferChanged -= CaptureLargeProgress; }

        // Disconnecting a peer interrupts pending offers so neither side gets permanently stuck on Accept/Decline.
        var orphaned = await one.OfferFileAsync(b.UserId, Path.Combine(directory, "source-0.bin"));
        await Until(() => !offers.IsEmpty);
        Check(offers.TryDequeue(out var orphanedIncoming) && orphanedIncoming!.State == "Waiting for approval", "Pending offer should await approval before disconnect.");
        await one.DisposeAsync();
        await Until(() => orphanedIncoming!.State == "Interrupted");
        Check(orphanedIncoming!.State == "Interrupted", "Disconnecting the sender must interrupt pending offers instead of leaving them stuck.");
    }
    finally { DeleteTestDirectory(directory); }
}
static async Task TestCancelTransfers()
{
    var a = Settings("Cancel Sender"); var b = Settings("Cancel Receiver");
    await using var one = new LanService(a); await using var two = new LanService(b);
    var offers = new ConcurrentQueue<FileTransfer>(); int online = 0;
    one.PeerOnline += _ => Interlocked.Increment(ref online); two.PeerOnline += _ => Interlocked.Increment(ref online);
    two.TransferChanged += t => { if (t.State == "Waiting for approval") offers.Enqueue(t); };
    await one.StartAsync(false, IPAddress.Loopback); await two.StartAsync(false, IPAddress.Loopback);
    await one.ConnectAsync(b.UserId, "127.0.0.1", two.ListeningPort); await Until(() => online == 2);
    var directory = Path.Combine(Path.GetTempPath(), "CydLan-cancel-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory);
    try
    {
        // 1. Pre-START receiver failure closes the TCP stream without START: sender reports a clear error instead of raw EndOfStreamException text.
        string blockingFile = Path.Combine(directory, "blocker");
        await File.WriteAllTextAsync(blockingFile, "not a directory");
        string smallSource = Path.Combine(directory, "small.bin");
        await File.WriteAllBytesAsync(smallSource, [1, 2, 3, 4]);
        var blockedOutgoing = await one.OfferFileAsync(b.UserId, smallSource);
        await Until(() => !offers.IsEmpty);
        Check(offers.TryDequeue(out var blockedIncoming), "Blocked offer should arrive.");
        await two.AcceptFileAsync(blockedIncoming!, Path.Combine(blockingFile, "child.bin"));
        await Until(() => blockedOutgoing.State.StartsWith("Failed", StringComparison.Ordinal) && blockedIncoming!.State.StartsWith("Failed", StringComparison.Ordinal));
        Check(!blockedOutgoing.State.Contains("Unable to read beyond the end of the stream", StringComparison.Ordinal),
            $"Sender should report a clear pre-start failure instead of '{blockedOutgoing.State}'.");

        // 2. Active single-file cancel by RECEIVER during Transferring (and while large file streams).
        string bigSource = Path.Combine(directory, "big.bin");
        using (var fs = File.Create(bigSource)) fs.SetLength(128L * 1024 * 1024);
        var recvStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void OnReceiverTransfer(FileTransfer t) { if (!t.Outgoing && t.Name == "big.bin" && t.State == "Transferring") recvStarted.TrySetResult(); }
        two.TransferChanged += OnReceiverTransfer;
        try
        {
            var outgoingRecvCancel = await one.OfferFileAsync(b.UserId, bigSource);
            await Until(() => !offers.IsEmpty);
            Check(offers.TryDequeue(out var incomingRecvCancel), "Receiver should receive offer for big.bin.");
            string recvTarget = Path.Combine(directory, "recv-cancelled.bin");
            await two.AcceptFileAsync(incomingRecvCancel!, recvTarget);
            await recvStarted.Task.WaitAsync(TimeSpan.FromSeconds(10));
            if (incomingRecvCancel!.State is "Accepted" or "Connecting" or "Transferring")
                await two.CancelFileAsync(incomingRecvCancel);
            await Until(() => incomingRecvCancel.State == "Cancelled" && (outgoingRecvCancel.State == "Aborted" || outgoingRecvCancel.State.StartsWith("Failed", StringComparison.Ordinal)));
            Check(incomingRecvCancel.State == "Cancelled", $"Expected receiver Cancelled, got {incomingRecvCancel.State}.");
            Check(outgoingRecvCancel.State == "Aborted", $"Expected sender Aborted, got {outgoingRecvCancel.State}.");
            Check(!File.Exists(recvTarget) && !Directory.EnumerateFiles(directory, "*.partial").Any(),
                "Cancelled receive must not leave a partial or destination file.");
        }
        finally { two.TransferChanged -= OnReceiverTransfer; }

        // 3. Active single-file cancel by SENDER during Transferring.
        var sendStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void OnSenderTransfer(FileTransfer t) { if (t.Outgoing && t.Name == "big.bin" && t.State == "Transferring") sendStarted.TrySetResult(); }
        one.TransferChanged += OnSenderTransfer;
        try
        {
            var outgoingSendCancel = await one.OfferFileAsync(b.UserId, bigSource);
            await Until(() => !offers.IsEmpty);
            Check(offers.TryDequeue(out var incomingSendCancel), "Receiver should receive second offer for big.bin.");
            string sendCancelTarget = Path.Combine(directory, "send-cancelled.bin");
            await two.AcceptFileAsync(incomingSendCancel!, sendCancelTarget);
            await sendStarted.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await one.CancelFileAsync(outgoingSendCancel);
            await Until(() => outgoingSendCancel.State == "Cancelled" && incomingSendCancel!.State == "Aborted");
            Check(!File.Exists(sendCancelTarget) && !Directory.EnumerateFiles(directory, "*.partial").Any(),
                "Sender-cancelled transfer must remove any staging partial file.");
        }
        finally { one.TransferChanged -= OnSenderTransfer; }

        // 4. Active native folder cancel by receiver.
        var folderRoot = Directory.CreateDirectory(Path.Combine(directory, "BigFolder"));
        using (var fs = File.Create(Path.Combine(folderRoot.FullName, "part1.bin"))) fs.SetLength(64L * 1024 * 1024);
        using (var fs = File.Create(Path.Combine(folderRoot.FullName, "part2.bin"))) fs.SetLength(64L * 1024 * 1024);
        var folderStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void OnFolderTransfer(FileTransfer t) { if (!t.Outgoing && t.Folder && t.State == "Transferring") folderStarted.TrySetResult(); }
        two.TransferChanged += OnFolderTransfer;
        try
        {
            var outgoingFolder = await one.OfferFolderAsync(b.UserId, folderRoot.FullName);
            await Until(() => !offers.IsEmpty);
            Check(offers.TryDequeue(out var incomingFolder) && incomingFolder!.Folder, "Native folder offer should arrive.");
            await two.AcceptFileIntoAsync(incomingFolder!, directory);
            await folderStarted.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await two.CancelFileAsync(incomingFolder!);
            await Until(() => incomingFolder!.State == "Cancelled" && outgoingFolder.State == "Aborted");
            Check(!Directory.Exists(Path.Combine(directory, "BigFolder [1]")) && !Directory.EnumerateDirectories(directory, "*.partial").Any(),
                "Cancelled folder transfer must clean up partial staging directories.");
        }
        finally { two.TransferChanged -= OnFolderTransfer; }
    }
    finally { DeleteTestDirectory(directory); }
}
static async Task TestDiscoveryReconnect()
{
    var a = Settings("Discovery A"); var b = Settings("Discovery B");
    await using var one = new LanService(a);
    int online = 0, offline = 0, received = 0;
    one.PeerOnline += _ => Interlocked.Increment(ref online);
    one.PeerOffline += _ => Interlocked.Increment(ref offline);
    one.MessageReceived += _ => Interlocked.Increment(ref received);
    await one.StartAsync(true, IPAddress.Loopback);
    await using (var two = new LanService(b))
    {
        await two.StartAsync(true, IPAddress.Loopback);
        await one.DiscoverAsync(IPAddress.Loopback, two.DiscoveryPort);
        await Until(() => online == 1);
        Check(await two.SendChatAsync(a.UserId, "Before restart", "601"), "UDP discovery must establish encrypted chat without knowing a peer ID.");
        await Until(() => received == 1);
    }
    await Until(() => offline == 1);
    await using var restarted = new LanService(b);
    await restarted.StartAsync(true, IPAddress.Loopback);
    await one.DiscoverAsync(IPAddress.Loopback, restarted.DiscoveryPort);
    await Until(() => online == 2);
    Check(await restarted.SendChatAsync(a.UserId, "Before restart", "601"), "A retransmission after reconnect must still be acknowledged.");
    Check(await restarted.SendChatAsync(a.UserId, "After restart", "602"), "Chat must resume with the same stable identity on a new endpoint.");
    await Until(() => received == 2);
    Check(received == 2, "A reconnect must not duplicate a message already received by the surviving peer.");
}
static async Task TestPortCoexistence()
{
    TcpListener? occupied = null;
    int port = 0;
    for (int candidatePort = 50410; candidatePort <= 60800; candidatePort += 137)
    {
        var tcp = new TcpListener(IPAddress.Loopback, candidatePort);
        try
        {
            tcp.Start();
            using var udp = new UdpClient(AddressFamily.InterNetwork);
            udp.ExclusiveAddressUse = false;
            udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            udp.Client.Bind(new IPEndPoint(IPAddress.Loopback, candidatePort));
            occupied = tcp;
            port = candidatePort;
            break;
        }
        catch (SocketException)
        {
            tcp.Stop();
        }
    }
    Check(occupied is not null, "Could not allocate a dual TCP/UDP test port.");
    try
    {
        var a = Settings("Coexisting"); a.Port = port;
        var b = Settings("Discoverer");
        await using var one = new LanService(a); await using var two = new LanService(b);
        int online = 0; two.PeerOnline += _ => Interlocked.Increment(ref online);
        await one.StartAsync(true, IPAddress.Loopback); await two.StartAsync(true, IPAddress.Loopback);
        Check(one.ListeningPort != port && one.DiscoveryPort == port, "TCP fallback must preserve the configured UDP discovery port.");
        await one.DiscoverAsync(IPAddress.Loopback, two.DiscoveryPort);
        await Until(() => online == 1);
        Check(await two.SendChatAsync(a.UserId, "Hello alternate port", "701"), "Discovery must route to the advertised fallback TCP port.");
    }
    finally { occupied?.Stop(); }
}
static async Task TestLegacyDualStackPort()
{
    // LAN Messenger (Qt) listens on [::] dual-stack without exclusive use. CydLan must not add a 0.0.0.0
    // listener on the same port, which would take every IPv4 connection meant for the old client.
    var legacy = new TcpListener(IPAddress.IPv6Any, 0); legacy.Server.DualMode = true; legacy.Start();
    try
    {
        int port = ((IPEndPoint)legacy.LocalEndpoint).Port;
        var settings = Settings("Same PC"); settings.Port = port;
        await using var service = new LanService(settings);
        await service.StartAsync(false); // all interfaces, no discovery traffic
        Check(service.ListeningPort != port, "A port held by a dual-stack legacy listener must trigger the fallback port.");
        using var probe = new TcpClient(); await probe.ConnectAsync(IPAddress.Loopback, port);
        using var accepted = await legacy.AcceptTcpClientAsync().WaitAsync(TimeSpan.FromSeconds(3));
        Check(accepted.Connected, "IPv4 connections to the legacy port must still reach the legacy client.");
    }
    finally { legacy.Stop(); }
}
static Task TestLegacyFileGreeting()
{
    // LAN Messenger reads the file greeting once, 64 bytes: "FILE" + 32-character file ID + sender ID.
    for (int i = 0; i < 100; i++)
    {
        var settings = new AppSettings(); var fileId = Guid.NewGuid().ToString("N").ToUpperInvariant();
        var greeting = Encoding.ASCII.GetBytes("FILE" + fileId + settings.UserId);
        Check(Encoding.ASCII.GetString(greeting, 36, Math.Min(64, greeting.Length) - 36) == settings.UserId, "Legacy must read the complete sender ID from the file greeting.");
    }
    bool rejected = false;
    try { _ = new LanService(new AppSettings { UserId = Guid.NewGuid().ToString("N") }); } catch (ArgumentException) { rejected = true; }
    Check(rejected, "IDs longer than legacy's file greeting allows must be rejected.");
    return Task.CompletedTask;
}
static async Task TestProfileAndAvatar()
{
    var directory = Path.Combine(Path.GetTempPath(), "CydLan-avatar-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory);
    try
    {
        // A tiny but real PNG: signature + IHDR is enough for the receiver's image check.
        var picture = Path.Combine(directory, "me.png");
        File.WriteAllBytes(picture, [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, .. Enumerable.Range(0, 200).Select(i => (byte)i)]);
        var a = Settings("Alex"); a.FirstName = "Alex"; a.LastName = "Rivera"; a.About = "Front desk 🙂"; a.AvatarPath = picture;
        var b = Settings("Jamie");
        await using var one = new LanService(a); await using var two = new LanService(b) { AvatarDirectory = Path.Combine(directory, "avatars") };
        string? avatar = null; (string First, string Last, string About)? profile = null; int files = 0;
        two.AvatarReceived += (id, path) => { if (id == a.UserId) avatar = path; };
        two.ProfileReceived += (id, first, last, about) => { if (id == a.UserId) profile = (first, last, about); };
        two.TransferChanged += _ => Interlocked.Increment(ref files);
        await one.StartAsync(false, IPAddress.Loopback); await two.StartAsync(false, IPAddress.Loopback);
        await one.ConnectAsync(b.UserId, "127.0.0.1", two.ListeningPort);
        await Until(() => avatar is not null && profile is not null);
        Check(File.ReadAllBytes(avatar!).SequenceEqual(File.ReadAllBytes(picture)), "The profile picture must arrive intact.");
        Check(Path.GetDirectoryName(avatar) == Path.Combine(directory, "avatars"), "Profile pictures are stored in the avatar folder under our own name.");
        Check(profile == ("Alex", "Rivera", "Front desk 🙂"), "First name, last name and About must arrive in the query reply.");
        Check(files == 0, "Profile pictures must not appear as file transfers.");
    }
    finally { try { Directory.Delete(directory, true); } catch (IOException) { } }
}
static Task TestLegacyRoomVersion()
{
    // LAN Messenger 1.2.39 chatroomwindow.cpp: compareVersions(version, "1.2.10") <= 0 keeps a user out of rooms.
    // It reads the first three dot-separated parts with toInt() and no bounds check.
    var parts = LanService.WireVersion.Split('.', StringSplitOptions.RemoveEmptyEntries);
    Check(parts.Length >= 3 && parts.Take(3).All(p => int.TryParse(p, out _)), "The first three version parts must be numbers.");
    int Compare(int[] a, int[] b) { for (int i = 0; i < 3; i++) if (a[i] != b[i]) return a[i] - b[i]; return 0; }
    Check(Compare(parts.Take(3).Select(int.Parse).ToArray(), [1, 2, 10]) > 0, "LAN Messenger must treat CydLan as newer than 1.2.10.");
    Check(LanService.IsCydLanVersion(LanService.DisplayVersion(LanService.WireVersion)), "CydLan peers must still recognise each other.");
    Check(LanService.DisplayVersion("1.2.39") == "1.2.39", "Legacy versions are shown unchanged.");
    return Task.CompletedTask;
}
static async Task TestConcurrentChat()
{
    var a = Settings("Concurrent A"); var b = Settings("Concurrent B");
    await using var one = new LanService(a); await using var two = new LanService(b);
    int online = 0, inboundA = 0, inboundB = 0;
    one.PeerOnline += _ => Interlocked.Increment(ref online); two.PeerOnline += _ => Interlocked.Increment(ref online);
    one.MessageReceived += _ => Interlocked.Increment(ref inboundA); two.MessageReceived += _ => Interlocked.Increment(ref inboundB);
    await one.StartAsync(false, IPAddress.Loopback); await two.StartAsync(false, IPAddress.Loopback);
    await one.ConnectAsync(b.UserId, "127.0.0.1", two.ListeningPort); await Until(() => online == 2);
    var receipts = await Task.WhenAll(Enumerable.Range(0, 40).SelectMany(i => new[] {
        one.SendChatAsync(b.UserId, $"A → B {i} 你好 🙂", (8000 + i).ToString()),
        two.SendChatAsync(a.UserId, $"B → A {i}\n& <test>", (9000 + i).ToString()) }));
    Check(receipts.All(r => r), "All 80 concurrent messages must receive real delivery acknowledgments.");
    await Until(() => inboundA == 40 && inboundB == 40);
    Check(await one.SendChatAsync(b.UserId, "A → B 0 你好 🙂", "8000"), "An explicit retry must receive a receipt.");
    Check(inboundB == 40, "An explicit retry must not create a duplicate.");
}
static async Task TestRecoveryBackoff()
{
    using var reservation = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
    reservation.Bind(new IPEndPoint(IPAddress.Loopback, 0));
    int port = ((IPEndPoint)reservation.LocalEndPoint!).Port;
    var saved = new Peer { Id = "unreachable", Address = "127.0.0.1", Port = port };
    await using var service = new LanService(Settings("Backoff test"), [saved]);
    int failures = 0;
    service.Diagnostic += message => { if (message.StartsWith("Could not connect to ", StringComparison.Ordinal)) Interlocked.Increment(ref failures); };
    await service.StartAsync(false, IPAddress.Loopback);
    await service.RefreshAsync();
    await service.RefreshAsync();
    await service.RefreshAsync();
    Check(failures == 1, "Repeated discovery should skip unreachable peers during the cooldown.");
}

static async Task TestSavedContactRecovery()
{
    var a = Settings("Saved-contact A"); var b = Settings("Saved-contact B");
    await using var two = new LanService(b);
    await two.StartAsync(false, IPAddress.Loopback);
    int originalPort = two.ListeningPort;
    var saved = new Peer { Id = b.UserId, Name = "Saved B", Address = "127.0.0.1", Port = originalPort };
    await using var one = new LanService(a, [saved]);
    int connections = 0;
    var recovered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    one.PeerOnline += _ => { if (Interlocked.Increment(ref connections) == 2) recovered.TrySetResult(); };
    bool offline = false; one.PeerOffline += _ => offline = true;
    await one.StartAsync(true, IPAddress.Loopback);
    await Until(() => connections == 1);
    Check(await one.SendChatAsync(b.UserId, "Restored contact", "15001"), "Saved contacts must reconnect without an announcement.");
    await two.DisposeAsync(); await Until(() => offline);
    b.Port = originalPort;
    await using var restarted = new LanService(b); await restarted.StartAsync(false, IPAddress.Loopback);
    await recovered.Task.WaitAsync(TimeSpan.FromSeconds(20));
    Check(await one.SendChatAsync(b.UserId, "Automatic recovery", "15002"), "The periodic recovery loop must restore chat without pressing refresh.");
}
static Task TestStore()
{
    var directory = Path.Combine(Path.GetTempPath(), "CydLan-store-" + Guid.NewGuid().ToString("N"));
    try
    {
        var store = new LocalStore(directory); var settings = store.LoadSettings(); store.SaveSettings(settings);
        Check(store.LoadSettings().UserId == settings.UserId, "Identity must remain stable across restarts.");
        store.SaveHistory([new() { Text = "Saved locally", PeerId = "test" }]); Check(store.LoadHistory().Single().Text == "Saved locally", "History must round-trip.");
        store.SaveDrafts(new Dictionary<string, string> { ["test"] = "Unsent draft 你好", ["empty"] = "" });
        Check(store.LoadDrafts().Count == 1 && store.LoadDrafts()["test"] == "Unsent draft 你好", "Only nonempty per-contact drafts should survive a restart.");
        File.WriteAllText(Path.Combine(directory, "history.json"), "broken json"); Check(store.LoadHistory().Count == 0, "Corrupt history must not prevent startup.");
        Check(Directory.EnumerateFiles(directory, "*.corrupt-*").Any(), "Corrupt history must be preserved for recovery.");
    }
    finally { if (Directory.Exists(directory)) DeleteTestDirectory(directory); }
    return Task.CompletedTask;
}
// Independent wire framing for the simulated released peer; avoids testing the
// protocol encoder against itself in the interoperability test.
static async Task RawWrite(Stream stream, string type, byte[] payload)
{
    var data = new byte[10 + payload.Length]; BinaryPrimitives.WriteInt32BigEndian(data, payload.Length + 6);
    Encoding.ASCII.GetBytes(type).CopyTo(data, 4); payload.CopyTo(data, 10);
    for (int offset = 0; offset < data.Length; offset += 7) await stream.WriteAsync(data.AsMemory(offset, Math.Min(7, data.Length - offset)));
}
static void DeleteTestDirectory(string directory)
{
    var full = Path.GetFullPath(directory);
    var temp = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
    if (!full.StartsWith(temp, StringComparison.OrdinalIgnoreCase) || !Path.GetFileName(full).StartsWith("CydLan-", StringComparison.Ordinal)) throw new InvalidOperationException("Refusing to delete outside the test directory.");
    Directory.Delete(full, true);
}
/// <summary>Next encrypted message, skipping the profile query and picture offer a new session starts with.</summary>
static async Task<XElement> ReadMessage(Stream stream, Aes aes)
{
    while (true)
    {
        var frame = await RawRead(stream); var xml = XElement.Parse(Encoding.UTF8.GetString(aes.DecryptCbc(frame.Payload, aes.IV)));
        if ((string?)xml.Element("head")?.Element("type") is not ("query" or "avatar")) return xml;
    }
}
static async Task<(string Type, byte[] Payload)> RawRead(Stream stream)
{
    var length = new byte[4]; await stream.ReadExactlyAsync(length); var data = new byte[BinaryPrimitives.ReadInt32BigEndian(length)]; await stream.ReadExactlyAsync(data);
    return (Encoding.ASCII.GetString(data[..6]), data[6..]);
}
sealed class FragmentedStream(byte[] data) : MemoryStream(data)
{
    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) => base.ReadAsync(buffer[..Math.Min(1, buffer.Length)], cancellationToken);
}
