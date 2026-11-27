using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CydLan.Core;

/// <summary>Compact wire encoding so CydLan peers share companion customizations safely in userdata/query messages.</summary>
public static class CompanionProfileCodec
{
    public const string Prefix = "CYDPET1:";
    public const int MaxLength = 768;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private sealed class WirePet
    {
        public string? Bo { get; set; }
        public string? Ea { get; set; }
        public string? Ey { get; set; }
        public string? Br { get; set; }
        public string? Mo { get; set; }
        public string? Ch { get; set; }
        public string? Gl { get; set; }
        public string? Ha { get; set; }
        public string? Ou { get; set; }
        public string? Ba { get; set; }
        public string? Pr { get; set; }
        public string? Fe { get; set; }
        public string? Pa { get; set; }
        public string? Vi { get; set; }
        public string? Au { get; set; }
        public string? Bc { get; set; }
        public string? Ac { get; set; }
        public string? Dc { get; set; }
        public double? Sz { get; set; }
    }

    public static string Encode(AppSettings s)
    {
        static string? NonDef(string? v, string d)
        {
            string t = (v ?? "").Trim().ToLowerInvariant();
            return t.Length == 0 || t == d ? null : t;
        }
        static string? NonEmptyHex(string? v)
        {
            string t = (v ?? "").Trim().ToUpperInvariant();
            return t.Length == 0 ? null : t;
        }
        double size = double.IsFinite(s.PetSize) ? Math.Round(Math.Clamp(s.PetSize, .75, 5.0), 2) : 1.0;

        var wire = new WirePet
        {
            Bo = NonDef(s.PetBody, "squircle"),
            Ea = NonDef(s.PetEars, "auto"),
            Ey = NonDef(s.PetEyes, "dot"),
            Br = NonDef(s.PetBrows, "none"),
            Mo = NonDef(s.PetMouth, "smile"),
            Ch = NonDef(s.PetCheeks, "blush"),
            Gl = NonDef(s.PetGlasses, "none"),
            Ha = NonDef(s.PetHat, "auto"),
            Ou = NonDef(s.PetOutfit, "auto"),
            Ba = NonDef(s.PetBack, "none"),
            Pr = NonDef(s.PetProp, "none"),
            Fe = NonDef(s.PetFeet, "paws"),
            Pa = NonDef(s.PetPattern, "solid"),
            Vi = NonDef(s.PetVibe, "bouncy"),
            Au = NonDef(s.PetAura, "none"),
            Bc = NonEmptyHex(s.PetBodyColor),
            Ac = NonEmptyHex(s.PetAccentColor),
            Dc = NonEmptyHex(s.PetDetailColor),
            Sz = Math.Abs(size - 1.0) > .01 ? size : null
        };

        byte[] json = JsonSerializer.SerializeToUtf8Bytes(wire, JsonOptions);
        string b64 = Convert.ToBase64String(json).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        return Prefix + b64;
    }

    public static string SanitizeWireCode(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "";
        string trimmed = raw.Trim();
        if (!trimmed.StartsWith(Prefix, StringComparison.Ordinal) || trimmed.Length > MaxLength) return "";
        return TryDecodeInto(trimmed, new AppSettings()) ? trimmed : "";
    }

    public static bool TryDecodeInto(string? code, AppSettings target)
    {
        if (string.IsNullOrWhiteSpace(code)) return false;
        string trimmed = code.Trim();
        if (!trimmed.StartsWith(Prefix, StringComparison.Ordinal) || trimmed.Length > MaxLength) return false;
        string payload = trimmed[Prefix.Length..].Replace('-', '+').Replace('_', '/');
        int mod = payload.Length % 4;
        if (mod == 1) return false;
        if (mod > 0) payload += new string('=', 4 - mod);

        try
        {
            byte[] json = Convert.FromBase64String(payload);
            var wire = JsonSerializer.Deserialize<WirePet>(json, JsonOptions);
            if (wire is null) return false;

            target.PetBody = wire.Bo ?? "squircle";
            target.PetEars = wire.Ea ?? "auto";
            target.PetEyes = wire.Ey ?? "dot";
            target.PetBrows = wire.Br ?? "none";
            target.PetMouth = wire.Mo ?? "smile";
            target.PetCheeks = wire.Ch ?? "blush";
            target.PetGlasses = wire.Gl ?? "none";
            target.PetHat = wire.Ha ?? "auto";
            target.PetOutfit = wire.Ou ?? "auto";
            target.PetBack = wire.Ba ?? "none";
            target.PetProp = wire.Pr ?? "none";
            target.PetFeet = wire.Fe ?? "paws";
            target.PetPattern = wire.Pa ?? "solid";
            target.PetVibe = wire.Vi ?? "bouncy";
            target.PetAura = wire.Au ?? "none";
            target.PetBodyColor = wire.Bc ?? "";
            target.PetAccentColor = wire.Ac ?? "";
            target.PetDetailColor = wire.Dc ?? "";
            target.PetSize = wire.Sz is { } sz && double.IsFinite(sz) ? Math.Clamp(sz, .75, 5.0) : 1.0;
            return true;
        }
        catch (Exception e) when (e is FormatException or JsonException)
        {
            return false;
        }
    }
}
