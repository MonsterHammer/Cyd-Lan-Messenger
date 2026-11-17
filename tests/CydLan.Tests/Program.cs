using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using CydLan.Core;

var tests = new (string Name, Func<Task> Run)[] {
    ("Legacy XML fixtures, escaping and Unicode", TestXml),
    ("Reject external entities and malformed packets", TestInvalidXml),
    ("Fragmented and coalesced TCP frames", TestFraming),
    ("Reject oversized and truncated frames", TestInvalidFrames),
    ("OpenSSL AES-256-CBC known-answer vector", TestCipher),
    ("OpenSSL RSA-OAEP-SHA1 legacy 1024-bit envelope", TestEnvelope),
    ("Real sockets: peer handshake, chat, receipts, typing and broadcast", TestPeers),
    ("Legacy fixture peer: inbound handshake, deduplication, numeric IDs", TestLegacyPeer),
    ("Legacy fixture acceptor: outbound 1024-bit RSA session and receipt", TestLegacyAcceptor),
    ("Simultaneous peer connections converge without a relay", TestSimultaneous),
    ("File offers, approval, binary transfer and empty files", TestFiles),
    ("Local persistence, stable identity and corrupt history recovery", TestStore)
};
int failures = 0;
foreach (var (name, run) in tests)
{
    try { await run().WaitAsync(TimeSpan.FromSeconds(25)); Console.WriteLine("PASS  " + name); }
    catch (Exception e) { failures++; Console.WriteLine("FAIL  " + name + "\n" + e); }
}
Console.WriteLine($"\n{tests.Length - failures}/{tests.Length} test groups passed. All sockets are loopback-only; no LAN announcements were sent.");
return failures == 0 ? 0 : 1;

static void Check(bool condition, string description) { if (!condition) throw new Exception(description); }
static async Task Until(Func<bool> predicate) { using var deadline = new CancellationTokenSource(8000); while (!predicate()) await Task.Delay(15, deadline.Token); }
static AppSettings Settings(string name) => new() { DisplayName = name, Port = 0 };
static Task TestXml()
{
    var bytes = LegacyProtocol.Encode("message", "AABBCCalice", "DDEEFFbob", "42", new Dictionary<string, string> { ["message"] = "<hello> & 你好\n🙂" });
    var message = LegacyProtocol.Decode(bytes);
    Check(message.Type == "message" && message.From == "AABBCCalice" && message.To == "DDEEFFbob" && message.Id == "42", "Legacy headers must survive encoding.");
    Check(message.Get("message") == "<hello> & 你好\n🙂", "Message text must survive XML escaping and UTF-8.");
    var announce = LegacyProtocol.Decode(Encoding.UTF8.GetBytes("<lmcmessage><head><from>001122334455OldUser</from><messageid>0</messageid><type>announce</type></head><body/></lmcmessage>"));
    Check(announce.Get("tcpport") == "", "Old clients announce without an advertised port.");
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
    int count = 0; string? name = null;
    service.PeerOnline += peer => name = peer.Name;
    service.MessageReceived += _ => Interlocked.Increment(ref count);
    await service.StartAsync(false, IPAddress.Loopback);
    using var client = new TcpClient(); await client.ConnectAsync(IPAddress.Loopback, service.ListeningPort);
    var stream = client.GetStream(); const string id = "001122334455LegacyUser";
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
    await Send("userdata", "1", new("userid", id), new("name", "Old LAN Messenger"), new("status", "chat"), new("queryop", "result"), new("version", "1.2.35"));
    await Until(() => name == "Old LAN Messenger");
    await Send("message", "22", new XElement("message", "Legacy hello"));
    await Send("message", "22", new XElement("message", "Legacy hello"));
    await Until(() => count == 1);
    for (int i = 0; i < 2; i++)
    {
        var ack = await RawRead(stream); var ackXml = XElement.Parse(Encoding.UTF8.GetString(aes.DecryptCbc(ack.Payload, aes.IV)));
        Check((string?)ackXml.Element("body")?.Element("messageid") == "22", "Each retry is acknowledged with the original numeric ID.");
    }
    Check(count == 1, "A legacy retry must not duplicate the message.");
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
            var message = await RawRead(stream); xml = XElement.Parse(Encoding.UTF8.GetString(aes.DecryptCbc(message.Payload, aes.IV)));
            Check((string?)xml.Element("body")?.Element("message") == "Hello from CydLan", "The old acceptor can decrypt a new message.");
            var id = (string?)xml.Element("head")?.Element("messageid"); Check(long.TryParse(id, out _), "New message IDs are decimal int64 values.");
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
        await two.DeclineFileAsync(offer!); await Until(() => declined.State == "Declined / cancelled");
    }
    finally { DeleteTestDirectory(directory); }
}
static Task TestStore()
{
    var directory = Path.Combine(Path.GetTempPath(), "CydLan-store-" + Guid.NewGuid().ToString("N"));
    try
    {
        var store = new LocalStore(directory); var settings = store.LoadSettings(); store.SaveSettings(settings);
        Check(store.LoadSettings().UserId == settings.UserId, "Identity must remain stable across restarts.");
        store.SaveHistory([new() { Text = "Saved locally", PeerId = "test" }]); Check(store.LoadHistory().Single().Text == "Saved locally", "History must round-trip.");
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
static async Task<(string Type, byte[] Payload)> RawRead(Stream stream)
{
    var length = new byte[4]; await stream.ReadExactlyAsync(length); var data = new byte[BinaryPrimitives.ReadInt32BigEndian(length)]; await stream.ReadExactlyAsync(data);
    return (Encoding.ASCII.GetString(data[..6]), data[6..]);
}
sealed class FragmentedStream(byte[] data) : MemoryStream(data)
{
    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) => base.ReadAsync(buffer[..Math.Min(1, buffer.Length)], cancellationToken);
}
