using System.Windows;
using Application = System.Windows.Application;
using Brush = System.Windows.Media.Brush;
using Microsoft.Win32;
using SolidColorBrush = System.Windows.Media.SolidColorBrush;

namespace CydLan.Desktop;

public sealed record ThemeSwatch(
    string Tag,
    string Name,
    bool IsSystem,
    bool IsDark,
    Brush Canvas,
    Brush Rail,
    Brush Sidebar,
    Brush Surface,
    Brush RowSelected,
    Brush Accent,
    Brush AccentInk,
    Brush Ink,
    Brush Line,
    CornerRadius ControlCorner,
    CornerRadius BubbleCorner);

/// <summary>Swaps the active appearance resources at runtime and supplies theme swatches for preview cards.</summary>
public static class ThemeManager
{
    public const string Light = "light", Dark = "dark", FollowSystem = "system";
    public const string Ocean = "ocean", Lavender = "lavender", Ember = "ember", Pixel = "pixel";
    public const string Forest = "forest", Sand = "sand";
    public const string Anime = "anime", Steampunk = "steampunk", Aurora = "aurora", Paper = "paper", Arcade = "arcade";
    public const string Sakura = "sakura";
    public const string Cyberpunk = "cyberpunk", Midnight = "midnight", Sunset = "sunset";
    public const string Mint = "mint", Matcha = "matcha", Frost = "frost";
    public const string Obsidian = "obsidian", Nord = "nord", Synthwave = "synthwave", Crimson = "crimson", Emerald = "emerald";
    public const string Rose = "rose", Espresso = "espresso", Cobalt = "cobalt", Sage = "sage", Amber = "amber";
    public const string Pineapple = "pineapple", CoralStar = "coralstar", NoirPink = "noirpink", Vampire = "vampire";
    public const string VoltSpark = "voltspark", KingdomRed = "kingdomred", Bubblegum = "bubblegum", RibbonBlush = "ribbonblush";
    public const string NoirGold = "noirgold", WebCrimson = "webcrimson", Jester = "jester", Cipher = "cipher";

    private static readonly (string Tag, string Name, string File)[] Definitions =
    [
        (Light, "Light", "Light"),
        (Dark, "Dark", "Dark"),
        (FollowSystem, "Match Windows", "Light"),
        (Ocean, "Ocean", "Ocean"),
        (Lavender, "Lavender", "Lavender"),
        (Ember, "Ember", "Ember"),
        (Forest, "Forest", "Forest"),
        (Sand, "Sand", "Sand"),
        (Anime, "Anime", "Anime"),
        (Sakura, "Sakura", "Sakura"),
        (Steampunk, "Steampunk", "Steampunk"),
        (Aurora, "Aurora", "Aurora"),
        (Paper, "Paper", "Paper"),
        (Arcade, "Arcade", "Arcade"),
        (Pixel, "Pixel", "Pixel"),
        (Cyberpunk, "Cyberpunk", "Cyberpunk"),
        (Midnight, "Midnight", "Midnight"),
        (Sunset, "Sunset", "Sunset"),
        (Mint, "Mint", "Mint"),
        (Matcha, "Matcha", "Matcha"),
        (Frost, "Frost", "Frost"),
        (Obsidian, "Obsidian", "Obsidian"),
        (Nord, "Nord", "Nord"),
        (Synthwave, "Synthwave", "Synthwave"),
        (Crimson, "Crimson", "Crimson"),
        (Emerald, "Emerald", "Emerald"),
        (Rose, "Rose", "Rose"),
        (Espresso, "Espresso", "Espresso"),
        (Cobalt, "Cobalt", "Cobalt"),
        (Sage, "Sage", "Sage"),
        (Amber, "Amber", "Amber"),
        (Pineapple, "Pineapple Sea", "Pineapple"),
        (CoralStar, "Coral Star", "CoralStar"),
        (NoirPink, "Noir Pink", "NoirPink"),
        (Vampire, "Vampire", "Vampire"),
        (VoltSpark, "Volt Spark", "VoltSpark"),
        (KingdomRed, "Kingdom Red", "KingdomRed"),
        (Bubblegum, "Bubblegum", "Bubblegum"),
        (RibbonBlush, "Ribbon Blush", "RibbonBlush"),
        (NoirGold, "Noir Gold", "NoirGold"),
        (WebCrimson, "Web Crimson", "WebCrimson"),
        (Jester, "Jester", "Jester"),
        (Cipher, "Cipher", "Cipher")
    ];

    private static ResourceDictionary? customControls;
    private static string? currentControlSkin;
    private static string mode = Light;
    private static AppearanceOverrides activeOverrides = AppearanceOverrides.Default;
    private static bool listening;
    private static IReadOnlyList<ThemeSwatch>? catalog;

    public static IReadOnlyList<ThemeSwatch> Catalog => catalog ??= BuildCatalog();
    public static AppearanceOverrides ActiveOverrides => activeOverrides;

    public static string Normalize(string? value) => value switch
    {
        "spongebob" => Pineapple,
        "patrick" => CoralStar,
        "blackpink" => NoirPink,
        "pikachu" => VoltSpark,
        "mario" => KingdomRed,
        "barbie" => Bubblegum,
        "hellokitty" => RibbonBlush,
        "batman" => NoirGold,
        "spiderman" => WebCrimson,
        "joker" => Jester,
        "matrix" => Cipher,
        Dark or FollowSystem or Ocean or Lavender or Ember or Pixel or Forest or Sand
            or Anime or Steampunk or Aurora or Paper or Arcade or Sakura
            or Cyberpunk or Midnight or Sunset or Mint or Matcha or Frost
            or Obsidian or Nord or Synthwave or Crimson or Emerald
            or Rose or Espresso or Cobalt or Sage or Amber
            or Pineapple or CoralStar or NoirPink or Vampire
            or VoltSpark or KingdomRed or Bubblegum or RibbonBlush
            or NoirGold or WebCrimson or Jester or Cipher => value,
        _ => Light
    };

    public static void Apply(string? value) => Apply(value, activeOverrides);

    public static void Apply(string? value, AppearanceOverrides? overrides)
    {
        mode = Normalize(value);
        activeOverrides = overrides ?? AppearanceOverrides.Default;
        if (Application.Current is not { } app) return;
        if (mode == FollowSystem && !listening) { SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged; listening = true; }
        var appearance = ResolveAppearance(mode);
        var palette = new ResourceDictionary { Source = new Uri($"/CydLan;component/Themes/{appearance}.xaml", UriKind.Relative) };
        // Swapping a merged dictionary leaves already-resolved template and style values on the old brushes.
        // Writing each key straight into the application resources forces every DynamicResource to refresh.
        foreach (var (key, brush) in Flatten(palette)) app.Resources[key] = brush;

        // Opt-in custom accent color remix (empty = bundled theme).
        var canvasColor = ((SolidColorBrush)app.Resources["Canvas"]).Color;
        var sidebarColor = ((SolidColorBrush)app.Resources["Sidebar"]).Color;
        var surfaceColor = ((SolidColorBrush)app.Resources["Surface"]).Color;
        var lineColor = ((SolidColorBrush)app.Resources["Line"]).Color;
        var inkColor = ((SolidColorBrush)app.Resources["Ink"]).Color;
        bool isDark = AppearanceOptions.RelativeLuminance(canvasColor) < .50;

        if (AppearanceOptions.TryParseHex(activeOverrides.CustomAccentColor, out var customAccent))
        {
            var accentInkColor = AppearanceOptions.RelativeLuminance(customAccent) >= .56
                ? System.Windows.Media.Color.FromRgb(0x16, 0x1B, 0x20)
                : System.Windows.Media.Colors.White;
            var accentSoftColor = EffectPalette.Blend(canvasColor, customAccent, isDark ? .26 : .14);
            var rowSelectedColor = EffectPalette.Blend(sidebarColor, customAccent, isDark ? .24 : .15);
            var navSelectedColor = EffectPalette.Blend(sidebarColor, customAccent, isDark ? .28 : .18);
            var avatarColor = EffectPalette.Blend(sidebarColor, customAccent, isDark ? .30 : .16);
            app.Resources["Accent"] = FrozenBrush(customAccent);
            app.Resources["AccentInk"] = FrozenBrush(accentInkColor);
            app.Resources["AccentSoft"] = FrozenBrush(accentSoftColor);
            app.Resources["RowSelected"] = FrozenBrush(rowSelectedColor);
            app.Resources["NavSelected"] = FrozenBrush(navSelectedColor);
            app.Resources["Avatar"] = FrozenBrush(avatarColor);
        }

        var accentColor = ((SolidColorBrush)app.Resources["Accent"]).Color;
        var accentInk = ((SolidColorBrush)app.Resources["AccentInk"]).Color;
        var accentSoft = ((SolidColorBrush)app.Resources["AccentSoft"]).Color;
        var effectColor = AppearanceOptions.TryParseHex(activeOverrides.CustomEffectColor, out var customEffect) ? customEffect : accentColor;
        app.Resources["EffectColor"] = effectColor;
        app.Resources["ChromeSurfaceColor"] = sidebarColor;
        app.Resources["ChromeLineColor"] = lineColor;
        var inputColor = ((SolidColorBrush)app.Resources["Input"]).Color;
        app.Resources["ComposerSeamlessBackground"] = FrozenBrush(System.Windows.Media.Color.FromArgb(0x38, inputColor.R, inputColor.G, inputColor.B));
        string surfaceMode = AppearanceOptions.NormalizeChatSurfaceMode(activeOverrides.ChatSurfaceMode);
        if (surfaceMode == AppearanceOptions.ChatSurfaceTheme)
        {
            app.Resources["ChatSurfaceBackground"] = System.Windows.Media.Brushes.Transparent;
            app.Resources["ChatPreviewBackground"] = app.Resources["Sidebar"];
        }
        else if (AppearanceOptions.TryParseHex(activeOverrides.CustomChatBackgroundColor, out var customChatBg))
        {
            var customChatBrush = FrozenBrush(customChatBg);
            app.Resources["ChatSurfaceBackground"] = customChatBrush;
            app.Resources["ChatPreviewBackground"] = customChatBrush;
        }
        else
        {
            app.Resources["ChatSurfaceBackground"] = app.Resources["Canvas"];
            app.Resources["ChatPreviewBackground"] = app.Resources["Canvas"];
        }
        DecorationDesign decoration = mode switch
        {
            Sakura => DecorationDesign.Sakura,
            Steampunk => DecorationDesign.Steampunk,
            Forest => DecorationDesign.Forest,
            Paper => DecorationDesign.Paper,
            Anime => DecorationDesign.Anime,
            Aurora => DecorationDesign.Aurora,
            Arcade => DecorationDesign.Arcade,
            Pixel => DecorationDesign.Pixel,
            Ocean => DecorationDesign.Ocean,
            Lavender => DecorationDesign.Lavender,
            Ember => DecorationDesign.Ember,
            Sand => DecorationDesign.Sand,
            Cyberpunk => DecorationDesign.Cyberpunk,
            Midnight => DecorationDesign.Midnight,
            Sunset => DecorationDesign.Sunset,
            Mint => DecorationDesign.Mint,
            Matcha => DecorationDesign.Matcha,
            Frost => DecorationDesign.Frost,
            Obsidian => DecorationDesign.Obsidian,
            Nord => DecorationDesign.Nord,
            Synthwave => DecorationDesign.Synthwave,
            Crimson => DecorationDesign.Crimson,
            Emerald => DecorationDesign.Emerald,
            Rose => DecorationDesign.Rose,
            Espresso => DecorationDesign.Espresso,
            Cobalt => DecorationDesign.Cobalt,
            Sage => DecorationDesign.Sage,
            Amber => DecorationDesign.Amber,
            Pineapple => DecorationDesign.Pineapple,
            CoralStar => DecorationDesign.CoralStar,
            NoirPink => DecorationDesign.NoirPink,
            Vampire => DecorationDesign.Vampire,
            VoltSpark => DecorationDesign.VoltSpark,
            KingdomRed => DecorationDesign.KingdomRed,
            Bubblegum => DecorationDesign.Bubblegum,
            RibbonBlush => DecorationDesign.RibbonBlush,
            NoirGold => DecorationDesign.NoirGold,
            WebCrimson => DecorationDesign.WebCrimson,
            Jester => DecorationDesign.Jester,
            Cipher => DecorationDesign.Cipher,
            _ => DecorationDesign.None
        };
        app.Resources["ThemeDecorationDesign"] = decoration;
        app.Resources["ThemeDetailsVisibility"] = decoration == DecorationDesign.None ? Visibility.Collapsed : Visibility.Visible;
        var selection = (SolidColorBrush)app.Resources["RowSelected"];
        if (mode == Forest)
        {
            var glow = new System.Windows.Media.LinearGradientBrush(
                EffectPalette.Blend(selection.Color, accentColor, .18), selection.Color, 5);
            glow.Freeze();
            app.Resources["DetailedRowSelected"] = glow;
        }
        else app.Resources["DetailedRowSelected"] = selection;
        // Set the geometry and corner keys directly so WPF refreshes instantiated templates and glyphs.
        string? controlSkin = mode is Light or Dark or FollowSystem ? null : appearance;
        if (controlSkin != currentControlSkin)
        {
            if (customControls is not null)
                foreach (string key in customControls.Keys) app.Resources.Remove(key);
            customControls = controlSkin is null ? null :
                new ResourceDictionary { Source = new Uri($"/CydLan;component/Themes/{controlSkin}Controls.xaml", UriKind.Relative) };
            if (customControls is not null)
                foreach (string key in customControls.Keys) app.Resources[key] = customControls[key];
            currentControlSkin = controlSkin;
        }
        else if (customControls is not null)
        {
            // Restore bundled corner keys before reapplying optional corner overrides.
            foreach (string key in customControls.Keys) app.Resources[key] = customControls[key];
        }
        else
        {
            var baseCorners = new ResourceDictionary { Source = new Uri("/CydLan;component/Themes/ControlCorners.xaml", UriKind.Relative) };
            foreach (string key in baseCorners.Keys) app.Resources[key] = baseCorners[key];
        }

        ApplyCornerOverrides(app, activeOverrides.CornerStyle);
        ApplyBubbleOverrides(app, activeOverrides, canvasColor, sidebarColor, surfaceColor, lineColor, inkColor, accentColor, accentInk, accentSoft, isDark);
        ApplyAvatarOverrides(app, activeOverrides.AvatarShape);
        ApplyUiFont(app, activeOverrides.UiFont);

        // WPF does not refresh a Window's own style-set Foreground on a palette swap, which leaves inherited
        // text in the old colour. Binding the window-level brushes directly keeps every open window in sync.
        foreach (Window window in app.Windows)
        {
            window.SetResourceReference(System.Windows.Controls.Control.ForegroundProperty, "Ink");
            window.SetResourceReference(System.Windows.Controls.Control.FontFamilyProperty, "UiFont");
            if (!window.AllowsTransparency) window.SetResourceReference(System.Windows.Controls.Control.BackgroundProperty, "Canvas");
            if (window is MainWindow) WindowOpacityHelper.Apply(window, activeOverrides.WindowOpacity);
        }
        ButtonEffects.RefreshTheme();
        TargetAura.RefreshTheme();
    }

    private static void ApplyCornerOverrides(Application app, string cornerStyle)
    {
        if (cornerStyle == AppearanceOptions.Default) return;
        double control = cornerStyle switch
        {
            AppearanceOptions.CornerSharp => 0,
            AppearanceOptions.CornerCrisp => 4,
            AppearanceOptions.CornerSmooth => 8,
            AppearanceOptions.CornerRound => 14,
            _ => 8
        };
        double panel = control == 0 ? 0 : control + 2;
        double composer = control == 0 ? 0 : control + 4;
        double segment = Math.Max(0, control - 2);
        double check = Math.Min(control, 6);
        app.Resources["ControlCorner"] = new CornerRadius(control);
        app.Resources["PanelCorner"] = new CornerRadius(panel);
        app.Resources["ComposerCorner"] = new CornerRadius(composer);
        app.Resources["SegmentCorner"] = new CornerRadius(segment);
        app.Resources["CheckCorner"] = new CornerRadius(check);
        app.Resources["OverlayCorner"] = new CornerRadius(composer + 2);
        app.Resources["MessageBubbleCorner"] = new CornerRadius(composer);
        app.Resources["FileBubbleCorner"] = new CornerRadius(panel);
    }

    private static void ApplyBubbleOverrides(
        Application app,
        AppearanceOverrides overrides,
        System.Windows.Media.Color canvas,
        System.Windows.Media.Color sidebar,
        System.Windows.Media.Color surface,
        System.Windows.Media.Color line,
        System.Windows.Media.Color ink,
        System.Windows.Media.Color accent,
        System.Windows.Media.Color accentInk,
        System.Windows.Media.Color accentSoft,
        bool isDark)
    {
        bool hasOutCustom = AppearanceOptions.TryParseHex(overrides.CustomOutgoingBubbleColor, out var outCustom);
        bool hasInCustom = AppearanceOptions.TryParseHex(overrides.CustomIncomingBubbleColor, out var inCustom);
        string style = AppearanceOptions.NormalizeBubbleStyle(overrides.BubbleStyle);

        System.Windows.Media.Color outBase = hasOutCustom ? outCustom : style == AppearanceOptions.BubbleSolid ? accent : accentSoft;
        System.Windows.Media.Color inBase = hasInCustom ? inCustom : surface;

        System.Windows.Media.Color ContrastInk(System.Windows.Media.Color bg, System.Windows.Media.Color fallback)
        {
            double luma = AppearanceOptions.RelativeLuminance(bg);
            bool bgLight = luma >= .54;
            bool fallbackLight = AppearanceOptions.RelativeLuminance(fallback) >= .54;
            if (bgLight != fallbackLight) return fallback;
            return bgLight ? System.Windows.Media.Color.FromRgb(0x16, 0x1B, 0x20) : System.Windows.Media.Colors.White;
        }

        Brush outBg, inBg, outBorder, inBorder;
        Thickness outThickness, inThickness;
        System.Windows.Media.Color outFg = hasOutCustom ? ContrastInk(outBase, ink) : style == AppearanceOptions.BubbleSolid ? accentInk : ink;
        System.Windows.Media.Color inFg = hasInCustom ? ContrastInk(inBase, ink) : ink;

        switch (style)
        {
            case AppearanceOptions.BubbleSolid:
                outBg = FrozenBrush(outBase);
                inBg = FrozenBrush(inBase);
                outBorder = FrozenBrush(System.Windows.Media.Colors.Transparent);
                inBorder = FrozenBrush(System.Windows.Media.Colors.Transparent);
                outThickness = inThickness = new Thickness(0);
                break;
            case AppearanceOptions.BubbleOutlined:
            {
                var outStroke = hasOutCustom ? outCustom : accent;
                var inStroke = hasInCustom ? inCustom : line;
                outBg = FrozenBrush(WithAlpha(outStroke, (byte)(isDark ? 34 : 24)));
                inBg = FrozenBrush(WithAlpha(hasInCustom ? inCustom : surface, (byte)(isDark ? 72 : 96)));
                outBorder = FrozenBrush(outStroke);
                inBorder = FrozenBrush(inStroke);
                outThickness = inThickness = new Thickness(1.5);
                outFg = ink;
                inFg = ink;
                break;
            }
            case AppearanceOptions.BubbleGlass:
            {
                var outTint = hasOutCustom ? outCustom : accentSoft;
                var inTint = hasInCustom ? inCustom : surface;
                outBg = FrozenBrush(WithAlpha(outTint, (byte)(isDark ? 168 : 196)));
                inBg = FrozenBrush(WithAlpha(inTint, (byte)(isDark ? 156 : 192)));
                outBorder = FrozenBrush(WithAlpha(hasOutCustom ? outCustom : accent, (byte)(isDark ? 128 : 96)));
                inBorder = FrozenBrush(WithAlpha(isDark ? System.Windows.Media.Colors.White : line, (byte)(isDark ? 54 : 185)));
                outThickness = inThickness = new Thickness(1);
                outFg = hasOutCustom ? ContrastInk(EffectPalette.Blend(canvas, outTint, .65), ink) : ink;
                inFg = hasInCustom ? ContrastInk(EffectPalette.Blend(canvas, inTint, .65), ink) : ink;
                break;
            }
            case AppearanceOptions.BubbleGradient:
            {
                var outEnd = EffectPalette.Blend(outBase, hasOutCustom ? accent : (isDark ? System.Windows.Media.Colors.White : accent), hasOutCustom ? .28 : (isDark ? .18 : .22));
                var inEnd = EffectPalette.Blend(inBase, sidebar, .35);
                var og = new System.Windows.Media.LinearGradientBrush(outBase, outEnd, 35);
                og.Freeze();
                var ig = new System.Windows.Media.LinearGradientBrush(inBase, inEnd, 35);
                ig.Freeze();
                outBg = og;
                inBg = ig;
                outBorder = FrozenBrush(WithAlpha(accent, (byte)(isDark ? 92 : 64)));
                inBorder = FrozenBrush(WithAlpha(line, 160));
                outThickness = inThickness = new Thickness(1);
                break;
            }
            case AppearanceOptions.BubbleMinimal:
            {
                var outBar = hasOutCustom ? outCustom : accent;
                var inBar = hasInCustom ? inCustom : line;
                outBg = FrozenBrush(WithAlpha(outBar, (byte)(isDark ? 36 : 24)));
                inBg = FrozenBrush(WithAlpha(hasInCustom ? inCustom : surface, (byte)(isDark ? 95 : 145)));
                outBorder = FrozenBrush(outBar);
                inBorder = FrozenBrush(inBar);
                outThickness = new Thickness(0, 0, 3, 0);
                inThickness = new Thickness(3, 0, 0, 0);
                outFg = ink;
                inFg = ink;
                break;
            }
            default: // Soft (bundled default)
                outBg = FrozenBrush(outBase);
                inBg = FrozenBrush(inBase);
                outBorder = FrozenBrush(System.Windows.Media.Colors.Transparent);
                inBorder = FrozenBrush(System.Windows.Media.Colors.Transparent);
                outThickness = inThickness = new Thickness(0);
                break;
        }

        app.Resources["OutgoingBubbleBackground"] = outBg;
        app.Resources["IncomingBubbleBackground"] = inBg;
        app.Resources["OutgoingBubbleBorder"] = outBorder;
        app.Resources["IncomingBubbleBorder"] = inBorder;
        app.Resources["OutgoingBubbleBorderThickness"] = outThickness;
        app.Resources["IncomingBubbleBorderThickness"] = inThickness;
        app.Resources["OutgoingBubbleForeground"] = FrozenBrush(outFg);
        app.Resources["IncomingBubbleForeground"] = FrozenBrush(inFg);

        var defaultCorner = app.Resources["MessageBubbleCorner"] is CornerRadius mcr ? mcr : new CornerRadius(12);
        var (inCorner, outCorner, fileCorner) = overrides.BubbleShape switch
        {
            AppearanceOptions.ShapeSharp => (new CornerRadius(2), new CornerRadius(2), new CornerRadius(2)),
            AppearanceOptions.ShapeSubtle => (new CornerRadius(6), new CornerRadius(6), new CornerRadius(6)),
            AppearanceOptions.ShapeRounded => (new CornerRadius(12), new CornerRadius(12), new CornerRadius(10)),
            AppearanceOptions.ShapePill => (new CornerRadius(18), new CornerRadius(18), new CornerRadius(14)),
            AppearanceOptions.ShapeTail => (new CornerRadius(14, 14, 14, 3), new CornerRadius(14, 14, 3, 14), new CornerRadius(12)),
            _ => (defaultCorner, defaultCorner, app.Resources["FileBubbleCorner"] is CornerRadius fcr ? fcr : new CornerRadius(12))
        };
        app.Resources["IncomingBubbleCorner"] = inCorner;
        app.Resources["OutgoingBubbleCorner"] = outCorner;
        app.Resources["FileBubbleCorner"] = fileCorner;

        var (padding, rowMargin, maxWidth) = overrides.ChatDensity switch
        {
            AppearanceOptions.DensityCompact => (new Thickness(9, 5, 9, 5), new Thickness(0, 0, 0, 9), 480.0),
            AppearanceOptions.DensitySpacious => (new Thickness(16, 11, 16, 11), new Thickness(0, 0, 0, 22), 620.0),
            _ => (new Thickness(12, 8, 12, 8), new Thickness(0, 0, 0, 16), 540.0)
        };
        app.Resources["MessageBubblePadding"] = padding;
        app.Resources["MessageRowMargin"] = rowMargin;
        app.Resources["MessageMaxWidth"] = maxWidth;
    }

    private static void ApplyAvatarOverrides(Application app, string avatarShape)
    {
        double themeRow = app.Resources["MessageBubbleCorner"] is CornerRadius mcr ? Math.Min(15, mcr.TopLeft) : 12;
        var (rowRadius, headerRadius) = avatarShape switch
        {
            AppearanceOptions.AvatarCircle => (15.0, 20.0),
            AppearanceOptions.AvatarSquircle => (10.0, 13.0),
            AppearanceOptions.AvatarRounded => (6.0, 8.0),
            AppearanceOptions.AvatarSquare => (2.0, 3.0),
            _ => (themeRow, 20.0)
        };
        app.Resources["RowAvatarCorner"] = new CornerRadius(rowRadius);
        app.Resources["RowAvatarRadius"] = rowRadius;
        app.Resources["HeaderAvatarCorner"] = new CornerRadius(headerRadius);
        app.Resources["HeaderAvatarRadius"] = headerRadius;
    }

    private static void ApplyUiFont(Application app, string uiFont)
    {
        try
        {
            app.Resources["UiFont"] = string.IsNullOrWhiteSpace(uiFont)
                ? new System.Windows.Media.FontFamily("Segoe UI Variable Text, Segoe UI")
                : new System.Windows.Media.FontFamily(uiFont + ", Segoe UI Variable Text, Segoe UI");
        }
        catch (ArgumentException)
        {
            app.Resources["UiFont"] = new System.Windows.Media.FontFamily("Segoe UI Variable Text, Segoe UI");
        }
    }

    private static SolidColorBrush FrozenBrush(System.Windows.Media.Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    private static System.Windows.Media.Color WithAlpha(System.Windows.Media.Color c, byte a) =>
        System.Windows.Media.Color.FromArgb(a, c.R, c.G, c.B);

    private static string ResolveAppearance(string value) => value switch
    {
        Dark => "Dark",
        FollowSystem when WindowsUsesDarkApps() => "Dark",
        Ocean => "Ocean",
        Lavender => "Lavender",
        Ember => "Ember",
        Forest => "Forest",
        Sand => "Sand",
        Anime => "Anime",
        Sakura => "Sakura",
        Steampunk => "Steampunk",
        Aurora => "Aurora",
        Paper => "Paper",
        Arcade => "Arcade",
        Pixel => "Pixel",
        Cyberpunk => "Cyberpunk",
        Midnight => "Midnight",
        Sunset => "Sunset",
        Mint => "Mint",
        Matcha => "Matcha",
        Frost => "Frost",
        Obsidian => "Obsidian",
        Nord => "Nord",
        Synthwave => "Synthwave",
        Crimson => "Crimson",
        Emerald => "Emerald",
        Rose => "Rose",
        Espresso => "Espresso",
        Cobalt => "Cobalt",
        Sage => "Sage",
        Amber => "Amber",
        Pineapple => "Pineapple",
        CoralStar => "CoralStar",
        NoirPink => "NoirPink",
        Vampire => "Vampire",
        VoltSpark => "VoltSpark",
        KingdomRed => "KingdomRed",
        Bubblegum => "Bubblegum",
        RibbonBlush => "RibbonBlush",
        NoirGold => "NoirGold",
        WebCrimson => "WebCrimson",
        Jester => "Jester",
        Cipher => "Cipher",
        _ => "Light"
    };

    private static IReadOnlyList<ThemeSwatch> BuildCatalog()
    {
        var baseCorners = new ResourceDictionary { Source = new Uri("/CydLan;component/Themes/ControlCorners.xaml", UriKind.Relative) };
        var defaultControl = (CornerRadius)baseCorners["ControlCorner"];
        var defaultBubble = (CornerRadius)baseCorners["MessageBubbleCorner"];
        var swatches = new List<ThemeSwatch>(Definitions.Length);

        foreach (var (tag, name, file) in Definitions)
        {
            var palette = Flatten(new ResourceDictionary { Source = new Uri($"/CydLan;component/Themes/{file}.xaml", UriKind.Relative) });
            var controlCorner = defaultControl;
            var bubbleCorner = defaultBubble;
            if (tag is not (Light or Dark or FollowSystem))
            {
                var controls = new ResourceDictionary { Source = new Uri($"/CydLan;component/Themes/{file}Controls.xaml", UriKind.Relative) };
                if (controls["ControlCorner"] is CornerRadius c) controlCorner = c;
                if (controls["MessageBubbleCorner"] is CornerRadius b) bubbleCorner = b;
            }
            var canvas = (SolidColorBrush)palette["Canvas"];
            double luma = (0.2126 * canvas.Color.R + 0.7152 * canvas.Color.G + 0.0722 * canvas.Color.B) / 255.0;
            swatches.Add(new ThemeSwatch(
                tag, name, tag == FollowSystem, luma < .50,
                canvas, (Brush)palette["Rail"], (Brush)palette["Sidebar"],
                (Brush)palette["Surface"], (Brush)palette["RowSelected"], (Brush)palette["Accent"],
                (Brush)palette["AccentInk"], (Brush)palette["Ink"], (Brush)palette["Line"],
                controlCorner, bubbleCorner));
        }
        return swatches;
    }

    // Base dictionaries first, so a theme's own keys override the ones it inherits.
    private static Dictionary<object, object> Flatten(ResourceDictionary dictionary)
    {
        var result = new Dictionary<object, object>();
        foreach (var inner in dictionary.MergedDictionaries)
            foreach (var pair in Flatten(inner)) result[pair.Key] = pair.Value;
        foreach (object key in dictionary.Keys) result[key] = dictionary[key];
        return result;
    }

    private static void OnUserPreferenceChanged(object? sender, UserPreferenceChangedEventArgs e)
    {
        if (mode == FollowSystem && e.Category == UserPreferenceCategory.General)
            Application.Current?.Dispatcher.BeginInvoke(() => Apply(FollowSystem));
    }

    private static bool WindowsUsesDarkApps()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is int light && light == 0;
        }
        catch { return false; }
    }
}
