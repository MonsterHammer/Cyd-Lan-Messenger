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
    /// <summary>Texture stacks and motion, more ambient/panel/aura effects, aura overflow, word pop, hover and cursor effects, solid input bar (see --effects-pack-only).</summary>
    private static async Task RunEffectsPackVerificationAsync()
    {
        string artifacts = Path.GetFullPath("artifacts");
        Directory.CreateDirectory(artifacts);

        TestTextureStacks();
        Console.WriteLine($"PASS  {ChromeTexture.Catalog.Count - 1} textures stack and normalize");

        await TestTextureMotionAsync(artifacts);
        Console.WriteLine("PASS  Texture motion animates (grassy fields sway, rain scrolls)");

        await TestEffectLayersRenderAsync(artifacts);
        Console.WriteLine($"PASS  {AmbientFxCatalog.All.Count} more ambient, {PanelFxCatalog.All.Count} more panel and {TargetAura.Catalog.Count - 1} aura effects render");

        await TestEffectsPackSettingsAsync(artifacts);
        Console.WriteLine("PASS  Settings pick several textures, motion, ambient/panel effects, aura spill, hover/cursor, word pop color and solid input bar");

        await TestInteractionEffectsAsync(artifacts);
        Console.WriteLine("PASS  Word pop, new typing/click effects, hover and cursor effects play");

        ThemeManager.Apply(ThemeManager.Light, AppearanceOverrides.Default);
    }

    private static void TestTextureStacks()
    {
        Check(ChromeTexture.Catalog.Count >= 45, "More textures.");
        Check(ChromeTexture.Normalize("grassy fields, Moon,bogus,moon") == "grassyfields,moon", "A stack normalizes, dedupes and drops unknowns.");
        Check(ChromeTexture.Normalize("off") == ChromeTexture.Off && ChromeTexture.Normalize("") == ChromeTexture.Off, "No texture is off.");
        Check(ChromeTexture.KindsFor("sakura,rain").SequenceEqual([ChromeTextureKind.Sakura, ChromeTextureKind.Rain]), "Kinds follow the stack order.");
        var s = new AppSettings { ChromeTexture = "grassyfields,aurora", TextureMotionEnabled = true };
        var back = new AppSettings();
        Check(RoundTrip(s, back) && back.ChromeTexture == "grassyfields,aurora" && back.TextureMotionEnabled,
            "Look codes keep stacked textures and motion.");
    }

    private static byte[] Pixels(FrameworkElement element)
    {
        // Drawn through a brush so the element's offset inside its parent doesn't push it out of frame.
        var size = new Size(element.ActualWidth, element.ActualHeight);
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen()) dc.DrawRectangle(new VisualBrush(element), null, new Rect(size));
        var bmp = new RenderTargetBitmap((int)size.Width, (int)size.Height, 96, 96, PixelFormats.Pbgra32);
        bmp.Render(visual);
        var pixels = new byte[bmp.PixelWidth * bmp.PixelHeight * 4];
        bmp.CopyPixels(pixels, bmp.PixelWidth * 4, 0);
        return pixels;
    }

    private static int Differences(byte[] a, byte[] b) => a.Zip(b).Count(p => Math.Abs(p.First - p.Second) > 3);

    private static async Task TestTextureMotionAsync(string artifacts)
    {
        foreach (var (texture, label) in new[] { ("grassyfields", "grass"), ("rain", "rain"), ("plaid", "plaid") })
        {
            var still = new ChromeTexture { Texture = texture, Intensity = 1, Width = 420, Height = 160 };
            var moving = new ChromeTexture { Texture = texture, Intensity = 1, Motion = true, Width = 420, Height = 160 };
            var host = new Window
            {
                Width = 460, Height = 400, WindowStyle = WindowStyle.None, ShowActivated = false, Left = -2000, Top = 0,
                Background = Brushes.Black,
                Content = new StackPanel { Children = { still, moving } }
            };
            host.Show();
            await Task.Delay(150);
            var stillA = Pixels(still);
            var movingA = Pixels(moving);
            Check(movingA.Any(b => b != 0), $"{label} texture draws.");
            await Task.Delay(700);
            var stillB = Pixels(still);
            var movingB = Pixels(moving);
            Check(Differences(stillA, stillB) == 0, $"{label} texture stays still without motion.");
            Check(Differences(movingA, movingB) > 200, $"{label} texture moves with motion on.");
            if (label == "grass") Capture(host, Path.Combine(artifacts, "texture-motion-grass.png"));
            host.Close();
        }

        string[] added = ["aurora", "clouds", "mountains", "ocean", "dunes", "sakura", "bamboo", "pines", "skyline", "snowfall", "lanterns", "rain", "plaid", "chevron"];
        foreach (var theme in new[] { ThemeManager.Light, ThemeManager.Dark })
        {
            ThemeManager.Apply(theme, AppearanceOverrides.Default);
            var wall = new WrapPanel { Width = 1240 };
            foreach (var value in added)
                wall.Children.Add(new Grid
                {
                    Width = 300, Height = 120, Margin = new Thickness(5),
                    Children = { new Border { Background = (Brush)Application.Current.Resources["Sidebar"] }, new ChromeTexture { Texture = value, Intensity = .85 },
                        new TextBlock { Text = value, Foreground = Brushes.Magenta, Margin = new Thickness(4) } }
                });
            var wallHost = new Window { Width = 1260, Height = 560, WindowStyle = WindowStyle.None, ShowActivated = false, Left = -3000, Content = wall };
            wallHost.Show();
            await Task.Delay(200);
            Capture(wallHost, Path.Combine(artifacts, $"new-textures-{theme}.png"));
            wallHost.Close();
        }
        ThemeManager.Apply(ThemeManager.Light, AppearanceOverrides.Default);

        var stack = new ChromeTexture { Texture = "starrynight,moon,grassyfields", Intensity = 1, Width = 420, Height = 160 };
        var stackHost = new Window { Width = 440, Height = 180, WindowStyle = WindowStyle.None, ShowActivated = false, Left = -2000, Background = Brushes.Black, Content = stack };
        stackHost.Show();
        await Task.Delay(150);
        var single = new ChromeTexture { Texture = "grassyfields", Intensity = 1, Width = 420, Height = 160 };
        stackHost.Content = single;
        await Task.Delay(100);
        var singlePixels = Pixels(single);
        stackHost.Content = stack;
        await Task.Delay(100);
        Check(Differences(Pixels(stack), singlePixels) > 500, "Stacked textures draw every layer.");
        Capture(stackHost, Path.Combine(artifacts, "texture-stack.png"));
        stackHost.Close();
    }

    private static async Task TestEffectLayersRenderAsync(string artifacts)
    {
        var grid = new WrapPanel { Width = 1300, Background = Brushes.Black };
        foreach (var option in AmbientFxCatalog.All)
            grid.Children.Add(new Border { Width = 160, Height = 90, Margin = new Thickness(2), Child = new AmbientFxLayer { Kind = option.Value, Intensity = 1 } });
        foreach (var option in PanelFxCatalog.All)
            grid.Children.Add(new Border { Width = 60, Height = 180, Margin = new Thickness(2), Child = new PanelFxLayer { Kind = option.Value, Intensity = 1 } });
        foreach (var swatch in TargetAura.Catalog.Where(c => c.Kind != TargetAuraKind.Off))
            grid.Children.Add(new Border { Width = 120, Height = 56, Margin = new Thickness(6), Background = Brushes.DimGray, Child = new TargetAura { Kind = swatch.Kind, Intensity = 1 } });
        var host = new Window { Width = 1320, Height = 1100, WindowStyle = WindowStyle.None, ShowActivated = false, Left = -3000, Background = Brushes.Black, Content = grid };
        AmbientEffect.SetAnimateWhenUnfocused(true);
        host.Show();
        var cells = grid.Children.Cast<Border>().ToList();
        var drawn = new HashSet<Border>();
        for (int shot = 0; shot < 4; shot++)
        {
            await Task.Delay(700);
            foreach (var cell in cells.Where(c => !drawn.Contains(c)))
                if (Pixels(cell).Where((_, i) => i % 4 != 3).Any(v => Math.Abs(v - (cell.Background is SolidColorBrush bg ? bg.Color.R : 0)) > 6))
                    drawn.Add(cell);
        }
        AmbientEffect.SetAnimateWhenUnfocused(false);
        var blank = cells.Where(c => !drawn.Contains(c)).Select(c => c.Child switch { MultiFxLayer m => m.Kind, TargetAura a => a.Kind.ToString(), _ => "?" }).ToList();
        // Storm only flashes now and then; everything else should be visible at any moment.
        Check(blank.All(k => k == "storm"), $"These effect previews drew nothing: {string.Join(", ", blank)}.");
        Capture(host, Path.Combine(artifacts, "effects-pack-gallery.png"));
        host.Close();
    }

    private static async Task TestEffectsPackSettingsAsync(string artifacts)
    {
        await using var model = new MainViewModel(preview: true);
        var window = new MainWindow(model);
        window.Show();
        var prefs = window.CreateSettingsWindow();
        prefs.Show();
        prefs.ShowPage("AppearancePage");

        prefs.ShowAppearanceTab("Textures");
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        var textures = (ListBox)prefs.FindName("ChromeTextureBox");
        ChromeTextureSwatch Swatch(string v) => ChromeTexture.Catalog.First(t => t.Value == v);
        textures.SelectedItems.Add(Swatch("grassyfields"));
        textures.SelectedItems.Add(Swatch("moon"));
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        Check(!textures.SelectedItems.Contains(Swatch("off")) && textures.SelectedItems.Count == 2, "Picking textures drops None and keeps both.");
        ((CheckBox)prefs.FindName("TextureMotionBox")).IsChecked = true;
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        Check(model.ChromeTexture == "grassyfields,moon" && model.TextureMotion, "The texture stack and motion preview live.");
        Capture(prefs, Path.Combine(artifacts, "textures-tab.png"));

        prefs.ShowAppearanceTab("Ambient");
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        var ambient = (WrapPanel)prefs.FindName("AmbientFxCards");
        Check(ambient.Children.Count == AmbientFxCatalog.All.Count && AmbientFxCatalog.All.Count >= 20, "Twenty-plus new ambient cards.");
        ((CheckBox)ambient.Children[0]).IsChecked = true;
        ((CheckBox)ambient.Children[4]).IsChecked = true;
        ((ComboBox)prefs.FindName("TypingEffectBox")).SelectedValue = "wordpop";
        ((ComboBox)prefs.FindName("HoverEffectBox")).SelectedValue = "ring";
        ((ComboBox)prefs.FindName("CursorEffectBox")).SelectedValue = "sparkle";
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        var popColors = (ListBox)prefs.FindName("WordPopColorBox");
        Check(popColors.IsVisible && popColors.Items.Count > 3, "Word pop shows its color choices.");
        popColors.SelectedIndex = 3;
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        Check(InteractionEffects.Hover == "ring" && InteractionEffects.Cursor == "sparkle", "Hover and cursor effects preview live.");
        Capture(prefs, Path.Combine(artifacts, "ambient-tab.png"));

        prefs.ShowAppearanceTab("Panels");
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        var rail = (WrapPanel)prefs.FindName("RailFxCards");
        var side = (WrapPanel)prefs.FindName("SidebarFxCards");
        Check(rail.Children.Count == PanelFxCatalog.All.Count && side.Children.Count == PanelFxCatalog.All.Count, "More panel cards for the side nav and side panel.");
        ((CheckBox)rail.Children[0]).IsChecked = true;
        ((CheckBox)side.Children[1]).IsChecked = true;
        ((CheckBox)side.Children[3]).IsChecked = true;

        prefs.ShowAppearanceTab("Aura");
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        ((ListBox)prefs.FindName("TargetAuraBox")).SelectedValue = "powersurge";
        ((CheckBox)prefs.FindName("AuraOverflowBox")).IsChecked = true;
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        Check(TargetAura.Overflow && TargetAura.ActiveKind == TargetAuraKind.PowerSurge, "Aura spill previews live.");
        Capture(prefs, Path.Combine(artifacts, "aura-tab.png"));

        prefs.ShowAppearanceTab("Customize");
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        var solid = (CheckBox)prefs.FindName("SolidComposerBox");
        Check(!solid.IsVisible, "Solid input bar only shows for the seamless bar.");
        ((CheckBox)prefs.FindName("SeamlessComposerBox")).IsChecked = true;
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        Check(solid.IsVisible, "Solid input bar appears with the seamless bar.");
        solid.IsChecked = true;
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        var surface = (Border)window.FindName("ComposerSurface");
        Check(model.SolidComposerEnabled && surface.Background == Application.Current.Resources["Input"], "The seamless input bar gets its own solid background.");

        ClickNamed(prefs, "Save");
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        var s = model.Settings;
        Check(s.ChromeTexture == "grassyfields,moon" && s.TextureMotionEnabled, "Textures and motion save.");
        Check(s.AmbientFx.Count == 2 && s.RailFx.Count == 1 && s.SidebarFx.Count == 2, "New ambient and panel effects save.");
        Check(s.TargetAura == "powersurge" && s.AuraOverflowEnabled, "Aura and spill save.");
        Check(s.TypingEffect == "wordpop" && s.WordPopColor.Length > 0 && s.HoverEffect == "ring" && s.CursorEffect == "sparkle", "Typing, hover, cursor and word pop color save.");
        Check(s.SeamlessComposerEnabled && s.SolidComposerEnabled, "Solid input bar saves.");
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        Check(FindVisual<AmbientFxLayer>(window, l => l.Name == "ConversationAmbientFx")!.Kinds.Count == 2, "The chat plays the chosen ambient effects.");
        Check(FindVisual<PanelFxLayer>(window, l => l.Name == "RailPanelFx")!.Kinds.Count == 1, "The side nav plays the chosen panel effect.");

        var back = new AppSettings();
        Check(RoundTrip(s, back) && back.AmbientFx.SequenceEqual(s.AmbientFx) && back.SidebarFx.SequenceEqual(s.SidebarFx)
            && back.AuraOverflowEnabled && back.HoverEffect == "ring" && back.CursorEffect == "sparkle" && back.SolidComposerEnabled && back.WordPopColor == s.WordPopColor,
            "Look codes keep the new options.");
        window.Close();
    }

    private static async Task TestInteractionEffectsAsync(string artifacts)
    {
        Check(ChatEffects.Normalize("wordpop", typing: true) == "wordpop" && ChatEffects.Normalize("wordpop") == "off", "Word pop is a typing effect only.");
        Check(ChatEffects.Normalize("shockwave") == "shockwave" && ChatEffects.Normalize("shockwave", typing: true) == "off", "Shockwave is a click effect only.");
        Check(InteractionEffects.NormalizeCursor("spotlight") == "off", "Removed spotlight cursor effect normalizes to off.");

        await using var model = new MainViewModel(preview: true);
        model.Settings.TypingEffect = "wordpop";
        model.Settings.WordPopColor = "#FF4FA3";
        model.Settings.HoverEffect = "brackets";
        model.Settings.CursorEffect = "comet";
        model.Settings.AuraOverflowEnabled = true;
        model.Settings.TargetAura = "kicharge";
        model.ApplySettings();
        var window = new MainWindow(model) { Width = 1100, Height = 720 };
        window.Show();
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        var effects = (ChatEffects)window.FindName("EffectLayer");
        var composer = (TextBox)window.FindName("Composer");

        composer.Focus();
        composer.Text = "hello";
        composer.CaretIndex = 5;
        composer.SelectedText = " ";
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        Check(effects.Children.OfType<Canvas>().Any(), "Finishing a word pops a comic highlight.");
        Check(InteractionEffects.WordPop == Color.FromRgb(0xFF, 0x4F, 0xA3), "Word pop uses the chosen color.");
        await Task.Delay(120);
        Capture(window, Path.Combine(artifacts, "word-pop.png"));

        int before = effects.Children.Count;
        foreach (var kind in new[] { "hearts", "stars", "confetti", "bubbles", "zap", "notes", "snow", "fire", "comic", "shockwave" })
            effects.Play(new Point(300, 300), kind, model.Settings.Theme, typing: false, 1);
        Check(effects.Children.Count > before + 10, "New click effects spawn their shapes.");

        before = effects.Children.Count;
        for (int i = 0; i < 6; i++) effects.Trail(new Point(100 + i * 12, 200));
        Check(effects.Children.Count > before, "The cursor trail follows the pointer.");

        var button = FindVisual<System.Windows.Controls.Primitives.ButtonBase>(window, b => b.IsVisible && b.ActualWidth is > 20 and < 200)!;
        before = effects.Children.Count;
        window.PlayHover(button);
        Check(effects.Children.Count >= before + 4, "Hover draws corner brackets.");

        var auras = new List<TargetAura>();
        for (int i = 0; i < 4 && auras.Count == 0; i++)
        {
            await Task.Delay(100);
            auras = FindAll<TargetAura>(window).Where(a => a.IsVisible).ToList();
        }
        Check(auras.Count > 0 && auras.All(a => !a.ClipToBounds && a.Margin.Top < -10), "Spilling auras are unclipped with room around them.");
        window.Close();
    }

    private static bool RoundTrip(AppSettings from, AppSettings into)
    {
        if (!AppearanceCodec.TryDecode(AppearanceCodec.Encode(from), out var data)) return false;
        AppearanceCodec.Apply(data, into);
        return true;
    }

    private static List<T> FindAll<T>(DependencyObject root) where T : DependencyObject
    {
        var found = new List<T>();
        if (root is T hit) found.Add(hit);
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++) found.AddRange(FindAll<T>(VisualTreeHelper.GetChild(root, i)));
        return found;
    }
}
