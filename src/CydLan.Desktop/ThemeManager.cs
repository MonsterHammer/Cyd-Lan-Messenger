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
        (Frost, "Frost", "Frost")
    ];

    private static ResourceDictionary? customControls;
    private static string? currentControlSkin;
    private static string mode = Light;
    private static bool listening;
    private static IReadOnlyList<ThemeSwatch>? catalog;

    public static IReadOnlyList<ThemeSwatch> Catalog => catalog ??= BuildCatalog();

    public static string Normalize(string? value) =>
        value is Dark or FollowSystem or Ocean or Lavender or Ember or Pixel or Forest or Sand
            or Anime or Steampunk or Aurora or Paper or Arcade or Sakura
            or Cyberpunk or Midnight or Sunset or Mint or Matcha or Frost ? value : Light;

    public static void Apply(string? value)
    {
        mode = Normalize(value);
        if (Application.Current is not { } app) return;
        if (mode == FollowSystem && !listening) { SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged; listening = true; }
        var appearance = ResolveAppearance(mode);
        var palette = new ResourceDictionary { Source = new Uri($"/CydLan;component/Themes/{appearance}.xaml", UriKind.Relative) };
        // Swapping a merged dictionary leaves already-resolved template and style values on the old brushes.
        // Writing each key straight into the application resources forces every DynamicResource to refresh.
        foreach (var (key, brush) in Flatten(palette)) app.Resources[key] = brush;
        app.Resources["EffectColor"] = ((SolidColorBrush)app.Resources["Accent"]).Color;
        app.Resources["ChromeSurfaceColor"] = ((SolidColorBrush)app.Resources["Sidebar"]).Color;
        app.Resources["ChromeLineColor"] = ((SolidColorBrush)app.Resources["Line"]).Color;
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
            _ => DecorationDesign.None
        };
        app.Resources["ThemeDecorationDesign"] = decoration;
        app.Resources["ThemeDetailsVisibility"] = decoration == DecorationDesign.None ? Visibility.Collapsed : Visibility.Visible;
        var selection = (SolidColorBrush)app.Resources["RowSelected"];
        if (mode == Forest)
        {
            var glow = new System.Windows.Media.LinearGradientBrush(
                EffectPalette.Blend(selection.Color, ((SolidColorBrush)app.Resources["Accent"]).Color, .18), selection.Color, 5);
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
        // WPF does not refresh a Window's own style-set Foreground on a palette swap, which leaves inherited
        // text in the old colour. Binding the window-level brushes directly keeps every open window in sync.
        foreach (Window window in app.Windows)
        {
            window.SetResourceReference(System.Windows.Controls.Control.ForegroundProperty, "Ink");
            if (!window.AllowsTransparency) window.SetResourceReference(System.Windows.Controls.Control.BackgroundProperty, "Canvas");
        }
        ButtonEffects.RefreshTheme();
        TargetAura.RefreshTheme();
    }

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
