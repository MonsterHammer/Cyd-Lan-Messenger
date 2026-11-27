using System.Windows;
using System.Windows.Media;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using Pen = System.Windows.Media.Pen;
using Point = System.Windows.Point;

namespace CydLan.Desktop;

public enum ColorFinishKind
{
    Off,
    Pastel,
    SoftMatte,
    Stylized,
    Cream,
    Frosted,
    Velvet,
    Muted,
    Watercolor,
    Vintage,
    Cinema,
    Dusk,
    Vivid
}

public enum ColorPaletteRole
{
    Canvas,
    Rail,
    Sidebar,
    Surface,
    Hover,
    Input,
    Line,
    Ink,
    Muted,
    Faint,
    Accent,
    AccentSoft,
    Status
}

public sealed record ColorFinishSwatch(string Value, string Name, ColorFinishKind Kind);

public readonly record struct ColorPaletteSet(
    Color Canvas,
    Color Rail,
    Color Sidebar,
    Color Surface,
    Color Hover,
    Color Input,
    Color Line,
    Color Ink,
    Color Muted,
    Color Faint,
    Color Accent,
    Color AccentSoft,
    Color AccentInk,
    Color Avatar,
    Color Online,
    Color Away,
    Color Danger,
    Color NavSelected,
    Color RowHover,
    Color RowSelected);

/// <summary>Applies smooth, spirit-preserving color grading and atmospheric surface washes across themes.</summary>
public static class ColorFinish
{
    public const string Off = "off";
    public const string Pastel = "pastel";
    public const string SoftMatte = "softmatte";
    public const string Stylized = "stylized";
    public const string Cream = "cream";
    public const string Frosted = "frosted";
    public const string Velvet = "velvet";
    public const string Muted = "muted";
    public const string Watercolor = "watercolor";
    public const string Vintage = "vintage";
    public const string Cinema = "cinema";
    public const string Dusk = "dusk";
    public const string Vivid = "vivid";

    public static IReadOnlyList<ColorFinishSwatch> Catalog { get; } =
    [
        new(Off, "Original", ColorFinishKind.Off),
        new(Pastel, "Pastel", ColorFinishKind.Pastel),
        new(SoftMatte, "Soft matte", ColorFinishKind.SoftMatte),
        new(Stylized, "Stylized", ColorFinishKind.Stylized),
        new(Cream, "Cream", ColorFinishKind.Cream),
        new(Frosted, "Frosted", ColorFinishKind.Frosted),
        new(Velvet, "Velvet", ColorFinishKind.Velvet),
        new(Muted, "Muted", ColorFinishKind.Muted),
        new(Watercolor, "Watercolor", ColorFinishKind.Watercolor),
        new(Vintage, "Vintage", ColorFinishKind.Vintage),
        new(Cinema, "Cinema", ColorFinishKind.Cinema),
        new(Dusk, "Cozy dusk", ColorFinishKind.Dusk),
        new(Vivid, "Vivid", ColorFinishKind.Vivid)
    ];

    public static string Normalize(string? value) => value switch
    {
        Pastel or SoftMatte or Stylized or Cream or Frosted or Velvet
            or Muted or Watercolor or Vintage or Cinema or Dusk or Vivid => value,
        _ => Off
    };

    public static ColorFinishKind KindFor(string? value) => Normalize(value) switch
    {
        Pastel => ColorFinishKind.Pastel,
        SoftMatte => ColorFinishKind.SoftMatte,
        Stylized => ColorFinishKind.Stylized,
        Cream => ColorFinishKind.Cream,
        Frosted => ColorFinishKind.Frosted,
        Velvet => ColorFinishKind.Velvet,
        Muted => ColorFinishKind.Muted,
        Watercolor => ColorFinishKind.Watercolor,
        Vintage => ColorFinishKind.Vintage,
        Cinema => ColorFinishKind.Cinema,
        Dusk => ColorFinishKind.Dusk,
        Vivid => ColorFinishKind.Vivid,
        _ => ColorFinishKind.Off
    };

    public static ColorPaletteSet TransformPalette(in ColorPaletteSet raw, string? finish, double intensity) =>
        TransformPalette(in raw, KindFor(finish), intensity);

    public static ColorPaletteSet TransformPalette(in ColorPaletteSet raw, ColorFinishKind kind, double intensity)
    {
        double t = Math.Clamp(double.IsFinite(intensity) ? intensity : .65, 0, 1);
        if (kind == ColorFinishKind.Off || t <= .001) return raw;

        bool isDark = AppearanceOptions.RelativeLuminance(raw.Canvas) < .50;
        Color baseAccent = raw.Accent, baseCanvas = raw.Canvas, baseSidebar = raw.Sidebar;

        Color T(Color color, ColorPaletteRole role) => TransformColor(color, role, kind, t, baseAccent, baseCanvas, baseSidebar, isDark);

        Color canvas = T(raw.Canvas, ColorPaletteRole.Canvas);
        Color rail = T(raw.Rail, ColorPaletteRole.Rail);
        Color sidebar = T(raw.Sidebar, ColorPaletteRole.Sidebar);
        Color surface = T(raw.Surface, ColorPaletteRole.Surface);
        Color hover = T(raw.Hover, ColorPaletteRole.Hover);
        Color input = T(raw.Input, ColorPaletteRole.Input);
        Color line = T(raw.Line, ColorPaletteRole.Line);
        Color accent = T(raw.Accent, ColorPaletteRole.Accent);
        Color accentSoft = T(raw.AccentSoft, ColorPaletteRole.AccentSoft);
        Color avatar = T(raw.Avatar, ColorPaletteRole.AccentSoft);
        Color navSelected = T(raw.NavSelected, ColorPaletteRole.AccentSoft);
        Color rowHover = T(raw.RowHover, ColorPaletteRole.Hover);
        Color rowSelected = T(raw.RowSelected, ColorPaletteRole.AccentSoft);
        Color online = T(raw.Online, ColorPaletteRole.Status);
        Color away = T(raw.Away, ColorPaletteRole.Status);
        Color danger = T(raw.Danger, ColorPaletteRole.Status);

        Color ink = EnsureReadableInk(T(raw.Ink, ColorPaletteRole.Ink), canvas, sidebar, isDark);
        Color muted = EnsureMutedContrast(T(raw.Muted, ColorPaletteRole.Muted), canvas, ink, isDark, 3.8);
        Color faint = EnsureMutedContrast(T(raw.Faint, ColorPaletteRole.Faint), canvas, ink, isDark, 2.7);
        Color accentInk = AppearanceOptions.RelativeLuminance(accent) >= .56
            ? Color.FromRgb(0x16, 0x1B, 0x20)
            : Colors.White;

        return new ColorPaletteSet(
            canvas, rail, sidebar, surface, hover, input, line,
            ink, muted, faint, accent, accentSoft, accentInk,
            avatar, online, away, danger, navSelected, rowHover, rowSelected);
    }

    public static Color TransformColor(
        Color raw,
        ColorPaletteRole role,
        ColorFinishKind kind,
        double intensity,
        Color rawAccent,
        Color rawCanvas,
        Color rawSidebar,
        bool isDark)
    {
        double t = Math.Clamp(double.IsFinite(intensity) ? intensity : .65, 0, 1);
        if (kind == ColorFinishKind.Off || t <= .001) return raw;

        Color target = ComputeTargetColor(raw, role, kind, rawAccent, rawCanvas, rawSidebar, isDark);
        return Mix(raw, target, t);
    }

    public static Brush BuildWashBrush(
        Color surfaceColor,
        Color accentColor,
        Color secondaryColor,
        ColorFinishKind kind,
        double intensity,
        bool isChat,
        bool isDark)
    {
        double t = Math.Clamp(double.IsFinite(intensity) ? intensity : .65, 0, 1);
        if (kind == ColorFinishKind.Off || t <= .001) return Brushes.Transparent;

        double scale = (isChat ? .82 : 1.0) * (.35 + .65 * t);
        byte Alpha(double a) => (byte)Math.Clamp(Math.Round(a * scale), 0, 110);

        Color lightCloud = Color.FromRgb(0xFF, 0xFF, 0xFF);
        Color warmCream = Color.FromRgb(0xFF, 0xF5, 0xDC);
        Color frostBlue = Color.FromRgb(0xDC, 0xEE, 0xFF);
        Color duskRose = Color.FromRgb(0xEC, 0xCE, 0xEA);
        Color duskGold = Color.FromRgb(0xF8, 0xDC, 0xB8);
        Color cineTeal = Color.FromRgb(0x16, 0x4E, 0x63);
        Color vintageSepia = Color.FromRgb(0xDF, 0xC8, 0x9E);

        var (c0, c1, c2, p0, p1) = kind switch
        {
            ColorFinishKind.Pastel => (
                WithAlpha(Mix(lightCloud, accentColor, isDark ? .26 : .14), Alpha(isDark ? 24 : 36)),
                WithAlpha(Mix(surfaceColor, accentColor, .18), Alpha(6)),
                WithAlpha(Mix(lightCloud, accentColor, isDark ? .34 : .28), Alpha(isDark ? 26 : 32)),
                new Point(0, 0), new Point(1, 1)),

            ColorFinishKind.SoftMatte => (
                WithAlpha(isDark ? Mix(surfaceColor, lightCloud, .16) : lightCloud, Alpha(isDark ? 22 : 28)),
                WithAlpha(surfaceColor, Alpha(4)),
                WithAlpha(isDark ? Colors.Black : Mix(secondaryColor, surfaceColor, .5), Alpha(isDark ? 28 : 20)),
                new Point(0, 0), new Point(0, 1)),

            ColorFinishKind.Stylized => (
                WithAlpha(Mix(secondaryColor, accentColor, .25), Alpha(isDark ? 28 : 24)),
                WithAlpha(Mix(surfaceColor, accentColor, .08), Alpha(6)),
                WithAlpha(Mix(accentColor, isDark ? lightCloud : secondaryColor, .22), Alpha(isDark ? 34 : 26)),
                new Point(0, 0), new Point(1, 1)),

            ColorFinishKind.Cream => (
                WithAlpha(warmCream, Alpha(isDark ? 20 : 42)),
                WithAlpha(Mix(warmCream, surfaceColor, .65), Alpha(10)),
                WithAlpha(Mix(warmCream, secondaryColor, .55), Alpha(isDark ? 18 : 30)),
                new Point(0, 0), new Point(1, .85)),

            ColorFinishKind.Frosted => (
                WithAlpha(frostBlue, Alpha(isDark ? 28 : 48)),
                WithAlpha(lightCloud, Alpha(isDark ? 10 : 18)),
                WithAlpha(Mix(frostBlue, accentColor, .22), Alpha(isDark ? 26 : 34)),
                new Point(.1, 0), new Point(.9, 1)),

            ColorFinishKind.Velvet => (
                WithAlpha(Mix(surfaceColor, accentColor, .20), Alpha(isDark ? 22 : 16)),
                WithAlpha(surfaceColor, Alpha(4)),
                WithAlpha(isDark ? Color.FromRgb(0x0B, 0x07, 0x12) : Mix(surfaceColor, Color.FromRgb(0x4A, 0x3D, 0x35), .42), Alpha(isDark ? 48 : 30)),
                new Point(0, 0), new Point(1, 1)),

            ColorFinishKind.Muted => (
                WithAlpha(isDark ? Color.FromRgb(0x88, 0x90, 0x99) : Color.FromRgb(0xF1, 0xF1, 0xEF), Alpha(isDark ? 16 : 26)),
                WithAlpha(surfaceColor, Alpha(4)),
                WithAlpha(isDark ? Color.FromRgb(0x10, 0x12, 0x16) : Color.FromRgb(0xDC, 0xDF, 0xE2), Alpha(isDark ? 22 : 20)),
                new Point(0, 0), new Point(1, 1)),

            ColorFinishKind.Watercolor => (
                WithAlpha(Mix(lightCloud, accentColor, .32), Alpha(isDark ? 28 : 38)),
                WithAlpha(Mix(surfaceColor, lightCloud, .30), Alpha(12)),
                WithAlpha(Mix(secondaryColor, accentColor, .42), Alpha(isDark ? 34 : 34)),
                new Point(.15, 0), new Point(.85, 1)),

            ColorFinishKind.Vintage => (
                WithAlpha(warmCream, Alpha(isDark ? 20 : 34)),
                WithAlpha(vintageSepia, Alpha(isDark ? 14 : 18)),
                WithAlpha(Mix(vintageSepia, secondaryColor, .45), Alpha(isDark ? 34 : 28)),
                new Point(0, 0), new Point(1, 1)),

            ColorFinishKind.Cinema => (
                WithAlpha(Mix(cineTeal, isDark ? Colors.Black : lightCloud, isDark ? .35 : .68), Alpha(isDark ? 42 : 26)),
                WithAlpha(surfaceColor, Alpha(4)),
                WithAlpha(Mix(duskGold, accentColor, .32), Alpha(isDark ? 28 : 26)),
                new Point(0, 0), new Point(1, 1)),

            ColorFinishKind.Dusk => (
                WithAlpha(duskGold, Alpha(isDark ? 22 : 34)),
                WithAlpha(Mix(duskRose, surfaceColor, .55), Alpha(12)),
                WithAlpha(Mix(duskRose, accentColor, .25), Alpha(isDark ? 34 : 30)),
                new Point(0, 0), new Point(1, 1)),

            _ => ( // Vivid
                WithAlpha(Mix(accentColor, lightCloud, isDark ? .12 : .24), Alpha(isDark ? 30 : 22)),
                WithAlpha(surfaceColor, Alpha(4)),
                WithAlpha(Mix(accentColor, secondaryColor, .45), Alpha(isDark ? 34 : 24)),
                new Point(0, 0), new Point(1, 1))
        };

        var brush = new LinearGradientBrush
        {
            StartPoint = p0,
            EndPoint = p1,
            GradientStops =
            [
                new GradientStop(c0, 0),
                new GradientStop(c1, .48),
                new GradientStop(c2, 1)
            ]
        };
        brush.Freeze();
        return brush;
    }

    private static Color ComputeTargetColor(
        Color raw,
        ColorPaletteRole role,
        ColorFinishKind kind,
        Color rawAccent,
        Color rawCanvas,
        Color rawSidebar,
        bool isDark)
    {
        if (role == ColorPaletteRole.Status)
            return TransformStatusColor(raw, kind, isDark);

        if (role is ColorPaletteRole.Ink or ColorPaletteRole.Muted or ColorPaletteRole.Faint)
            return TransformInkColor(raw, role, kind, isDark);

        return kind switch
        {
            ColorFinishKind.Pastel => ApplyPastel(raw, role, rawAccent, isDark),
            ColorFinishKind.SoftMatte => ApplySoftMatte(raw, role, rawAccent, isDark),
            ColorFinishKind.Stylized => ApplyStylized(raw, role, rawAccent, isDark),
            ColorFinishKind.Cream => ApplyTintedSmoothing(raw, role, rawAccent, isDark,
                lightNeutral: Color.FromRgb(0xFA, 0xF3, 0xE2), darkNeutral: Color.FromRgb(0x24, 0x1E, 0x17),
                surfaceMix: .34, accentMix: .14, maxSurfaceS: .36, hueShift: 4),
            ColorFinishKind.Frosted => ApplyTintedSmoothing(raw, role, rawAccent, isDark,
                lightNeutral: Color.FromRgb(0xEE, 0xF5, 0xFC), darkNeutral: Color.FromRgb(0x16, 0x1E, 0x28),
                surfaceMix: .34, accentMix: .18, maxSurfaceS: .34, hueShift: -8),
            ColorFinishKind.Velvet => ApplyVelvet(raw, role, rawAccent, isDark),
            ColorFinishKind.Muted => ApplyMuted(raw, role, isDark),
            ColorFinishKind.Watercolor => ApplyWatercolor(raw, role, rawAccent, isDark),
            ColorFinishKind.Vintage => ApplyVintage(raw, role, isDark),
            ColorFinishKind.Cinema => ApplyCinema(raw, role, rawAccent, isDark),
            ColorFinishKind.Dusk => ApplyTintedSmoothing(raw, role, rawAccent, isDark,
                lightNeutral: Color.FromRgb(0xF5, 0xE9, 0xF2), darkNeutral: Color.FromRgb(0x23, 0x19, 0x2C),
                surfaceMix: .28, accentMix: .18, maxSurfaceS: .40, hueShift: -10),
            ColorFinishKind.Vivid => ApplyVivid(raw, role, rawAccent, rawCanvas, rawSidebar, isDark),
            _ => raw
        };
    }

    private static Color ApplyPastel(Color raw, ColorPaletteRole role, Color rawAccent, bool isDark)
    {
        var (h, s, l) = ToHsl(raw);
        if (s < .05) h = ToHsl(rawAccent).H;

        if (role == ColorPaletteRole.Accent)
        {
            double targetS = Math.Clamp(s < .12 ? .42 : s * .68, .36, .64);
            double targetL = isDark ? Math.Clamp(Math.Max(l, .64), .60, .74) : Math.Clamp(Math.Max(l + .12, .50), .48, .66);
            return FromHsl(h, targetS, targetL);
        }

        if (role == ColorPaletteRole.AccentSoft)
        {
            Color pastelWash = Mix(raw, FromHsl(ToHsl(rawAccent).H, .48, isDark ? .28 : .90), .42);
            return CapSaturation(pastelWash, isDark ? .38 : .42);
        }

        if (isDark)
        {
            Color dust = Color.FromRgb(0x23, 0x23, 0x30);
            double maxS = role == ColorPaletteRole.Line ? .28 : .32;
            double liftL = role switch
            {
                ColorPaletteRole.Canvas => Math.Clamp(Math.Max(l, .16), .15, .21),
                ColorPaletteRole.Rail => Math.Clamp(Math.Max(l, .20), .18, .25),
                ColorPaletteRole.Sidebar => Math.Clamp(Math.Max(l, .18), .17, .23),
                ColorPaletteRole.Surface => Math.Clamp(Math.Max(l, .23), .21, .28),
                ColorPaletteRole.Hover => Math.Clamp(Math.Max(l, .27), .24, .32),
                ColorPaletteRole.Input => Math.Clamp(Math.Max(l, .20), .18, .25),
                _ => Math.Clamp(Math.Max(l, .29), .26, .35)
            };
            Color lifted = FromHsl(h, Math.Min(s, maxS), liftL);
            return Mix(lifted, Mix(dust, FromHsl(ToHsl(rawAccent).H, .28, liftL), .35), .24);
        }
        else
        {
            Color cloud = Color.FromRgb(0xFB, 0xFA, 0xFD);
            double maxS = role switch
            {
                ColorPaletteRole.Canvas => .30,
                ColorPaletteRole.Input => .24,
                ColorPaletteRole.Line => .32,
                _ => .36
            };
            double targetL = role switch
            {
                ColorPaletteRole.Canvas => Lerp(l, .96, .55),
                ColorPaletteRole.Input => Lerp(l, .98, .60),
                ColorPaletteRole.Sidebar => Lerp(l, .93, .55),
                ColorPaletteRole.Rail => Lerp(l, .90, .55),
                ColorPaletteRole.Surface => Lerp(l, .91, .50),
                ColorPaletteRole.Hover => Lerp(l, .87, .45),
                ColorPaletteRole.Line => Lerp(l, .85, .45),
                _ => l
            };
            Color calmed = FromHsl(h, Math.Min(s, maxS), Math.Clamp(targetL, .82, .98));
            return Mix(calmed, cloud, .18);
        }
    }

    private static Color ApplySoftMatte(Color raw, ColorPaletteRole role, Color rawAccent, bool isDark)
    {
        var (h, s, l) = ToHsl(raw);
        if (role == ColorPaletteRole.Accent)
            return FromHsl(h, Math.Clamp(s * .76, .28, .68), isDark ? Math.Clamp(l, .50, .70) : Math.Clamp(l, .36, .56));

        if (role == ColorPaletteRole.AccentSoft)
        {
            Color matteAccent = Mix(raw, FromHsl(ToHsl(rawAccent).H, .34, isDark ? .26 : .89), .38);
            return CapSaturation(matteAccent, .34);
        }

        Color matteNeutral = isDark ? Color.FromRgb(0x1E, 0x20, 0x24) : Color.FromRgb(0xF3, 0xF2, 0xEE);
        double maxS = role is ColorPaletteRole.Canvas or ColorPaletteRole.Input ? .22 : .28;
        Color softened = FromHsl(h, Math.Min(s, maxS), isDark
            ? Math.Clamp(l < .12 ? Lerp(l, .16, .65) : l, .14, .30)
            : Math.Clamp(l > .97 ? .95 : Lerp(l, .92, .35), .76, .96));
        return Mix(softened, matteNeutral, role is ColorPaletteRole.Canvas or ColorPaletteRole.Input ? .26 : .34);
    }

    private static Color ApplyStylized(Color raw, ColorPaletteRole role, Color rawAccent, bool isDark)
    {
        var (h, s, l) = ToHsl(raw);
        var (ah, _, _) = ToHsl(rawAccent);
        double baseH = s < .06 ? ah : h;

        if (role == ColorPaletteRole.Accent)
            return FromHsl(ah, Math.Clamp(s < .20 ? .58 : s * 1.08, .54, .82), isDark ? Math.Clamp(l, .56, .72) : Math.Clamp(l, .38, .54));

        if (role == ColorPaletteRole.AccentSoft)
        {
            Color rich = FromHsl(ah, isDark ? .42 : .44, isDark ? .27 : .90);
            return Mix(CapSaturation(raw, .42), rich, .52);
        }

        double warmOrCoolShift = baseH is >= 35 and <= 75 ? -6 : 8;
        double shift = role switch
        {
            ColorPaletteRole.Rail => -8,
            ColorPaletteRole.Sidebar => warmOrCoolShift * .5,
            ColorPaletteRole.Surface or ColorPaletteRole.Hover => warmOrCoolShift,
            ColorPaletteRole.Line => -6,
            _ => 0
        };
        double targetS = role switch
        {
            ColorPaletteRole.Canvas or ColorPaletteRole.Input => Math.Min(s, .20),
            ColorPaletteRole.Rail => Math.Clamp(Math.Max(Math.Min(s, .42), .16), .12, .42),
            ColorPaletteRole.Surface or ColorPaletteRole.Hover => Math.Min(s, .34),
            _ => Math.Min(Math.Max(s * .60, .14), .38)
        };
        double targetL = isDark
            ? Math.Clamp(l, .11, .28)
            : Math.Clamp(role switch
            {
                ColorPaletteRole.Canvas => Math.Max(l, .95),
                ColorPaletteRole.Surface => Lerp(l, .92, .45),
                ColorPaletteRole.Hover => Lerp(l, .87, .35),
                _ => l
            }, .78, .97);
        Color hueShifted = FromHsl(NormalizeHue(baseH + shift), targetS, targetL);

        double accentInfuse = role switch
        {
            ColorPaletteRole.Rail => .28,
            ColorPaletteRole.Sidebar => .10,
            ColorPaletteRole.Line => .22,
            _ => .08
        };
        Color harmonizer = FromHsl(ah, isDark ? .36 : .34, isDark ? .23 : .90);
        return Mix(hueShifted, harmonizer, accentInfuse);
    }

    private static Color ApplyTintedSmoothing(
        Color raw,
        ColorPaletteRole role,
        Color rawAccent,
        bool isDark,
        Color lightNeutral,
        Color darkNeutral,
        double surfaceMix,
        double accentMix,
        double maxSurfaceS,
        double hueShift)
    {
        var (h, s, l) = ToHsl(raw);
        if (role == ColorPaletteRole.Accent)
        {
            Color accTint = isDark ? Mix(lightNeutral, Colors.White, .35) : Mix(lightNeutral, rawAccent, .55);
            return Mix(FromHsl(NormalizeHue(h + hueShift * .4), Math.Clamp(s * .82, .24, .74), l), accTint, .10);
        }

        Color anchor = isDark ? darkNeutral : lightNeutral;
        if (role == ColorPaletteRole.AccentSoft)
            return Mix(CapSaturation(raw, maxSurfaceS + .08), Mix(anchor, rawAccent, accentMix + .14), .42);

        Color calmed = FromHsl(NormalizeHue(h + (s > .06 ? hueShift : 0)), Math.Min(s, maxSurfaceS), isDark
            ? Math.Clamp(l, .13, .29)
            : Math.Clamp(l, .78, .96));
        return Mix(calmed, anchor, role is ColorPaletteRole.Canvas or ColorPaletteRole.Input ? surfaceMix * .85 : surfaceMix);
    }

    private static Color ApplyVelvet(Color raw, ColorPaletteRole role, Color rawAccent, bool isDark)
    {
        var (h, s, l) = ToHsl(raw);
        if (role == ColorPaletteRole.Accent)
            return FromHsl(h, Math.Clamp(s * .88, .35, .78), isDark ? Math.Clamp(l - .03, .46, .66) : Math.Clamp(l - .06, .30, .48));

        if (role == ColorPaletteRole.AccentSoft)
            return Mix(CapSaturation(raw, .40), FromHsl(ToHsl(rawAccent).H, .34, isDark ? .22 : .83), .42);

        if (isDark)
        {
            Color velvetDeep = Color.FromRgb(0x16, 0x11, 0x1F);
            double darkL = role == ColorPaletteRole.Canvas ? Math.Clamp(l * .82, .07, .15) : Math.Clamp(l * .90, .10, .24);
            return Mix(FromHsl(h, Math.Min(s * .76, .34), darkL), velvetDeep, .26);
        }

        double maxS = role is ColorPaletteRole.Canvas or ColorPaletteRole.Input ? .28 : .36;
        double lowerL = role switch
        {
            ColorPaletteRole.Canvas => Math.Clamp(l - .06, .84, .91),
            ColorPaletteRole.Input => Math.Clamp(l - .04, .88, .94),
            _ => Math.Clamp(l - .05, .74, .88)
        };
        return Mix(FromHsl(h, Math.Min(s, maxS), lowerL), Color.FromRgb(0xE8, 0xE2, 0xDC), .18);
    }

    private static Color ApplyMuted(Color raw, ColorPaletteRole role, bool isDark)
    {
        var (h, s, l) = ToHsl(raw);
        if (role == ColorPaletteRole.Accent)
            return FromHsl(h, Math.Min(s * .55, .48), isDark ? Math.Clamp(l, .50, .68) : Math.Clamp(l, .36, .54));

        if (role == ColorPaletteRole.AccentSoft)
            return FromHsl(h, Math.Min(s * .42, .26), isDark ? Math.Clamp(l, .18, .28) : Math.Clamp(l, .84, .94));

        Color neutral = isDark ? Color.FromRgb(0x1F, 0x22, 0x27) : Color.FromRgb(0xF1, 0xF2, 0xF3);
        Color desat = FromHsl(h, Math.Min(s * .32, .18), isDark ? Math.Clamp(l, .13, .28) : Math.Clamp(l, .78, .96));
        return Mix(desat, neutral, .30);
    }

    private static Color ApplyWatercolor(Color raw, ColorPaletteRole role, Color rawAccent, bool isDark)
    {
        var (h, s, l) = ToHsl(raw);
        var (ah, _, _) = ToHsl(rawAccent);
        if (role == ColorPaletteRole.Accent)
            return FromHsl(ah, Math.Clamp(s * .84, .34, .74), isDark ? Math.Clamp(l + .04, .52, .70) : Math.Clamp(l + .04, .38, .58));

        Color paper = isDark ? Color.FromRgb(0x19, 0x1D, 0x26) : Color.FromRgb(0xFC, 0xFA, 0xF5);
        if (role is ColorPaletteRole.Canvas or ColorPaletteRole.Input)
            return Mix(CapSaturation(raw, .26), paper, .44);

        double safeDrift = h is >= 35 and <= 75 ? -8 : 10;
        double drift = role is ColorPaletteRole.Rail or ColorPaletteRole.Line ? -10 : safeDrift;
        Color washed = FromHsl(NormalizeHue((s < .06 ? ah : h) + drift), Math.Min(s, .38), isDark
            ? Math.Clamp(l, .14, .28)
            : Math.Clamp(l, .80, .95));
        Color pigment = FromHsl(ah, .42, isDark ? .26 : .88);
        return Mix(Mix(washed, paper, .22), pigment, role == ColorPaletteRole.AccentSoft ? .40 : .16);
    }

    private static Color ApplyVintage(Color raw, ColorPaletteRole role, bool isDark)
    {
        var (h, s, l) = ToHsl(raw);
        Color filmCream = isDark ? Color.FromRgb(0x28, 0x22, 0x1A) : Color.FromRgb(0xF4, 0xEC, 0xD8);
        if (role == ColorPaletteRole.Accent)
        {
            Color warmAcc = FromHsl(NormalizeHue(h + 8), Math.Clamp(s * .70, .30, .66), isDark ? Math.Clamp(l, .52, .68) : Math.Clamp(l, .38, .54));
            return Mix(warmAcc, Color.FromRgb(0xC9, 0x9E, 0x67), .14);
        }

        double agedL = isDark ? Math.Clamp(Math.Max(l, .15), .15, .30) : Math.Clamp(Lerp(l, .90, .25), .76, .95);
        Color aged = FromHsl(NormalizeHue(h + (s > .06 ? 6 : 0)), Math.Min(s * .58, .32), agedL);
        return Mix(aged, filmCream, role is ColorPaletteRole.Canvas or ColorPaletteRole.Input ? .34 : .28);
    }

    private static Color ApplyCinema(Color raw, ColorPaletteRole role, Color rawAccent, bool isDark)
    {
        var (h, s, l) = ToHsl(raw);
        if (role == ColorPaletteRole.Accent)
            return FromHsl(h, Math.Clamp(s * .96, .42, .82), isDark ? Math.Clamp(l, .52, .68) : Math.Clamp(l, .36, .54));

        Color coolShadow = isDark ? Color.FromRgb(0x13, 0x21, 0x2B) : Color.FromRgb(0xE4, 0xEE, 0xF2);
        Color warmHighlight = isDark ? Color.FromRgb(0x29, 0x21, 0x1A) : Color.FromRgb(0xFB, 0xF3, 0xE6);

        Color graded = CapSaturation(raw, .38);
        return role switch
        {
            ColorPaletteRole.Rail or ColorPaletteRole.Line or ColorPaletteRole.Hover => Mix(graded, coolShadow, .26),
            ColorPaletteRole.Surface or ColorPaletteRole.Input or ColorPaletteRole.AccentSoft => Mix(graded, warmHighlight, .24),
            ColorPaletteRole.Canvas => Mix(graded, isDark ? Color.FromRgb(0x14, 0x1D, 0x24) : Color.FromRgb(0xF6, 0xF5, 0xF0), .24),
            _ => Mix(graded, Mix(coolShadow, rawAccent, .14), .20)
        };
    }

    private static Color ApplyVivid(Color raw, ColorPaletteRole role, Color rawAccent, Color rawCanvas, Color rawSidebar, bool isDark)
    {
        var (h, s, l) = ToHsl(raw);
        var (ah, _, _) = ToHsl(rawAccent);
        double hue = s < .05 ? ah : h;

        if (role == ColorPaletteRole.Accent)
            return FromHsl(ah, Math.Clamp(Math.Max(s * 1.24, .68), .62, .92), isDark ? Math.Clamp(l, .54, .68) : Math.Clamp(l, .38, .52));

        if (role == ColorPaletteRole.AccentSoft)
            return FromHsl(ah, isDark ? .48 : .44, isDark ? .28 : .89);

        if (role == ColorPaletteRole.Canvas)
            return FromHsl(hue, Math.Min(s, isDark ? .34 : .24), isDark ? Math.Clamp(l * .88, .08, .15) : Math.Clamp(Math.Max(l, .95), .94, .98));

        double liftedS = s > .58 ? Math.Min(s * .72, .52) : Math.Clamp(Math.Max(s * 1.18, .14), .12, .48);
        return FromHsl(hue, liftedS, isDark ? Math.Clamp(l, .12, .26) : Math.Clamp(l, .78, .94));
    }

    private static Color TransformStatusColor(Color raw, ColorFinishKind kind, bool isDark)
    {
        var (h, s, l) = ToHsl(raw);
        return kind switch
        {
            ColorFinishKind.Pastel => FromHsl(h, Math.Clamp(s * .68, .38, .66), isDark ? Math.Clamp(l + .08, .58, .76) : Math.Clamp(l + .08, .44, .62)),
            ColorFinishKind.Muted => FromHsl(h, Math.Clamp(s * .62, .28, .56), l),
            ColorFinishKind.Vintage => Mix(FromHsl(h, Math.Clamp(s * .74, .32, .68), l), Color.FromRgb(0xC9, 0xAD, 0x80), .12),
            ColorFinishKind.Vivid => FromHsl(h, Math.Clamp(s * 1.15, .52, .88), l),
            _ => FromHsl(h, Math.Clamp(s * .86, .36, .78), l)
        };
    }

    private static Color TransformInkColor(Color raw, ColorPaletteRole role, ColorFinishKind kind, bool isDark)
    {
        Color tint = kind switch
        {
            ColorFinishKind.Cream or ColorFinishKind.Vintage => isDark ? Color.FromRgb(0xF8, 0xEE, 0xDC) : Color.FromRgb(0x36, 0x2A, 0x1D),
            ColorFinishKind.Frosted or ColorFinishKind.Cinema => isDark ? Color.FromRgb(0xEA, 0xF3, 0xFB) : Color.FromRgb(0x16, 0x24, 0x33),
            ColorFinishKind.Velvet or ColorFinishKind.Dusk => isDark ? Color.FromRgb(0xF3, 0xEB, 0xF6) : Color.FromRgb(0x28, 0x1E, 0x2D),
            _ => raw
        };
        double mixAmt = role == ColorPaletteRole.Ink ? .18 : .24;
        return kind is ColorFinishKind.Cream or ColorFinishKind.Vintage or ColorFinishKind.Frosted or ColorFinishKind.Cinema or ColorFinishKind.Velvet or ColorFinishKind.Dusk
            ? Mix(raw, tint, mixAmt)
            : raw;
    }

    private static Color EnsureReadableInk(Color ink, Color canvas, Color sidebar, bool isDark)
    {
        double bgLuma = isDark
            ? Math.Max(AppearanceOptions.RelativeLuminance(canvas), AppearanceOptions.RelativeLuminance(sidebar))
            : Math.Min(AppearanceOptions.RelativeLuminance(canvas), AppearanceOptions.RelativeLuminance(sidebar));

        double ratio = ContrastRatio(AppearanceOptions.RelativeLuminance(ink), bgLuma);
        if (ratio >= 6.5) return ink;

        Color anchor = isDark ? Color.FromRgb(0xF4, 0xF6, 0xF9) : Color.FromRgb(0x16, 0x1B, 0x22);
        for (double step = .2; step <= 1.0; step += .2)
        {
            Color candidate = Mix(ink, anchor, step);
            if (ContrastRatio(AppearanceOptions.RelativeLuminance(candidate), bgLuma) >= 6.5)
                return candidate;
        }
        return anchor;
    }

    private static Color EnsureMutedContrast(Color fg, Color canvas, Color ink, bool isDark, double minRatio)
    {
        double bgLuma = AppearanceOptions.RelativeLuminance(canvas);
        if (ContrastRatio(AppearanceOptions.RelativeLuminance(fg), bgLuma) >= minRatio) return fg;
        for (double step = .2; step <= 1.0; step += .2)
        {
            Color candidate = Mix(fg, ink, step);
            if (ContrastRatio(AppearanceOptions.RelativeLuminance(candidate), bgLuma) >= minRatio)
                return candidate;
        }
        return Mix(fg, ink, .65);
    }

    private static double ContrastRatio(double l1, double l2)
    {
        double hi = Math.Max(l1, l2) + .05;
        double lo = Math.Min(l1, l2) + .05;
        return hi / lo;
    }

    private static Color CapSaturation(Color c, double maxS)
    {
        var (h, s, l) = ToHsl(c);
        return s <= maxS ? c : FromHsl(h, maxS, l);
    }

    private static Color Mix(Color a, Color b, double amount) => EffectPalette.Blend(a, b, Math.Clamp(amount, 0, 1));
    private static double Lerp(double a, double b, double t) => a + (b - a) * Math.Clamp(t, 0, 1);
    private static double NormalizeHue(double h) => ((h % 360.0) + 360.0) % 360.0;
    private static Color WithAlpha(Color c, byte a) => Color.FromArgb(a, c.R, c.G, c.B);

    public static (double H, double S, double L) ToHsl(Color c)
    {
        double r = c.R / 255.0;
        double g = c.G / 255.0;
        double b = c.B / 255.0;
        double max = Math.Max(r, Math.Max(g, b));
        double min = Math.Min(r, Math.Min(g, b));
        double l = (max + min) * .5;
        double d = max - min;
        if (d < 1e-6) return (0, 0, l);

        double s = l > .5 ? d / (2.0 - max - min) : d / (max + min);
        double h = max == r ? ((g - b) / d + (g < b ? 6 : 0))
            : max == g ? ((b - r) / d + 2)
            : ((r - g) / d + 4);
        return (h * 60.0, s, l);
    }

    public static Color FromHsl(double h, double s, double l)
    {
        s = Math.Clamp(s, 0, 1);
        l = Math.Clamp(l, 0, 1);
        if (s < 1e-6)
        {
            byte v = (byte)Math.Clamp(Math.Round(l * 255), 0, 255);
            return Color.FromRgb(v, v, v);
        }

        double q = l < .5 ? l * (1 + s) : l + s - l * s;
        double p = 2 * l - q;
        double hk = NormalizeHue(h) / 360.0;

        byte Channel(double t)
        {
            if (t < 0) t += 1;
            if (t > 1) t -= 1;
            double v = t < 1.0 / 6.0 ? p + (q - p) * 6 * t
                : t < .5 ? q
                : t < 2.0 / 3.0 ? p + (q - p) * (2.0 / 3.0 - t) * 6
                : p;
            return (byte)Math.Clamp(Math.Round(v * 255), 0, 255);
        }

        return Color.FromRgb(Channel(hk + 1.0 / 3.0), Channel(hk), Channel(hk - 1.0 / 3.0));
    }
}

/// <summary>Renders a live miniature UI preview of how a ColorFinish treats the currently active theme palette.</summary>
public sealed class ColorFinishPreview : FrameworkElement
{
    public static readonly DependencyProperty KindProperty = DependencyProperty.Register(
        nameof(Kind), typeof(ColorFinishKind), typeof(ColorFinishPreview),
        new FrameworkPropertyMetadata(ColorFinishKind.Off, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty IntensityProperty = DependencyProperty.Register(
        nameof(Intensity), typeof(double), typeof(ColorFinishPreview),
        new FrameworkPropertyMetadata(.85, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty RawCanvasProperty = DependencyProperty.Register(
        nameof(RawCanvas), typeof(Color), typeof(ColorFinishPreview),
        new FrameworkPropertyMetadata(Color.FromRgb(0xFF, 0xFF, 0xFF), FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty RawRailProperty = DependencyProperty.Register(
        nameof(RawRail), typeof(Color), typeof(ColorFinishPreview),
        new FrameworkPropertyMetadata(Color.FromRgb(0xF3, 0xF4, 0xF5), FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty RawSidebarProperty = DependencyProperty.Register(
        nameof(RawSidebar), typeof(Color), typeof(ColorFinishPreview),
        new FrameworkPropertyMetadata(Color.FromRgb(0xF7, 0xF8, 0xF8), FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty RawSurfaceProperty = DependencyProperty.Register(
        nameof(RawSurface), typeof(Color), typeof(ColorFinishPreview),
        new FrameworkPropertyMetadata(Color.FromRgb(0xF0, 0xF2, 0xF3), FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty RawLineProperty = DependencyProperty.Register(
        nameof(RawLine), typeof(Color), typeof(ColorFinishPreview),
        new FrameworkPropertyMetadata(Color.FromRgb(0xE1, 0xE4, 0xE7), FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty RawInkProperty = DependencyProperty.Register(
        nameof(RawInk), typeof(Color), typeof(ColorFinishPreview),
        new FrameworkPropertyMetadata(Color.FromRgb(0x1D, 0x23, 0x26), FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty RawAccentProperty = DependencyProperty.Register(
        nameof(RawAccent), typeof(Color), typeof(ColorFinishPreview),
        new FrameworkPropertyMetadata(Color.FromRgb(0x3B, 0x7A, 0x8C), FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty RawAccentSoftProperty = DependencyProperty.Register(
        nameof(RawAccentSoft), typeof(Color), typeof(ColorFinishPreview),
        new FrameworkPropertyMetadata(Color.FromRgb(0xE1, 0xED, 0xF1), FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty RawRowSelectedProperty = DependencyProperty.Register(
        nameof(RawRowSelected), typeof(Color), typeof(ColorFinishPreview),
        new FrameworkPropertyMetadata(Color.FromRgb(0xE4, 0xEE, 0xF1), FrameworkPropertyMetadataOptions.AffectsRender));

    public ColorFinishKind Kind { get => (ColorFinishKind)GetValue(KindProperty); set => SetValue(KindProperty, value); }
    public double Intensity { get => (double)GetValue(IntensityProperty); set => SetValue(IntensityProperty, value); }
    public Color RawCanvas { get => (Color)GetValue(RawCanvasProperty); set => SetValue(RawCanvasProperty, value); }
    public Color RawRail { get => (Color)GetValue(RawRailProperty); set => SetValue(RawRailProperty, value); }
    public Color RawSidebar { get => (Color)GetValue(RawSidebarProperty); set => SetValue(RawSidebarProperty, value); }
    public Color RawSurface { get => (Color)GetValue(RawSurfaceProperty); set => SetValue(RawSurfaceProperty, value); }
    public Color RawLine { get => (Color)GetValue(RawLineProperty); set => SetValue(RawLineProperty, value); }
    public Color RawInk { get => (Color)GetValue(RawInkProperty); set => SetValue(RawInkProperty, value); }
    public Color RawAccent { get => (Color)GetValue(RawAccentProperty); set => SetValue(RawAccentProperty, value); }
    public Color RawAccentSoft { get => (Color)GetValue(RawAccentSoftProperty); set => SetValue(RawAccentSoftProperty, value); }
    public Color RawRowSelected { get => (Color)GetValue(RawRowSelectedProperty); set => SetValue(RawRowSelectedProperty, value); }

    public ColorFinishPreview()
    {
        IsHitTestVisible = false;
        ClipToBounds = true;
        SetResourceReference(RawCanvasProperty, "RawCanvasColor");
        SetResourceReference(RawRailProperty, "RawRailColor");
        SetResourceReference(RawSidebarProperty, "RawSidebarColor");
        SetResourceReference(RawSurfaceProperty, "RawSurfaceColor");
        SetResourceReference(RawLineProperty, "RawLineColor");
        SetResourceReference(RawInkProperty, "RawInkColor");
        SetResourceReference(RawAccentProperty, "RawAccentColor");
        SetResourceReference(RawAccentSoftProperty, "RawAccentSoftColor");
        SetResourceReference(RawRowSelectedProperty, "RawRowSelectedColor");
    }

    protected override void OnRender(DrawingContext dc)
    {
        double w = ActualWidth, h = ActualHeight;
        if (w <= 2 || h <= 2) return;

        bool isDark = AppearanceOptions.RelativeLuminance(RawCanvas) < .50;
        var rawSet = new ColorPaletteSet(
            RawCanvas, RawRail, RawSidebar, RawSurface, RawSurface, RawCanvas, RawLine,
            RawInk, RawInk, RawInk, RawAccent, RawAccentSoft, Colors.White,
            RawAccentSoft, RawAccent, RawAccent, RawAccent, RawAccentSoft, RawSurface, RawRowSelected);

        var p = ColorFinish.TransformPalette(in rawSet, Kind, Intensity);

        double railW = Math.Round(w * .11);
        double sideW = Math.Round(w * .28);
        double chatX = railW + sideW;

        Brush BrushOf(Color c)
        {
            var b = new SolidColorBrush(c);
            b.Freeze();
            return b;
        }

        Pen LinePen()
        {
            var pen = new Pen(BrushOf(p.Line), 1);
            pen.Freeze();
            return pen;
        }

        var linePen = LinePen();
        dc.DrawRectangle(BrushOf(p.Rail), null, new Rect(0, 0, railW, h));
        dc.DrawRectangle(BrushOf(p.Sidebar), null, new Rect(railW, 0, sideW, h));
        dc.DrawRectangle(BrushOf(p.Canvas), null, new Rect(chatX, 0, Math.Max(0, w - chatX), h));

        var chromeWash = ColorFinish.BuildWashBrush(p.Sidebar, p.Accent, p.Rail, Kind, Intensity, isChat: false, isDark);
        if (chromeWash != Brushes.Transparent)
            dc.DrawRectangle(chromeWash, null, new Rect(0, 0, chatX, h));

        var chatWash = ColorFinish.BuildWashBrush(p.Canvas, p.Accent, p.Surface, Kind, Intensity, isChat: true, isDark);
        if (chatWash != Brushes.Transparent)
            dc.DrawRectangle(chatWash, null, new Rect(chatX, 0, Math.Max(0, w - chatX), h));

        dc.DrawLine(linePen, new Point(railW, 0), new Point(railW, h));
        dc.DrawLine(linePen, new Point(chatX, 0), new Point(chatX, h));

        // Mini rail dot + sidebar pills
        dc.DrawRoundedRectangle(BrushOf(p.Accent), null, new Rect(3, 7, Math.Max(4, railW - 6), 5), 2, 2);
        dc.DrawRoundedRectangle(BrushOf(p.RowSelected), null, new Rect(railW + 4, 7, Math.Max(8, sideW - 8), 8), 2.5, 2.5);
        dc.DrawRoundedRectangle(BrushOf(p.Surface), null, new Rect(railW + 4, 18, Math.Max(6, sideW - 12), 6), 2, 2);

        // Mini incoming & outgoing chat bubbles
        var inPen = LinePen();
        dc.DrawRoundedRectangle(BrushOf(p.Surface), inPen, new Rect(chatX + 7, 7, Math.Max(14, (w - chatX) * .56), 10), 4, 4);
        var outInkBrush = BrushOf(Color.FromArgb(140, p.Ink.R, p.Ink.G, p.Ink.B));
        dc.DrawRoundedRectangle(outInkBrush, null, new Rect(chatX + 12, 11, Math.Max(8, (w - chatX) * .30), 2), 1, 1);

        double outW = Math.Max(14, (w - chatX) * .52);
        dc.DrawRoundedRectangle(BrushOf(p.AccentSoft), null, new Rect(w - outW - 7, h - 17, outW, 10), 4, 4);
        dc.DrawRoundedRectangle(BrushOf(p.Accent), null, new Rect(w - outW - 2, h - 13, Math.Max(8, outW * .48), 2), 1, 1);
    }
}
