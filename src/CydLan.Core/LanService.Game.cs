namespace CydLan.Core;

public sealed partial class LanService
{
    /// <summary>Message operations of the companion battle protocol.</summary>
    public static readonly IReadOnlySet<string> GameOps = new HashSet<string>(StringComparer.Ordinal)
        { "invite", "accept", "decline", "cancel", "move", "leave", "alive" };

    private static readonly Dictionary<string, int> GameFieldLimits = new(StringComparer.Ordinal)
    {
        ["op"] = 12, ["bid"] = 40, ["card"] = PetGame.MaxCardChars, ["seed"] = 24, ["turn"] = 6, ["move"] = 12, ["hash"] = 24, ["reason"] = 24
    };

    private void HandleGame(Session session, WireMessage message)
    {
        string op = message.Get("op");
        string bid = message.Get("bid");
        if (!GameOps.Contains(op) || bid.Length is < 1 or > 40 || !bid.All(char.IsAsciiLetterOrDigit)) return;
        var fields = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (key, limit) in GameFieldLimits)
        {
            string value = message.Get(key);
            if (value.Length > limit) return;
            if (value.Length > 0) fields[key] = value;
        }
        GameMessageReceived?.Invoke(session.Id, fields);
    }

    /// <summary>Sends a companion battle message. Throws <see cref="IOException"/> when the peer is offline.</summary>
    public Task SendGameAsync(string peerId, string op, string battleId, IReadOnlyDictionary<string, string>? fields = null)
    {
        if (!GameOps.Contains(op)) throw new ArgumentException("Unknown game operation.", nameof(op));
        var body = new Dictionary<string, string> { ["op"] = op, ["bid"] = battleId };
        if (fields is not null)
            foreach (var (key, value) in fields)
                if (GameFieldLimits.TryGetValue(key, out int limit) && key is not ("op" or "bid"))
                    body[key] = value.Length > limit ? value[..limit] : value;
        return SendAsync(peerId, "cydgame", body);
    }
}
