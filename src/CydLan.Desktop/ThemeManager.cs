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
    public const string GundamFreedom = "gundamfreedom", IronMan = "ironman";

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
        (Cipher, "Cipher", "Cipher"),
        (GundamFreedom, "Gundam Freedom", "GundamFreedom"),
        (IronMan, "Iron Man", "IronMan")
    ];

    private static ResourceDictionary? customControls;
    private static string? currentControlSkin;
    private static string mode = Light;
    private static AppearanceOverrides activeOverrides = AppearanceOverrides.Default;
    private static bool listening;
    private static IReadOnlyList<ThemeSwatch>? catalog;

    public static IReadOnlyList<ThemeSwatch> Catalog => catalog ??= BuildCatalog();
    public static AppearanceOverrides ActiveOverrides => activeOverrides;

    public static ThemeSwatch SwatchFor(string? value)
    {
        string normalized = Normalize(value);
        return Catalog.FirstOrDefault(s => s.Tag == normalized) ?? Catalog[0];
    }

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
        "gundam" or "freedom" or "gundamfreedone" => GundamFreedom,
        "stark" or "tonystark" => IronMan,
        Dark or FollowSystem or Ocean or Lavender or Ember or Pixel or Forest or Sand
            or Anime or Steampunk or Aurora or Paper or Arcade or Sakura
            or Cyberpunk or Midnight or Sunset or Mint or Matcha or Frost
            or Obsidian or Nord or Synthwave or Crimson or Emerald
            or Rose or Espresso or Cobalt or Sage or Amber
            or Pineapple or CoralStar or NoirPink or Vampire
            or VoltSpark or KingdomRed or Bubblegum or RibbonBlush
            or NoirGold or WebCrimson or Jester or Cipher
            or GundamFreedom or IronMan => value,
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
        var rawCanvasColor = ((SolidColorBrush)app.Resources["Canvas"]).Color;
        var rawSidebarColor = ((SolidColorBrush)app.Resources["Sidebar"]).Color;
        bool rawIsDark = AppearanceOptions.RelativeLuminance(rawCanvasColor) < .50;

        if (AppearanceOptions.TryParseHex(activeOverrides.CustomAccentColor, out var customAccent))
        {
            var accentInkColor = AppearanceOptions.RelativeLuminance(customAccent) >= .56
                ? System.Windows.Media.Color.FromRgb(0x16, 0x1B, 0x20)
                : System.Windows.Media.Colors.White;
            var accentSoftColor = EffectPalette.Blend(rawCanvasColor, customAccent, rawIsDark ? .26 : .14);
            var rowSelectedColor = EffectPalette.Blend(rawSidebarColor, customAccent, rawIsDark ? .24 : .15);
            var navSelectedColor = EffectPalette.Blend(rawSidebarColor, customAccent, rawIsDark ? .28 : .18);
            var avatarColor = EffectPalette.Blend(rawSidebarColor, customAccent, rawIsDark ? .30 : .16);
            app.Resources["Accent"] = FrozenBrush(customAccent);
            app.Resources["AccentInk"] = FrozenBrush(accentInkColor);
            app.Resources["AccentSoft"] = FrozenBrush(accentSoftColor);
            app.Resources["RowSelected"] = FrozenBrush(rowSelectedColor);
            app.Resources["NavSelected"] = FrozenBrush(navSelectedColor);
            app.Resources["Avatar"] = FrozenBrush(avatarColor);
        }

        System.Windows.Media.Color KeyColor(string key) => ((SolidColorBrush)app.Resources[key]).Color;

        var rawPalette = new ColorPaletteSet(
            rawCanvasColor,
            KeyColor("Rail"),
            rawSidebarColor,
            KeyColor("Surface"),
            KeyColor("Hover"),
            KeyColor("Input"),
            KeyColor("Line"),
            KeyColor("Ink"),
            KeyColor("Muted"),
            KeyColor("Faint"),
            KeyColor("Accent"),
            KeyColor("AccentSoft"),
            KeyColor("AccentInk"),
            KeyColor("Avatar"),
            KeyColor("Online"),
            KeyColor("Away"),
            KeyColor("Danger"),
            KeyColor("NavSelected"),
            KeyColor("RowHover"),
            KeyColor("RowSelected"));

        // Preserve un-graded theme + custom colors so swatch cards preview live from raw sources.
        app.Resources["RawCanvasColor"] = rawPalette.Canvas;
        app.Resources["RawRailColor"] = rawPalette.Rail;
        app.Resources["RawSidebarColor"] = rawPalette.Sidebar;
        app.Resources["RawSurfaceColor"] = rawPalette.Surface;
        app.Resources["RawLineColor"] = rawPalette.Line;
        app.Resources["RawInkColor"] = rawPalette.Ink;
        app.Resources["RawAccentColor"] = rawPalette.Accent;
        app.Resources["RawAccentSoftColor"] = rawPalette.AccentSoft;
        app.Resources["RawRowSelectedColor"] = rawPalette.RowSelected;

        var finishKind = ColorFinish.KindFor(activeOverrides.ColorFinish);
        double finishIntensity = double.IsFinite(activeOverrides.ColorFinishIntensity)
            ? Math.Clamp(activeOverrides.ColorFinishIntensity, .1, 1.0)
            : .65;
        var graded = ColorFinish.TransformPalette(in rawPalette, finishKind, finishIntensity);

        if (finishKind != ColorFinishKind.Off)
        {
            app.Resources["Canvas"] = FrozenBrush(graded.Canvas);
            app.Resources["Rail"] = FrozenBrush(graded.Rail);
            app.Resources["Sidebar"] = FrozenBrush(graded.Sidebar);
            app.Resources["Surface"] = FrozenBrush(graded.Surface);
            app.Resources["Hover"] = FrozenBrush(graded.Hover);
            app.Resources["Input"] = FrozenBrush(graded.Input);
            app.Resources["Line"] = FrozenBrush(graded.Line);
            app.Resources["Ink"] = FrozenBrush(graded.Ink);
            app.Resources["Muted"] = FrozenBrush(graded.Muted);
            app.Resources["Faint"] = FrozenBrush(graded.Faint);
            app.Resources["Accent"] = FrozenBrush(graded.Accent);
            app.Resources["AccentSoft"] = FrozenBrush(graded.AccentSoft);
            app.Resources["AccentInk"] = FrozenBrush(graded.AccentInk);
            app.Resources["Avatar"] = FrozenBrush(graded.Avatar);
            app.Resources["Online"] = FrozenBrush(graded.Online);
            app.Resources["Away"] = FrozenBrush(graded.Away);
            app.Resources["Danger"] = FrozenBrush(graded.Danger);
            app.Resources["NavSelected"] = FrozenBrush(graded.NavSelected);
            app.Resources["RowHover"] = FrozenBrush(graded.RowHover);
            app.Resources["RowSelected"] = FrozenBrush(graded.RowSelected);
        }

        var canvasColor = graded.Canvas;
        var railColor = graded.Rail;
        var sidebarColor = graded.Sidebar;
        var surfaceColor = graded.Surface;
        var lineColor = graded.Line;
        var inkColor = graded.Ink;
        bool isDark = AppearanceOptions.RelativeLuminance(canvasColor) < .50;

        var accentColor = graded.Accent;
        var accentInk = graded.AccentInk;
        var accentSoft = graded.AccentSoft;
        var effectColor = AppearanceOptions.TryParseHex(activeOverrides.CustomEffectColor, out var customEffect)
            ? ColorFinish.TransformColor(customEffect, ColorPaletteRole.Accent, finishKind, finishIntensity * .65, rawPalette.Accent, rawPalette.Canvas, rawPalette.Sidebar, rawIsDark)
            : accentColor;
        app.Resources["EffectColor"] = effectColor;
        app.Resources["ChromeSurfaceColor"] = sidebarColor;
        app.Resources["ChromeLineColor"] = lineColor;
        var inputColor = graded.Input;
        app.Resources["ComposerSeamlessBackground"] = FrozenBrush(System.Windows.Media.Color.FromArgb(0x38, inputColor.R, inputColor.G, inputColor.B));

        app.Resources["ChromeColorFinishBrush"] = ColorFinish.BuildWashBrush(sidebarColor, accentColor, railColor, finishKind, finishIntensity, isChat: false, isDark);

        string surfaceMode = AppearanceOptions.NormalizeChatSurfaceMode(activeOverrides.ChatSurfaceMode);
        System.Windows.Media.Color chatBaseForWash = canvasColor;
        bool chatIsDark = isDark;
        var contentInk = inkColor;
        var contentMuted = graded.Muted;
        var contentFaint = graded.Faint;
        if (surfaceMode == AppearanceOptions.ChatSurfaceTheme)
        {
            app.Resources["ChatSurfaceBackground"] = System.Windows.Media.Brushes.Transparent;
            app.Resources["ChatPreviewBackground"] = app.Resources["Sidebar"];
            app.Resources["ChatColorFinishBrush"] = System.Windows.Media.Brushes.Transparent;
        }
        else if (AppearanceOptions.TryParseHex(activeOverrides.CustomChatBackgroundColor, out var customChatBg))
        {
            bool customChatDark = AppearanceOptions.RelativeLuminance(customChatBg) < .50;
            var gradedChatBg = ColorFinish.TransformColor(customChatBg, ColorPaletteRole.Canvas, finishKind, finishIntensity, rawPalette.Accent, customChatBg, rawPalette.Sidebar, customChatDark);
            var customChatBrush = FrozenBrush(gradedChatBg);
            app.Resources["ChatSurfaceBackground"] = customChatBrush;
            app.Resources["ChatPreviewBackground"] = customChatBrush;
            chatBaseForWash = gradedChatBg;
            chatIsDark = customChatDark;
            app.Resources["ChatColorFinishBrush"] = ColorFinish.BuildWashBrush(chatBaseForWash, accentColor, surfaceColor, finishKind, finishIntensity, isChat: true, chatIsDark);
            (contentInk, contentMuted, contentFaint) = ContrastTextFor(gradedChatBg, inkColor, graded.Muted, graded.Faint);
        }
        else
        {
            app.Resources["ChatSurfaceBackground"] = app.Resources["Canvas"];
            app.Resources["ChatPreviewBackground"] = app.Resources["Canvas"];
            app.Resources["ChatColorFinishBrush"] = ColorFinish.BuildWashBrush(chatBaseForWash, accentColor, surfaceColor, finishKind, finishIntensity, isChat: true, chatIsDark);
        }
        app.Resources["ContentInk"] = FrozenBrush(contentInk);
        app.Resources["ContentMuted"] = FrozenBrush(contentMuted);
        app.Resources["ContentFaint"] = FrozenBrush(contentFaint);

        ApplyRegionOverrides(app, activeOverrides, finishKind, finishIntensity, rawPalette, graded);
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
            GundamFreedom => DecorationDesign.GundamFreedom,
            IronMan => DecorationDesign.IronMan,
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
        app.Resources["SidebarDetailedRowSelected"] = AppearanceOptions.TryParseHex(activeOverrides.CustomSidebarColor, out _)
            ? app.Resources["SidebarRowSelected"]
            : app.Resources["DetailedRowSelected"];
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

    private static void ApplyRegionOverrides(
        Application app,
        AppearanceOverrides overrides,
        ColorFinishKind finishKind,
        double finishIntensity,
        in ColorPaletteSet rawPalette,
        in ColorPaletteSet graded)
    {
        var ink = graded.Ink;
        var muted = graded.Muted;
        var faint = graded.Faint;
        var accent = graded.Accent;
        var rawAccent = rawPalette.Accent;
        var rawCanvas = rawPalette.Canvas;

        System.Windows.Media.Color GradeSurface(System.Windows.Media.Color raw, ColorPaletteRole role)
        {
            bool dark = AppearanceOptions.RelativeLuminance(raw) < .50;
            return ColorFinish.TransformColor(raw, role, finishKind, finishIntensity, rawAccent, rawCanvas, raw, dark);
        }

        // Title bar
        if (AppearanceOptions.TryParseHex(overrides.CustomTitleColor, out var customTitle))
        {
            var bg = GradeSurface(customTitle, ColorPaletteRole.Sidebar);
            var (tInk, tMuted, _) = ContrastTextFor(bg, ink, muted, faint);
            var bgBrush = FrozenBrush(bg);
            app.Resources["TitleBarBackground"] = bgBrush;
            app.Resources["TitleBarInk"] = FrozenBrush(tInk);
            app.Resources["TitleBarMuted"] = FrozenBrush(tMuted);
            app.Resources["TitleBarPreviewBackground"] = bgBrush;
        }
        else
        {
            app.Resources["TitleBarBackground"] = System.Windows.Media.Brushes.Transparent;
            app.Resources["TitleBarInk"] = app.Resources["Ink"];
            app.Resources["TitleBarMuted"] = app.Resources["Muted"];
            app.Resources["TitleBarPreviewBackground"] = app.Resources["Sidebar"];
        }

        // Header
        if (AppearanceOptions.TryParseHex(overrides.CustomHeaderColor, out var customHeader))
        {
            var bg = GradeSurface(customHeader, ColorPaletteRole.Sidebar);
            var (hInk, hMuted, _) = ContrastTextFor(bg, ink, muted, faint);
            var bgBrush = FrozenBrush(bg);
            app.Resources["HeaderBackground"] = bgBrush;
            app.Resources["HeaderInk"] = FrozenBrush(hInk);
            app.Resources["HeaderMuted"] = FrozenBrush(hMuted);
            app.Resources["HeaderPreviewBackground"] = bgBrush;
        }
        else
        {
            app.Resources["HeaderBackground"] = System.Windows.Media.Brushes.Transparent;
            app.Resources["HeaderInk"] = app.Resources["Ink"];
            app.Resources["HeaderMuted"] = app.Resources["Muted"];
            app.Resources["HeaderPreviewBackground"] = app.Resources["Sidebar"];
        }

        // Side nav (rail)
        if (AppearanceOptions.TryParseHex(overrides.CustomRailColor, out var customRail))
        {
            var bg = GradeSurface(customRail, ColorPaletteRole.Rail);
            bool railDark = AppearanceOptions.RelativeLuminance(bg) < .50;
            var (rInk, rMuted, _) = ContrastTextFor(bg, ink, muted, faint);
            var navSel = EffectPalette.Blend(bg, accent, railDark ? .28 : .18);
            var bgBrush = FrozenBrush(bg);
            app.Resources["RailBackground"] = bgBrush;
            app.Resources["RailInk"] = FrozenBrush(rInk);
            app.Resources["RailMuted"] = FrozenBrush(rMuted);
            app.Resources["RailNavSelected"] = FrozenBrush(navSel);
            app.Resources["RailPreviewBackground"] = bgBrush;
        }
        else
        {
            app.Resources["RailBackground"] = System.Windows.Media.Brushes.Transparent;
            app.Resources["RailInk"] = app.Resources["Ink"];
            app.Resources["RailMuted"] = app.Resources["Muted"];
            app.Resources["RailNavSelected"] = app.Resources["NavSelected"];
            app.Resources["RailPreviewBackground"] = app.Resources["Sidebar"];
        }

        // Side content (sidebar)
        if (AppearanceOptions.TryParseHex(overrides.CustomSidebarColor, out var customSidebar))
        {
            var bg = GradeSurface(customSidebar, ColorPaletteRole.Sidebar);
            bool sideDark = AppearanceOptions.RelativeLuminance(bg) < .50;
            var (sInk, sMuted, sFaint) = ContrastTextFor(bg, ink, muted, faint);
            var sSurface = EffectPalette.Blend(bg, sInk, sideDark ? .09 : .06);
            var sHover = EffectPalette.Blend(bg, sInk, .07);
            var sSelected = EffectPalette.Blend(bg, accent, sideDark ? .24 : .15);
            var bgBrush = FrozenBrush(bg);
            app.Resources["SidebarBackground"] = bgBrush;
            app.Resources["SidebarPanelBackground"] = bgBrush;
            app.Resources["SidebarInk"] = FrozenBrush(sInk);
            app.Resources["SidebarMuted"] = FrozenBrush(sMuted);
            app.Resources["SidebarFaint"] = FrozenBrush(sFaint);
            app.Resources["SidebarSurface"] = FrozenBrush(sSurface);
            app.Resources["SidebarRowHover"] = FrozenBrush(sHover);
            app.Resources["SidebarRowSelected"] = FrozenBrush(sSelected);
            app.Resources["SidebarPreviewBackground"] = bgBrush;
        }
        else
        {
            app.Resources["SidebarBackground"] = System.Windows.Media.Brushes.Transparent;
            app.Resources["SidebarPanelBackground"] = app.Resources["Sidebar"];
            app.Resources["SidebarInk"] = app.Resources["Ink"];
            app.Resources["SidebarMuted"] = app.Resources["Muted"];
            app.Resources["SidebarFaint"] = app.Resources["Faint"];
            app.Resources["SidebarSurface"] = app.Resources["Surface"];
            app.Resources["SidebarRowHover"] = app.Resources["RowHover"];
            app.Resources["SidebarRowSelected"] = app.Resources["RowSelected"];
            app.Resources["SidebarPreviewBackground"] = app.Resources["Sidebar"];
        }
    }

    private static (System.Windows.Media.Color Ink, System.Windows.Media.Color Muted, System.Windows.Media.Color Faint) ContrastTextFor(
        System.Windows.Media.Color bg,
        System.Windows.Media.Color themeInk,
        System.Windows.Media.Color themeMuted,
        System.Windows.Media.Color themeFaint)
    {
        double bgLuma = AppearanceOptions.RelativeLuminance(bg);
        double inkLuma = AppearanceOptions.RelativeLuminance(themeInk);
        double ratio = (Math.Max(bgLuma, inkLuma) + .05) / (Math.Min(bgLuma, inkLuma) + .05);
        if (ratio >= 4.5) return (themeInk, themeMuted, themeFaint);

        bool bgDark = bgLuma < .50;
        var ink = bgDark ? System.Windows.Media.Color.FromRgb(0xF3, 0xF5, 0xF8) : System.Windows.Media.Color.FromRgb(0x16, 0x1B, 0x20);
        var muted = EffectPalette.Blend(bg, ink, .72);
        var faint = EffectPalette.Blend(bg, ink, .52);
        return (ink, muted, faint);
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
        var finishKind = ColorFinish.KindFor(overrides.ColorFinish);
        double finishIntensity = double.IsFinite(overrides.ColorFinishIntensity)
            ? Math.Clamp(overrides.ColorFinishIntensity, .1, 1.0)
            : .65;
        System.Windows.Media.Color GradeBubble(System.Windows.Media.Color c, ColorPaletteRole role, bool darkRef) =>
            ColorFinish.TransformColor(c, role, finishKind, finishIntensity, accent, canvas, sidebar, darkRef);

        if (hasOutCustom && finishKind != ColorFinishKind.Off)
            outCustom = GradeBubble(outCustom, ColorPaletteRole.AccentSoft, isDark);
        if (hasInCustom && finishKind != ColorFinishKind.Off)
            inCustom = GradeBubble(inCustom, ColorPaletteRole.Surface, isDark);

        string style = AppearanceOptions.NormalizeBubbleStyle(overrides.BubbleStyle);
        string effectiveStyle = style == AppearanceOptions.BubbleSoft
            ? mode switch
            {
                GundamFreedom => AppearanceOptions.BubbleMecha,
                IronMan => AppearanceOptions.BubbleReactor,
                _ => style
            }
            : style;

        System.Windows.Media.Color outBase = hasOutCustom ? outCustom : effectiveStyle == AppearanceOptions.BubbleSolid ? accent : accentSoft;
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
        System.Windows.Media.Color outFg = hasOutCustom ? ContrastInk(outBase, ink) : effectiveStyle == AppearanceOptions.BubbleSolid ? accentInk : ink;
        System.Windows.Media.Color inFg = hasInCustom ? ContrastInk(inBase, ink) : ink;
        DecorationDesign bubbleDesign = DecorationDesign.None;

        switch (effectiveStyle)
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
            case AppearanceOptions.BubbleMecha:
            {
                bubbleDesign = DecorationDesign.GundamFreedom;
                var wingBlue = GradeBubble(System.Windows.Media.Color.FromRgb(0x1D, 0x4E, 0xD8), ColorPaletteRole.Accent, false);
                var beamCyan = GradeBubble(System.Windows.Media.Color.FromRgb(0x38, 0xBD, 0xF8), ColorPaletteRole.Accent, false);
                var wingCrimson = GradeBubble(System.Windows.Media.Color.FromRgb(0xE1, 0x1D, 0x48), ColorPaletteRole.Accent, false);
                var outStart = hasOutCustom ? outCustom : mode == GundamFreedom ? GradeBubble(System.Windows.Media.Color.FromRgb(0xE8, 0xF0, 0xFF), ColorPaletteRole.AccentSoft, false) : outBase;
                var outEnd = hasOutCustom
                    ? EffectPalette.Blend(outCustom, beamCyan, .24)
                    : mode == GundamFreedom ? GradeBubble(System.Windows.Media.Color.FromRgb(0xC5, 0xDA, 0xFF), ColorPaletteRole.AccentSoft, false) : EffectPalette.Blend(outBase, accent, isDark ? .26 : .20);
                var inStart = hasInCustom ? inCustom : mode == GundamFreedom ? GradeBubble(System.Windows.Media.Color.FromRgb(0xF8, 0xFA, 0xFF), ColorPaletteRole.Surface, false) : inBase;
                var inEnd = hasInCustom
                    ? EffectPalette.Blend(inCustom, sidebar, .28)
                    : mode == GundamFreedom ? GradeBubble(System.Windows.Media.Color.FromRgb(0xE3, 0xEC, 0xFB), ColorPaletteRole.Surface, false) : EffectPalette.Blend(inBase, accentSoft, .24);
                var og = new System.Windows.Media.LinearGradientBrush(outStart, outEnd, 32); og.Freeze();
                var ig = new System.Windows.Media.LinearGradientBrush(inStart, inEnd, 32); ig.Freeze();
                outBg = og;
                inBg = ig;
                var ob = new System.Windows.Media.LinearGradientBrush
                {
                    StartPoint = new System.Windows.Point(0, 0),
                    EndPoint = new System.Windows.Point(1, 1),
                    GradientStops =
                    [
                        new System.Windows.Media.GradientStop(mode == GundamFreedom ? wingBlue : accent, 0),
                        new System.Windows.Media.GradientStop(beamCyan, .55),
                        new System.Windows.Media.GradientStop(wingCrimson, 1)
                    ]
                };
                ob.Freeze();
                var ib = new System.Windows.Media.LinearGradientBrush(
                    EffectPalette.Blend(line, beamCyan, .35),
                    EffectPalette.Blend(accent, line, .45),
                    35);
                ib.Freeze();
                outBorder = ob;
                inBorder = ib;
                outThickness = new Thickness(1.5);
                inThickness = new Thickness(1.2);
                outFg = hasOutCustom || mode != GundamFreedom ? ContrastInk(EffectPalette.Blend(outStart, outEnd, .5), ink) : ContrastInk(EffectPalette.Blend(outStart, outEnd, .5), System.Windows.Media.Color.FromRgb(0x0B, 0x15, 0x2B));
                inFg = hasInCustom || mode != GundamFreedom ? ContrastInk(EffectPalette.Blend(inStart, inEnd, .5), ink) : ContrastInk(EffectPalette.Blend(inStart, inEnd, .5), System.Windows.Media.Color.FromRgb(0x0F, 0x17, 0x2A));
                break;
            }
            case AppearanceOptions.BubbleReactor:
            {
                bubbleDesign = DecorationDesign.IronMan;
                var starkGold = GradeBubble(System.Windows.Media.Color.FromRgb(0xF5, 0xB7, 0x00), ColorPaletteRole.Accent, true);
                var arcCyan = GradeBubble(System.Windows.Media.Color.FromRgb(0x38, 0xBD, 0xF8), ColorPaletteRole.Accent, true);
                var brightGold = GradeBubble(System.Windows.Media.Color.FromRgb(0xFD, 0xE0, 0x47), ColorPaletteRole.Accent, true);
                var outStart = hasOutCustom ? outCustom : mode == IronMan ? GradeBubble(System.Windows.Media.Color.FromRgb(0x64, 0x10, 0x1D), ColorPaletteRole.AccentSoft, true) : EffectPalette.Blend(outBase, GradeBubble(System.Windows.Media.Color.FromRgb(0x5B, 0x0E, 0x1A), ColorPaletteRole.AccentSoft, true), isDark ? .38 : .18);
                var outEnd = hasOutCustom ? EffectPalette.Blend(outCustom, starkGold, .22) : mode == IronMan ? GradeBubble(System.Windows.Media.Color.FromRgb(0x38, 0x07, 0x0F), ColorPaletteRole.AccentSoft, true) : EffectPalette.Blend(outStart, accent, .20);
                var inStart = hasInCustom ? inCustom : mode == IronMan ? GradeBubble(System.Windows.Media.Color.FromRgb(0x28, 0x10, 0x18), ColorPaletteRole.Surface, true) : inBase;
                var inEnd = hasInCustom ? EffectPalette.Blend(inCustom, arcCyan, .16) : mode == IronMan ? GradeBubble(System.Windows.Media.Color.FromRgb(0x18, 0x09, 0x0E), ColorPaletteRole.Surface, true) : EffectPalette.Blend(inBase, sidebar, .35);
                var og = new System.Windows.Media.LinearGradientBrush(outStart, outEnd, 35); og.Freeze();
                var ig = new System.Windows.Media.LinearGradientBrush(inStart, inEnd, 35); ig.Freeze();
                outBg = og;
                inBg = ig;
                var ob = new System.Windows.Media.LinearGradientBrush
                {
                    StartPoint = new System.Windows.Point(0, 0),
                    EndPoint = new System.Windows.Point(1, 1),
                    GradientStops =
                    [
                        new System.Windows.Media.GradientStop(starkGold, 0),
                        new System.Windows.Media.GradientStop(brightGold, .55),
                        new System.Windows.Media.GradientStop(arcCyan, 1)
                    ]
                };
                ob.Freeze();
                var ib = new System.Windows.Media.LinearGradientBrush(arcCyan, starkGold, 35);
                ib.Freeze();
                outBorder = ob;
                inBorder = ib;
                outThickness = new Thickness(1.5);
                inThickness = new Thickness(1.2);
                outFg = ContrastInk(EffectPalette.Blend(outStart, outEnd, .5), System.Windows.Media.Color.FromRgb(0xFF, 0xF8, 0xEB));
                inFg = ContrastInk(EffectPalette.Blend(inStart, inEnd, .5), ink);
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
        bool useOutFgForIcon = effectiveStyle == AppearanceOptions.BubbleSolid || hasOutCustom;
        bool useInFgForIcon = hasInCustom;
        app.Resources["OutgoingFileIconBrush"] = FrozenBrush(useOutFgForIcon ? outFg : accent);
        app.Resources["IncomingFileIconBrush"] = FrozenBrush(useInFgForIcon ? inFg : accent);
        app.Resources["BubbleDecorationDesign"] = bubbleDesign;
        app.Resources["BubbleDecorationVisibility"] = bubbleDesign == DecorationDesign.None ? Visibility.Collapsed : Visibility.Visible;

        var defaultCorner = app.Resources["MessageBubbleCorner"] is CornerRadius mcr ? mcr : new CornerRadius(12);
        var defaultFileCorner = app.Resources["FileBubbleCorner"] is CornerRadius fcr ? fcr : new CornerRadius(12);
        var (inCorner, outCorner, inFileCorner, outFileCorner) = overrides.BubbleShape switch
        {
            AppearanceOptions.ShapeSharp => (new CornerRadius(2), new CornerRadius(2), new CornerRadius(2), new CornerRadius(2)),
            AppearanceOptions.ShapeSubtle => (new CornerRadius(6), new CornerRadius(6), new CornerRadius(6), new CornerRadius(6)),
            AppearanceOptions.ShapeRounded => (new CornerRadius(12), new CornerRadius(12), new CornerRadius(10), new CornerRadius(10)),
            AppearanceOptions.ShapePill => (new CornerRadius(18), new CornerRadius(18), new CornerRadius(14), new CornerRadius(14)),
            AppearanceOptions.ShapeTail => (new CornerRadius(14, 14, 14, 3), new CornerRadius(14, 14, 3, 14), new CornerRadius(12, 12, 12, 3), new CornerRadius(12, 12, 3, 12)),
            _ when effectiveStyle == AppearanceOptions.BubbleMecha => (new CornerRadius(3, 13, 13, 13), new CornerRadius(13, 3, 13, 13), new CornerRadius(3, 10, 10, 10), new CornerRadius(10, 3, 10, 10)),
            _ when effectiveStyle == AppearanceOptions.BubbleReactor => (new CornerRadius(4, 11, 4, 11), new CornerRadius(11, 4, 11, 4), new CornerRadius(4, 9, 4, 9), new CornerRadius(9, 4, 9, 4)),
            _ => (defaultCorner, defaultCorner, defaultFileCorner, defaultFileCorner)
        };
        app.Resources["IncomingBubbleCorner"] = inCorner;
        app.Resources["OutgoingBubbleCorner"] = outCorner;
        app.Resources["IncomingFileBubbleCorner"] = inFileCorner;
        app.Resources["OutgoingFileBubbleCorner"] = outFileCorner;
        app.Resources["FileBubbleCorner"] = outFileCorner;

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
        GundamFreedom => "GundamFreedom",
        IronMan => "IronMan",
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
