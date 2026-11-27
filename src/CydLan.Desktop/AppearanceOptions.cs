using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using CydLan.Core;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;

namespace CydLan.Desktop;

public sealed record ColorOption(string Value, string Name, Brush Swatch, bool IsDefault = false);
public sealed record StyleOption(string Value, string Name);

public sealed record AppearanceOverrides(
    string CustomAccentColor,
    string CustomEffectColor,
    string CustomOutgoingBubbleColor,
    string CustomIncomingBubbleColor,
    string BubbleStyle,
    string BubbleShape,
    string ChatDensity,
    string CornerStyle,
    string AvatarShape,
    string UiFont,
    double WindowOpacity,
    string ChatSurfaceMode = AppearanceOptions.ChatSurfaceSeparate,
    string CustomChatBackgroundColor = "",
    string ColorFinish = Desktop.ColorFinish.Off,
    double ColorFinishIntensity = .65,
    string CustomTitleColor = "",
    string CustomHeaderColor = "",
    string CustomRailColor = "",
    string CustomSidebarColor = "")
{
    public static AppearanceOverrides Default { get; } = new(
        "", "", "", "",
        AppearanceOptions.BubbleSoft,
        AppearanceOptions.Default,
        AppearanceOptions.DensityCozy,
        AppearanceOptions.Default,
        AppearanceOptions.Default,
        "",
        1.0,
        AppearanceOptions.ChatSurfaceSeparate,
        "",
        Desktop.ColorFinish.Off,
        .65,
        "", "", "", "");

    public static AppearanceOverrides From(AppSettings s) => new(
        AppearanceOptions.NormalizeHex(s.CustomAccentColor),
        AppearanceOptions.NormalizeHex(s.CustomEffectColor),
        AppearanceOptions.NormalizeHex(s.CustomOutgoingBubbleColor),
        AppearanceOptions.NormalizeHex(s.CustomIncomingBubbleColor),
        AppearanceOptions.NormalizeBubbleStyle(s.BubbleStyle),
        AppearanceOptions.NormalizeBubbleShape(s.BubbleShape),
        AppearanceOptions.NormalizeChatDensity(s.ChatDensity),
        AppearanceOptions.NormalizeCornerStyle(s.CornerStyle),
        AppearanceOptions.NormalizeAvatarShape(s.AvatarShape),
        (s.UiFont ?? "").Trim(),
        double.IsFinite(s.WindowOpacity) ? Math.Clamp(s.WindowOpacity, .80, 1.0) : 1.0,
        AppearanceOptions.NormalizeChatSurfaceMode(s.ChatSurfaceMode),
        AppearanceOptions.NormalizeHex(s.CustomChatBackgroundColor),
        Desktop.ColorFinish.Normalize(s.ColorFinish),
        double.IsFinite(s.ColorFinishIntensity) ? Math.Clamp(s.ColorFinishIntensity, .1, 1.0) : .65,
        AppearanceOptions.NormalizeHex(s.CustomTitleColor),
        AppearanceOptions.NormalizeHex(s.CustomHeaderColor),
        AppearanceOptions.NormalizeHex(s.CustomRailColor),
        AppearanceOptions.NormalizeHex(s.CustomSidebarColor));
}

/// <summary>Normalization, curated swatch catalogs, and Win32 layered window opacity support.</summary>
public static class AppearanceOptions
{
    public const string Default = "default";
    public const string ChatSurfaceSeparate = "separate", ChatSurfaceTheme = "theme";
    public const string BubbleSoft = "soft", BubbleSolid = "solid", BubbleOutlined = "outlined", BubbleGlass = "glass", BubbleGradient = "gradient", BubbleMinimal = "minimal", BubbleMecha = "mecha", BubbleReactor = "reactor";
    public const string ShapeSharp = "sharp", ShapeSubtle = "subtle", ShapeRounded = "rounded", ShapePill = "pill", ShapeTail = "tail";
    public const string DensityCompact = "compact", DensityCozy = "cozy", DensitySpacious = "spacious";
    public const string CornerSharp = "sharp", CornerCrisp = "crisp", CornerSmooth = "smooth", CornerRound = "round";
    public const string AvatarCircle = "circle", AvatarSquircle = "squircle", AvatarRounded = "rounded", AvatarSquare = "square";

    public static IReadOnlyList<ColorOption> AccentSwatches { get; } = BuildColorSwatches("Theme");
    public static IReadOnlyList<ColorOption> EffectSwatches { get; } = BuildColorSwatches("Match accent");
    public static IReadOnlyList<ColorOption> BubbleSwatches { get; } = BuildColorSwatches("Theme");
    public static IReadOnlyList<ColorOption> SurfaceSwatches { get; } = BuildChatBackgroundSwatches();
    public static IReadOnlyList<ColorOption> ChatBackgroundSwatches { get; } = BuildChatBackgroundSwatches();

    public static IReadOnlyList<StyleOption> BubbleStyles { get; } =
    [
        new(BubbleSoft, "Soft"),
        new(BubbleSolid, "Solid"),
        new(BubbleOutlined, "Outlined"),
        new(BubbleGlass, "Glass"),
        new(BubbleGradient, "Gradient"),
        new(BubbleMinimal, "Minimal"),
        new(BubbleMecha, "Mecha"),
        new(BubbleReactor, "Arc core")
    ];

    public static IReadOnlyList<StyleOption> BubbleShapes { get; } =
    [
        new(Default, "Theme"),
        new(ShapeSharp, "Sharp"),
        new(ShapeSubtle, "Subtle"),
        new(ShapeRounded, "Rounded"),
        new(ShapePill, "Pill"),
        new(ShapeTail, "Tail")
    ];

    public static IReadOnlyList<StyleOption> CornerStyles { get; } =
    [
        new(Default, "Theme"),
        new(CornerSharp, "Sharp"),
        new(CornerCrisp, "Crisp"),
        new(CornerSmooth, "Smooth"),
        new(CornerRound, "Round")
    ];

    public static IReadOnlyList<StyleOption> AvatarShapes { get; } =
    [
        new(Default, "Theme"),
        new(AvatarCircle, "Circle"),
        new(AvatarSquircle, "Squircle"),
        new(AvatarRounded, "Rounded"),
        new(AvatarSquare, "Square")
    ];

    public static string NormalizeChatSurfaceMode(string? value) =>
        value == ChatSurfaceTheme ? ChatSurfaceTheme : ChatSurfaceSeparate;

    public static string NormalizeBubbleStyle(string? value) =>
        value is BubbleSolid or BubbleOutlined or BubbleGlass or BubbleGradient or BubbleMinimal or BubbleMecha or BubbleReactor ? value : BubbleSoft;

    public static string NormalizeBubbleShape(string? value) =>
        value is ShapeSharp or ShapeSubtle or ShapeRounded or ShapePill or ShapeTail ? value : Default;

    public static string NormalizeChatDensity(string? value) =>
        value is DensityCompact or DensitySpacious ? value : DensityCozy;

    public static string NormalizeCornerStyle(string? value) =>
        value is CornerSharp or CornerCrisp or CornerSmooth or CornerRound ? value : Default;

    public static string NormalizeAvatarShape(string? value) =>
        value is AvatarCircle or AvatarSquircle or AvatarRounded or AvatarSquare ? value : Default;

    public static string NormalizeHex(string? value) => TryParseHex(value, out var color)
        ? $"#{color.R:X2}{color.G:X2}{color.B:X2}"
        : "";

    public static bool TryParseHex(string? value, out Color color)
    {
        color = default;
        if (string.IsNullOrWhiteSpace(value)) return false;
        string s = value.Trim();
        if (s.StartsWith('#')) s = s[1..];
        if (s.Length == 3) s = string.Concat(s[0], s[0], s[1], s[1], s[2], s[2]);
        if (s.Length != 6 || !uint.TryParse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint rgb)) return false;
        color = Color.FromRgb((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);
        return true;
    }

    public static double RelativeLuminance(Color c) => (0.2126 * c.R + 0.7152 * c.G + 0.0722 * c.B) / 255.0;

    private static IReadOnlyList<ColorOption> BuildColorSwatches(string defaultLabel) => BuildPaletteSwatches(defaultLabel,
    [
        ("#3B82F6", "Cobalt"),
        ("#06B6D4", "Cyan"),
        ("#10B981", "Emerald"),
        ("#84CC16", "Lime"),
        ("#F59E0B", "Amber"),
        ("#F97316", "Tangerine"),
        ("#EF4444", "Crimson"),
        ("#EC4899", "Rose"),
        ("#D946EF", "Fuchsia"),
        ("#8B5CF6", "Violet"),
        ("#6366F1", "Indigo"),
        ("#94A3B8", "Slate")
    ]);

    private static IReadOnlyList<ColorOption> BuildChatBackgroundSwatches() => BuildPaletteSwatches("Theme",
    [
        ("#0B0F17", "Midnight"),
        ("#121218", "Obsidian"),
        ("#16111B", "Plum Noir"),
        ("#1A1014", "Crimson Dark"),
        ("#0D1917", "Deep Pine"),
        ("#0F172A", "Slate Navy"),
        ("#1C1917", "Warm Charcoal"),
        ("#F8FAFC", "Snow"),
        ("#F5F3EF", "Parchment"),
        ("#FDF2F8", "Blush"),
        ("#EFF6FF", "Ice"),
        ("#F0FDF4", "Mint Mist")
    ]);

    private static IReadOnlyList<ColorOption> BuildPaletteSwatches(string defaultLabel, (string Hex, string Name)[] palette)
    {
        var list = new List<ColorOption>(palette.Length + 1)
        {
            new("", defaultLabel, System.Windows.Media.Brushes.Transparent, IsDefault: true)
        };
        foreach (var (hex, name) in palette)
        {
            TryParseHex(hex, out var color);
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            list.Add(new ColorOption(hex, name, brush));
        }
        return list;
    }
}

/// <summary>Applies window opacity via Win32 WS_EX_LAYERED without disabling hardware acceleration or WindowChrome.</summary>
internal static class WindowOpacityHelper
{
    private const int GwlExStyle = -20;
    private const int WsExLayered = 0x00080000;
    private const uint LwaAlpha = 0x00000002;

    public static void Apply(Window window, double opacity)
    {
        if (window.AllowsTransparency) return;
        double clamped = double.IsFinite(opacity) ? Math.Clamp(opacity, .80, 1.0) : 1.0;
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero) return;
        int exStyle = GetWindowLong(handle, GwlExStyle);
        if (clamped >= .999)
        {
            if ((exStyle & WsExLayered) != 0)
            {
                SetLayeredWindowAttributes(handle, 0, 255, LwaAlpha);
                SetWindowLong(handle, GwlExStyle, exStyle & ~WsExLayered);
            }
            return;
        }
        if ((exStyle & WsExLayered) == 0)
            SetWindowLong(handle, GwlExStyle, exStyle | WsExLayered);
        SetLayeredWindowAttributes(handle, 0, (byte)Math.Round(clamped * 255), LwaAlpha);
    }

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")] private static extern int GetWindowLong(IntPtr hwnd, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongW")] private static extern int SetWindowLong(IntPtr hwnd, int index, int value);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint crKey, byte bAlpha, uint dwFlags);
}
