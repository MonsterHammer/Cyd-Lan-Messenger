// Wire behavior researched from LAN Messenger (GPL-3.0-or-later).
// See docs/protocol.md and THIRD-PARTY-NOTICES.md.
using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace CydLan.Core;

public sealed record WireMessage(string Type, string From, string To, string Id, Dictionary<string, string> Body)
{
    public string Get(string key) => Body.GetValueOrDefault(key, "");
}

public static class LegacyProtocol
{
    public const int MaxFrame = 16 * 1024 * 1024;
    public static byte[] Encode(string type, string from, string? to, string id, IDictionary<string, string>? body = null)
    {
        var head = new XElement("head", new XElement("from", from), new XElement("messageid", id), new XElement("type", type));
        if (!string.IsNullOrEmpty(to)) head.Add(new XElement("to", to));
        return Encoding.UTF8.GetBytes(new XElement("lmcmessage", head,
            new XElement("body", body?.Select(p => new XElement(p.Key, p.Value)))).ToString(SaveOptions.DisableFormatting));
    }

    public static WireMessage Decode(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length > MaxFrame) throw new InvalidDataException("Message is too large.");
        using var input = new MemoryStream(bytes.ToArray());
        using var reader = XmlReader.Create(input, new() { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = MaxFrame });
        var root = XElement.Load(reader);
        if (root.Name != "lmcmessage" || root.Element("head") is not { } head || root.Element("body") is not { } body)
            throw new InvalidDataException("Invalid LAN Messenger XML.");
        // LAN Messenger reuses one message object when sending a group message to several people and
        // appends a fresh header for each recipient, so later copies carry several from/to/messageid/type
        // elements. The last of each belongs to this copy.
        string Last(string name) => (string?)head.Elements(name).LastOrDefault() ?? "";
        var from = Last("from");
        if (from.Length is < 1 or > 61) throw new InvalidDataException("Invalid peer identifier.");
        return new(Last("type"), from, Last("to"), Last("messageid"),
            body.Elements().GroupBy(e => e.Name.LocalName).ToDictionary(g => g.Key, g => g.First().Value));
    }

    public static async Task WriteFrameAsync(Stream stream, string type, byte[] payload, CancellationToken ct)
    {
        if (type.Length != 6 || payload.Length > MaxFrame - 6) throw new InvalidDataException("Invalid frame.");
        var frame = new byte[10 + payload.Length];
        BinaryPrimitives.WriteInt32BigEndian(frame, payload.Length + 6);
        Encoding.ASCII.GetBytes(type, frame.AsSpan(4, 6));
        payload.CopyTo(frame, 10);
        await stream.WriteAsync(frame, ct);
    }

    public static async Task<(string Type, byte[] Payload)> ReadFrameAsync(Stream stream, CancellationToken ct)
    {
        var header = new byte[4];
        await stream.ReadExactlyAsync(header, ct);
        int size = BinaryPrimitives.ReadInt32BigEndian(header);
        if (size < 6 || size > MaxFrame) throw new InvalidDataException("Invalid frame length.");
        var frame = new byte[size];
        await stream.ReadExactlyAsync(frame, ct);
        return (Encoding.ASCII.GetString(frame, 0, 6), frame[6..]);
    }
}

public sealed class LegacyCipher : IDisposable
{
    private readonly Aes aes = Aes.Create();
    public LegacyCipher(byte[] material)
    {
        if (material.Length != 48) throw new CryptographicException("Expected a 32-byte key and 16-byte IV.");
        aes.Key = material[..32]; aes.IV = material[32..];
    }
    // LAN Messenger resets each packet to the negotiated IV. Preserve this legacy
    // behavior for compatibility; it is not an authenticated modern channel.
    // A session may read from more than one socket (see LanService.RunSpareChannelAsync), so serialize use of the key.
    public byte[] Encrypt(byte[] clear) { lock (aes) return aes.EncryptCbc(clear, aes.IV, PaddingMode.PKCS7); }
    public byte[] Decrypt(byte[] cipher) { lock (aes) return aes.DecryptCbc(cipher, aes.IV, PaddingMode.PKCS7); }
    public void Dispose() => aes.Dispose();
}
