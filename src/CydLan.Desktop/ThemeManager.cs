using System.Windows;
using Application = System.Windows.Application;
using Microsoft.Win32;

namespace CydLan.Desktop;

/// <summary>Swaps the palette dictionary (Themes/Light.xaml or Themes/Dark.xaml) at runtime.</summary>
public static class ThemeManager
{
    public const string Light = "light", Dark = "dark", FollowSystem = "system";
    private static ResourceDictionary? active;
    private static string mode = Light;
    private static bool listening;

    public static string Normalize(string? value) => value is Dark or FollowSystem ? value : Light;

    public static void Apply(string? value)
    {
        mode = Normalize(value);
        if (Application.Current is not { } app) return;
        if (mode == FollowSystem && !listening) { SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged; listening = true; }
        bool dark = mode == Dark || (mode == FollowSystem && WindowsUsesDarkApps());
        var palette = new ResourceDictionary { Source = new Uri($"/CydLan;component/Themes/{(dark ? "Dark" : "Light")}.xaml", UriKind.Relative) };
        var merged = app.Resources.MergedDictionaries;
        if (active is not null) merged.Remove(active);
        merged.Add(palette); // last merged dictionary wins over the default palette inside Theme.xaml
        active = palette;
        // WPF does not refresh a Window's own style-set Foreground on a palette swap, which leaves inherited
        // text in the old colour. Binding the window-level brushes directly keeps every open window in sync.
        foreach (Window window in app.Windows)
        {
            window.SetResourceReference(System.Windows.Controls.Control.ForegroundProperty, "Ink");
            if (!window.AllowsTransparency) window.SetResourceReference(System.Windows.Controls.Control.BackgroundProperty, "Canvas");
        }
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
