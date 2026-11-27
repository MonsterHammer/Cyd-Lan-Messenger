using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using CydLan.Core;
using CydLan.Desktop;

internal static partial class Program
{
    private static T? FindVisual<T>(DependencyObject root, Func<T, bool>? match = null) where T : DependencyObject
    {
        if (root is T hit && (match is null || match(hit))) return hit;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            if (FindVisual(VisualTreeHelper.GetChild(root, i), match) is { } found) return found;
        return null;
    }

    /// <summary>Font import, logo styles and effects, extra title/header effects and backgrounds.</summary>
    private static async Task TestMoreLooksAsync(string artifacts)
    {
        await using var model = new MainViewModel(preview: true);
        var window = new MainWindow(model);
        window.Show();
        var prefs = window.CreateSettingsWindow();
        prefs.Show();
        prefs.ShowPage("AppearancePage");
        prefs.ShowAppearanceTab("Customize");
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);

        Check(prefs.FindName("UiFontBox") is null, "The separate interface font list is gone.");
        string source = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "georgia.ttf");
        Check(prefs.ImportFont(source), "A font file imports.");
        Check(Application.Current.Resources["UiFont"] is FontFamily imported && imported.Source.Contains("#Georgia") && imported.Source.StartsWith("file:"),
            "The imported font becomes the app font.");
        Check(prefs.ImportFont(Path.Combine(artifacts, "pet-500.png")) == false && ((TextBlock)prefs.FindName("FontImportError")).IsVisible, "A bad file shows an error.");

        var styles = (ListBox)prefs.FindName("LogoStyleBox");
        Check(styles.Items.Count >= 20, "Twenty logo choices.");
        var oldMark = Application.Current.Resources["BrandMark"];
        styles.SelectedValue = "cat";
        ((ListBox)prefs.FindName("LogoEffectBox")).SelectedValue = "spin";
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        Check(!ReferenceEquals(oldMark, Application.Current.Resources["BrandMark"]), "Choosing a logo redraws the mark.");
        var titleLogo = FindVisual<BrandLogo>(window)!;
        Check(titleLogo.LogoEffect == "spin" && titleLogo.Source == Application.Current.Resources["BrandMark"], "The title bar logo plays the effect.");
        prefs.BringIntoView();
        ((FrameworkElement)prefs.FindName("LogoStyleBox")).BringIntoView();
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        Capture(prefs, Path.Combine(artifacts, "logo-choices.png"));

        prefs.ShowAppearanceTab("Headers");
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        var titleCards = (WrapPanel)prefs.FindName("TitleEffectCards");
        var headerCards = (WrapPanel)prefs.FindName("HeaderEffectCards");
        Check(titleCards.Children.Count >= 9 + BandEffects.All.Count && headerCards.Children.Count >= 11 + BandEffects.All.Count, "More title and header effects.");
        CheckBox Card(WrapPanel panel, string name) => panel.Children.OfType<CheckBox>().First(c => (string)c.Tag == name);
        Card(titleCards, "Title fireworks").IsChecked = true;
        Card(titleCards, "Title rainbow").IsChecked = true;
        Card(headerCards, "Header code rain").IsChecked = true;
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        Check(((FrameworkElement)prefs.FindName("TitleFxIntensityPanel")).IsVisible, "Intensity shows for the new effects.");
        Card(titleCards, "Title fireworks").BringIntoView();
        await Task.Delay(400);
        Capture(prefs, Path.Combine(artifacts, "more-effects.png"));

        ClickNamed(prefs, "Save");
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        Check(model.Settings.UiFont.Contains("#Georgia") && model.Settings.ImportedFonts.Count == 1, "Imported font saves.");
        Check(model.Settings.LogoStyle == "cat" && model.Settings.LogoEffect == "spin", "Logo choice and effect save.");
        Check(model.Settings.TitleFx.SequenceEqual(["fireworks", "rainbow"]) && model.Settings.HeaderFx.SequenceEqual(["coderain"]), "Extra effects save.");
        var band = (FxBand)window.FindName("TitleBarFx");
        Check(band.IsVisible && band.Kinds.Contains("fireworks"), "Title bar plays the new effects.");
        var look = AppearanceCodec.Capture(model.Settings);
        var back = new AppSettings();
        Check(AppearanceCodec.TryDecode(AppearanceCodec.Encode(look), out var decoded), "Look code encodes.");
        AppearanceCodec.Apply(decoded, back);
        Check(back.TitleFx.SequenceEqual(model.Settings.TitleFx) && back.HeaderFx.SequenceEqual(model.Settings.HeaderFx), "Look codes carry the new effects.");
        await Task.Delay(500);
        Capture(window, Path.Combine(artifacts, "app-more-looks.png"));
        if (prefs.IsVisible) prefs.Close();

        // Every effect and background draws something.
        foreach (var fx in BandEffects.All)
        {
            var e = new FxBand { Kind = fx.Value, Width = 400, Height = 40, Intensity = 1 };
            e.Measure(new Size(400, 40)); e.Arrange(new Rect(0, 0, 400, 40));
            var bmp = new RenderTargetBitmap(400, 40, 96, 96, PixelFormats.Pbgra32);
            bool any = false;
            for (int f = 0; f < 8 && !any; f++)
            {
                e.SetCurrentValue(FrameworkElement.TagProperty, f);
                bmp.Clear(); bmp.Render(e);
                var px = new byte[400 * 40 * 4]; bmp.CopyPixels(px, 1600, 0);
                any = px.Any(b => b != 0);
                await Task.Delay(120);
                e.InvalidateVisual();
            }
            Check(any, $"Effect {fx.Name} draws.");
        }
        Check(ChatCanvasOverlay.Catalog.Count >= 22, "More backgrounds.");
        foreach (var bg in ChatCanvasOverlay.Catalog.Where(c => c.Value != ChatCanvasOverlay.Off))
        {
            var o = new ChatCanvasOverlay { Gradient = bg.Value, Width = 120, Height = 80, Intensity = 1 };
            o.Measure(new Size(120, 80)); o.Arrange(new Rect(0, 0, 120, 80));
            var bmp = new RenderTargetBitmap(120, 80, 96, 96, PixelFormats.Pbgra32);
            bmp.Render(o);
            var px = new byte[120 * 80 * 4]; bmp.CopyPixels(px, 480, 0);
            Check(px.Any(b => b != 0), $"Background {bg.Name} draws.");
        }
        window.Close();
    }
}
