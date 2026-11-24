using System.Windows;
using Application = System.Windows.Application;
using Microsoft.Win32;
using SolidColorBrush = System.Windows.Media.SolidColorBrush;

namespace CydLan.Desktop;

/// <summary>Swaps the active appearance resources at runtime.</summary>
public static class ThemeManager
{
    public const string Light = "light", Dark = "dark", FollowSystem = "system";
    public const string Ocean = "ocean", Lavender = "lavender", Ember = "ember", Pixel = "pixel";
    public const string Forest = "forest", Sand = "sand";
    public const string Anime = "anime", Steampunk = "steampunk", Aurora = "aurora", Paper = "paper", Arcade = "arcade";
    public const string Sakura = "sakura";
    private static ResourceDictionary? customControls;
    private static string? currentControlSkin;
    private static string mode = Light;
    private static bool listening;

    public static string Normalize(string? value) =>
        value is Dark or FollowSystem or Ocean or Lavender or Ember or Pixel or Forest or Sand or Anime or Steampunk or Aurora or Paper or Arcade or Sakura ? value : Light;

    public static void Apply(string? value)
    {
        mode = Normalize(value);
        if (Application.Current is not { } app) return;
        if (mode == FollowSystem && !listening) { SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged; listening = true; }
        var appearance = mode switch
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
            _ => "Light"
        };
        var palette = new ResourceDictionary { Source = new Uri($"/CydLan;component/Themes/{appearance}.xaml", UriKind.Relative) };
        // Swapping a merged dictionary leaves already-resolved template and style values on the old brushes.
        // Writing each key straight into the application resources forces every DynamicResource to refresh.
        foreach (var (key, brush) in Flatten(palette)) app.Resources[key] = brush;
        app.Resources["EffectColor"] = ((SolidColorBrush)app.Resources["Accent"]).Color;
        DecorationDesign decoration = mode switch
        {
            Sakura => DecorationDesign.Sakura,
            Steampunk => DecorationDesign.Steampunk,
            Forest => DecorationDesign.Forest,
            Paper => DecorationDesign.Paper,
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
