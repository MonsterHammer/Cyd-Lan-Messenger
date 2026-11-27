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
    /// <summary>Companion size and theme independence, quiet notifications, app font, logo and icon colors, sets (see --custom-only).</summary>
    private static async Task RunLookAndBrandVerificationAsync()
    {
        string artifacts = Path.GetFullPath("artifacts");
        Directory.CreateDirectory(artifacts);

        await TestCompanionScaleAsync(artifacts);
        Console.WriteLine("PASS  Companion scales to 500% and its hit box follows the size");

        TestCompanionIgnoresTheme();
        Console.WriteLine("PASS  Companion looks the same across themes");

        await TestQuietToastAsync();
        Console.WriteLine("PASS  Confirmations are gone and problems float without moving the chat");

        await TestBrandAndFontAsync(artifacts);
        Console.WriteLine("PASS  App font gallery, logo color and icon color");

        await TestAppearanceSetsAsync(artifacts);
        Console.WriteLine($"PASS  {AppearanceSets.All.Count} appearance sets apply; {FloatingPetStyle.Presets.Count} companion and {PetTraits.Presets.Count} personality presets");

        await TestWindowOpacityAsync();
        Console.WriteLine("PASS  Window opacity lets what's behind show through");

        await TestMoreLooksAsync(artifacts);
        Console.WriteLine($"PASS  Font import, {LogoCatalog.Values.Count} logos with {LogoEffects.All.Count - 1} effects, {BandEffects.All.Count} new title/header effects, {ChatCanvasOverlay.Catalog.Count - 1} backgrounds");

        ThemeManager.Apply(ThemeManager.Light, AppearanceOverrides.Default);
    }

    private static async Task TestCompanionScaleAsync(string artifacts)
    {
        await using var model = new MainViewModel(preview: true);
        using var controller = new FloatingPetController(model, () => { });
        model.UpdateFloatingState(enabled: true);
        controller.SyncVisibility();
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        var pet = controller.PetWindow!;
        var hit = (FrameworkElement)pet.FindName("PetHitArea");
        Check(Math.Abs(hit.Width - 136) < .5 && Math.Abs(pet.Width - 260) < .5 && Math.Abs(pet.Height - 300) < .5, "Default size keeps the original window.");
        Rect before = pet.PetScreenRectDip();

        model.PreviewPetAppearance(model.PetAppearance with { Size = 5.0 });
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        Check(pet.AppliedPetSize == 5.0, "Size goes up to 500%.");
        Check(Math.Abs(hit.Width - 680) < .5 && Math.Abs(hit.Height - 620) < .5, "Hit box grows with the companion.");
        Check(pet.Width >= hit.Width && pet.Height >= hit.Height + 172, "Window grows to hold it.");
        Rect big = pet.PetScreenRectDip();
        var work = SystemParameters.WorkArea;
        Check(big.Bottom <= work.Bottom + 1 && big.Left >= work.Left - 1 && big.Right <= work.Right + 1, "Big companion stays on screen.");
        pet.UpdateLayout();
        var drawn = (FrameworkElement)pet.FindName("PetVisual");
        Check(Math.Abs(drawn.ActualWidth - 680) < .5 && Math.Abs(drawn.ActualHeight - 620) < .5, "The companion drawing fills its hit box.");
        Capture(pet, Path.Combine(artifacts, "pet-500.png"));

        model.PreviewPetAppearance(model.PetAppearance with { Size = .75 });
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        Check(Math.Abs(hit.Width - 102) < .5 && Math.Abs(hit.Height - 93) < .5, "Hit box shrinks for a small companion.");
        model.PreviewPetAppearance(null);
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        Rect after = pet.PetScreenRectDip();
        Check(Math.Abs(after.Bottom - before.Bottom) < 2 && Math.Abs(after.Left + after.Width / 2 - (before.Left + before.Width / 2)) < 2, "Resizing keeps the companion's feet in place.");
        Check(FloatingPetStyle.ClampSize(9) == 5.0 && SharedSizeSurvives(), "Shared companions keep big sizes.");
    }

    private static bool SharedSizeSurvives()
    {
        var wire = new AppSettings();
        return CompanionProfileCodec.TryDecodeInto(CompanionProfileCodec.Encode(new AppSettings { PetSize = 4.2 }), wire) && Math.Abs(wire.PetSize - 4.2) < .01;
    }

    private static byte[] RenderPet(PetAppearance style)
    {
        var visual = new FloatingPetVisual { Width = 136, Height = 124, PetStyle = style };
        visual.Measure(new Size(136, 124));
        visual.Arrange(new Rect(0, 0, 136, 124));
        var bmp = new RenderTargetBitmap(136, 124, 96, 96, PixelFormats.Pbgra32);
        bmp.Render(visual);
        var pixels = new byte[136 * 124 * 4];
        bmp.CopyPixels(pixels, 136 * 4, 0);
        return pixels;
    }

    private static void TestCompanionIgnoresTheme()
    {
        ThemeManager.Apply(ThemeManager.Light, AppearanceOverrides.Default);
        byte[] light = RenderPet(PetAppearance.Default);
        ThemeManager.Apply(ThemeManager.Pineapple, AppearanceOverrides.Default);
        byte[] pineapple = RenderPet(PetAppearance.Default);
        ThemeManager.Apply(ThemeManager.FreedomWings, AppearanceOverrides.Default);
        byte[] wings = RenderPet(PetAppearance.Default);
        Check(light.SequenceEqual(pineapple) && light.SequenceEqual(wings), "Theme changes must not restyle the companion.");
        Check(light.Any(b => b != 0), "Companion still draws.");
        ThemeManager.Apply(ThemeManager.Light, AppearanceOverrides.Default);
    }

    private static async Task TestQuietToastAsync()
    {
        await using var model = new MainViewModel(preview: true);
        var window = new MainWindow(model);
        window.Show();
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        var composer = (FrameworkElement)window.FindName("ComposerSurface");
        Point At() => composer.TransformToAncestor(window).Transform(new Point(0, 0));
        Point before = At();
        model.Toast = "Contact offline";
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        var banner = (FrameworkElement)window.FindName("ToastBanner");
        Check(banner.IsVisible, "Problems still show.");
        Check(Grid.GetRow((UIElement)banner.Parent) == 1, "The problem banner floats over the content row.");
        Check(At() == before, "The chat box does not move when a problem shows.");

        model.Toast = "";
        window.OpenReminders(null);
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        ClickNamed(window, "New reminder");
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        ((TextBox)window.FindName("ReminderTitleBox")).Text = "Water plants";
        ClickNamed(window, "Save reminder");
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        Check(model.Reminders.Any(x => x.Title == "Water plants") && model.Toast == "", "Saving a reminder shows no notification.");
        window.Close();
    }

    private static async Task TestBrandAndFontAsync(string artifacts)
    {
        await using var model = new MainViewModel(preview: true);
        var window = new MainWindow(model);
        window.Show();
        var prefs = window.CreateSettingsWindow();
        prefs.Show();
        prefs.ShowPage("AppearancePage");
        prefs.ShowAppearanceTab("Customize");
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);

        var gallery = (ListBox)prefs.FindName("AppFontGallery");
        Check(gallery.IsVisible && gallery.Items.Count >= 6, "App font gallery shows installed faces.");
        var georgia = gallery.Items.OfType<FontOption>().FirstOrDefault(f => f.Value == "Georgia") ?? gallery.Items.OfType<FontOption>().Last();
        gallery.SelectedItem = georgia;
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        Check(Application.Current.Resources["UiFont"] is FontFamily ff && ff.Source.StartsWith(georgia.Value), "Picking a font restyles the app.");
        Check(window.FontFamily.Source.StartsWith(georgia.Value), "Main window uses the app font.");

        var oldMark = Application.Current.Resources["BrandMark"];
        ((ListBox)prefs.FindName("LogoColorBox")).SelectedValue = "#EC4899";
        ((ListBox)prefs.FindName("IconColorBox")).SelectedValue = "#F59E0B";
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        Check(!ReferenceEquals(oldMark, Application.Current.Resources["BrandMark"]), "Logo color recolors the mark.");
        Check(Application.Current.Resources["IconTint"] is SolidColorBrush { Color: var c } && c == Color.FromRgb(0xF5, 0x9E, 0x0B), "Icon color is applied.");
        var glyph = new Glyph { Data = Geometry.Parse("M2,2 L22,22"), Size = 24, Foreground = Brushes.Black };
        Check(glyph.IconTint is SolidColorBrush { Color: var g } && g == c, "Glyphs pick up the tint.");
        var colorMark = Application.Current.Resources["BrandMark"];
        ((ListBox)prefs.FindName("LogoDotColorBox")).SelectedValue = "#EF4444";
        ((ListBox)prefs.FindName("LogoDotEffectBox")).SelectedValue = "pulse";
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        Check(Application.Current.Resources["BrandMark"] is DrawingImage { IsFrozen: false } dotMark && !ReferenceEquals(colorMark, dotMark), "Dot color and effect redraw the mark with a live dot.");
        Check(((ListBox)prefs.FindName("LogoDotEffectBox")).Items.Count >= 8, "Dot effects are offered.");
        Check(ThemeManager.CustomAppIcon is { } appIcon && ReferenceEquals(window.Icon, appIcon), "Window icon follows the logo.");
        using (var stock = new System.Drawing.Icon(Application.GetResourceStream(new Uri("pack://application:,,,/CydLan;component/Assets/cydlan.ico")).Stream, 32, 32))
        using (var drawn = App.MakeIcon(32))
        using (var a = stock.ToBitmap())
        using (var b = drawn.ToBitmap())
            Check(Enumerable.Range(0, 32 * 32).Any(i => a.GetPixel(i % 32, i / 32) != b.GetPixel(i % 32, i / 32)), "Taskbar and tray icon follow the logo.");
        Capture(prefs, Path.Combine(artifacts, "fonts-logo-icons.png"));
        Capture(window, Path.Combine(artifacts, "app-tinted.png"));
        ClickNamed(prefs, "Save");
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        Check(model.Settings.UiFont == georgia.Value && model.Settings.LogoColor == "#EC4899" && model.Settings.IconColor == "#F59E0B", "Font, logo and icon colors save.");
        Check(model.Settings.LogoDotColor == "#EF4444" && model.Settings.LogoDotEffect == "pulse", "Dot color and effect save.");
        if (prefs.IsVisible) prefs.Close();

        model.Settings.UiFont = ""; model.Settings.LogoColor = ""; model.Settings.IconColor = ""; model.Settings.LogoDotColor = ""; model.Settings.LogoDotEffect = "none";
        model.ApplySettings();
        Check(Application.Current.Resources["IconTint"] is SolidColorBrush { Color.A: 0 }, "Clearing the icon color restores the theme.");
        Check(ThemeManager.CustomAppIcon is null && window.Icon is DrawingImage, "Clearing the logo restores the stock app icon.");
        window.Close();
    }

    private static async Task TestAppearanceSetsAsync(string artifacts)
    {
        foreach (var set in AppearanceSets.All)
        {
            var s = new AppSettings();
            AppearanceCodec.Apply(set.Look, s);
            Check(ThemeManager.Catalog.Any(t => t.Tag == s.Theme), $"Set {set.Name} uses a real theme.");
            Check(AppearanceCodec.TryDecode(AppearanceCodec.Encode(s), out _), $"Set {set.Name} round-trips.");
        }
        Check(AppearanceSets.All.Select(s => s.Name).Distinct().Count() == AppearanceSets.All.Count, "Set names are unique.");
        foreach (var preset in FloatingPetStyle.Presets)
        {
            var ps = new AppSettings();
            FloatingPetStyle.Apply(preset.Appearance, ps);
            Check(PetAppearance.From(ps) == preset.Appearance, $"Companion preset {preset.Name} uses real parts.");
        }

        await using var model = new MainViewModel(preview: true);
        model.Settings.UiFont = "Georgia"; model.Settings.LogoColor = "#EC4899";
        model.ApplySettings();
        var window = new MainWindow(model);
        window.Show();
        var prefs = window.CreateSettingsWindow();
        prefs.Show();
        prefs.ShowPage("AppearancePage");
        prefs.ShowAppearanceTab("Themes");
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        var box = (ListBox)prefs.FindName("AppearanceSetBox");
        Check(box.IsVisible && box.Items.Count == AppearanceSets.All.Count, "Sets show on the Themes tab.");
        Check(box.TranslatePoint(new Point(0, 0), (UIElement)prefs.FindName("ThemeFilterAll")).Y < 0, "Sets sit above the theme grid.");
        box.SelectedItem = box.Items.OfType<StyleOption>().First(o => o.Name == "Neon city");
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        Capture(prefs, Path.Combine(artifacts, "sets.png"));
        ClickNamed(prefs, "Save");
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        Check(model.Settings.Theme == ThemeManager.Cyberpunk && model.Settings.MatrixEnabled && model.Settings.UiFont == "Georgia" && model.Settings.LogoColor == "#EC4899",
            "A set saves its look and keeps the user's font and logo color.");
        if (prefs.IsVisible) prefs.Close();
        window.Close();
    }
}
