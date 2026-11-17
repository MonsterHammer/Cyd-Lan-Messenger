namespace CydLan.Core;

/// <summary>
/// LAN Messenger's emoticons. They travel as plain text codes and each client draws its own picture in their place,
/// so CydLan uses the same codes and replacement order (LAN Messenger 1.2.39 <c>uidefinitions.h</c> / <c>ChatHelper::decodeSmileys</c>).
/// </summary>
public static class Smileys
{
    /// <summary>Pictures 1–70, in LAN Messenger's picker order.</summary>
    public const int Count = 70;

    /// <summary>Every recognised code, in the order LAN Messenger replaces them, with its picture number (1-based).</summary>
    public static readonly (string Code, int Picture)[] Map = BuildMap();

    /// <summary>The code the picker inserts for each picture (index 0 is picture 1).</summary>
    public static IReadOnlyList<string> PickerCodes { get; } = Map.Take(Count).Select(m => m.Code).ToArray();

    private static (string, int)[] BuildMap()
    {
        string[] codes = [
            ":-)", ":-D", ";-)", ":-O", ":-P", "(H)", ":-@", ":-S", ":-$", ":-(",
            ":'(", ":-|", "(ST)", "8o|", "8-|", "+o(", "<:o)", "|-)", "*-)", ":-#",
            ":-*", "^o)", "8-)", "(L)", "(U)", "(D)", "(B)", "(@)", "(&)", "(SN)",
            "(BAH)", "(S)", "(*)", "(#)", "(R)", "({)", "(})", "(K)", "(F)", "(W)",
            "(O)", "(G)", "(^)", "(P)", "(I)", "(C)", "(T)", "(MP)", "(AU)", "(AP)",
            "(CO)", "(MO)", "(~)", "(8)", "(PI)", "(SO)", "(E)", "(Z)", "(X)", "(IP)",
            "(UM)", "(OK)", "(SS)", "(Y)", "(N)", "(YN)", "(H5)", "(TU)", ":-[", "(M)",
            ":)", ":D", ";)", ":O", ":P", ":@", ":S", ":$", ":(", ":|", ":["];
        int[] aliases = [1, 2, 3, 4, 5, 7, 8, 9, 10, 12, 70];
        return codes.Select((code, i) => (code, i < Count ? i + 1 : aliases[i - Count])).ToArray();
    }

    /// <summary>A run of text, or a picture number when <see cref="Picture"/> is above zero.</summary>
    public readonly record struct Segment(string Text, int Picture);

    /// <summary>
    /// Splits text into plain runs and pictures. Codes are matched case-insensitively and replaced one code at a time
    /// in table order, exactly as LAN Messenger does, so both apps show the same pictures for the same text.
    /// </summary>
    public static List<Segment> Split(string text)
    {
        // Each character is either still plain text (0) or belongs to a picture; a picture is recorded at its first character.
        var owner = new int[text.Length];
        var starts = new Dictionary<int, (int Picture, int Length)>();
        foreach (var (code, picture) in Map)
        {
            int from = 0;
            while (from <= text.Length - code.Length)
            {
                int at = text.IndexOf(code, from, StringComparison.OrdinalIgnoreCase);
                if (at < 0) break;
                if (owner.AsSpan(at, code.Length).ContainsAnyExcept(0)) { from = at + 1; continue; }
                owner.AsSpan(at, code.Length).Fill(picture);
                starts[at] = (picture, code.Length);
                from = at + code.Length;
            }
        }
        var segments = new List<Segment>();
        int runStart = 0;
        for (int i = 0; i < text.Length;)
        {
            if (starts.TryGetValue(i, out var hit))
            {
                if (i > runStart) segments.Add(new(text[runStart..i], 0));
                segments.Add(new(text.Substring(i, hit.Length), hit.Picture));
                i += hit.Length; runStart = i;
            }
            else i++;
        }
        if (runStart < text.Length) segments.Add(new(text[runStart..], 0));
        return segments;
    }

    public static bool HasAny(string text) => text.Length > 1 && Split(text).Any(s => s.Picture > 0);
}
