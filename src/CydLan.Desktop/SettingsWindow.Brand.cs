using System.IO;
using System.Windows.Controls;
using CydLan.Core;
using System.Windows.Media;
using FontFamily = System.Windows.Media.FontFamily;

namespace CydLan.Desktop;

public sealed record FontOption(string Value, string Name, FontFamily Preview);

public partial class SettingsWindow
{
    private bool syncingFontGallery;

    // Curated interface faces; only the ones installed on this PC are offered.
    private static readonly (string Family, string Name)[] AppFontCandidates =
    [
        ("Segoe UI Variable Display", "Display"),
        ("Segoe UI", "Segoe Classic"),
        ("Aptos", "Aptos"),
        ("Bahnschrift", "Bahnschrift"),
        ("Calibri", "Calibri"),
        ("Candara", "Candara"),
        ("Corbel", "Corbel"),
        ("Trebuchet MS", "Trebuchet"),
        ("Verdana", "Verdana"),
        ("Tahoma", "Tahoma"),
        ("Franklin Gothic Medium", "Franklin"),
        ("Leelawadee UI", "Leelawadee"),
        ("Yu Gothic UI", "Yu Gothic"),
        ("Georgia", "Georgia"),
        ("Cambria", "Cambria"),
        ("Palatino Linotype", "Palatino"),
        ("Sitka Text", "Sitka"),
        ("Constantia", "Constantia"),
        ("Cascadia Code", "Cascadia"),
        ("Consolas", "Consolas"),
        ("Lucida Console", "Lucida"),
        ("Comic Sans MS", "Comic"),
        ("Ink Free", "Ink Free"),
        ("Segoe Print", "Print"),
        ("Segoe Script", "Script"),
        ("Gabriola", "Gabriola")
    ];

    private void AppearanceSet_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!IsInitialized || closing || AppearanceSetBox.SelectedItem is not StyleOption pick || AppearanceSets.All.FirstOrDefault(s => s.Name == pick.Value) is not { } set) return;
        // A set restyles the app but keeps the user's own font, logo and icon colors.
        string font = SelectedUiFont, logo = logoHex, icon = iconHex, dot = logoDotHex;
        set.Look.UiFont = font.Length == 0 ? null : font;
        ApplyLookData(set.Look);
        SetColorChannel(appearanceChannels[9], logo);
        SetColorChannel(appearanceChannels[10], icon);
        SetColorChannel(appearanceChannels[11], dot);
        ApplyLiveThemeOverrides();
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, () => AppearanceSetBox.SelectedItem = null);
    }

    private string chosenUiFont = "";
    private readonly List<string> importedFonts = [];
    private readonly List<FontOption> fontOptions = [];

    private void InitAppFontGallery(IReadOnlyCollection<string> installed, AppSettings s)
    {
        var have = new HashSet<string>(installed, StringComparer.OrdinalIgnoreCase);
        fontOptions.Add(new("", "Default", new FontFamily("Segoe UI Variable Text, Segoe UI")));
        foreach (var (family, name) in AppFontCandidates)
            if (have.Contains(family)) fontOptions.Add(new FontOption(family, name, new FontFamily(family)));
        foreach (string font in s.ImportedFonts ?? [])
            if (ImportedFontOption(font) is { } option) { importedFonts.Add(font); fontOptions.Add(option); }
        AppFontGallery.ItemsSource = fontOptions;
        AppearanceSetBox.ItemsSource = AppearanceSets.All.Select(set => new StyleOption(set.Name, set.Name)).ToList();
    }

    /// <summary>Imported fonts are stored as "file:///folder/#Family" so WPF loads them straight from the copied file.</summary>
    private static FontOption? ImportedFontOption(string value)
    {
        int hash = value.LastIndexOf('#');
        if (hash < 0) return null;
        try
        {
            string folder = new Uri(value[..hash]).LocalPath;
            if (!Directory.Exists(folder)) return null;
            return new FontOption(value, value[(hash + 1)..], new FontFamily(value));
        }
        catch (Exception ex) when (ex is UriFormatException or ArgumentException) { return null; }
    }

    private void SelectAppFont(string? value)
    {
        string font = (value ?? "").Trim();
        if (font.Length > 0 && fontOptions.All(o => !string.Equals(o.Value, font, StringComparison.OrdinalIgnoreCase)))
        {
            // A face chosen in an earlier version that isn't in the gallery stays available.
            var extra = font.Contains('#') ? ImportedFontOption(font) : new FontOption(font, font, new FontFamily(font));
            if (extra is null) font = "";
            else { fontOptions.Add(extra); AppFontGallery.Items.Refresh(); }
        }
        chosenUiFont = font;
        syncingFontGallery = true;
        try { AppFontGallery.SelectedItem = fontOptions.FirstOrDefault(o => string.Equals(o.Value, font, StringComparison.OrdinalIgnoreCase)); }
        finally { syncingFontGallery = false; }
    }

    private void AppFontGallery_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!IsInitialized || closing || syncingFontGallery || AppFontGallery.SelectedValue is not string font) return;
        chosenUiFont = font;
        ApplyLiveThemeOverrides();
    }

    private void ImportFont_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        var picker = new Microsoft.Win32.OpenFileDialog { Filter = "Fonts|*.ttf;*.otf;*.ttc", Multiselect = false };
        if (picker.ShowDialog(this) != true) return;
        ImportFont(picker.FileName);
    }

    internal bool ImportFont(string path)
    {
        FontImportError.Visibility = System.Windows.Visibility.Collapsed;
        try
        {
            string family = new GlyphTypeface(new Uri(path)).Win32FamilyNames.Values.FirstOrDefault()
                ?? throw new ArgumentException("No family name");
            string folder = Path.Combine(model.FontsDirectory, Guid.NewGuid().ToString("N")[..8]);
            Directory.CreateDirectory(folder);
            File.Copy(path, Path.Combine(folder, Path.GetFileName(path)), true);
            string value = new Uri(folder + Path.DirectorySeparatorChar).AbsoluteUri + "#" + family;
            importedFonts.Add(value);
            fontOptions.Add(new FontOption(value, family, new FontFamily(value)));
            AppFontGallery.Items.Refresh();
            SelectAppFont(value);
            ApplyLiveThemeOverrides();
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or UriFormatException or FileFormatException or NotSupportedException)
        {
            FontImportError.Text = "That font couldn't be imported.";
            FontImportError.Visibility = System.Windows.Visibility.Visible;
            return false;
        }
    }
}

public partial class SettingsWindow
{
    private string logoStyle = LogoCatalog.Classic, logoEffect = LogoEffects.None, logoDotEffect = LogoDotEffects.None;
    private string logoPreviewKey = "";

    private void LoadLogoControls(AppSettings s)
    {
        logoStyle = LogoCatalog.Normalize(s.LogoStyle);
        logoEffect = LogoEffects.Normalize(s.LogoEffect);
        logoDotEffect = LogoDotEffects.Normalize(s.LogoDotEffect);
        LogoEffectBox.ItemsSource ??= LogoEffects.All;
        LogoDotEffectBox.ItemsSource ??= LogoDotEffects.All;
        bool was = applyingAppearance;
        applyingAppearance = true;
        RefreshLogoPreviews();
        LogoEffectBox.SelectedValue = logoEffect;
        LogoDotEffectBox.SelectedValue = logoDotEffect;
        applyingAppearance = was;
    }

    /// <summary>Redraws the logo tiles in the current logo color so the gallery matches what the app shows.</summary>
    private void RefreshLogoPreviews()
    {
        bool dark = TryFindResource("Canvas") is SolidColorBrush canvas && AppearanceOptions.RelativeLuminance(canvas.Color) < .5;
        var stroke = AppearanceOptions.TryParseHex(logoHex, out var c) ? c : ThemeManager.LogoStroke(dark);
        var dot = AppearanceOptions.TryParseHex(logoDotHex, out var d) ? d : ThemeManager.LogoDot(dark);
        string key = $"{stroke}|{dot}|{dark}";
        bool was = applyingAppearance;
        applyingAppearance = true;
        if (key != logoPreviewKey)
        {
            logoPreviewKey = key;
            LogoStyleBox.ItemsSource = LogoCatalog.Options(stroke, dot);
        }
        LogoStyleBox.SelectedValue = logoStyle;
        applyingAppearance = was;
    }

    private void LogoStyle_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!IsInitialized || closing || applyingAppearance || LogoStyleBox.SelectedValue is not string style) return;
        logoStyle = style;
        ApplyLiveThemeOverrides();
    }

    private void LogoEffect_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!IsInitialized || closing || applyingAppearance || LogoEffectBox.SelectedValue is not string effect) return;
        logoEffect = effect;
        ApplyLiveThemeOverrides();
    }

    private void LogoDotEffect_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!IsInitialized || closing || applyingAppearance || LogoDotEffectBox.SelectedValue is not string effect) return;
        logoDotEffect = effect;
        ApplyLiveThemeOverrides();
    }
}
