using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using CydLan.Core;
using CydLan.Desktop;

internal static class Program
{
    // Use WPF's normal application lifetime while bypassing the desktop entry
    // point, which would otherwise open real LAN sockets and the user's profile.
    private sealed class TestApplication : App
    {
        public required Func<Task> RunTests { get; init; }
        protected override async void OnStartup(StartupEventArgs e) => await RunTests();
    }

    [STAThread]
    private static int Main(string[] args)
    {
        int exitCode = 0;
        TestApplication? app = null;
        app = new TestApplication { RunTests = async () => {
            try
            {
                if (args.Contains("--effects-benchmark")) await RunEffectsBenchmarkAsync();
                else await RunAsync(args.Contains("--offline"), args.Contains("--startup-loopback"));
            }
            catch (Exception e) { Console.WriteLine("FAIL  " + e); exitCode = 1; }
            finally { await app!.QuitAsync(); }
        } };
        app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        app.Resources = new ResourceDictionary { Source = new Uri("/CydLan;component/Theme.xaml", UriKind.Relative) };
        app.Run();
        return exitCode;
    }

    private static void Check(bool ok, string reason) { if (!ok) throw new Exception(reason); }

    private static async Task RunEffectsBenchmarkAsync()
    {
        await using var model = new MainViewModel(preview: true);
        await model.SavePreferencesAsync(settings =>
        {
            settings.FirefliesEnabled = settings.RainEnabled = settings.SnowEnabled = true;
            settings.FireflyIntensity = settings.RainIntensity = settings.SnowIntensity = 1;
        });
        ThemeManager.Apply(ThemeManager.Ocean);
        var window = new MainWindow(model) { WindowState = WindowState.Maximized };
        window.Show();
        window.Activate();
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        Check(window.IsActive && window.WindowState == WindowState.Maximized, "The effects benchmark needs an active maximized preview.");
        Console.WriteLine($"READY {Environment.ProcessId} {window.ActualWidth:0}x{window.ActualHeight:0}");
        await Task.Delay(TimeSpan.FromSeconds(14));
        window.Close();
    }
    private static async Task Until(Func<bool> predicate)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        while (!predicate()) await Task.Delay(15, deadline.Token);
    }

    private static async Task RunAsync(bool offlineOnly, bool startupLoopback)
    {
        await using (var preview = new MainViewModel(preview: true))
        {
            await preview.StartAsync();
            var jamie = preview.SelectedPeer!; preview.Draft = "Jamie's draft";
            preview.SelectedPeer = preview.Peers[1]; Check(preview.Draft == "", "Drafts leaked between contacts.");
            preview.Draft = "Sofia's draft";
            preview.Broadcasting = true; preview.Draft = "Announcement draft";
            preview.Broadcasting = false; Check(preview.Draft == "Sofia's draft", "Broadcast replaced a direct draft.");
            preview.SelectedPeer = jamie; Check(preview.Draft == "Jamie's draft", "Switching contacts lost the draft.");
            await preview.SendAsync(); Check(preview.Messages.Last().Delivery == "Preview only", "Preview fabricated a network receipt.");
            Check(preview.DeviceLabel.Contains("NO LAN CONNECTION"), "Preview must remain clearly offline.");
            Check(!preview.HasLogs && preview.LogsDirectory == "", "Offline previews must not create persistent diagnostics.");
            Console.WriteLine("PASS  Offline preview, separate contact/broadcast drafts, honest receipts");

            // Switching theme while windows are open must recolour inherited text, not only explicit brushes.
            var themed = new MainWindow(preview); themed.Show();
            var prefs = new SettingsWindow(preview) { Owner = themed }; prefs.Show();
            prefs.ShowPage("AboutPage");
            string version = typeof(MainWindow).Assembly.GetName().Version!.ToString(3);
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            Check(FindText(prefs, $"Version {version}") is { IsVisible: true } &&
                FindText(prefs, "MonsterHammer") is { IsVisible: true }, "About should show the installed version and developer.");
            prefs.ShowPage("AlertsPage");
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            Check(FindText(prefs, "Show notifications during Do not disturb") is { IsVisible: true } &&
                FindText(prefs, "Play sounds during Do not disturb") is { IsVisible: true }, "Alert labels should identify the action and status.");
            var popupOption = (System.Windows.Controls.CheckBox)prefs.FindName("NotificationPreviewsBox");
            Check(popupOption is { IsVisible: true, IsEnabled: true, IsChecked: false }, "Popup previews should be an unchecked option by default.");
            prefs.ShowPage("AppearancePage");
            var themeBox = (System.Windows.Controls.ListBox)prefs.FindName("ThemeBox");
            Check(ThemeManager.Catalog.Count == 45 && themeBox.Items.Count == 45,
                $"Expected 45 theme sample boxes (44 themes + Match Windows), found {ThemeManager.Catalog.Count}/{themeBox.Items.Count}.");
            var chatButton = (System.Windows.Controls.Button)themed.FindName("ChatNav");
            var chatGlyph = (Glyph)chatButton.Content;
            var standardGlyph = chatGlyph.Data;
            string[] primaryIconNames = ["Chat", "Users", "Settings", "Send", "Folder", "Megaphone", "File", "Search"];
            string[] detailIconNames = ["Sidebar", "Refresh", "Info", "Smile", "Plus", "Transfer", "Download", "More", "Quit", "Close", "ChevronDown", "Check"];
            var standardIcons = primaryIconNames.Concat(detailIconNames)
                .ToDictionary(name => name, name => themed.FindResource("Icon" + name));
            string appearanceArtifacts = Path.GetFullPath("artifacts");
            Directory.CreateDirectory(appearanceArtifacts);
            var buttonRoot = (System.Windows.Controls.Grid)chatButton.Template.FindName("Root", chatButton);
            var buttonChrome = (System.Windows.Controls.Border)buttonRoot.Children[0];
            foreach (var (mode, ink) in new[] {
                ("dark", "#FFECECEC"), ("ocean", "#FFE9F5FF"), ("lavender", "#FF282039"),
                ("ember", "#FFFFF0E3"), ("forest", "#FFECF7EE"), ("sand", "#FF30261E"),
                ("anime", "#FF302039"), ("sakura", "#FF39262F"), ("steampunk", "#FFF5E3C7"), ("aurora", "#FFE8F9F5"),
                ("paper", "#FF232A28"), ("arcade", "#FFF4F1FF"), ("pixel", "#FFF0F7D9"),
                ("cyberpunk", "#FFEDF6FF"), ("midnight", "#FFF3F5FC"), ("sunset", "#FFFFF2ED"),
                ("mint", "#FF172B24"), ("matcha", "#FF242B1E"), ("frost", "#FF192736"),
                ("light", "#FF1D2326")
            })
            {
                themeBox.SelectedValue = mode;
                await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
                bool pixel = mode == "pixel";
                Check((buttonChrome.CornerRadius.TopLeft == 0) == pixel,
                    $"Incorrect button shape in {mode}: {buttonChrome.CornerRadius}, app resource {themed.FindResource("ControlCorner")}.");
                bool customIcons = mode is not ("light" or "dark");
                Check((!ReferenceEquals(chatGlyph.Data, standardGlyph)) == customIcons, $"Incorrect chat geometry in {mode}.");
                bool fullIconSet = mode is "pixel" or "anime" or "sakura" or "steampunk" or "aurora" or "paper" or "arcade" or
                    "cyberpunk" or "midnight" or "sunset" or "mint" or "matcha" or "frost";
                foreach (string name in primaryIconNames.Concat(detailIconNames))
                {
                    bool expected = fullIconSet || (customIcons && primaryIconNames.Contains(name));
                    Check((!ReferenceEquals(themed.FindResource("Icon" + name), standardIcons[name])) == expected,
                        $"Incorrect Icon{name} when switching to {mode}.");
                }
                if (mode is "forest" or "sakura" or "steampunk" or "cyberpunk" or "midnight" or "frost")
                    Capture(themed, Path.Combine(appearanceArtifacts, $"theme-{mode}-icons-tested.png"));
                foreach (Window w in new Window[] { themed, prefs })
                {
                    string actual = ((SolidColorBrush)w.Foreground).Color.ToString();
                    Check(actual == ink, $"{w.GetType().Name} text stayed {actual} after switching to {mode}.");
                    var heading = w is SettingsWindow ? FindText(w, "Preferences") : (System.Windows.Controls.TextBlock)w.FindName("ConversationTitle");
                    string inherited = ((SolidColorBrush)heading!.Foreground).Color.ToString();
                    Check(inherited == ink, $"{w.GetType().Name} inherited text stayed {inherited} after switching to {mode}.");
                }
            }
            foreach (var (mode, ink) in new[] {
                ("obsidian", "#FFF0F3F8"), ("nord", "#FFECEFF4"), ("synthwave", "#FFFCEEFF"),
                ("crimson", "#FFFDF1F3"), ("emerald", "#FFECFDF5"), ("rose", "#FF2C1620"),
                ("espresso", "#FF2D1E14"), ("cobalt", "#FF151E33"), ("sage", "#FF1A2921"), ("amber", "#FF2C2111"),
                ("pineapple", "#FF332609"), ("coralstar", "#FF341522"), ("noirpink", "#FFFFF0F7"), ("vampire", "#FFF9ECEF"),
                ("voltspark", "#FF2E2307"), ("kingdomred", "#FF18223A"), ("bubblegum", "#FF351126"), ("ribbonblush", "#FF2C1720"),
                ("noirgold", "#FFF3F5F8"), ("webcrimson", "#FFF4F6FF"), ("jester", "#FFF5F0FF"), ("cipher", "#FFD9FFE6"),
                ("gundamfreedom", "#FF0F172A"), ("ironman", "#FFFFF7EB")
            })
            {
                themeBox.SelectedValue = mode;
                await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
                foreach (Window w in new Window[] { themed, prefs })
                {
                    string actual = ((SolidColorBrush)w.Foreground).Color.ToString();
                    Check(actual == ink, $"{w.GetType().Name} text stayed {actual} after switching to {mode}.");
                }
            }
            themeBox.SelectedValue = "pixel";
            prefs.Close();
            Check(ReferenceEquals(chatGlyph.Data, standardGlyph), "Cancel did not restore the original icons.");
            Check(preview.Settings.TypingEffect == "off" && preview.Settings.ClickEffect == "off" && preview.ChatBackground is null,
                "Changing themes must leave effects and backgrounds off by default.");
            var composer = (System.Windows.Controls.TextBox)themed.FindName("Composer");
            var effects = (ChatEffects)themed.FindName("EffectLayer");
            Check(!effects.IsHitTestVisible && ChatEffects.Resolve("theme", "anime", true) == "sparkles" &&
                ChatEffects.Resolve("theme", "steampunk", false) == "steam" &&
                ChatEffects.Resolve("ripple", "steampunk", true) == "ripple" &&
                ChatEffects.Normalize("burst") == "burst" && ChatEffects.Normalize("petals") == "petals",
                "Themes should style opted-in effects without overriding a selected effect.");
            string previousDraft = preview.Draft;
            composer.Text = "hello";
            composer.CaretIndex = composer.Text.Length;
            themed.Activate();
            composer.Focus();
            themed.UpdateLayout();
            TypeInComposer(composer, " ");
            Check(effects.Children.Count == 0 && composer.Text == "hello ", "Typing effects should be off by default.");
            preview.Settings.TypingEffect = "sparkles";
            TypeInComposer(composer, "world");
            TypeInComposer(composer, "!");
            Check(effects.Children.Count == (SystemParameters.ClientAreaAnimation ? 1 : 0) && composer.Text == "hello world!",
                "Completing a word with punctuation should animate without changing the typed text.");
            preview.Settings.TypingEffect = "off";
            preview.Settings.ClickEffect = "steam";
            int beforeClick = effects.Children.Count;
            themed.RaiseEvent(new System.Windows.Input.MouseButtonEventArgs(System.Windows.Input.Mouse.PrimaryDevice, 0, System.Windows.Input.MouseButton.Left)
                { RoutedEvent = UIElement.PreviewMouseLeftButtonDownEvent });
            Check(effects.Children.Count == beforeClick + (SystemParameters.ClientAreaAnimation ? 1 : 0),
                "Mouse effects should work independently of typing effects.");
            preview.Settings.ClickEffect = "off";
            preview.Draft = previousDraft;
            Console.WriteLine("PASS  Live theme switching, Pixel controls and icons, Cancel restores appearance");

            var enginePrefs = new SettingsWindow(preview) { Owner = themed };
            enginePrefs.Show();
            ((System.Windows.Controls.Primitives.Selector)enginePrefs.FindName("ThemeBox")).SelectedValue = ThemeManager.Steampunk;
            enginePrefs.ShowPage("AppearancePage");
            enginePrefs.ShowAppearanceTab("Headers");
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            var engineToggle = (System.Windows.Controls.CheckBox)enginePrefs.FindName("SteampunkEngineBox");
            var enginePreview = (SteampunkEngine)enginePrefs.FindName("EnginePreview");
            var engine = (SteampunkEngine)themed.FindName("TitleEngine");
            Check(engineToggle.IsVisible && engineToggle.IsChecked == false && enginePreview.IsVisible && !engine.IsVisible,
                $"The Steampunk engine preview card must show by default while MainWindow stays off. Toggle visible={engineToggle.IsVisible}; checked={engineToggle.IsChecked}; preview visible={enginePreview.IsVisible}; engine visible={engine.IsVisible}.");
            engineToggle.IsChecked = true;
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            Check(enginePreview.IsVisible && ((FrameworkElement)enginePrefs.FindName("EngineIntensityBox")).IsVisible && !engine.IsVisible,
                "Enabling the card should reveal its intensity slider before saving.");
            ClickNamed(enginePrefs, "Save");
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            Check(engine.IsVisible && !engine.IsHitTestVisible && preview.Settings.SteampunkEngineEnabled,
                "Saving should reveal the engine in the title bar without intercepting input.");
            if (SystemParameters.ClientAreaAnimation)
                await Until(() => engine.ShaftAngle > 10);
            else
                Check(engine.ShaftAngle == 0, "Reduced motion should leave the engine still.");
            Capture(themed, Path.Combine(appearanceArtifacts, "steampunk-engine-tested.png"));
            themed.WindowState = WindowState.Minimized;
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            Check(engine.ShaftAngle == 0, "Minimizing must stop the engine animation.");
            themed.WindowState = WindowState.Normal;
            ThemeManager.Apply(ThemeManager.Anime);
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            Check(engine.IsVisible && engine.Tint == ThemeAccent() && preview.Settings.SteampunkEngineEnabled,
                "Switching themes should recolor the enabled engine.");
            await preview.SavePreferencesAsync(st => { st.SteampunkEngineEnabled = false; st.Theme = ThemeManager.Light; });
            Console.WriteLine("PASS  Optional Steampunk engine preview, motion, minimize and theme switching");

            var sakuraPrefs = new SettingsWindow(preview) { Owner = themed };
            sakuraPrefs.Show();
            ((System.Windows.Controls.Primitives.Selector)sakuraPrefs.FindName("ThemeBox")).SelectedValue = ThemeManager.Sakura;
            sakuraPrefs.ShowPage("AppearancePage");
            sakuraPrefs.ShowAppearanceTab("Ambient");
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            var petalsToggle = (System.Windows.Controls.CheckBox)sakuraPrefs.FindName("SakuraPetalsBox");
            var petalsPreview = (SakuraPetals)sakuraPrefs.FindName("PetalsPreview");
            var petals = (SakuraPetals)themed.FindName("ConversationPetals");
            var intensity = (System.Windows.Controls.Slider)sakuraPrefs.FindName("PetalIntensityBox");
            Check(petalsToggle.IsVisible && petalsToggle.IsChecked == false && petalsPreview.IsVisible && !petals.IsVisible,
                "Sakura petals preview must show by default while MainWindow stays opt-in.");
            petalsToggle.IsChecked = true;
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            intensity.Value = .35;
            Check(petalsPreview.IsVisible && intensity.IsVisible && !petals.IsVisible && petalsPreview.Intensity == .35,
                "The card toggle and intensity slider should preview the petals before saving.");
            ClickNamed(sakuraPrefs, "Save");
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            Check(petals.IsVisible && !petals.IsHitTestVisible && preview.Settings.SakuraPetalsEnabled && !engine.IsVisible &&
                petals.Intensity == .35 && petals.Tint == ThemeAccent(),
                "Petals should appear behind chat without intercepting input or enabling the engine.");
            if (SystemParameters.ClientAreaAnimation)
            {
                themed.Activate();
                await Until(() => petals.Progress > .005);
            }
            else
                Check(petals.Progress == 0, "Reduced motion should leave petals still.");
            Capture(themed, Path.Combine(appearanceArtifacts, "sakura-tested.png"));
            themed.Hide();
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            Check(petals.Progress == 0, "Hiding the chat should stop the petal animation.");
            themed.Show();
            ThemeManager.Apply(ThemeManager.Steampunk);
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            Check(petals.IsVisible && petals.Tint == ThemeAccent() && petals.Tint.R > petals.Tint.G && petals.Tint.G > petals.Tint.B,
                "Steampunk should give the same petals a warm brown palette.");
            await preview.SavePreferencesAsync(st => { st.SteampunkEngineEnabled = true; st.EngineIntensity = .65; st.Theme = ThemeManager.Steampunk; });
            Capture(themed, Path.Combine(appearanceArtifacts, "appearance-steampunk-effects-tested.png"));
            ThemeManager.Apply(ThemeManager.Ocean);
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            Check(petals.IsVisible && engine.IsVisible && petals.Tint == ThemeAccent() && engine.Tint == ThemeAccent() &&
                engine.Opacity == .65 && petals.Intensity == .35, "Both effects should follow the theme and use independent strengths.");
            await preview.SavePreferencesAsync(st => { st.SakuraPetalsEnabled = false; st.SteampunkEngineEnabled = false; st.Theme = ThemeManager.Light; });
            ThemeManager.Apply(ThemeManager.Light);
            themed.Close();
            Console.WriteLine("PASS  Optional Sakura theme petals, preview, motion and visibility");
            await TestAdditionalEffectsAsync(appearanceArtifacts);
            await TestThemeDetailsAsync(appearanceArtifacts);
            await TestHeaderAndSidebarEffectsAsync(appearanceArtifacts);
            await TestChromeTexturesAsync(appearanceArtifacts);
            await TestTargetAuraAsync(appearanceArtifacts);
            await TestCustomizationSuiteAsync(appearanceArtifacts);
            await TestFloatingLanSuiteAsync(appearanceArtifacts);
            await TestMediaModuleSuiteAsync(appearanceArtifacts);

            var recentWindow = new MainWindow(preview);
            recentWindow.Show();
            var recentList = (System.Windows.Controls.ListBox)recentWindow.FindName("RecentList");
            var recentTab = (System.Windows.Controls.RadioButton)recentWindow.FindName("RecentTab");
            var peopleTab = (System.Windows.Controls.RadioButton)recentWindow.FindName("PeopleTab");
            recentTab.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
            var group = preview.Groups.Single();
            Check(recentList.IsVisible && recentList.Items.Cast<object>().SequenceEqual(new object[] { jamie, group }),
                "Recent should show only conversations with activity, newest first.");
            Capture(recentWindow, Path.Combine(Path.GetFullPath("artifacts"), "recent-chats-tested.png"));
            recentList.SelectedItem = group;
            Check(preview.InGroup && preview.SelectedGroup == group, "Selecting a recent group should open it.");
            preview.GroupsOpen = false;
            recentList.SelectedItem = group;
            Check(preview.InGroup, "Selecting the same recent group should reopen it from the Chats section.");
            preview.GroupsOpen = false;
            var sofia = preview.Peers[1];
            preview.SelectedPeer = sofia;
            await preview.SendAsync();
            Check(recentList.Items[0] == sofia && recentList.SelectedItem == sofia,
                "Sending should move the chat to the top and preserve its selection.");
            preview.Search = group.Name;
            Check(recentList.Items.Count == 1 && recentList.Items[0] == group, "Recent should filter by group name.");
            preview.Search = "";
            preview.SelectedPeer = sofia;
            preview.DeleteConversation();
            Check(!recentList.Items.Contains(sofia), "Deleting chat history should remove it from Recent.");
            peopleTab.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
            Check(!recentList.IsVisible && peopleTab.IsChecked == true, "People should restore the contact directory.");
            preview.SelectedPeer = jamie;
            var incoming = new ChatMessage { Text = "ASDF" };
            var outgoing = new ChatMessage { Text = "OK", Outgoing = true, Delivery = "Delivered" };
            var longMessage = new ChatMessage { Text = string.Concat(Enumerable.Repeat("A longer message still wraps in a compact window. ", 4)) };
            preview.Messages.Add(incoming);
            preview.Messages.Add(outgoing);
            preview.Messages.Add(longMessage);
            recentWindow.UpdateLayout();
            var messageList = (System.Windows.Controls.ItemsControl)recentWindow.FindName("MessageList");
            FrameworkElement RowPart(ChatMessage message, string name)
            {
                var row = (System.Windows.Controls.ContentPresenter)messageList.ItemContainerGenerator.ContainerFromItem(message);
                return (FrameworkElement)messageList.ItemTemplate.FindName(name, row);
            }
            foreach (var message in new[] { incoming, outgoing })
            {
                var bubble = RowPart(message, "TextBubble");
                var metadata = RowPart(message, "MessageMetadata");
                var edge = message.Outgoing ? HorizontalAlignment.Right : HorizontalAlignment.Left;
                Check(bubble.ActualWidth > 0 && bubble.ActualWidth < metadata.ActualWidth &&
                    bubble.HorizontalAlignment == edge && metadata.HorizontalAlignment == edge,
                    "A short bubble should size to its text and align with its timestamp on the correct side.");
            }
            var wrapped = RowPart(longMessage, "TextBubble");
            Check(wrapped.ActualWidth <= messageList.ActualWidth && wrapped.ActualHeight > RowPart(incoming, "TextBubble").ActualHeight,
                "A long message should wrap within the available chat width.");
            preview.Messages.Remove(incoming);
            preview.Messages.Remove(outgoing);
            preview.Messages.Remove(longMessage);
            recentWindow.Close();
            Console.WriteLine("PASS  Recent chats, group navigation, live ordering, search and deletion");
            Console.WriteLine("PASS  Short bubbles fit the text; long messages wrap");

            var startupAlert = new MainWindow(preview) { ShowInTaskbar = false };
            IntPtr foregroundBeforeAlert = GetForegroundWindow();
            startupAlert.RequestTaskbarAttention();
            Check(startupAlert.IsVisible && startupAlert.ShowInTaskbar && IsIconic(new WindowInteropHelper(startupAlert).Handle) &&
                !startupAlert.IsActive && GetForegroundWindow() == foregroundBeforeAlert,
                "The first alert in a tray-only session should create a minimized taskbar button without taking focus.");
            startupAlert.Close();
            const string taskbarTestTitle = "CydLan tray alert test";
            var hiddenAlert = new MainWindow(preview) { Title = taskbarTestTitle };
            hiddenAlert.Show();
            hiddenAlert.Hide();
            var foregroundHost = new Window { Title = "CydLan test foreground", ShowInTaskbar = false, Width = 260, Height = 150 };
            foregroundHost.Show();
            foregroundHost.Activate();
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            var foreground = GetForegroundWindow();
            preview.Settings.Sounds = false;
            ((App)Application.Current).ConnectNotifications(hiddenAlert, preview);
            preview.NotifyIncoming("Offline sender", "Private sample message");
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            IntPtr hiddenHandle = new WindowInteropHelper(hiddenAlert).Handle;
            const int appWindow = 0x00040000, toolWindow = 0x00000080;
            int extendedStyle = GetWindowLong(hiddenHandle, -20);
            Check(hiddenAlert.ShowInTaskbar && IsIconic(hiddenHandle) &&
                (extendedStyle & (appWindow | toolWindow)) == appWindow &&
                !hiddenAlert.IsActive && GetForegroundWindow() == foreground,
                $"Tray return should stay minimized and preserve focus. Taskbar={hiddenAlert.ShowInTaskbar}; minimized={IsIconic(hiddenHandle)}; active={hiddenAlert.IsActive}; foreground before={foreground}, after={GetForegroundWindow()}.");
            hiddenAlert.Close();
            foregroundHost.Close();
            Console.WriteLine("PASS  Tray-only startup and incoming alert restore a private minimized taskbar window");

            // Popups open over a dimmed main window, fit inside it, and cannot be moved outside it.
            var host = new MainWindow(preview) { Left = 100, Top = 100, Width = 800, Height = 560 }; host.Show();
            var popup = new SettingsWindow(preview);
            PopupHost.Attach(host, popup); host.SetScrim(true); popup.Show();
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            var scrim = (FrameworkElement)host.FindName("Scrim");
            Check(scrim.Visibility == Visibility.Visible, "Opening a popup did not show the backdrop.");
            bool Inside() => popup.Left >= host.Left && popup.Top >= host.Top
                && popup.Left + popup.ActualWidth <= host.Left + host.ActualWidth + 0.5
                && popup.Top + popup.ActualHeight <= host.Top + host.ActualHeight + 0.5;
            Check(Inside(), $"Popup opened outside the main window ({popup.Left},{popup.Top} {popup.ActualWidth}x{popup.ActualHeight}).");
            foreach (var (x, y) in new[] { (-5000.0, -5000.0), (9000.0, 9000.0), (host.Left + 400, host.Top - 300) })
            {
                popup.Left = x; popup.Top = y;
                await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
                Check(Inside(), $"Moving the popup to {x},{y} let it leave the main window ({popup.Left},{popup.Top}).");
            }
            popup.Close(); host.SetScrim(false); host.Close();
            Console.WriteLine("PASS  Popup backdrop, fit-to-window and confined dragging");

            // Default size is the compact minimum; the sidebar slides closed/open; search grows in place and collapses on Escape.
            var motion = new MainWindow(preview) { WindowStartupLocation = WindowStartupLocation.Manual, Left = 120, Top = 120 }; motion.Show();
            Check(motion.Width == motion.MinWidth && motion.Height == motion.MinHeight, "The window should open at its smallest size.");
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            if (int.TryParse(Environment.GetEnvironmentVariable("CYDLAN_TEST_HOLD_MS"), out int hold)) await Task.Delay(hold); // lets a screen grab see real window corners
            var sidebar = (FrameworkElement)motion.FindName("SidebarPanel");
            double openWidth = sidebar.ActualWidth;
            ClickNamed(motion, "Toggle conversations sidebar");
            await Until(() => sidebar.Visibility == Visibility.Collapsed);
            ClickNamed(motion, "Toggle conversations sidebar");
            await Until(() => Math.Abs(sidebar.ActualWidth - openWidth) < 0.5);
            Check(Math.Abs(sidebar.ActualWidth - openWidth) < 0.5, $"Sidebar reopened to {sidebar.ActualWidth}, expected {openWidth}.");
            var pill = (FrameworkElement)motion.FindName("SearchPill");
            ClickNamed(motion, "Search this conversation"); await Task.Delay(450);
            Check(Math.Abs(pill.ActualWidth - 220) < 0.5, $"Search should expand in place to 220 (was {pill.ActualWidth}).");
            Check(((UIElement)motion.FindName("MessageSearchBox")).IsKeyboardFocusWithin, "Expanded search should take keyboard focus.");
            Capture(motion, Path.Combine(Path.GetFullPath("artifacts"), "search-open-tested.png"));
            motion.RaiseEvent(new System.Windows.Input.KeyEventArgs(System.Windows.Input.Keyboard.PrimaryDevice, PresentationSource.FromVisual(motion)!, 0, System.Windows.Input.Key.Escape) { RoutedEvent = UIElement.PreviewKeyDownEvent });
            await Task.Delay(400);
            Check(Math.Abs(pill.ActualWidth - 32) < 0.5, $"Escape should shrink search back to its icon (was {pill.ActualWidth}).");

            preview.SelectedPeer = preview.Peers[0];
            var avatar = (System.Windows.Shapes.Ellipse)motion.FindName("ConversationAvatar");
            var avatarBrush = (ImageBrush)avatar.Fill;
            Check(avatarBrush.ImageSource is null && avatar.IsVisible, "A contact without a photo must still have a visible header avatar.");
            preview.SelectedPeer.AvatarPath = preview.Messages.First(m => m.HasImage).ImagePath;
            preview.Peers[1].Online = false;
            ClickNamed(motion, "Maximize");
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            motion.UpdateLayout();
            Check(avatarBrush.ImageSource is not null, "The header did not pick up the contact's profile photo.");
            Check(FindText(motion, "Online") is not null && FindText(motion, "Offline") is not null,
                "The conversation list must show both online and offline presence in text.");
            var handle = new System.Windows.Interop.WindowInteropHelper(motion).Handle;
            var workArea = System.Windows.Forms.Screen.FromHandle(handle).WorkingArea;
            var root = (FrameworkElement)motion.FindName("RootBorder");
            var contentStart = root.PointToScreen(new Point(0, 0));
            var contentEnd = root.PointToScreen(new Point(root.ActualWidth, root.ActualHeight));
            Check(motion.WindowState == WindowState.Maximized &&
                Math.Abs(contentStart.X - workArea.Left) <= 2 && Math.Abs(contentStart.Y - workArea.Top) <= 2 &&
                Math.Abs(contentEnd.X - workArea.Right) <= 2 && Math.Abs(contentEnd.Y - workArea.Bottom) <= 2,
                $"Maximized content runs from {contentStart} to {contentEnd} but the work area is {workArea}.");
            var chat = (FrameworkElement)motion.FindName("ChatContentGrid");
            var wideMessageList = (FrameworkElement)motion.FindName("MessageList");
            var composerSurface = (FrameworkElement)motion.FindName("ComposerSurface");
            Check(chat.ActualWidth - wideMessageList.ActualWidth is >= 48 and <= 70 &&
                Math.Abs(chat.ActualWidth - composerSurface.ActualWidth - 52) < 1,
                $"The timeline and composer should fill the maximized chat pane with gutters. Chat={chat.ActualWidth}, messages={wideMessageList.ActualWidth}, composer={composerSurface.ActualWidth}.");
            Capture(motion, Path.Combine(Path.GetFullPath("artifacts"), "chat-maximized-tested.png"));
            ClickNamed(motion, "Restore");
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            Check(motion.WindowState == WindowState.Normal, "The window must return to normal size on Restore.");
            var chatImage = FindChatImage(motion) ?? throw new Exception("Chat image menu is missing.");
            var menu = chatImage.ContextMenu;
            menu.PlacementTarget = chatImage;
            menu.IsOpen = true;
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            var actions = menu.Items.OfType<System.Windows.Controls.MenuItem>().ToArray();
            Check(actions.Length == 5 && actions.All(item => ReferenceEquals(item.DataContext, chatImage.DataContext)),
                "Each image action should target the clicked chat image.");
            menu.IsOpen = false;
            bool showedImage = false;
            var closeViewer = motion.Dispatcher.InvokeAsync(() =>
            {
                var viewer = motion.OwnedWindows.OfType<ImagePreviewWindow>().SingleOrDefault();
                if (viewer is null) return;
                showedImage = viewer.IsVisible &&
                    ((System.Windows.Controls.Image)viewer.FindName("PreviewImage")).Source is BitmapSource;
                viewer.Close();
            }, DispatcherPriority.ApplicationIdle);
            actions[0].RaiseEvent(new RoutedEventArgs(System.Windows.Controls.MenuItem.ClickEvent));
            await closeViewer;
            Check(showedImage, "Opening a chat image should show its bitmap inside CydLan.");
            motion.Close();
            Console.WriteLine("PASS  Chat profile photo and presence, maximize and restore, compact sidebar and search");
        }

        string directory = Path.Combine(Path.GetTempPath(), "CydLan-desktop-" + Guid.NewGuid().ToString("N"));
        var store = new LocalStore(directory);
        int port = 0;
        for (int candidatePort = 50420; candidatePort <= 60800; candidatePort += 137)
        {
            var tcp = new TcpListener(IPAddress.Loopback, candidatePort);
            try
            {
                tcp.Start();
                using var udp = new UdpClient(new IPEndPoint(IPAddress.Loopback, candidatePort));
                port = candidatePort;
                break;
            }
            catch (SocketException) { }
            finally { tcp.Stop(); }
        }
        Check(port != 0, "Could not reserve a dual TCP/UDP desktop test port.");
        store.SaveSettings(new() { DisplayName = "Home desktop", Port = port });
        var remoteSettings = new AppSettings { DisplayName = "Experiment peer", Port = 0 };
        try
        {
            await TestOfflinePagingAsync(Path.Combine(directory, "paging"));
            await TestStartupRosterAsync(Path.Combine(directory, "startup"));
            if (startupLoopback)
            {
                await TestStartupConnectionAsync(Path.Combine(directory, "startup-loopback"), port);
                return;
            }
            if (offlineOnly)
            {
                Console.WriteLine("PASS  Local desktop checks completed with networking disabled.");
                return;
            }
            await using (var peer = new LanService(remoteSettings))
            await using (var model = new MainViewModel(localStore: store))
            {
                Check(!model.Preview && model.Peers.Count == 0, "Default desktop mode must use real contacts.");
                Check(model.HasLogs && model.LogsDirectory == Path.Combine(directory, "logs"), "Live diagnostics must stay inside the isolated application profile.");
                await peer.StartAsync(true, IPAddress.Loopback);
                await model.StartAsync(true, IPAddress.Loopback);
                await model.ConnectByAddressAsync($"127.0.0.1:{peer.DiscoveryPort}");
                await Until(() => model.OnlineCount == 1);
                model.SelectedPeer = model.Peers.Single();
                model.Replying = true;
                var received = new TaskCompletionSource<ChatMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
                int peerMessageCount = 0;
                peer.MessageReceived += message => { Interlocked.Increment(ref peerMessageCount); received.TrySetResult(message); };
                model.Draft = "Desktop → LAN peer 👋\n你好 & <hello>";
                Check(model.CanSend, "Online desktop composer should allow a nonempty message.");
                await model.SendAsync();
                Check((await received.Task.WaitAsync(TimeSpan.FromSeconds(8))).Text.Contains("你好"), "Desktop failed to send Unicode.");
                Check(model.Messages.Single().Delivery == "Delivered" && model.Draft == "", "Desktop must show a real receipt and clear the sent draft.");
                var retry = model.Messages.Single(); retry.Delivery = "Unconfirmed";
                await model.RetryAsync(retry);
                Check(retry.Delivery == "Delivered" && peerMessageCount == 1, "Desktop retry must confirm delivery without duplicating the message.");
                var alertHost = new MainWindow(model);
                ((App)Application.Current).ConnectNotifications(alertHost, model);
                alertHost.Show();
                alertHost.Activate();
                await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
                Check(alertHost.IsActive && model.ShouldAlert && !model.ShouldShowNotificationPopup,
                    "The foreground notification test needs an active window and taskbar-only alerts by default.");
                model.Replying = true;
                var alertHandle = new WindowInteropHelper(alertHost).Handle;
                int windowCount = Application.Current.Windows.Count;
                string alertTitle = alertHost.Title;
                Check(await peer.SendChatAsync(model.Settings.UserId, "Peer → desktop ✅\nMessages and receipts are working.", "14001"), "Desktop did not acknowledge inbound chat.");
                await Until(() => model.Messages.Count == 2);
                Check(model.SelectedPeer.Unread == 0, "A focused conversation should not accumulate unread messages.");
                Check(Application.Current.Windows.Count == windowCount && alertHost.IsActive,
                    "An inbound message should not open an alert window or interrupt a focused chat.");
                model.Settings.Notifications = false;
                alertHost.Hide();
                model.TransfersOpen = true;
                Check(await peer.SendChatAsync(model.Settings.UserId, "A message while viewing file transfers", "14003"), "Transfers view stopped receiving chat.");
                await Until(() => model.SelectedPeer.Unread == 1);
                Check(!alertHost.IsVisible && Application.Current.Windows.Count == windowCount,
                    "Turning notifications off should keep CydLan hidden in the tray.");
                model.TransfersOpen = false;
                Check(model.SelectedPeer.Unread == 1, "Opening a conversation must not mark it read before the user replies.");
                model.Replying = true;
                Check(model.SelectedPeer.Unread == 0, "Engaging the composer must clear the unread count.");
                model.Settings.Notifications = true;
                model.Replying = false;
                var foregroundWindow = new Window { Title = "Taskbar test", Width = 280, Height = 160, ShowInTaskbar = false };
                foregroundWindow.Show();
                foregroundWindow.Activate();
                await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
                IntPtr foreground = GetForegroundWindow();
                Check(await peer.SendChatAsync(model.Settings.UserId, "A message while the window is inactive", "14002"), "Inactive window stopped receiving chat.");
                await Until(() => model.SelectedPeer.Unread == 1);
                Check(model.PeopleSummary.Contains("1 unread"), "Unread totals did not update.");
                Check(alertHost.IsVisible && alertHost.ShowInTaskbar && alertHost.WindowState == WindowState.Minimized &&
                    IsIconic(alertHandle) && !alertHost.IsActive && GetForegroundWindow() == foreground &&
                    Application.Current.Windows.Count == windowCount + 1 && alertHost.Title == alertTitle,
                    $"Expected a private taskbar alert. Visible: {alertHost.IsVisible}, taskbar: {alertHost.ShowInTaskbar}, " +
                    $"state: {alertHost.WindowState}, native minimized: {IsIconic(alertHandle)}, active: {alertHost.IsActive}, " +
                    $"foreground before/after: {foreground}/{GetForegroundWindow()}.");
                foregroundWindow.Close();
                App.ShowWindow(alertHost);
                Check(alertHost.IsVisible && !IsIconic(alertHandle) && model.SelectedPeer.Unread == 1,
                    "Restoring the chat should leave the message unread until the user engages the composer.");
                await model.SavePreferencesAsync(s => s.NotificationPreviews = true);
                Check(store.LoadSettings().NotificationPreviews, "The popup opt-in should be saved.");
                const string popupText = "Message with popup previews enabled";
                Check(await peer.SendChatAsync(model.Settings.UserId, popupText, "14004"), "The popup test message was not delivered.");
                MessageNotificationWindow? popup = null;
                await Until(() => (popup = Application.Current.Windows.OfType<MessageNotificationWindow>().SingleOrDefault()) is { IsVisible: true });
                Check(popup is { IsActive: false } && FindText(popup, popupText) is not null,
                    "Opting in should show a message preview without taking focus.");
                await model.SavePreferencesAsync(s => s.NotificationPreviews = false);
                Check(!popup!.IsVisible && !store.LoadSettings().NotificationPreviews,
                    "Turning previews off should dismiss the current popup and save the preference.");
                alertHost.Close();
                Console.WriteLine("PASS  Private taskbar alerts by default and persistent opt-in popup previews");
                Console.WriteLine("PASS  Live desktop discovery, send/receive, receipts and unread counts");

                // Images: CydLan peers exchange them inline (auto-accepted, shown in the chat); plain offers still need approval.
                string peerImages = Path.Combine(directory, "peer-images"); peer.InlineImageDirectory = peerImages;
                var peerTransfers = new List<FileTransfer>();
                peer.TransferChanged += t => { lock (peerTransfers) if (!peerTransfers.Contains(t)) peerTransfers.Add(t); };
                string png = Path.Combine(directory, "photo.png");
                {
                    var visual = new DrawingVisual();
                    using (var dc = visual.RenderOpen()) dc.DrawRectangle(Brushes.SteelBlue, null, new Rect(0, 0, 40, 30));
                    var bitmap = new RenderTargetBitmap(40, 30, 96, 96, PixelFormats.Pbgra32); bitmap.Render(visual);
                    var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                    using var file = File.Create(png); encoder.Save(file);
                }
                Check(model.AddAttachment(png) is null && model.HasAttachments && model.CanSend, "An attached image alone should be sendable.");
                {
                    var composerView = new MainWindow(model); composerView.Show();
                    await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
                    Directory.CreateDirectory(Path.GetFullPath("artifacts"));
                    Capture(composerView, Path.Combine(Path.GetFullPath("artifacts"), "composer-attachment-tested.png"));

                    // A Snipping Tool capture (Win+Shift+S) leaves only image formats on the clipboard, no text.
                    // Pasting it into the message box must attach it. The user's clipboard is restored afterwards.
                    var backup = new DataObject();
                    try
                    {
                        if (Clipboard.GetDataObject() is { } current)
                            foreach (var format in current.GetFormats(false)) { try { if (current.GetData(format) is { } value) backup.SetData(format, value); } catch (Exception) { } }
                    }
                    catch (System.Runtime.InteropServices.COMException) { }
                    try
                    {
                        var snip = new DataObject();
                        snip.SetData("PNG", new MemoryStream(File.ReadAllBytes(png)));
                        snip.SetImage(BitmapFrame.Create(new Uri(png), BitmapCreateOptions.None, BitmapCacheOption.OnLoad));
                        Clipboard.SetDataObject(snip, true);
                        var composer = (System.Windows.Controls.TextBox)composerView.FindName("Composer");
                        composer.Focus();
                        Check(System.Windows.Input.ApplicationCommands.Paste.CanExecute(null, composer), "Paste must be available when the clipboard holds only a screenshot.");
                        System.Windows.Input.ApplicationCommands.Paste.Execute(null, composer);
                        Check(model.Attachments.Count == 2 && model.Attachments[1].IsImage, "Pasting a screenshot should attach it as an image.");
                        model.RemoveAttachment(model.Attachments[1]);
                    }
                    finally { try { Clipboard.SetDataObject(backup, true); } catch (System.Runtime.InteropServices.COMException) { } }
                    composerView.Hide();
                }
                await model.SendAsync();
                Check(!model.HasAttachments, "Sending should clear the composer attachments.");
                FileTransfer? inbound = null;
                await Until(() => { lock (peerTransfers) return (inbound = peerTransfers.SingleOrDefault(t => t.Inline)) is not null; });
                Check(inbound!.State == "Waiting for approval" && !Directory.Exists(peerImages),
                    "Incoming images must wait for approval instead of bypassing Accept/Decline when auto-accept is off.");
                await peer.AcceptFileIntoAsync(inbound, directory);
                await Until(() => inbound.State == "Received");
                Check(inbound.Inline && File.ReadAllBytes(inbound.Path).SequenceEqual(File.ReadAllBytes(png)), "Accepting an inline image should receive the exact image bytes.");
                Check(Path.GetDirectoryName(inbound.Path) == peerImages, "Accepted inline images must be stored in the receiver's image folder.");
                await Until(() => model.Messages.Last().Delivery == "Sent");
                Check(model.Messages.Last().HasImage && File.Exists(model.Messages.Last().ImagePath) && model.Transfers.All(t => t.Inline), "The sent image should appear in the chat (and be listed in Files as inline).");
                // Receiving multiple inline images (e.g. repeated pasted screenshots): each waits for approval, and each can be accepted or declined independently.
                await peer.OfferFileAsync(model.Settings.UserId, png, inlineImage: true);
                await peer.OfferFileAsync(model.Settings.UserId, png, inlineImage: true);
                await Until(() => model.Transfers.Count(t => !t.Outgoing && t.Inline) == 2);
                var incomingImages = model.Transfers.Where(t => !t.Outgoing && t.Inline).ToList();
                var incomingImageBubbles = model.Messages.Where(m => !m.Outgoing && m.AwaitingAnswer).ToList();
                Check(incomingImages.All(t => t.State == "Waiting for approval") && incomingImageBubbles.Count == 2,
                    "Incoming screenshots/images must show Accept/Decline in chat instead of bypassing approval.");
                await model.AcceptMessageFileAsync(incomingImageBubbles[0]);
                await Until(() => incomingImageBubbles[0] is { HasImage: true, HasFile: false, Delivery: "Received" });
                Check(File.Exists(incomingImageBubbles[0].ImagePath), "Accepting an inline image offer should display the photo inline in chat.");
                await model.DeclineMessageFileAsync(incomingImageBubbles[1]);
                await Until(() => incomingImageBubbles[1] is { Delivery: "Declined", AwaitingAnswer: false, HasImage: false });
                Check(model.Transfers.All(t => t.Inline), "Inline image offers remain recorded in Files.");
                await peer.OfferFileAsync(model.Settings.UserId, png);
                await Until(() => model.Transfers.Any(t => !t.Inline));
                var offer = model.Transfers.Single(t => !t.Inline);
                Check(offer.State == "Waiting for approval", "Ordinary file offers must still wait for the user to accept.");
                // Accepting saves straight into the received files folder, naming a copy "name [1].ext" like LAN Messenger.
                Check(model.ReceivedFilesFolder == Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Received Files"), "Received files default to LAN Messenger's Documents\\Received Files.");
                string receivedFolder = Path.Combine(directory, "Received Files"); Directory.CreateDirectory(receivedFolder);
                File.WriteAllText(Path.Combine(receivedFolder, offer.Name), "already here");
                model.Settings.ReceivedFilesFolder = receivedFolder;
                await model.AcceptFileAsync(offer);
                await Until(() => offer.State == "Received");
                string expectedCopy = Path.Combine(receivedFolder, Path.GetFileNameWithoutExtension(offer.Name) + " [1]" + Path.GetExtension(offer.Name));
                Check(offer.Path == expectedCopy && File.ReadAllBytes(expectedCopy).SequenceEqual(File.ReadAllBytes(png)) && File.ReadAllText(Path.Combine(receivedFolder, offer.Name)) == "already here",
                    "An accepted file must be saved as \"name [1].ext\" beside an existing one.");
                // The conversation's files panel: Received and Sent tabs for the open person only.
                model.SetConversationFilesOpen(true);
                model.ConversationFilesSent = false;
                Check(model.ConversationFiles.Cast<FileTransfer>().Any() && model.ConversationFiles.Cast<FileTransfer>().All(t => !t.Outgoing && t.PeerId == model.SelectedPeer!.Id), "The Received tab lists only files from this person.");
                model.ConversationFilesSent = true;
                Check(model.ConversationFiles.Cast<FileTransfer>().All(t => t.Outgoing) && model.ConversationSentCount == model.Transfers.Count(t => t.Outgoing), "The Sent tab lists only files sent to this person.");
                model.ConversationFilesSent = false;
                Console.WriteLine("PASS  Inline images both ways; accepted files saved like LAN Messenger; files panel tabs");

                var projectFolder = Directory.CreateDirectory(Path.Combine(directory, "Project files"));
                File.WriteAllText(Path.Combine(projectFolder.FullName, "report.txt"), "project notes");
                Check(model.AddAttachment(projectFolder.FullName) is null &&
                    model.Attachments.Single() is { IsFolder: true, Name: "Project files" } && model.CanSend,
                    "The composer should accept a folder attachment.");
                await model.SendAsync();
                var folderBubble = model.Messages.Last();
                Check(folderBubble is { FolderAttachment: true, FileName: "Project files", Delivery: "Waiting for them to accept" } &&
                    model.TransferFor(folderBubble)?.Path == projectFolder.FullName, "The outgoing folder should use the existing file bubble and keep its local source.");
                FileTransfer? folderOffer = null;
                await Until(() => { lock (peerTransfers) return (folderOffer = peerTransfers.SingleOrDefault(t => t.Folder && t.Name == "Project files")) is not null; });
                await peer.DeclineFileAsync(folderOffer!);
                await Until(() => folderBubble.Delivery == "Declined");
                Console.WriteLine("PASS  Attach and send a folder through the file-transfer flow");

                // A sent file's bubble follows the legacy outcome: waiting → declined / accepted → sent / cancelled.
                string doc = Path.Combine(directory, "notes.txt"); File.WriteAllText(doc, new string('x', 200_000));
                async Task<(ChatMessage Message, FileTransfer Offer)> SendDocAsync()
                {
                    int before; lock (peerTransfers) before = peerTransfers.Count;
                    Check(model.AddAttachment(doc) is null, "A file should attach.");
                    await model.SendAsync();
                    var bubble = model.Messages.Last(m => m.HasFile);
                    Check(bubble.Delivery == "Waiting for them to accept", $"A file offer should wait for the other side (was {bubble.Delivery}).");
                    await Until(() => { lock (peerTransfers) return peerTransfers.Count > before; });
                    lock (peerTransfers) return (bubble, peerTransfers.Last());
                }
                var (declinedBubble, declinedOffer) = await SendDocAsync();
                await peer.DeclineFileAsync(declinedOffer);
                await Until(() => declinedBubble.Delivery == "Declined");
                Check(declinedBubble.DeliveryProblem, "Declined should be shown as a problem.");
                var (acceptedBubble, acceptedOffer) = await SendDocAsync();
                await peer.AcceptFileAsync(acceptedOffer, Path.Combine(directory, "notes-received.txt"));
                await Until(() => acceptedBubble.Delivery == "Sent");
                await Until(() => acceptedOffer.State == "Received");
                Check(!acceptedBubble.DeliveryProblem && File.ReadAllText(Path.Combine(directory, "notes-received.txt")).Length == 200_000, "Accepted files should arrive and show Sent.");
                var (cancelledBubble, _) = await SendDocAsync();
                await model.CancelOfferAsync(model.Transfers.First(t => t.Outgoing && t.State == "Waiting for approval"));
                await Until(() => cancelledBubble.Delivery == "Cancelled");
                await Until(() => { lock (peerTransfers) return peerTransfers.Last().State == "Cancelled"; });
                string activeCancelDoc = Path.Combine(directory, "active-cancel.bin");
                using (var fs = File.Create(activeCancelDoc)) fs.SetLength(128L * 1024 * 1024);
                int beforeActive; lock (peerTransfers) beforeActive = peerTransfers.Count;
                Check(model.AddAttachment(activeCancelDoc) is null, "Large file should attach.");
                await model.SendAsync();
                var activeCancelBubble = model.Messages.Last(m => m.HasFile);
                await Until(() => { lock (peerTransfers) return peerTransfers.Count > beforeActive; });
                FileTransfer activePeerOffer; lock (peerTransfers) activePeerOffer = peerTransfers.Last();
                string activeCancelTarget = Path.Combine(directory, "active-cancel-received.bin");
                await peer.AcceptFileAsync(activePeerOffer, activeCancelTarget);
                await Until(() => model.TransferFor(activeCancelBubble)?.State is "Connecting" or "Transferring");
                await model.CancelMessageFileAsync(activeCancelBubble);
                await Until(() => activeCancelBubble.Delivery == "Cancelled" && activePeerOffer.State == "Aborted");
                Check(!activeCancelBubble.CanCancel && !File.Exists(activeCancelTarget) && !Directory.EnumerateFiles(directory, "*.partial").Any(),
                    "Active cancellation from the UI view-model should cancel both sides and clean partial files.");
                Console.WriteLine("PASS  File bubbles show waiting, declined, accepted/sent and cancelled");

                // Group chat speaks LAN Messenger's wire format: groupmessage + thread + groupmsgop, no acknowledgements.
                var peerGroup = new List<(string From, string Thread, string Op, string Text)>();
                peer.GroupMessageReceived += (from, thread, op, text) => { lock (peerGroup) peerGroup.Add((from, thread, op, text)); };
                var group = await model.CreateGroupAsync("Lunch", [model.Peers.Single().Id]);
                Check(group is not null && model.IsGroupsSection && model.SelectedGroup == group && model.ChatTitle == "Lunch", "Creating a group should open it.");
                await Until(() => { lock (peerGroup) return peerGroup.Any(g => g.Op == "request" && g.Thread == group!.Id && g.From == model.Settings.UserId); });
                Check(group!.Id.StartsWith(model.Settings.UserId, StringComparison.Ordinal), "Thread IDs start with the creator's user ID, as in LAN Messenger.");
                await peer.SendGroupAsync(model.Settings.UserId, group.Id, "message", "Pizza? 🍕");
                await Until(() => model.Messages.Any(m => m.GroupId == group.Id && m.Text == "Pizza? 🍕" && m.SenderName == "Experiment peer"));
                Check(model.Messages.All(m => m.GroupId == group.Id), "The group view must only show that group's messages.");
                model.Draft = "Yes please";
                Check(model.CanSend, "An online member should make the group sendable.");
                await model.SendAsync();
                await Until(() => { lock (peerGroup) return peerGroup.Any(g => g.Op == "message" && g.Text == "Yes please"); });
                Check(model.Messages.Last().Delivery == "Sent", "Group messages are reported Sent (LAN Messenger has no group receipts).");
                await peer.SendGroupAsync(model.Settings.UserId, "unknown-thread", "message", "should be dropped");
                await peer.SendGroupAsync(model.Settings.UserId, "peer-room-1", "request");
                await Until(() => model.Groups.Any(g => g.Id == "peer-room-1"));
                await Until(() => { lock (peerGroup) return peerGroup.Any(g => g.Op == "join" && g.Thread == "peer-room-1"); });
                Check(model.Groups.Count == 2 && !model.Groups.Any(g => g.Id == "unknown-thread"), "Only invitations create groups; stray messages are dropped.");
                await peer.SendGroupAsync(model.Settings.UserId, group.Id, "leave");
                await Until(() => group.Members.Count == 0);
                model.GroupsOpen = false;
                Check(model.IsConversationSection && model.Messages.All(m => m.GroupId.Length == 0), "Direct chat must not show group messages.");
                Console.WriteLine("PASS  Group chat request/join/message/leave with a LAN Messenger-format peer");

                // Files section: everything sent and received, filterable.
                model.TransfersOpen = true;
                Check(model.IsFilesSection && model.ReceivedCount >= 2 && model.SentCount >= 1, "Files should list received and sent transfers.");
                model.TransferFilter = "sent";
                Check(model.TransfersView.Cast<FileTransfer>().All(t => t.Outgoing), "The Sent filter shows only sent files.");
                model.TransferFilter = "received";
                Check(model.TransfersView.Cast<FileTransfer>().All(t => !t.Outgoing), "The Received filter shows only received files.");
                model.TransferFilter = "all"; model.TransfersOpen = false;

                // Preferences apply immediately.
                await model.SavePreferencesAsync(st => { st.MessageFontSize = "large"; st.FirstName = "Home"; st.About = "Desk 4"; st.SendWithCtrlEnter = true; });
                Check((double)Application.Current.Resources["MessageFontSize"] == 17, "Message text size should change right away.");
                Check(model.Settings.SendWithCtrlEnter && model.Settings.FirstName == "Home", "Preferences should be stored.");
                await model.SavePreferencesAsync(st => { st.MessageFontSize = "medium"; st.SendWithCtrlEnter = false; });
                var chosenBackground = Images.LoadChatBackground(png) ?? throw new Exception("A local PNG should load as a background.");
                string backgroundPath = model.SaveChatBackground(chosenBackground);
                await model.SavePreferencesAsync(st =>
                {
                    st.ChatBackgroundPath = backgroundPath; st.ChatBackgroundOpacity = .4;
                    st.TypingEffect = "theme"; st.ClickEffect = "ripple"; st.MessageFont = "Consolas"; st.SteampunkEngineEnabled = true;
                    st.SakuraPetalsEnabled = true;
                });
                Console.WriteLine("PASS  Files filters and preferences (font size, profile, send key)");

                var window = new MainWindow(model); window.Show();
                await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
                var background = (System.Windows.Controls.Image)window.FindName("ChatBackgroundImage");
                Check(background.Source is not null && background.Opacity == .4 && !background.IsHitTestVisible &&
                    ((FontFamily)Application.Current.Resources["MessageFontFamily"]).Source.Contains("Consolas"),
                    "Background and font choices should apply independently in chat.");
                var activeBubble = new ChatMessage
                {
                    Id = "bubble-cancel-test", PeerId = model.SelectedPeer!.Id, Outgoing = true,
                    FileName = "Test attachment.bin", TransferId = "progress-ui-test", Delivery = "Accepted", CanCancel = true
                };
                model.Messages.Add(activeBubble);
                var loading = new FileTransfer
                {
                    Id = "progress-ui-test", PeerId = model.SelectedPeer!.Id, Name = "Test attachment.bin",
                    Size = 100, Outgoing = false, State = "Accepted"
                };
                model.Transfers.Insert(0, loading);
                await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
                var progress = FindTransferProgress(window, loading);
                Check(progress is { IsVisible: true, IsIndeterminate: true }, "Accepted attachments should show connecting progress in chat.");
                Check(FindButtons(window, "Cancel transfer").Count(b => b.IsVisible) >= 2,
                    "Active transfers should show Cancel buttons in both the conversation bubble and active bar.");
                loading.TransferredBytes = 40;
                loading.Progress = 40;
                loading.State = "Transferring";
                activeBubble.Delivery = "Sending 40% · 0 KB / 0.1 KB";
                await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
                Check(progress is { IsIndeterminate: false, Value: 40 } &&
                    loading.ActiveLabel.StartsWith("Receiving 40%", StringComparison.Ordinal) &&
                    FindText(window, loading.ActiveLabel) is { IsVisible: true },
                    "An incoming attachment should show its live byte progress.");
                string artifacts = Path.GetFullPath("artifacts"); Directory.CreateDirectory(artifacts);
                Capture(window, Path.Combine(artifacts, "active-transfer-cancel-tested.png"));
                loading.State = "Received";
                activeBubble.Delivery = "Sent";
                activeBubble.CanCancel = false;
                await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
                Check(progress is { IsVisible: false } && FindButtons(window, "Cancel transfer").All(b => !b.IsVisible),
                    "Completed attachments should stop showing loading and hide Cancel buttons.");
                model.Transfers.Remove(loading);
                model.Messages.Remove(activeBubble);
                Capture(window, Path.Combine(artifacts, "live-chat-tested.png"));
                ClickNamed(window, "Files in this conversation");
                await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
                Check(((FrameworkElement)window.FindName("FilesPanel")).IsVisible, "The files button should open the files panel.");
                model.Transfers.Insert(0, loading);
                loading.State = "Transferring";
                await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
                Check(FindButtons((FrameworkElement)window.FindName("FilesPanel"), "Cancel transfer").Any(b => b.IsVisible),
                    "Files panel should show a Cancel button while a transfer is active.");
                Capture(window, Path.Combine(artifacts, "files-panel-cancel-tested.png"));
                model.TransfersOpen = true;
                await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
                Check(FindButtons((FrameworkElement)window.FindName("TransfersList"), "Cancel transfer").Any(b => b.IsVisible),
                    "Transfers page should show a Cancel button while a transfer is active.");
                Capture(window, Path.Combine(artifacts, "transfers-page-cancel-tested.png"));
                model.TransfersOpen = false;
                loading.State = "Received";
                model.Transfers.Remove(loading);
                await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
                Capture(window, Path.Combine(artifacts, "files-panel-tested.png"));
                ClickNamed(window, "Contact details");
                await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
                Check(!((FrameworkElement)window.FindName("FilesPanel")).IsVisible && ((FrameworkElement)window.FindName("DetailsPanel")).IsVisible, "Only one side panel is open at a time.");
                ClickNamed(window, "Contact details");
                window.Width = window.MinWidth; window.Height = window.MinHeight;
                await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
                Capture(window, Path.Combine(artifacts, "compact-chat-tested.png"));
                var preferences = new SettingsWindow(model) { Owner = window }; preferences.Show();
                await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
                Capture(preferences, Path.Combine(artifacts, "live-preferences-tested.png"));
                preferences.ShowPage("AppearancePage");
                preferences.ShowAppearanceTab("Customize");
                await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
                var previewImage = (System.Windows.Controls.Image)preferences.FindName("BackgroundPreview");
                var opacityControl = (System.Windows.Controls.Slider)preferences.FindName("BackgroundOpacityBox");
                var dimControl = (System.Windows.Controls.Slider)preferences.FindName("BackgroundDimBox");
                var scaleControl = (System.Windows.Controls.Slider)preferences.FindName("BackgroundScaleBox");
                var fitFill = (System.Windows.Controls.Primitives.ToggleButton)preferences.FindName("BgFitFill");
                var anchorCenter = (System.Windows.Controls.Primitives.ToggleButton)preferences.FindName("BgAnchorCenter");
                var anchorTopLeft = (System.Windows.Controls.Primitives.ToggleButton)preferences.FindName("BgAnchorTopLeft");
                var anchorBottomRight = (System.Windows.Controls.Primitives.ToggleButton)preferences.FindName("BgAnchorBottomRight");
                var anchorLabel = (System.Windows.Controls.TextBlock)preferences.FindName("BackgroundAnchorLabel");
                var grayBox = (System.Windows.Controls.CheckBox)preferences.FindName("BackgroundGrayscaleBox");
                Check(previewImage.IsVisible && previewImage.Source == background.Source && opacityControl.IsVisible &&
                    opacityControl.Maximum == 1.0 && opacityControl.Value == .4 &&
                    dimControl.Value == 0 && scaleControl.Value == 1.0 &&
                    fitFill.IsChecked == true && anchorCenter.IsChecked == true && anchorLabel.Text == "Center" &&
                    grayBox.IsChecked == false &&
                    opacityControl.Template.FindName("PART_Track", opacityControl) is not null,
                    "The background settings should preview the saved background with its controls.");
                anchorTopLeft.IsChecked = true;
                await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
                Check(model.ChatBackgroundAnchor == BackgroundAnchor.TopLeft && anchorLabel.Text == "Top left",
                    "Selecting the top-left anchor should update live preview and label.");
                anchorBottomRight.IsChecked = true;
                await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
                Check(model.ChatBackgroundAnchor == BackgroundAnchor.BottomRight && anchorLabel.Text == "Bottom right",
                    "Selecting the bottom-right anchor should update live preview and label.");
                anchorCenter.IsChecked = true;
                await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
                Capture(preferences, Path.Combine(artifacts, "customization-background-tested.png"));
                Check(((FrameworkElement)preferences.FindName("FontBox")).IsVisible && previewImage.IsVisible,
                    "Font and background controls should share Appearance.");
                preferences.Close(); window.Hide();

                await model.SaveProfileAsync("Home desktop updated", "away", false, false);
                model.Draft = "Draft kept after restart";
                await peer.DisposeAsync();
                await Until(() => model.OnlineCount == 0);
                Check(model.CanCompose && !model.CanSend, "Offline contact should keep its draft but disable sending.");
                var failed = model.Messages.First(); failed.Delivery = "Unconfirmed";
                await model.RetryAsync(failed);
                Check(failed.Delivery == "Unconfirmed", "Retrying an offline peer fabricated a receipt.");
                Console.WriteLine("PASS  Offline draft preservation and retry feedback; live/compact/preferences rendered");
            }
            await using var restored = new MainViewModel(localStore: store);
            restored.SelectedPeer = restored.Peers.Single();
            Check(restored.Draft == "Draft kept after restart", "Draft did not survive restart.");
            Check(restored.Messages.Count(m => m.HasText && m.GroupId.Length == 0) == 5 && restored.Messages.First().CanRetry, "History or retry state did not survive restart.");
            Check(restored.Messages.Count(m => m.HasImage && File.Exists(m.ImagePath)) == 2, "Sent and received images did not survive restart.");
            Check(restored.Groups.Count == 2 && restored.Groups.Any(g => g.Name == "Lunch"), "Groups did not survive restart.");
            Check(restored.RecentChats.Contains(restored.SelectedPeer) &&
                restored.RecentChats.Contains(restored.Groups.Single(g => g.Name == "Lunch")) &&
                !restored.SelectedPeer.Online, "Recent should include saved direct and group chats after restart, including offline contacts.");
            Check(restored.Transfers.Count >= 3 && restored.Settings.FirstName == "Home", "File history and profile did not survive restart.");
            Check(restored.Settings.DisplayName == "Home desktop updated", "Preferences were not persisted.");
            Check(restored.ChatBackground is not null && restored.ChatBackgroundOpacity == .4 &&
                restored.Settings is { TypingEffect: "theme", ClickEffect: "ripple", MessageFont: "Consolas", SteampunkEngineEnabled: true, SakuraPetalsEnabled: true },
                "The background, font and each effect should survive restart independently.");
            string savedBackground = restored.Settings.ChatBackgroundPath;
            await restored.SavePreferencesAsync(st => st.ChatBackgroundPath = "");
            Check(restored.ChatBackground is null && !File.Exists(savedBackground) && restored.Settings.ClickEffect == "ripple",
                "Removing the background should remove its managed copy and keep the effect preferences.");
            Console.WriteLine("PASS  Desktop restart restores history, drafts, profile and retry state");

            // Delete chat removes only the open conversation (and CydLan's picture copies); delete all chats removes every one.
            var contact = restored.Peers.Single(); restored.SelectedPeer = contact;
            var pictures = restored.Messages.Where(m => m.HasImage).Select(m => m.ImagePath).ToList();
            restored.DeleteConversation();
            Check(restored.Messages.Count == 0 && contact.Preview == "" && pictures.Count == 2 && pictures.All(p => !File.Exists(p)), "Delete chat should remove this person's messages and picture copies.");
            Check(restored.Transfers.Any(t => !t.Inline && !t.Outgoing && t.State == "Received" && File.Exists(t.Path)), "Delete chat keeps received files and the files list.");
            restored.SelectedGroup = restored.Groups.First(g => g.Name == "Lunch");
            Check(restored.Messages.Count == 2, "Delete chat must not touch other chats.");
            restored.ClearMessageHistory();
            Check(restored.Messages.Count == 0 && restored.Groups.All(g => g.Preview == ""), "Delete all chats should remove every conversation.");
            Console.WriteLine("PASS  Delete chat and delete all chats");
            await TestIsolatedAccountsAsync(directory);
            Console.WriteLine("13/13 desktop test groups passed. Live networking was loopback-only, with isolated temporary application data.");
        }
        finally
        {
            string full = Path.GetFullPath(directory);
            string temp = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!full.StartsWith(temp, StringComparison.OrdinalIgnoreCase) || !Path.GetFileName(full).StartsWith("CydLan-desktop-", StringComparison.Ordinal))
                throw new InvalidOperationException("Refusing cleanup outside the desktop test directory.");
            try { Directory.Delete(full, true); } catch (IOException) { }
        }
    }

    private static Color ThemeAccent() => ((SolidColorBrush)Application.Current.Resources["Accent"]).Color;

    private static async Task TestStartupRosterAsync(string directory)
    {
        var store = new LocalStore(directory);
        var bitmap = BitmapSource.Create(112, 112, 96, 96, PixelFormats.Bgr32, null, new byte[112 * 112 * 4], 112 * 4);
        var people = Enumerable.Range(1, 72).Select(index =>
        {
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            var path = Path.Combine(directory, $"avatar-{index}.png");
            using (var stream = File.Create(path)) encoder.Save(stream);
            return new Peer { Id = $"contact-{index}", Name = $"Saved contact {index:00}", AvatarPath = path };
        }).ToArray();
        store.SavePeers(people);
        store.SaveHistory(Enumerable.Range(0, 10000).Select(index => new ChatMessage
        {
            Id = index.ToString(), PeerId = people[index % people.Length].Id, Text = $"Sample history {index}"
        }));

        var timer = System.Diagnostics.Stopwatch.StartNew();
        await using var model = new MainViewModel(localStore: store);
        long profileMilliseconds = timer.ElapsedMilliseconds;
        var window = new MainWindow(model);
        window.Show();
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        long paintMilliseconds = timer.ElapsedMilliseconds - profileMilliseconds;
        var roster = (System.Windows.Controls.ListBox)window.FindName("PeopleList");
        int realized = Enumerable.Range(0, people.Length).Count(index => roster.ItemContainerGenerator.ContainerFromIndex(index) is not null);
        Console.WriteLine($"Startup fixture: {roster.Items.Count} contacts, {realized} realized rows, profile {profileMilliseconds} ms, window {paintMilliseconds} ms.");
        Check(roster.Items.Count == people.Length && realized > 0 && realized < people.Length / 2,
            "The sidebar should retain the whole roster and only construct nearby rows on first paint.");
        roster.ScrollIntoView(model.Peers[^1]);
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        Check(roster.ItemContainerGenerator.ContainerFromItem(model.Peers[^1]) is not null,
            "Scrolling should realize saved contacts outside the first viewport.");
        model.Search = people[^1].Name;
        Check(roster.Items.Count == 1 && roster.Items[0] == model.Peers[^1], "Search must still find contacts outside the first viewport.");
        window.Close();
    }

    private static async Task TestStartupConnectionAsync(string directory, int port)
    {
        var store = new LocalStore(directory);
        var settings = new AppSettings { DisplayName = "Startup test host", Port = port };
        store.SaveSettings(settings);
        store.SaveHistory([new ChatMessage { PeerId = "saved", Text = "Existing history" }]);
        var historyPath = Path.Combine(directory, "history.json");
        File.SetLastWriteTimeUtc(historyPath, new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        DateTime historyWrite = File.GetLastWriteTimeUtc(historyPath);
        await using var model = new MainViewModel(localStore: store);
        await model.StartAsync(discovery: false, bindAddress: IPAddress.Loopback);
        var remotes = Enumerable.Range(1, 4).Select(index =>
            new LanService(new AppSettings { DisplayName = $"Startup peer {index}", Port = 0 })).ToArray();
        try
        {
            await Task.WhenAll(remotes.Select(service => service.StartAsync(discovery: false, bindAddress: IPAddress.Loopback)));
            await Task.WhenAll(remotes.Select(service => service.ConnectAsync(settings.UserId, IPAddress.Loopback.ToString(), port)));
            await Until(() => model.OnlineCount == remotes.Length && store.LoadPeers().Count == remotes.Length);
            Check(File.GetLastWriteTimeUtc(historyPath) == historyWrite && store.LoadHistory().Single().Text == "Existing history",
                "A burst of reconnects should save contact data without rewriting chat history.");
            Console.WriteLine("PASS  Startup and batched contact persistence across four local loopback connections; no chat history rewrite.");
        }
        finally
        {
            foreach (var remote in remotes) await remote.DisposeAsync();
        }
    }

    private static async Task TestAdditionalEffectsAsync(string artifacts)
    {
        await using var model = new MainViewModel(preview: true);
        var window = new MainWindow(model);
        window.Show();
        var weather = new[] { "Fireflies", "Rain", "Snow" }
            .Select(name => (WeatherEffect)window.FindName("Conversation" + name)).ToArray();
        var layer = (ChatEffects)window.FindName("EffectLayer");
        var composer = (System.Windows.Controls.TextBox)window.FindName("Composer");
        Check(weather.All(effect => !effect.IsVisible) && !model.Settings.SendPulseEnabled,
            "New effects should be off by default.");

        var preferences = new SettingsWindow(model) { Owner = window };
        preferences.Show();
        preferences.ShowPage("AppearancePage");
        preferences.ShowAppearanceTab("Ambient");
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        var typingBox = (System.Windows.Controls.ComboBox)preferences.FindName("TypingEffectBox");
        Check(typingBox.IsVisible && ((FrameworkElement)preferences.FindName("StarfieldPreview")).IsVisible &&
            !((FrameworkElement)preferences.FindName("RainIntensityBox")).IsVisible,
            "Ambient cards and interaction pickers should be visible directly without hiding previews behind checkboxes.");
        ((System.Windows.Controls.Primitives.Selector)preferences.FindName("ThemeBox")).SelectedValue = ThemeManager.Forest;
        ((System.Windows.Controls.ComboBox)preferences.FindName("TypingEffectBox")).SelectedValue = "ink";
        foreach (string name in new[] { "Fireflies", "Rain", "Snow", "SendPulse" })
            ((System.Windows.Controls.CheckBox)preferences.FindName(name + "Box")).IsChecked = true;
        foreach (var (name, value) in new[] { ("Firefly", .85), ("Rain", .5), ("Snow", .65), ("InkBloom", .75), ("SendPulse", .4) })
            ((System.Windows.Controls.Slider)preferences.FindName(name + "IntensityBox")).Value = value;
        ((System.Windows.Controls.Slider)preferences.FindName("TypingIntensityBox")).Value = .2;
        ((System.Windows.Controls.Slider)preferences.FindName("ClickIntensityBox")).Value = .9;
        ClickNamed(preferences, "Save");
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        Check(weather.All(effect => effect.IsVisible && !effect.IsHitTestVisible && effect.Tint == ThemeAccent()) &&
            weather.Select(effect => effect.Intensity).SequenceEqual(new[] { .85, .5, .65 }) &&
            model.Settings is { TypingEffect: "ink", InkBloomIntensity: .75, SendPulseEnabled: true, SendPulseIntensity: .4 },
            "Appearance should apply independent strengths and the current palette.");
        Check(model.TypingIntensity == .2 && model.ClickIntensity == .9,
            "Collapsing interaction effects should preserve separate intensity changes.");
        window.Activate();
        if (SystemParameters.ClientAreaAnimation) await Until(() => weather.All(effect => effect.Progress > .005));
        else Check(weather.All(effect => effect.Progress == 0), "Reduced motion should prevent weather animation.");
        Capture(window, Path.Combine(artifacts, "appearance-weather-tested.png"));
        ThemeManager.Apply(ThemeManager.Ocean);
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        Check(weather.All(effect => effect.IsVisible && effect.Tint == ThemeAccent()), "Weather should recolor without being disabled.");
        window.WindowState = WindowState.Maximized;
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        var dpi = VisualTreeHelper.GetDpi(weather[0]);
        double area = weather[0].ActualWidth * weather[0].ActualHeight * dpi.DpiScaleX * dpi.DpiScaleY;
        if (SystemParameters.ClientAreaAnimation && area > 800_000)
            Check(weather.All(effect => effect.ActiveFrameRate == 18), "Large chat surfaces should cap all ambient frame rates.");
        var foreground = new Window { Width = 220, Height = 140, ShowInTaskbar = false, Owner = window };
        foreground.Show();
        foreground.Activate();
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        Check(!window.IsActive && weather.All(effect => effect.ActiveFrameRate == 0 && effect.Progress == 0),
            "Ambient rendering should stop while another window has focus.");
        AmbientEffect.SetAnimateWhenUnfocused(true);
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        if (SystemParameters.ClientAreaAnimation) await Until(() => weather.All(effect => effect.Progress > .005));
        Check(!window.IsActive && (!SystemParameters.ClientAreaAnimation || weather.All(effect => effect.ActiveFrameRate > 0)),
            "Ambient rendering should keep playing while unfocused when AnimateWhenUnfocused is enabled.");
        AmbientEffect.SetAnimateWhenUnfocused(false);
        foreground.Close();
        window.Activate();
        window.WindowState = WindowState.Minimized;
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        Check(weather.All(effect => effect.Progress == 0), "Minimizing should stop weather clocks.");
        window.WindowState = WindowState.Normal;
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);

        window.Activate();
        composer.Focus();
        TypeInComposer(composer, "hello");
        TypeInComposer(composer, " ");
        int expected = SystemParameters.ClientAreaAnimation ? 1 : 0;
        Check(layer.Children.Count == expected && composer.Text == "hello " &&
            ChatEffects.Resolve("ink", ThemeManager.Ocean, typing: false) == "off", "Ink bloom should only respond to typing.");
        ClickNamed(window, "Send message");
        Check(layer.Children.Count == expected * 2 && model.Messages[^1] is { Text: "hello", Delivery: "Preview only" },
            "A successful offline preview should pulse on send with honest delivery status.");
        ClickNamed(window, "Send message");
        Check(layer.Children.Count == expected * 2, "An empty draft must not trigger send feedback.");
        await model.SavePreferencesAsync(settings => settings.SendPulseEnabled = false);
        composer.Text = "silent";
        ClickNamed(window, "Send message");
        Check(layer.Children.Count == expected * 2 && model.Messages[^1].Text == "silent",
            "Turning off send pulse should preserve message sending.");
        await model.SavePreferencesAsync(settings =>
        {
            settings.FirefliesEnabled = false;
            settings.SnowEnabled = false;
            settings.RainIntensity = 1;
        });
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        Capture(window, Path.Combine(artifacts, "fast-rain-tested.png"));
        window.Close();
        ThemeManager.Apply(ThemeManager.Light);
        Console.WriteLine("PASS  Weather palettes, independent controls, ink bloom, send pulse and reduced motion");
    }

    private static async Task TestThemeDetailsAsync(string artifacts)
    {
        await using var model = new MainViewModel(preview: true);
        var window = new MainWindow(model);
        window.Show();
        var sidebar = (ThemeDecoration)window.FindName("SidebarDecoration");
        var rail = (ThemeDecoration)window.FindName("RailDecoration");
        var title = (ThemeDecoration)window.FindName("TitleDecoration");
        var header = (ThemeDecoration)window.FindName("HeaderDecoration");
        var composer = (ThemeDecoration)window.FindName("ComposerDecoration");
        var preferences = new SettingsWindow(model) { Owner = window };
        preferences.Show();
        preferences.ShowPage("AppearancePage");
        var scroller = (System.Windows.Controls.ScrollViewer)preferences.FindName("PreferencesScroller");
        var themeBox = (System.Windows.Controls.ListBox)preferences.FindName("ThemeBox");
        var checkbox = (System.Windows.Controls.CheckBox)preferences.FindName("ThemeDetailsBox");
        Check(themeBox.IsVisible && themeBox.Items.Count == 45, "The visual theme sample boxes should be shown directly in Appearance.");
        preferences.UpdateLayout();
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        var firstCard = (FrameworkElement)themeBox.ItemContainerGenerator.ContainerFromIndex(0);
        firstCard.RaiseEvent(new System.Windows.Input.MouseWheelEventArgs(System.Windows.Input.Mouse.PrimaryDevice, 0, -240)
            { RoutedEvent = UIElement.MouseWheelEvent });
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        Check(scroller.VerticalOffset > 0, "Mouse wheel over a theme card must scroll the Appearance page.");
        scroller.ScrollToTop();
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);

        Check(!checkbox.IsVisible && !sidebar.IsVisible && !header.IsVisible && !rail.IsVisible, "Standard themes should hide the optional detail control.");
        themeBox.SelectedValue = ThemeManager.Sakura;
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        Check(checkbox.IsVisible && checkbox.IsChecked == false && !sidebar.IsVisible && !header.IsVisible, "Theme details should be opt-in.");
        checkbox.IsChecked = true;
        Capture(preferences, Path.Combine(artifacts, "appearance-groups-tested.png"));

        ((System.Windows.Controls.RadioButton)preferences.FindName("ThemeFilterDark")).IsChecked = true;
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        Check(themeBox.Items.Count == 23, $"Expected 23 dark themes, got {themeBox.Items.Count}.");
        ((System.Windows.Controls.RadioButton)preferences.FindName("ThemeFilterLight")).IsChecked = true;
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        Check(themeBox.Items.Count == 21 && (string?)themeBox.SelectedValue == ThemeManager.Sakura,
            $"Expected 21 light themes keeping Sakura selected, got {themeBox.Items.Count} / {themeBox.SelectedValue}.");
        Capture(preferences, Path.Combine(artifacts, "appearance-theme-light-filter-tested.png"));

        foreach (var (tab, file) in new[]
        {
            ("Textures", "appearance-textures-tab-tested.png"),
            ("Ambient", "appearance-ambient-tab-tested.png"),
            ("Headers", "appearance-headers-tab-tested.png"),
            ("Panels", "appearance-panels-tab-tested.png"),
            ("Aura", "appearance-aura-tab-tested.png")
        })
        {
            preferences.ShowAppearanceTab(tab);
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            Capture(preferences, Path.Combine(artifacts, file));
        }

        preferences.ShowAppearanceTab("Customize");
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        Check(((FrameworkElement)preferences.FindName("FontBox")).IsVisible && ((FrameworkElement)preferences.FindName("RemoveBackgroundButton")).IsVisible,
            "Fonts & bg tab should display font and background controls.");
        Capture(preferences, Path.Combine(artifacts, "appearance-customize-tab-tested.png"));

        preferences.ShowAppearanceTab("Themes");
        ((System.Windows.Controls.RadioButton)preferences.FindName("ThemeFilterAll")).IsChecked = true;
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        var themeBoxTail = (FrameworkElement?)themeBox.ItemContainerGenerator.ContainerFromIndex(themeBox.Items.Count - 1);
        themeBoxTail?.BringIntoView();
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        Capture(preferences, Path.Combine(artifacts, "appearance-theme-boxes-bottom-tested.png"));
        ClickNamed(preferences, "Save");
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        foreach (var (theme, design) in new[]
        {
            (ThemeManager.Sakura, DecorationDesign.Sakura), (ThemeManager.Steampunk, DecorationDesign.Steampunk),
            (ThemeManager.Forest, DecorationDesign.Forest), (ThemeManager.Paper, DecorationDesign.Paper),
            (ThemeManager.Anime, DecorationDesign.Anime), (ThemeManager.Aurora, DecorationDesign.Aurora),
            (ThemeManager.Arcade, DecorationDesign.Arcade), (ThemeManager.Pixel, DecorationDesign.Pixel),
            (ThemeManager.Ocean, DecorationDesign.Ocean), (ThemeManager.Lavender, DecorationDesign.Lavender),
            (ThemeManager.Ember, DecorationDesign.Ember), (ThemeManager.Sand, DecorationDesign.Sand),
            (ThemeManager.Cyberpunk, DecorationDesign.Cyberpunk), (ThemeManager.Midnight, DecorationDesign.Midnight),
            (ThemeManager.Sunset, DecorationDesign.Sunset), (ThemeManager.Mint, DecorationDesign.Mint),
            (ThemeManager.Matcha, DecorationDesign.Matcha), (ThemeManager.Frost, DecorationDesign.Frost),
            (ThemeManager.Obsidian, DecorationDesign.Obsidian), (ThemeManager.Nord, DecorationDesign.Nord),
            (ThemeManager.Synthwave, DecorationDesign.Synthwave), (ThemeManager.Crimson, DecorationDesign.Crimson),
            (ThemeManager.Emerald, DecorationDesign.Emerald), (ThemeManager.Rose, DecorationDesign.Rose),
            (ThemeManager.Espresso, DecorationDesign.Espresso), (ThemeManager.Cobalt, DecorationDesign.Cobalt),
            (ThemeManager.Sage, DecorationDesign.Sage), (ThemeManager.Amber, DecorationDesign.Amber),
            (ThemeManager.Pineapple, DecorationDesign.Pineapple), (ThemeManager.CoralStar, DecorationDesign.CoralStar),
            (ThemeManager.NoirPink, DecorationDesign.NoirPink), (ThemeManager.Vampire, DecorationDesign.Vampire),
            (ThemeManager.VoltSpark, DecorationDesign.VoltSpark), (ThemeManager.KingdomRed, DecorationDesign.KingdomRed),
            (ThemeManager.Bubblegum, DecorationDesign.Bubblegum), (ThemeManager.RibbonBlush, DecorationDesign.RibbonBlush),
            (ThemeManager.NoirGold, DecorationDesign.NoirGold), (ThemeManager.WebCrimson, DecorationDesign.WebCrimson),
            (ThemeManager.Jester, DecorationDesign.Jester), (ThemeManager.Cipher, DecorationDesign.Cipher),
            (ThemeManager.GundamFreedom, DecorationDesign.GundamFreedom), (ThemeManager.IronMan, DecorationDesign.IronMan)
        })
        {
            ThemeManager.Apply(theme);
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            Check(new[] { sidebar, rail, title, header, composer }.All(surface => surface.IsVisible && !surface.IsHitTestVisible && surface.Design == design),
                $"Theme surfaces should follow {theme} without intercepting chat controls.");
            if (theme is ThemeManager.Sakura or ThemeManager.Steampunk or ThemeManager.Forest or ThemeManager.Paper or
                ThemeManager.Anime or ThemeManager.Aurora or ThemeManager.Arcade or ThemeManager.Ocean or
                ThemeManager.Cyberpunk or ThemeManager.Midnight or ThemeManager.Sunset or ThemeManager.Mint or
                ThemeManager.Matcha or ThemeManager.Frost or ThemeManager.GundamFreedom or ThemeManager.IronMan)
                Capture(window, Path.Combine(artifacts, $"theme-{theme}-details-tested.png"));
        }
        await model.SavePreferencesAsync(settings => settings.ThemeDetailsEnabled = false);
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        Check(!sidebar.IsVisible && !rail.IsVisible && !title.IsVisible && !header.IsVisible && !composer.IsVisible, "Theme details should switch off independently.");
        window.Close();
        ThemeManager.Apply(ThemeManager.Light);
        Console.WriteLine("PASS  Optional theme details, live palette, and collapsed Appearance groups");
    }

    private static async Task TestHeaderAndSidebarEffectsAsync(string artifacts)
    {
        await using var model = new MainViewModel(preview: true);
        var window = new MainWindow(model);
        window.Show();
        var headerAurora = (HeaderEffect)window.FindName("ConversationHeaderEffect");
        var headerPulse = (HeaderEffect)window.FindName("ConversationHeaderPulse");
        var headerPrism = (HeaderEffect)window.FindName("ConversationHeaderPrism");
        var headerRipples = (HeaderEffect)window.FindName("ConversationHeaderRipples");
        var headerCircuit = (HeaderEffect)window.FindName("ConversationHeaderCircuit");
        var headerStarlight = (HeaderEffect)window.FindName("ConversationHeaderStarlight");
        var headerNebula = (HeaderEffect)window.FindName("ConversationHeaderNebula");
        var headerSilk = (HeaderEffect)window.FindName("ConversationHeaderSilk");
        var headerPlasma = (HeaderEffect)window.FindName("ConversationHeaderPlasma");
        var headerBurst = (HeaderEffect)window.FindName("ConversationHeaderBurst");
        var headerArc = (HeaderEffect)window.FindName("ConversationHeaderArc");
        var titleSparkles = (HeaderEffect)window.FindName("TitleBarSparkles");
        var titleShimmer = (HeaderEffect)window.FindName("TitleBarShimmer");
        var titleComet = (HeaderEffect)window.FindName("TitleBarComet");
        var titleEqualizer = (HeaderEffect)window.FindName("TitleBarEqualizer");
        var titleLaser = (HeaderEffect)window.FindName("TitleBarLaser");
        var titleAurora = (HeaderEffect)window.FindName("TitleBarAurora");
        var titleWave = (HeaderEffect)window.FindName("TitleBarWave");
        var titleWings = (HeaderEffect)window.FindName("TitleBarWings");
        var titleReactor = (HeaderEffect)window.FindName("TitleBarReactor");
        var titleEffects = new[] { titleSparkles, titleShimmer, titleComet, titleEqualizer, titleLaser, titleAurora, titleWave, titleWings, titleReactor };
        Check(titleEffects.All(effect => effect.Margin == new Thickness(0)),
            "All Title bar effects must span the entire title bar edge-to-edge without clipping to the middle.");
        var railBeacon = (RailEffect)window.FindName("RailBeaconEffect");
        var railStream = (RailEffect)window.FindName("RailStreamEffect");
        var railStardust = (RailEffect)window.FindName("RailStardustEffect");
        var railScan = (RailEffect)window.FindName("RailScanEffect");
        var railAurora = (RailEffect)window.FindName("RailAuroraEffect");
        var railPulse = (RailEffect)window.FindName("RailPulseEffect");
        var railPlasma = (RailEffect)window.FindName("RailPlasmaEffect");
        var railCascade = (RailEffect)window.FindName("RailCascadeEffect");
        var railWings = (RailEffect)window.FindName("RailWingsEffect");
        var railArc = (RailEffect)window.FindName("RailArcEffect");
        var sidebarMotes = (SidebarEffect)window.FindName("SidebarMotesEffect");
        var sidebarGlow = (SidebarEffect)window.FindName("SidebarEdgeGlowEffect");
        var sidebarPrism = (SidebarEffect)window.FindName("SidebarPrismEffect");
        var sidebarSweep = (SidebarEffect)window.FindName("SidebarSweepEffect");
        var sidebarAurora = (SidebarEffect)window.FindName("SidebarAuroraEffect");
        var sidebarNebula = (SidebarEffect)window.FindName("SidebarNebulaEffect");
        var sidebarWaves = (SidebarEffect)window.FindName("SidebarWavesEffect");
        var sidebarRipples = (SidebarEffect)window.FindName("SidebarRipplesEffect");
        var sidebarCircuit = (SidebarEffect)window.FindName("SidebarCircuitEffect");
        var sidebarWings = (SidebarEffect)window.FindName("SidebarWingsEffect");
        var sidebarHud = (SidebarEffect)window.FindName("SidebarHudEffect");
        var sidebarRain = (WeatherEffect)window.FindName("SidebarRain");
        var sidebarStarfield = (WeatherEffect)window.FindName("SidebarStarfield");
        var sidebarBubbles = (WeatherEffect)window.FindName("SidebarBubbles");
        var sidebarEmbers = (WeatherEffect)window.FindName("SidebarEmbers");
        var sidebarWingPlumes = (WeatherEffect)window.FindName("SidebarWingPlumes");
        var sidebarRepulsor = (WeatherEffect)window.FindName("SidebarRepulsor");
        var contentStarfield = (WeatherEffect)window.FindName("ConversationStarfield");
        var contentBubbles = (WeatherEffect)window.FindName("ConversationBubbles");
        var contentEmbers = (WeatherEffect)window.FindName("ConversationEmbers");
        var contentWingPlumes = (WeatherEffect)window.FindName("ConversationWingPlumes");
        var contentRepulsor = (WeatherEffect)window.FindName("ConversationRepulsor");
        var ambientEffects = new AmbientEffect[]
        {
            headerAurora, headerPulse, headerPrism, headerRipples, headerCircuit, headerStarlight, headerNebula, headerSilk, headerPlasma, headerBurst, headerArc,
            titleSparkles, titleShimmer, titleComet, titleEqualizer, titleLaser, titleAurora, titleWave, titleWings, titleReactor,
            railBeacon, railStream, railStardust, railScan, railAurora, railPulse, railPlasma, railCascade, railWings, railArc,
            sidebarMotes, sidebarGlow, sidebarPrism, sidebarSweep, sidebarAurora, sidebarNebula, sidebarWaves, sidebarRipples, sidebarCircuit, sidebarWings, sidebarHud,
            sidebarRain, sidebarStarfield, sidebarBubbles, sidebarEmbers, sidebarWingPlumes, sidebarRepulsor,
            contentStarfield, contentBubbles, contentEmbers, contentWingPlumes, contentRepulsor
        };
        Check(ambientEffects.All(effect => !effect.IsVisible) &&
            !ButtonEffects.GlowEnabled && !ButtonEffects.SheenEnabled && !ButtonEffects.BounceEnabled && !ButtonEffects.RingEnabled &&
            !ButtonEffects.PulseEnabled && !ButtonEffects.LiftEnabled && !ButtonEffects.SparkEnabled,
            "All header, title, rail, side panel, ambient, and highlight button effects must be opt-in.");

        var preferences = new SettingsWindow(model) { Owner = window };
        preferences.Show();
        preferences.ShowPage("AppearancePage");
        ((System.Windows.Controls.ListBox)preferences.FindName("ThemeBox")).SelectedValue = ThemeManager.Aurora;
        ((System.Windows.Controls.CheckBox)preferences.FindName("ThemeDetailsBox")).IsChecked = true;

        var defaultPreviews = new (string Tab, string[] Previews)[]
        {
            ("Headers", ["EnginePreview", "TitleSparklesPreview", "TitleShimmerPreview", "TitleCometPreview", "TitleEqualizerPreview", "TitleLaserPreview",
                "TitleAuroraPreview", "TitleWavePreview", "TitleWingsPreview", "TitleReactorPreview",
                "HeaderAuroraPreview", "HeaderPulsePreview", "HeaderPrismPreview", "HeaderRipplesPreview", "HeaderCircuitPreview", "HeaderStarlightPreview",
                "HeaderNebulaPreview", "HeaderSilkPreview", "HeaderPlasmaPreview", "HeaderBurstPreview", "HeaderArcPreview"]),
            ("Panels", ["RailBeaconPreview", "RailStreamPreview", "RailStardustPreview", "RailScanPreview",
                "RailAuroraPreview", "RailPulsePreview", "RailPlasmaPreview", "RailCascadePreview", "RailWingsPreview", "RailArcPreview",
                "SidebarMotesPreview", "SidebarGlowPreview", "SidebarPrismPreview", "SidebarSweepPreview",
                "SidebarAuroraPreview", "SidebarNebulaPreview", "SidebarWavesPreview", "SidebarRipplesPreview", "SidebarCircuitPreview", "SidebarWingsPreview", "SidebarHudPreview"]),
            ("Ambient", ["PetalsPreview", "StarfieldPreview", "BubblesPreview", "EmbersPreview", "WingPlumesPreview", "RepulsorPreview"]),
            ("Aura", ["ButtonPreviewPrimary", "ButtonPreviewNav", "ButtonPreviewSend"])
        };
        foreach (var (tab, previews) in defaultPreviews)
        {
            preferences.ShowAppearanceTab(tab);
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            Check(previews.All(name => ((FrameworkElement)preferences.FindName(name)).IsVisible),
                $"All effect previews in the {tab} tab must be visible by default without checking a box.");
        }
        preferences.ShowAppearanceTab("Headers");
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        Check(FindText(preferences, "Title bar") is { IsVisible: true } && FindText(preferences, "Chat header") is { IsVisible: true },
            "Title bar and Chat header effects should be cleanly separated in the Headers tab.");

        foreach (string name in new[]
        {
            "HeaderAurora", "HeaderPulse", "HeaderPrism", "HeaderRipples", "HeaderCircuit", "HeaderStarlight", "HeaderNebula", "HeaderSilk", "HeaderPlasma", "HeaderBurst", "HeaderArc",
            "TitleSparkles", "TitleShimmer", "TitleComet", "TitleEqualizer", "TitleLaser", "TitleAurora", "TitleWave", "TitleWings", "TitleReactor",
            "RailBeacon", "RailStream", "RailStardust", "RailScan", "RailAurora", "RailPulse", "RailPlasma", "RailCascade", "RailWings", "RailArc",
            "SidebarMotes", "SidebarGlow", "SidebarPrism", "SidebarSweep", "SidebarAurora", "SidebarNebula", "SidebarWaves", "SidebarRipples", "SidebarCircuit", "SidebarWings", "SidebarHud",
            "Rain", "Starfield", "Bubbles", "Embers", "WingPlumes", "Repulsor", "AmbientOnSidebar",
            "ButtonGlow", "ButtonSheen", "ButtonBounce", "ButtonRing", "ButtonPulse", "ButtonLift", "ButtonSpark"
        })
            ((System.Windows.Controls.CheckBox)preferences.FindName(name + "Box")).IsChecked = true;
        foreach (var (name, value) in new[]
        {
            ("HeaderAurora", .8), ("HeaderPulse", .7), ("HeaderPrism", .75), ("HeaderRipples", .7), ("HeaderCircuit", .8), ("HeaderStarlight", .65),
            ("HeaderNebula", .85), ("HeaderSilk", .75), ("HeaderPlasma", .8), ("HeaderBurst", .85), ("HeaderArc", .8),
            ("TitleSparkles", .55), ("TitleShimmer", .7), ("TitleComet", .75), ("TitleEqualizer", .8), ("TitleLaser", .85), ("TitleAurora", .8), ("TitleWave", .75),
            ("TitleWings", .8), ("TitleReactor", .85),
            ("RailBeacon", .75), ("RailStream", .7), ("RailStardust", .65), ("RailScan", .6),
            ("RailAurora", .8), ("RailPulse", .75), ("RailPlasma", .7), ("RailCascade", .8), ("RailWings", .75), ("RailArc", .85),
            ("SidebarMotes", .75), ("SidebarGlow", .65), ("SidebarPrism", .7), ("SidebarSweep", .65),
            ("SidebarAurora", .8), ("SidebarNebula", .75), ("SidebarWaves", .7), ("SidebarRipples", .75), ("SidebarCircuit", .8), ("SidebarWings", .8), ("SidebarHud", .85),
            ("Starfield", .8), ("Bubbles", .7), ("Embers", .75), ("WingPlumes", .8), ("Repulsor", .85),
            ("ButtonGlow", .85), ("ButtonSheen", .8), ("ButtonBounce", .7), ("ButtonRing", .85),
            ("ButtonPulse", .75), ("ButtonLift", .7), ("ButtonSpark", .85)
        })
            ((System.Windows.Controls.Slider)preferences.FindName(name + "IntensityBox")).Value = value;
        preferences.ShowAppearanceTab("Panels");
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        Capture(preferences, Path.Combine(artifacts, "appearance-header-sidebar-prefs-tested.png"));
        preferences.ShowAppearanceTab("Aura");
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        ((FrameworkElement)preferences.FindName("ButtonPreviewPrimary")).BringIntoView();
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        Capture(preferences, Path.Combine(artifacts, "appearance-rail-buttons-prefs-tested.png"));
        ClickNamed(preferences, "Save");
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);

        Check(ambientEffects.All(effect => effect.IsVisible && !effect.IsHitTestVisible && effect.Tint == ThemeAccent()) &&
            headerAurora.Intensity == .8 && headerPulse.Intensity == .7 && headerPrism.Intensity == .75 && headerRipples.Intensity == .7 &&
            headerCircuit.Intensity == .8 && headerStarlight.Intensity == .65 &&
            headerNebula.Intensity == .85 && headerSilk.Intensity == .75 && headerPlasma.Intensity == .8 && headerBurst.Intensity == .85 && headerArc.Intensity == .8 &&
            titleSparkles.Intensity == .55 && titleShimmer.Intensity == .7 && titleComet.Intensity == .75 &&
            titleEqualizer.Intensity == .8 && titleLaser.Intensity == .85 && titleAurora.Intensity == .8 && titleWave.Intensity == .75 &&
            titleWings.Intensity == .8 && titleReactor.Intensity == .85 &&
            railBeacon.Intensity == .75 && railStream.Intensity == .7 && railStardust.Intensity == .65 && railScan.Intensity == .6 &&
            railAurora.Intensity == .8 && railPulse.Intensity == .75 && railPlasma.Intensity == .7 && railCascade.Intensity == .8 &&
            railWings.Intensity == .75 && railArc.Intensity == .85 &&
            sidebarMotes.Intensity == .75 && sidebarGlow.Intensity == .65 && sidebarPrism.Intensity == .7 && sidebarSweep.Intensity == .65 &&
            sidebarAurora.Intensity == .8 && sidebarNebula.Intensity == .75 && sidebarWaves.Intensity == .7 && sidebarRipples.Intensity == .75 && sidebarCircuit.Intensity == .8 &&
            sidebarWings.Intensity == .8 && sidebarHud.Intensity == .85 &&
            contentStarfield.Intensity == .8 && contentBubbles.Intensity == .7 && contentEmbers.Intensity == .75 &&
            contentWingPlumes.Intensity == .8 && contentRepulsor.Intensity == .85 &&
            ButtonEffects.GlowEnabled && ButtonEffects.SheenEnabled && ButtonEffects.BounceEnabled && ButtonEffects.RingEnabled &&
            ButtonEffects.PulseEnabled && ButtonEffects.LiftEnabled && ButtonEffects.SparkEnabled,
            "All UI zone effects should activate with independent intensities and the active theme colour.");
        window.Activate();
        if (SystemParameters.ClientAreaAnimation) await Until(() => ambientEffects.All(effect => effect.Progress > .005));
        else Check(ambientEffects.All(effect => effect.Progress == 0), "Reduced motion should stop header, rail, sidebar, and ambient animations.");
        Capture(window, Path.Combine(artifacts, "header-sidebar-aurora-tested.png"));
        ThemeManager.Apply(ThemeManager.Cyberpunk);
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        Check(ambientEffects.All(effect => effect.Tint == ThemeAccent()), "All zone effects should recolor live on theme switches.");
        Capture(window, Path.Combine(artifacts, "effects-all-cyberpunk-tested.png"));
        ThemeManager.Apply(ThemeManager.Anime);
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        Check(ambientEffects.All(effect => effect.Tint == ThemeAccent()), "Effects should recolor cleanly for light specialty themes.");
        Capture(window, Path.Combine(artifacts, "header-sidebar-anime-tested.png"));
        await model.SavePreferencesAsync(s =>
        {
            AppearanceCodec.ResetDefaults(s);
            s.Theme = ThemeManager.Vampire;
            s.TitleAuroraEnabled = true;
            s.TitleAuroraIntensity = .85;
            s.HeaderPulseEnabled = true;
            s.HeaderPulseIntensity = .85;
        });
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        if (SystemParameters.ClientAreaAnimation) await Until(() => titleAurora.Progress > .005 && headerPulse.Progress > .005);
        Capture(window, Path.Combine(artifacts, "header-pulse-title-aurora-tested.png"));
        await model.SavePreferencesAsync(s =>
        {
            s.TitleAuroraEnabled = false;
            s.TitleLaserEnabled = true;
            s.TitleLaserIntensity = .85;
            s.HeaderPulseEnabled = false;
            s.HeaderNebulaEnabled = true;
            s.HeaderNebulaIntensity = .85;
            s.RailAuroraEnabled = true;
            s.RailAuroraIntensity = .85;
            s.SidebarAuroraEnabled = true;
            s.SidebarAuroraIntensity = .85;
            s.ChromeTexture = ChromeTexture.Gothic;
            s.ChromeTextureIntensity = .80;
            s.ChatSurfaceMode = AppearanceOptions.ChatSurfaceTheme;
            s.ChatCanvasGradient = ChatCanvasOverlay.Aurora;
            s.ChatCanvasGradientIntensity = .75;
            s.StarfieldEnabled = true;
            s.StarfieldIntensity = .75;
            s.SeamlessComposerEnabled = true;
        });
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        if (SystemParameters.ClientAreaAnimation) await Until(() => titleLaser.Progress > .005 && headerNebula.Progress > .005 && railAurora.Progress > .005 && sidebarAurora.Progress > .005);
        Check(((System.Windows.Controls.Border)window.FindName("ChatSurfaceLayer")).Background == Brushes.Transparent,
            "Match theme chat surface mode should let the themed chrome and texture flow cleanly through the chat area.");
        Capture(window, Path.Combine(artifacts, "header-nebula-title-laser-tested.png"));
        model.Messages.Clear();
        model.Messages.Add(new ChatMessage { PeerId = model.SelectedPeer!.Id, Outgoing = false, Text = "METEOR system online. All wing binders green." });
        model.Messages.Add(new ChatMessage { PeerId = model.SelectedPeer!.Id, Outgoing = true, Text = "HiMAT Full Burst ready — locking targets.", Delivery = "Delivered" });
        model.Messages.Add(new ChatMessage { PeerId = model.SelectedPeer!.Id, Outgoing = false, FileName = "WingSchematics.pdf", Delivery = "Received" });
        model.Messages.Add(new ChatMessage { PeerId = model.SelectedPeer!.Id, Outgoing = true, FileName = "MarkLXXXVSpecs.pdf", Delivery = "Sent" });
        model.Messages.Add(new ChatMessage { PeerId = model.SelectedPeer!.Id, Outgoing = false, Text = "Repulsor diagnostics nominal at 100%." });
        await model.SavePreferencesAsync(s =>
        {
            AppearanceCodec.ResetDefaults(s);
            s.Theme = ThemeManager.GundamFreedom;
            s.ThemeDetailsEnabled = true;
            s.ChromeTexture = ChromeTexture.FreedomWings;
            s.ChromeTextureIntensity = .85;
            s.ChatSurfaceMode = AppearanceOptions.ChatSurfaceTheme;
            s.ChatCanvasGradient = ChatCanvasOverlay.FullBurst;
            s.ChatCanvasGradientIntensity = .80;
            s.WingPlumesEnabled = true;
            s.WingPlumesIntensity = .85;
            s.AmbientOnSidebarEnabled = true;
            s.TitleWingsEnabled = true;
            s.TitleWingsIntensity = .85;
            s.HeaderBurstEnabled = true;
            s.HeaderBurstIntensity = .85;
            s.RailWingsEnabled = true;
            s.RailWingsIntensity = .85;
            s.SidebarWingsEnabled = true;
            s.SidebarWingsIntensity = .85;
            s.TargetAura = TargetAura.WingFlare;
            s.TargetAuraIntensity = .85;
            s.TypingEffect = "theme";
        });
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        if (SystemParameters.ClientAreaAnimation) await Until(() => titleWings.Progress > .005 && headerBurst.Progress > .005 && railWings.Progress > .005 && sidebarWings.Progress > .005 && contentWingPlumes.Progress > .005);
        Check((DecorationDesign)Application.Current.Resources["BubbleDecorationDesign"] == DecorationDesign.GundamFreedom &&
            Application.Current.Resources["OutgoingBubbleBackground"] is LinearGradientBrush &&
            ChatEffects.Resolve("theme", ThemeManager.GundamFreedom, true) == "feathers",
            "Gundam Freedom should activate mecha gradient bubbles, bubble corner markings, and wing plume typing effect.");
        Capture(window, Path.Combine(artifacts, "gundamfreedom-showcase-tested.png"));

        await model.SavePreferencesAsync(s =>
        {
            AppearanceCodec.ResetDefaults(s);
            s.Theme = ThemeManager.IronMan;
            s.ThemeDetailsEnabled = true;
            s.ChromeTexture = ChromeTexture.ArcReactor;
            s.ChromeTextureIntensity = .88;
            s.ChatSurfaceMode = AppearanceOptions.ChatSurfaceTheme;
            s.ChatCanvasGradient = ChatCanvasOverlay.ArcCore;
            s.ChatCanvasGradientIntensity = .85;
            s.RepulsorEnabled = true;
            s.RepulsorIntensity = .85;
            s.AmbientOnSidebarEnabled = true;
            s.TitleReactorEnabled = true;
            s.TitleReactorIntensity = .85;
            s.HeaderArcEnabled = true;
            s.HeaderArcIntensity = .85;
            s.RailArcEnabled = true;
            s.RailArcIntensity = .85;
            s.SidebarHudEnabled = true;
            s.SidebarHudIntensity = .85;
            s.TargetAura = TargetAura.ArcReactor;
            s.TargetAuraIntensity = .85;
            s.OnlineRingEnabled = true;
            s.ClickEffect = "theme";
        });
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        if (SystemParameters.ClientAreaAnimation) await Until(() => titleReactor.Progress > .005 && headerArc.Progress > .005 && railArc.Progress > .005 && sidebarHud.Progress > .005 && contentRepulsor.Progress > .005);
        Check((DecorationDesign)Application.Current.Resources["BubbleDecorationDesign"] == DecorationDesign.IronMan &&
            Application.Current.Resources["OutgoingBubbleBackground"] is LinearGradientBrush &&
            ChatEffects.Resolve("theme", ThemeManager.IronMan, false) == "repulsor",
            "Iron Man should activate reactor gradient bubbles, arc emblems, and repulsor click effect.");
        Capture(window, Path.Combine(artifacts, "ironman-showcase-tested.png"));

        window.WindowState = WindowState.Minimized;
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        Check(ambientEffects.All(effect => effect.ActiveFrameRate == 0 && effect.Progress == 0),
            "Minimizing should stop all ambient effect clocks.");
        window.Close();
        ThemeManager.Apply(ThemeManager.Light);
        Console.WriteLine("PASS  Header, rail, side panel, content & highlight button effects and live previews");
    }

    private static async Task TestChromeTexturesAsync(string artifacts)
    {
        Check(ChromeTexture.Catalog.Count == 32, $"Expected 31 background textures + None, found {ChromeTexture.Catalog.Count}.");
        Check(ChromeTexture.Normalize("starry night") == ChromeTexture.StarryNight
            && ChromeTexture.Normalize("lonely grass") == ChromeTexture.LonelyGrass
            && ChromeTexture.Normalize("grassy fields") == ChromeTexture.GrassyFields
            && ChromeTexture.Normalize("GALAXY") == ChromeTexture.Galaxy,
            "Scenic texture aliases must normalize cleanly.");
        await using var model = new MainViewModel(preview: true);
        var window = new MainWindow(model);
        window.Show();
        var textureLayer = (ChromeTexture)window.FindName("ChromeTextureLayer");
        Check(!textureLayer.IsVisible && !textureLayer.IsHitTestVisible, "Uniform chrome textures must be opt-in and non-interactive.");

        var preferences = new SettingsWindow(model) { Owner = window };
        preferences.Show();
        preferences.ShowPage("AppearancePage");
        preferences.ShowAppearanceTab("Textures");
        var textureBox = (System.Windows.Controls.ListBox)preferences.FindName("ChromeTextureBox");
        var intensityBox = (System.Windows.Controls.Slider)preferences.FindName("ChromeTextureIntensityBox");
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        Check(textureBox.IsVisible && textureBox.Items.Count == 32 && (string)textureBox.SelectedValue == ChromeTexture.Off,
            "All 31 textures + None should appear directly on the Textures tab.");

        ((System.Windows.Controls.ListBox)preferences.FindName("ThemeBox")).SelectedValue = ThemeManager.Cyberpunk;
        textureBox.SelectedValue = ChromeTexture.Silk;
        intensityBox.Value = .85;
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        Check(textureLayer.IsVisible && textureLayer.Kind == ChromeTextureKind.Silk && textureLayer.Intensity == .85,
            "Selecting a chrome texture should live-preview across the unified shell.");
        Capture(preferences, Path.Combine(artifacts, "appearance-texture-prefs-tested.png"));

        textureBox.SelectedValue = ChromeTexture.Galaxy;
        var prefScroller = (System.Windows.Controls.ScrollViewer)preferences.FindName("PreferencesScroller");
        prefScroller.ScrollToVerticalOffset(190);
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        Capture(preferences, Path.Combine(artifacts, "appearance-texture-prefs-scenic-tested.png"));

        preferences.Close();
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        Check(!textureLayer.IsVisible && model.ChromeTexture == ChromeTexture.Off, "Cancel must revert live chrome texture previews.");

        var confirmPrefs = new SettingsWindow(model) { Owner = window };
        confirmPrefs.Show();
        confirmPrefs.ShowPage("AppearancePage");
        confirmPrefs.ShowAppearanceTab("Textures");
        ((System.Windows.Controls.ListBox)confirmPrefs.FindName("ChromeTextureBox")).SelectedValue = ChromeTexture.Hex;
        ((System.Windows.Controls.Slider)confirmPrefs.FindName("ChromeTextureIntensityBox")).Value = .75;
        ClickNamed(confirmPrefs, "Save");
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        Check(model.Settings.ChromeTexture == ChromeTexture.Hex && model.Settings.ChromeTextureIntensity == .75 && textureLayer.IsVisible,
            "Saved chrome texture should stay active after Preferences closes.");

        foreach (var (theme, texture, expectedKind, onChat) in new[]
        {
            (ThemeManager.Cyberpunk, ChromeTexture.Hex, ChromeTextureKind.Hex, false),
            (ThemeManager.Midnight, ChromeTexture.Silk, ChromeTextureKind.Silk, false),
            (ThemeManager.Aurora, ChromeTexture.Topo, ChromeTextureKind.Topo, false),
            (ThemeManager.Frost, ChromeTexture.Grid, ChromeTextureKind.Grid, false),
            (ThemeManager.Matcha, ChromeTexture.Woven, ChromeTextureKind.Woven, false),
            (ThemeManager.Dark, ChromeTexture.Grain, ChromeTextureKind.Grain, false),
            (ThemeManager.Obsidian, ChromeTexture.Brushed, ChromeTextureKind.Brushed, false),
            (ThemeManager.Rose, ChromeTexture.Marble, ChromeTextureKind.Marble, false),
            (ThemeManager.Synthwave, ChromeTexture.Carbon, ChromeTextureKind.Carbon, false),
            (ThemeManager.Emerald, ChromeTexture.Circuit, ChromeTextureKind.Circuit, false),
            (ThemeManager.Nord, ChromeTexture.Waves, ChromeTextureKind.Waves, false),
            (ThemeManager.Amber, ChromeTexture.ArtDeco, ChromeTextureKind.ArtDeco, false),
            (ThemeManager.Espresso, ChromeTexture.Terrazzo, ChromeTextureKind.Terrazzo, false),
            (ThemeManager.Cobalt, ChromeTexture.Mesh, ChromeTextureKind.Mesh, false),
            (ThemeManager.Pineapple, ChromeTexture.Bubbles, ChromeTextureKind.Bubbles, false),
            (ThemeManager.Vampire, ChromeTexture.Gothic, ChromeTextureKind.Gothic, false),
            (ThemeManager.VoltSpark, ChromeTexture.Lightning, ChromeTextureKind.Lightning, false),
            (ThemeManager.WebCrimson, ChromeTexture.Web, ChromeTextureKind.Web, false),
            (ThemeManager.NoirPink, ChromeTexture.Starburst, ChromeTextureKind.Starburst, false),
            (ThemeManager.Sage, ChromeTexture.Botanical, ChromeTextureKind.Botanical, true),
            (ThemeManager.Vampire, ChromeTexture.Botanical, ChromeTextureKind.Botanical, true),
            (ThemeManager.Midnight, ChromeTexture.Galaxy, ChromeTextureKind.Galaxy, true),
            (ThemeManager.Paper, ChromeTexture.Galaxy, ChromeTextureKind.Galaxy, true),
            (ThemeManager.Nord, ChromeTexture.StarryNight, ChromeTextureKind.StarryNight, true),
            (ThemeManager.Sand, ChromeTexture.Sun, ChromeTextureKind.Sun, true),
            (ThemeManager.Dark, ChromeTexture.Moon, ChromeTextureKind.Moon, true),
            (ThemeManager.Frost, ChromeTexture.Moon, ChromeTextureKind.Moon, true),
            (ThemeManager.Paper, ChromeTexture.LonelyGrass, ChromeTextureKind.LonelyGrass, true),
            (ThemeManager.Matcha, ChromeTexture.Leaves, ChromeTextureKind.Leaves, true),
            (ThemeManager.Matcha, ChromeTexture.GrassyFields, ChromeTextureKind.GrassyFields, true),
            (ThemeManager.Midnight, ChromeTexture.GrassyFields, ChromeTextureKind.GrassyFields, true),
            (ThemeManager.Cipher, ChromeTexture.Cipher, ChromeTextureKind.Cipher, false),
            (ThemeManager.RibbonBlush, ChromeTexture.Watercolor, ChromeTextureKind.Watercolor, false),
            (ThemeManager.GundamFreedom, ChromeTexture.FreedomWings, ChromeTextureKind.FreedomWings, false),
            (ThemeManager.IronMan, ChromeTexture.ArcReactor, ChromeTextureKind.ArcReactor, false)
        })
        {
            await model.SavePreferencesAsync(s =>
            {
                s.Theme = theme;
                s.ChromeTexture = texture;
                s.ChromeTextureIntensity = .78;
                s.ChatSurfaceMode = onChat ? AppearanceOptions.ChatSurfaceTheme : AppearanceOptions.ChatSurfaceSeparate;
            });
            ThemeManager.Apply(theme, AppearanceOverrides.From(model.Settings));
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            Check(textureLayer.IsVisible && textureLayer.Kind == expectedKind, $"Chrome texture {texture} should render cleanly in {theme}.");
            Capture(window, Path.Combine(artifacts, $"chrome-texture-{theme}-{texture}-tested.png"));
        }

        string encoded = AppearanceCodec.Encode(model.Settings);
        Check(AppearanceCodec.TryDecode(encoded, out var decoded) && decoded.ChromeTexture == ChromeTexture.ArcReactor,
            "Look code should round-trip active chrome textures.");

        window.Close();
        ThemeManager.Apply(ThemeManager.Light, AppearanceOverrides.Default);
        Console.WriteLine("PASS  Uniform chrome shell with all 31 theme-adaptive background textures");
    }

    private static async Task TestTargetAuraAsync(string artifacts)
    {
        Check(TargetAura.Catalog.Count == 14, $"Expected 13 target aura styles + None, found {TargetAura.Catalog.Count}.");
        await using var model = new MainViewModel(preview: true);
        model.Draft = "Looking great!";
        var window = new MainWindow(model);
        window.Show();
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);

        var chatNav = (System.Windows.Controls.Button)window.FindName("ChatNav");
        var sendBtn = (System.Windows.Controls.Button)window.FindName("SendButton");
        var peopleList = (System.Windows.Controls.ListBox)window.FindName("PeopleList");
        var selectedRow = (System.Windows.Controls.ListBoxItem)peopleList.ItemContainerGenerator.ContainerFromIndex(peopleList.SelectedIndex);
        var moduleAura = (TargetAura)chatNav.Template.FindName("ButtonAura", chatNav);
        var sendAura = (TargetAura)sendBtn.Template.FindName("ButtonAura", sendBtn);
        var rowAura = (TargetAura)selectedRow.Template.FindName("RowAura", selectedRow);

        Check(!moduleAura.IsVisible && !sendAura.IsVisible && !rowAura.IsVisible && TargetAura.ActiveKind == TargetAuraKind.Off,
            "Target fire/smoke/aura effects must be opt-in by default.");

        var preferences = new SettingsWindow(model) { Owner = window };
        preferences.Show();
        preferences.ShowPage("AppearancePage");
        preferences.ShowAppearanceTab("Aura");
        var auraBox = (System.Windows.Controls.ListBox)preferences.FindName("TargetAuraBox");
        var moduleBox = (System.Windows.Controls.CheckBox)preferences.FindName("AuraModuleBox");
        var personBox = (System.Windows.Controls.CheckBox)preferences.FindName("AuraPersonBox");
        var buttonsBox = (System.Windows.Controls.CheckBox)preferences.FindName("AuraButtonsBox");
        var intensityBox = (System.Windows.Controls.Slider)preferences.FindName("TargetAuraIntensityBox");
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        Check(auraBox.IsVisible && auraBox.Items.Count == 14 && (string)auraBox.SelectedValue == TargetAura.Off,
            "All 13 aura styles + None should appear directly on the Aura tab.");

        ((System.Windows.Controls.ListBox)preferences.FindName("ThemeBox")).SelectedValue = ThemeManager.Cyberpunk;
        ((System.Windows.Controls.ListBox)preferences.FindName("ChromeTextureBox")).SelectedValue = ChromeTexture.Hex;
        auraBox.SelectedValue = TargetAura.Fire;
        intensityBox.Value = .85;
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);

        Check(moduleAura.IsVisible && sendAura.IsVisible && rowAura.IsVisible &&
            moduleAura.Kind == TargetAuraKind.Fire && rowAura.Kind == TargetAuraKind.Fire && sendAura.Kind == TargetAuraKind.Fire,
            "Selecting Fire should light up selected module, selected person row, and enabled highlight buttons live.");
        Check(moduleBox.IsChecked == true && personBox.IsChecked == true && buttonsBox.IsChecked == true,
            "All three target toggles should default on once an aura style is chosen.");

        moduleBox.IsChecked = false;
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        Check(!moduleAura.IsVisible && rowAura.IsVisible && sendAura.IsVisible, "Target checkboxes should isolate module, person, and button auras.");
        moduleBox.IsChecked = true;
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        Capture(preferences, Path.Combine(artifacts, "appearance-aura-prefs-tested.png"));

        preferences.Close();
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        Check(!moduleAura.IsVisible && !sendAura.IsVisible && !rowAura.IsVisible && TargetAura.ActiveKind == TargetAuraKind.Off,
            "Cancel must restore opt-in aura state on all targets.");

        var savePrefs = new SettingsWindow(model) { Owner = window };
        savePrefs.Show();
        savePrefs.ShowPage("AppearancePage");
        savePrefs.ShowAppearanceTab("Aura");
        ((System.Windows.Controls.ListBox)savePrefs.FindName("TargetAuraBox")).SelectedValue = TargetAura.Fire;
        ((System.Windows.Controls.Slider)savePrefs.FindName("TargetAuraIntensityBox")).Value = .85;
        ClickNamed(savePrefs, "Save");
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        Check(model.Settings.TargetAura == TargetAura.Fire && moduleAura.IsVisible && sendAura.IsVisible && rowAura.IsVisible,
            "Saving TargetAura should persist across module, person, and highlight buttons.");

        window.Activate();
        foreach (var (theme, texture, style, kind) in new[]
        {
            (ThemeManager.Cyberpunk, ChromeTexture.Hex, TargetAura.Fire, TargetAuraKind.Fire),
            (ThemeManager.Midnight, ChromeTexture.Silk, TargetAura.Aura, TargetAuraKind.Aura),
            (ThemeManager.Matcha, ChromeTexture.Woven, TargetAura.Smoke, TargetAuraKind.Smoke),
            (ThemeManager.Frost, ChromeTexture.Grid, TargetAura.Arcane, TargetAuraKind.Arcane),
            (ThemeManager.Nord, ChromeTexture.Waves, TargetAura.Frost, TargetAuraKind.Frost),
            (ThemeManager.Synthwave, ChromeTexture.Carbon, TargetAura.Electric, TargetAuraKind.Electric),
            (ThemeManager.Crimson, ChromeTexture.ArtDeco, TargetAura.Neon, TargetAuraKind.Neon),
            (ThemeManager.Rose, ChromeTexture.Marble, TargetAura.Sakura, TargetAuraKind.Sakura),
            (ThemeManager.Obsidian, ChromeTexture.Mesh, TargetAura.Prism, TargetAuraKind.Prism),
            (ThemeManager.Emerald, ChromeTexture.Circuit, TargetAura.Pulse, TargetAuraKind.Pulse),
            (ThemeManager.GundamFreedom, ChromeTexture.FreedomWings, TargetAura.WingFlare, TargetAuraKind.WingFlare),
            (ThemeManager.IronMan, ChromeTexture.ArcReactor, TargetAura.ArcReactor, TargetAuraKind.ArcReactor)
        })
        {
            await model.SavePreferencesAsync(s => { s.Theme = theme; s.ChromeTexture = texture; s.TargetAura = style; s.TargetAuraIntensity = .85; });
            ThemeManager.Apply(theme);
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            if (SystemParameters.ClientAreaAnimation) await Until(() => moduleAura.Progress > .005 && rowAura.Progress > .005);
            Check(moduleAura.Kind == kind && rowAura.Kind == kind && sendAura.Kind == kind,
                $"Aura {style} should update live across all three targets in {theme}.");
            Capture(window, Path.Combine(artifacts, $"target-aura-{theme}-{style}-tested.png"));
        }

        window.WindowState = WindowState.Minimized;
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        Check(moduleAura.ActiveFrameRate == 0 && rowAura.ActiveFrameRate == 0 && sendAura.ActiveFrameRate == 0,
            "Minimizing must pause all target aura clocks.");
        window.Close();
        ThemeManager.Apply(ThemeManager.Light);
        Console.WriteLine("PASS  Target fire, smoke, embers & aura on selected module, person row, and highlight buttons");
    }

    private static async Task TestCustomizationSuiteAsync(string artifacts)
    {
        await using var model = new MainViewModel(preview: true);
        ThemeManager.Apply(model.Settings.Theme, AppearanceOverrides.From(model.Settings));
        var window = new MainWindow(model);
        window.Show();
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);

        var canvasLayer = (ChatCanvasOverlay)window.FindName("ChatCanvasGradientLayer");
        var matrixLayer = (WeatherEffect)window.FindName("ConversationMatrix");
        var constellationLayer = (WeatherEffect)window.FindName("ConversationConstellation");
        var bokehLayer = (WeatherEffect)window.FindName("ConversationBokeh");

        // Bundled defaults: zero overrides active.
        Check(ThemeManager.ActiveOverrides == AppearanceOverrides.Default, "Default theme must start with bundled AppearanceOverrides.Default.");
        Check(!canvasLayer.IsVisible && !matrixLayer.IsVisible && !constellationLayer.IsVisible && !bokehLayer.IsVisible,
            "Canvas gradients and new ambient effects must be off by default.");
        Check(AmbientEffect.SpeedMultiplier == 1.0 && MessageMotion.ActiveStyle == MessageMotion.Off &&
            model.UseCircularHeaderAvatar && !model.UseCustomAvatarShape && !model.OnlineRingEnabled && model.ChatBackgroundBlur == 0,
            "Geometry, motion, speed, and blur overrides must default to bundled behavior.");

        var preferences = new SettingsWindow(model) { Owner = window };
        preferences.Show();
        preferences.ShowPage("AppearancePage");

        // 1. Colors tab: color finishes (12 styles + Original) + intensity slider + custom channels.
        preferences.ShowAppearanceTab("Colors");
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        var colorFinishBox = (System.Windows.Controls.ListBox)preferences.FindName("ColorFinishBox");
        var colorFinishIntensity = (System.Windows.Controls.Slider)preferences.FindName("ColorFinishIntensityBox");
        var chromeFinishLayer = (System.Windows.Controls.Border)window.FindName("ChromeColorFinishLayer");
        var chatFinishLayer = (System.Windows.Controls.Border)window.FindName("ChatColorFinishLayer");
        var themeSelector = (System.Windows.Controls.ListBox)preferences.FindName("ThemeBox");
        Check(ColorFinish.Catalog.Count == 13 && colorFinishBox.Items.Count == 13,
            $"Expected 13 color finish options (Original + 12 finishes), found {ColorFinish.Catalog.Count}/{colorFinishBox.Items.Count}.");
        Check(colorFinishBox.SelectedValue as string == ColorFinish.Off && !colorFinishIntensity.IsVisible &&
            chromeFinishLayer.Background == Brushes.Transparent && chatFinishLayer.Background == Brushes.Transparent,
            "Color finish must default to Original with no wash overlay and collapsed slider.");

        // Switch to Pineapple Sea (SpongeBob) and verify smoothing finishes soften harsh solid yellow while keeping yellow/blue spirit.
        themeSelector.SelectedValue = ThemeManager.Pineapple;
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        Capture(window, Path.Combine(artifacts, "pineapple-original-tested.png"));
        var rawPineSidebar = ((SolidColorBrush)Application.Current.Resources["Sidebar"]).Color;
        var rawPineAccent = ((SolidColorBrush)Application.Current.Resources["Accent"]).Color;
        var (_, rawPineSat, _) = ColorFinish.ToHsl(rawPineSidebar);

        foreach (var finish in ColorFinish.Catalog.Where(f => f.Value != ColorFinish.Off))
        {
            colorFinishBox.SelectedValue = finish.Value;
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            var gradedSidebar = ((SolidColorBrush)Application.Current.Resources["Sidebar"]).Color;
            var gradedInk = ((SolidColorBrush)Application.Current.Resources["Ink"]).Color;
            double contrast = (Math.Max(AppearanceOptions.RelativeLuminance(gradedSidebar), AppearanceOptions.RelativeLuminance(gradedInk)) + .05) /
                              (Math.Min(AppearanceOptions.RelativeLuminance(gradedSidebar), AppearanceOptions.RelativeLuminance(gradedInk)) + .05);
            Check(colorFinishIntensity.IsVisible && chromeFinishLayer.Background is LinearGradientBrush &&
                chatFinishLayer.Background is LinearGradientBrush && contrast >= 6.0,
                $"Finish {finish.Name} should activate smooth wash brushes and maintain crisp ink contrast ({contrast:F2}).");
        }

        // Verify slider reactivity and Pineapple saturation reduction on Pastel / Soft matte / Stylized.
        colorFinishBox.SelectedValue = ColorFinish.Pastel;
        colorFinishIntensity.Value = .25;
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        var lowBlendSidebar = ((SolidColorBrush)Application.Current.Resources["Sidebar"]).Color;
        colorFinishIntensity.Value = 1.0;
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        var fullPastelSidebar = ((SolidColorBrush)Application.Current.Resources["Sidebar"]).Color;
        var (pastelHue, pastelSat, _) = ColorFinish.ToHsl(fullPastelSidebar);
        Check(lowBlendSidebar != fullPastelSidebar && pastelSat < rawPineSat * .55 && pastelHue is >= 25 and <= 75,
            $"Pastel at 100% should visibly tone Pineapple saturation ({rawPineSat:F2} -> {pastelSat:F2}) while keeping golden hue ({pastelHue:F0}) and responding to slider.");
        Capture(window, Path.Combine(artifacts, "pineapple-pastel-tested.png"));
        Capture(preferences, Path.Combine(artifacts, "appearance-colors-finishes-tested.png"));

        colorFinishBox.SelectedValue = ColorFinish.Stylized;
        colorFinishIntensity.Value = .85;
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        Capture(window, Path.Combine(artifacts, "pineapple-stylized-tested.png"));

        themeSelector.SelectedValue = ThemeManager.IronMan;
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        Capture(window, Path.Combine(artifacts, "ironman-stylized-tested.png"));

        // Restore Light + Original finish before verifying un-graded hex channel overrides.
        colorFinishBox.SelectedValue = ColorFinish.Off;
        colorFinishIntensity.Value = .65;
        themeSelector.SelectedValue = ThemeManager.Light;
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        Check(((SolidColorBrush)Application.Current.Resources["Sidebar"]).Color != fullPastelSidebar &&
            rawPineAccent != Color.FromRgb(0xEC, 0x48, 0x99) &&
            chromeFinishLayer.Background == Brushes.Transparent,
            "Restoring Original color finish must clear washes and restore raw colors.");

        var accentBox = (System.Windows.Controls.ListBox)preferences.FindName("AccentColorBox");
        var effectHexBox = (System.Windows.Controls.TextBox)preferences.FindName("EffectHexBox");
        var outBubbleBox = (System.Windows.Controls.ListBox)preferences.FindName("OutgoingBubbleColorBox");
        var inBubbleBox = (System.Windows.Controls.ListBox)preferences.FindName("IncomingBubbleColorBox");
        accentBox.SelectedValue = "#EC4899";
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        Check(((SolidColorBrush)Application.Current.Resources["Accent"]).Color == Color.FromRgb(0xEC, 0x48, 0x99),
            "Accent chip should remix the live Accent resource.");
        Check((Color)Application.Current.Resources["EffectColor"] == Color.FromRgb(0xEC, 0x48, 0x99),
            "EffectColor should follow custom accent when effect tint is set to Match accent.");

        effectHexBox.Text = "#00FFAA";
        effectHexBox.RaiseEvent(new System.Windows.Input.KeyEventArgs(
            System.Windows.Input.Keyboard.PrimaryDevice,
            PresentationSource.FromVisual(effectHexBox)!, 0, System.Windows.Input.Key.Enter)
        { RoutedEvent = UIElement.KeyDownEvent });
        outBubbleBox.SelectedValue = "#6366F1";
        inBubbleBox.SelectedValue = "#3B82F6";
        var chatSurfaceLayer = (System.Windows.Controls.Border)window.FindName("ChatSurfaceLayer");
        ((System.Windows.Controls.RadioButton)preferences.FindName("ChatSurfaceTheme")).IsChecked = true;
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        Check(chatSurfaceLayer.Background == Brushes.Transparent &&
            ((System.Windows.Controls.CheckBox)preferences.FindName("TextureOnChatBox")).IsChecked == true,
            "Match theme chat mode should make the chat surface transparent and sync the Textures tab toggle.");
        ((System.Windows.Controls.RadioButton)preferences.FindName("ChatSurfaceSeparate")).IsChecked = true;
        ((System.Windows.Controls.ListBox)preferences.FindName("ChatBackgroundColorBox")).SelectedValue = "#16111B";
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        Check(chatSurfaceLayer.Background is SolidColorBrush chatBgBrush && chatBgBrush.Color == Color.FromRgb(0x16, 0x11, 0x1B) &&
            ((System.Windows.Controls.CheckBox)preferences.FindName("TextureOnChatBox")).IsChecked == false,
            "Separate chat background color should apply a distinct custom chat color and uncheck Match theme.");
        Check((Color)Application.Current.Resources["EffectColor"] == Color.FromRgb(0x00, 0xFF, 0xAA) &&
            ((SolidColorBrush)Application.Current.Resources["Accent"]).Color == Color.FromRgb(0xEC, 0x48, 0x99),
            "Custom effect hex should override EffectColor independently of Accent.");
        Capture(preferences, Path.Combine(artifacts, "appearance-colors-prefs-tested.png"));

        // 2. Bubbles & geometry tab.
        preferences.ShowAppearanceTab("Bubbles");
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        ((System.Windows.Controls.ListBox)preferences.FindName("BubbleStyleBox")).SelectedValue = AppearanceOptions.BubbleGradient;
        ((System.Windows.Controls.ListBox)preferences.FindName("BubbleShapeBox")).SelectedValue = AppearanceOptions.ShapePill;
        ((System.Windows.Controls.RadioButton)preferences.FindName("DensitySpacious")).IsChecked = true;
        ((System.Windows.Controls.RadioButton)preferences.FindName("AnimPop")).IsChecked = true;
        ((System.Windows.Controls.ListBox)preferences.FindName("CornerStyleBox")).SelectedValue = AppearanceOptions.CornerSharp;
        ((System.Windows.Controls.ListBox)preferences.FindName("AvatarShapeBox")).SelectedValue = AppearanceOptions.AvatarSquircle;
        ((System.Windows.Controls.CheckBox)preferences.FindName("OnlineRingBox")).IsChecked = true;
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        Check(Application.Current.Resources["OutgoingBubbleBackground"] is LinearGradientBrush,
            "Gradient bubble style should apply a gradient brush to outgoing bubbles.");
        Check(((CornerRadius)Application.Current.Resources["OutgoingBubbleCorner"]).TopLeft >= 18,
            "Pill bubble shape should round bubble corners.");

        // Verify live rendered incoming + outgoing TextBubble and FileBubble elements both update.
        var msgList = (System.Windows.Controls.ItemsControl)window.FindName("MessageList");
        var outFileSample = new ChatMessage { Id = "bubble-file-out", PeerId = model.SelectedPeer!.Id, Outgoing = true, FileName = "out.bin", Delivery = "Sent" };
        var inFileSample = new ChatMessage { Id = "bubble-file-in", PeerId = model.SelectedPeer!.Id, Outgoing = false, FileName = "in.bin", Delivery = "Received" };
        model.Messages.Add(outFileSample);
        model.Messages.Add(inFileSample);
        ((System.Windows.Controls.ListBox)preferences.FindName("BubbleShapeBox")).SelectedValue = AppearanceOptions.ShapeTail;
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        System.Windows.Controls.Border FindBubble(ChatMessage m, string name)
        {
            var container = (FrameworkElement)msgList.ItemContainerGenerator.ContainerFromItem(m);
            container.ApplyTemplate();
            return (System.Windows.Controls.Border)msgList.ItemTemplate.FindName(name, container);
        }
        var outTextMsg = model.Messages.First(m => m.Outgoing && m.HasText);
        var inTextMsg = model.Messages.First(m => !m.Outgoing && m.HasText);
        var outTextBorder = FindBubble(outTextMsg, "TextBubble");
        var inTextBorder = FindBubble(inTextMsg, "TextBubble");
        var outFileBorder = FindBubble(outFileSample, "FileBubble");
        var inFileBorder = FindBubble(inFileSample, "FileBubble");
        Check(outTextBorder.Background is LinearGradientBrush && inTextBorder.Background is LinearGradientBrush &&
            ReferenceEquals(outTextBorder.Background, Application.Current.Resources["OutgoingBubbleBackground"]) &&
            ReferenceEquals(inTextBorder.Background, Application.Current.Resources["IncomingBubbleBackground"]) &&
            outTextBorder.CornerRadius.BottomRight == 3 && inTextBorder.CornerRadius.BottomLeft == 3 &&
            outFileBorder.CornerRadius.BottomRight == 3 && inFileBorder.CornerRadius.BottomLeft == 3 &&
            ReferenceEquals(outFileBorder.Background, Application.Current.Resources["OutgoingBubbleBackground"]) &&
            ReferenceEquals(inFileBorder.Background, Application.Current.Resources["IncomingBubbleBackground"]),
            "Rendered outgoing & incoming text and file bubbles must both reflect live custom colors, gradient style, and tail corners.");

        // Switching to GundamFreedom / IronMan while keeping custom colors must keep Mecha/Reactor style on both sides.
        var prevTheme = themeSelector.SelectedValue;
        ((System.Windows.Controls.ListBox)preferences.FindName("BubbleStyleBox")).SelectedValue = AppearanceOptions.BubbleSoft;
        ((System.Windows.Controls.ListBox)preferences.FindName("BubbleShapeBox")).SelectedValue = AppearanceOptions.Default;
        themeSelector.SelectedValue = ThemeManager.IronMan;
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        Check(outTextBorder.BorderBrush is LinearGradientBrush && inTextBorder.BorderBrush is LinearGradientBrush &&
            outTextBorder.CornerRadius.TopLeft > outTextBorder.CornerRadius.TopRight &&
            inTextBorder.CornerRadius.TopRight > inTextBorder.CornerRadius.TopLeft &&
            outFileBorder.CornerRadius.TopLeft > outFileBorder.CornerRadius.TopRight &&
            inFileBorder.CornerRadius.TopRight > inFileBorder.CornerRadius.TopLeft,
            "Iron Man theme should apply Reactor borders and directional bevels to both outgoing and incoming text and file bubbles even with custom colors.");
        // Restore theme & Pill/Gradient overrides for downstream assertions in this test.
        themeSelector.SelectedValue = prevTheme;
        ((System.Windows.Controls.ListBox)preferences.FindName("BubbleStyleBox")).SelectedValue = AppearanceOptions.BubbleGradient;
        ((System.Windows.Controls.ListBox)preferences.FindName("BubbleShapeBox")).SelectedValue = AppearanceOptions.ShapePill;
        model.Messages.Remove(outFileSample);
        model.Messages.Remove(inFileSample);
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        Check(((CornerRadius)Application.Current.Resources["PanelCorner"]).TopLeft == 0,
            "Sharp global corners should override PanelCorner.");
        Check(MessageMotion.ActiveStyle == MessageMotion.Pop && model.UseCustomAvatarShape && model.OnlineRingEnabled,
            "Bubble motion, avatar shape, and online presence ring should live-preview.");
        Capture(preferences, Path.Combine(artifacts, "appearance-bubbles-prefs-tested.png"));

        // 3. Ambient tab: Matrix, Constellation, Bokeh + global speed.
        preferences.ShowAppearanceTab("Ambient");
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        ((System.Windows.Controls.CheckBox)preferences.FindName("MatrixBox")).IsChecked = true;
        ((System.Windows.Controls.CheckBox)preferences.FindName("ConstellationBox")).IsChecked = true;
        ((System.Windows.Controls.CheckBox)preferences.FindName("BokehBox")).IsChecked = true;
        ((System.Windows.Controls.Slider)preferences.FindName("EffectSpeedBox")).Value = 1.6;
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        Check(Math.Abs(AmbientEffect.SpeedMultiplier - 1.6) < .01, "Effect speed slider should live-update AmbientEffect.SpeedMultiplier.");

        // 4. Customize tab: UI font, message font, exact font px, window opacity, canvas gradient, background image + opacity + blur.
        preferences.ShowAppearanceTab("Customize");
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        ((System.Windows.Controls.ComboBox)preferences.FindName("UiFontBox")).SelectedItem = "Consolas";
        ((System.Windows.Controls.ComboBox)preferences.FindName("FontBox")).SelectedItem = "Verdana";
        ((System.Windows.Controls.Slider)preferences.FindName("MessageFontPxBox")).Value = 19;
        ((System.Windows.Controls.Slider)preferences.FindName("WindowOpacityBox")).Value = .92;
        ((System.Windows.Controls.ListBox)preferences.FindName("ChatCanvasBox")).SelectedValue = ChatCanvasOverlay.Mesh;
        ((System.Windows.Controls.Slider)preferences.FindName("ChatCanvasIntensityBox")).Value = .80;
        var backdropLayer = (System.Windows.Controls.Grid)window.FindName("ChatBackdropLayer");
        Check(System.Windows.Controls.Grid.GetRowSpan(backdropLayer) == 1 && !model.SeamlessComposerEnabled,
            "Seamless input bar must be opt-in by default.");
        ((System.Windows.Controls.CheckBox)preferences.FindName("SeamlessComposerBox")).IsChecked = true;
        ((System.Windows.Controls.Slider)preferences.FindName("BackgroundBlurBox")).Value = 8;
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        Check(((FontFamily)Application.Current.Resources["UiFont"]).Source.Contains("Consolas") &&
            ((FontFamily)Application.Current.Resources["MessageFontFamily"]).Source.Contains("Verdana"),
            "Interface and message font selectors should update app resources live.");
        Check(Math.Abs(ThemeManager.ActiveOverrides.WindowOpacity - .92) < .005,
            "Window opacity slider should update active overrides.");
        Check(canvasLayer.IsVisible && canvasLayer.Kind == ChatCanvasGradientKind.Mesh && Math.Abs(canvasLayer.Intensity - .80) < .01 &&
            System.Windows.Controls.Grid.GetRowSpan(backdropLayer) == 4 && model.SeamlessComposerEnabled,
            "Canvas gradient and Seamless input bar should live-preview behind chat and composer.");
        Check(model.ChatBackgroundBlur == 8 && model.ChatBackgroundBlurEffect is System.Windows.Media.Effects.BlurEffect,
            "Background blur slider should live-preview a BlurEffect.");

        // 5. Themes tab: Saved presets, Look code encode/decode (including fonts & background image), Reset, Load, Randomize.
        preferences.ShowAppearanceTab("Colors");
        ((System.Windows.Controls.ListBox)preferences.FindName("ColorFinishBox")).SelectedValue = ColorFinish.Velvet;
        ((System.Windows.Controls.Slider)preferences.FindName("ColorFinishIntensityBox")).Value = .80;
        preferences.ShowAppearanceTab("Themes");
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        ((System.Windows.Controls.ListBox)preferences.FindName("ThemeBox")).SelectedValue = ThemeManager.Midnight;
        ((System.Windows.Controls.TextBox)preferences.FindName("PresetNameBox")).Text = "Neon Lab";
        ClickContentButton(preferences, "Save look");
        var presetBox = (System.Windows.Controls.ComboBox)preferences.FindName("PresetBox");
        Check(presetBox.Items.Count == 1, "Saving a look should add it to Saved looks.");

        ClickContentButton(preferences, "Copy code");
        string code = ((System.Windows.Controls.TextBox)preferences.FindName("LookCodeBox")).Text;
        Check(code.StartsWith(AppearanceCodec.Prefix, StringComparison.Ordinal) &&
            AppearanceCodec.TryDecode(code, out var decoded) &&
            decoded.Theme == ThemeManager.Midnight &&
            decoded.ColorFinish == ColorFinish.Velvet &&
            Math.Abs((decoded.ColorFinishIntensity ?? 0) - .80) < .01 &&
            decoded.Accent == "#EC4899" &&
            decoded.EffectColor == "#00FFAA" &&
            decoded.BubbleStyle == AppearanceOptions.BubbleGradient &&
            decoded.BubbleShape == AppearanceOptions.ShapePill &&
            decoded.CanvasGradient == ChatCanvasOverlay.Mesh &&
            decoded.SeamlessComposer == true &&
            decoded.UiFont == "Consolas" &&
            decoded.MsgFont == "Verdana" &&
            decoded.MsgSizePx == 19 &&
            decoded.Matrix == true && decoded.Constellation == true && decoded.Bokeh == true,
            "Look code should round-trip all appearance overrides including fonts cleanly.");
        Check(!AppearanceCodec.TryDecode("CYDLAN1:invalid!!!", out _), "Malformed look codes must be rejected safely.");

        // Verify encoding + extracting a Look code carrying a 1280x720 background image + opacity + fonts within the chat message limit.
        var bgVisual = new DrawingVisual();
        using (var bgDc = bgVisual.RenderOpen())
        {
            bgDc.DrawRectangle(new LinearGradientBrush(Colors.MidnightBlue, Colors.MediumPurple, 35), null, new Rect(0, 0, 1280, 720));
            bgDc.DrawEllipse(new SolidColorBrush(Color.FromArgb(140, 255, 190, 110)), null, new Point(920, 220), 210, 210);
            bgDc.DrawRectangle(new SolidColorBrush(Color.FromArgb(120, 20, 40, 80)), null, new Rect(0, 440, 1280, 280));
        }
        var bgRender = new RenderTargetBitmap(1280, 720, 96, 96, PixelFormats.Pbgra32);
        bgRender.Render(bgVisual);
        bgRender.Freeze();
        var bgSampleSettings = new AppSettings
        {
            Theme = ThemeManager.Midnight,
            UiFont = "Consolas",
            MessageFont = "Verdana",
            MessageFontSizePx = 19,
            ChatBackgroundOpacity = .42,
            ChatBackgroundBlur = 6,
            ChatBackgroundAnchor = BackgroundAnchor.BottomRight,
            ChatBackgroundTransition = BackgroundSideTransition.Horizontal,
            ChatBackgroundTransitionStrength = .80
        };
        string codeWithBg = AppearanceCodec.Encode(bgSampleSettings, bgRender);
        Check(codeWithBg.Length <= AppearanceCodec.MaxCodeLength &&
            AppearanceCodec.TryExtractLookCode("Try my theme\n" + codeWithBg, out var extractedCode, out var bgLook, out var extractedCaption) &&
            extractedCode == codeWithBg &&
            extractedCaption == "Try my theme" &&
            bgLook.UiFont == "Consolas" &&
            bgLook.MsgFont == "Verdana" &&
            bgLook.BgAnchor == BackgroundAnchor.BottomRight &&
            bgLook.BgFade == BackgroundSideTransition.Horizontal &&
            Math.Abs((bgLook.BgFadeStrength ?? 0) - .80) < .01 &&
            Math.Abs((bgLook.BgOpacity ?? 0) - .42) < .01 &&
            AppearanceCodec.TryDecodeBackgroundImage(bgLook) is { PixelWidth: 1280, PixelHeight: 720 },
            "Look code must embed full-resolution background image, anchor, opacity, blur, side transitions, and fonts cleanly.");

        // Verify 9-point BackgroundAnchor geometry math + live WPF viewport positioning + visual verification screenshots.
        Check(BackgroundAnchor.Normalize("TL") == BackgroundAnchor.TopLeft &&
            BackgroundAnchor.Normalize("unknown") == BackgroundAnchor.Center &&
            BackgroundAnchor.CalculateRect(new Size(800, 600), new Size(400, 800), "fill", BackgroundAnchor.Top, 1.0) == new Rect(0, 0, 800, 1600) &&
            BackgroundAnchor.CalculateRect(new Size(800, 600), new Size(400, 800), "fill", BackgroundAnchor.Center, 1.0) == new Rect(0, -500, 800, 1600) &&
            BackgroundAnchor.CalculateRect(new Size(800, 600), new Size(400, 800), "fill", BackgroundAnchor.Bottom, 1.0) == new Rect(0, -1000, 800, 1600) &&
            BackgroundAnchor.CalculateRect(new Size(800, 600), new Size(1200, 300), "fit", BackgroundAnchor.BottomRight, 1.0) == new Rect(0, 400, 800, 200) &&
            BackgroundAnchor.CalculateRect(new Size(800, 600), new Size(200, 100), "center", BackgroundAnchor.TopRight, 1.5) == new Rect(500, 0, 300, 150) &&
            BackgroundAnchor.CalculateRect(new Size(800, 600), new Size(800, 600), "stretch", BackgroundAnchor.BottomRight, 1.25) == new Rect(-200, -150, 1000, 750),
            "BackgroundAnchor calculations must place overflow, fitted margins, original sizes, and zoomed backgrounds accurately.");

        var tallVisual = new DrawingVisual();
        using (var tdc = tallVisual.RenderOpen())
        {
            tdc.DrawRectangle(new LinearGradientBrush(Color.FromRgb(0x0F, 0x17, 0x2A), Color.FromRgb(0x31, 0x1B, 0x4D), 90), null, new Rect(0, 0, 480, 960));
            tdc.DrawEllipse(new SolidColorBrush(Color.FromRgb(0xFF, 0x6B, 0x6B)), null, new Point(120, 120), 85, 85);
            tdc.DrawRoundedRectangle(new SolidColorBrush(Color.FromRgb(0x38, 0xBD, 0xF8)), null, new Rect(220, 405, 190, 150), 24, 24);
            tdc.DrawEllipse(new SolidColorBrush(Color.FromRgb(0xFB, 0xBF, 0x24)), null, new Point(360, 840), 85, 85);
        }
        var tallBmp = new RenderTargetBitmap(480, 960, 96, 96, PixelFormats.Pbgra32);
        tallBmp.Render(tallVisual);
        tallBmp.Freeze();

        var anchorChatImage = (System.Windows.Controls.Image)window.FindName("ChatBackgroundImage");
        var anchorChatViewport = (BackgroundImageViewport)VisualTreeHelper.GetParent(anchorChatImage);
        var anchorPreviewImage = (System.Windows.Controls.Image)preferences.FindName("BackgroundPreview");
        var anchorPreviewViewport = (BackgroundImageViewport)VisualTreeHelper.GetParent(anchorPreviewImage);

        model.PreviewChatBackground(true, tallBmp, .85);
        model.PreviewBackgroundOptions(0, 1.0, "fill", false, BackgroundAnchor.TopLeft);
        AnimatedGifBehavior.SetFallbackSource(anchorPreviewImage, tallBmp);
        ((System.Windows.Controls.Slider)preferences.FindName("BackgroundOpacityBox")).Value = .85;
        ((FrameworkElement)preferences.FindName("BackgroundAdjustments")).Visibility = Visibility.Visible;
        ((System.Windows.Controls.Primitives.ToggleButton)preferences.FindName("BgFitFill")).IsChecked = true;
        ((System.Windows.Controls.Primitives.ToggleButton)preferences.FindName("BgAnchorTopLeft")).IsChecked = true;
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        double chatTopLeftY = anchorChatImage.TranslatePoint(new Point(0, 0), anchorChatViewport).Y;
        Check(Math.Abs(chatTopLeftY) < 1.0 && anchorChatImage.ActualHeight > anchorChatViewport.ActualHeight + 50,
            $"Top-left fill anchor should start at y=0 (was {chatTopLeftY:0.0}).");
        Capture(window, Path.Combine(artifacts, "bg-anchor-chat-top-left.png"));

        ((System.Windows.Controls.Primitives.ToggleButton)preferences.FindName("BgAnchorBottomRight")).IsChecked = true;
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        double chatBottomRightY = anchorChatImage.TranslatePoint(new Point(0, 0), anchorChatViewport).Y;
        double previewBottomY = anchorPreviewImage.TranslatePoint(new Point(0, 0), anchorPreviewViewport).Y;
        Check(chatBottomRightY < -50 && Math.Abs(chatBottomRightY + anchorChatImage.ActualHeight - anchorChatViewport.ActualHeight) < 1.5 &&
            previewBottomY < -50 && Math.Abs(previewBottomY + anchorPreviewImage.ActualHeight - anchorPreviewViewport.ActualHeight) < 1.5,
            $"Bottom-right fill anchor must pin bottom edge in both main chat and Settings preview.");
        ((FrameworkElement)preferences.FindName("BgAnchorCenter")).BringIntoView();
        ((FrameworkElement)preferences.FindName("BackgroundPreview")).BringIntoView();
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        Capture(window, Path.Combine(artifacts, "bg-anchor-chat-bottom-right.png"));
        Capture(preferences, Path.Combine(artifacts, "bg-anchor-settings-bottom-right.png"));

        // Verify persisted anchor across LocalStore save + reload.
        string anchorTempDir = Path.Combine(Path.GetTempPath(), "cydlan-anchor-" + Guid.NewGuid().ToString("N"));
        try
        {
            await using (var roundTripVm = new MainViewModel(localStore: new LocalStore(anchorTempDir)))
            {
                await roundTripVm.SavePreferencesAsync(s =>
                {
                    s.ChatBackgroundFit = "fit";
                    s.ChatBackgroundAnchor = BackgroundAnchor.BottomRight;
                    s.ChatBackgroundScale = 1.35;
                });
            }
            var reloaded = new LocalStore(anchorTempDir).LoadSettings();
            Check(reloaded.ChatBackgroundAnchor == BackgroundAnchor.BottomRight && reloaded.ChatBackgroundFit == "fit",
                "Background anchor must persist through LocalStore.");
        }
        finally { try { Directory.Delete(anchorTempDir, true); } catch { } }
        model.PreviewChatBackground(false, null, null);
        model.PreviewBackgroundOptions(null, null, null, null, null);

        ClickContentButton(preferences, "Reset");
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        Check(ThemeManager.ActiveOverrides == AppearanceOverrides.Default && !canvasLayer.IsVisible &&
            Math.Abs(AmbientEffect.SpeedMultiplier - 1.0) < .01 && MessageMotion.ActiveStyle == MessageMotion.Off,
            "Reset must restore bundled theme defaults without leaving overrides behind.");

        ClickContentButton(preferences, "Load");
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        Check(ThemeManager.ActiveOverrides.CustomAccentColor == "#EC4899" &&
            ThemeManager.ActiveOverrides.CustomEffectColor == "#00FFAA" &&
            ThemeManager.ActiveOverrides.BubbleStyle == AppearanceOptions.BubbleGradient &&
            canvasLayer.IsVisible && canvasLayer.Kind == ChatCanvasGradientKind.Mesh,
            "Loading a saved look must restore every override live.");

        ClickContentButton(preferences, "Randomize");
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        Check(((System.Windows.Controls.TextBlock)preferences.FindName("PresetStatusText")).Text == "Randomized",
            "Randomize should apply a valid look without error.");

        // 6. Cancel restores original bundled state completely.
        preferences.Close();
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        Check(ThemeManager.ActiveOverrides == AppearanceOverrides.Default &&
            !canvasLayer.IsVisible && !model.UseCustomAvatarShape && !model.OnlineRingEnabled &&
            model.ChatBackgroundBlur == 0 && Math.Abs(AmbientEffect.SpeedMultiplier - 1.0) < .01 &&
            MessageMotion.ActiveStyle == MessageMotion.Off,
            "Canceling Preferences must restore bundled defaults across every new setting.");

        // 7. Import look code + Save persists everything and activates new ambient layers.
        var savePrefs = new SettingsWindow(model) { Owner = window };
        savePrefs.Show();
        savePrefs.ShowPage("AppearancePage");
        ((System.Windows.Controls.TextBox)savePrefs.FindName("LookCodeBox")).Text = code;
        ClickContentButton(savePrefs, "Import");
        ((System.Windows.Controls.TextBox)savePrefs.FindName("PresetNameBox")).Text = "Neon Lab";
        ClickContentButton(savePrefs, "Save look");
        ClickNamed(savePrefs, "Save");
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);

        Check(model.Settings.Theme == ThemeManager.Midnight &&
            model.Settings.ColorFinish == ColorFinish.Velvet &&
            Math.Abs(model.Settings.ColorFinishIntensity - .80) < .01 &&
            model.Settings.CustomAccentColor == "#EC4899" &&
            model.Settings.CustomEffectColor == "#00FFAA" &&
            model.Settings.BubbleStyle == AppearanceOptions.BubbleGradient &&
            model.Settings.BubbleShape == AppearanceOptions.ShapePill &&
            model.Settings.ChatDensity == AppearanceOptions.DensitySpacious &&
            model.Settings.CornerStyle == AppearanceOptions.CornerSharp &&
            model.Settings.AvatarShape == AppearanceOptions.AvatarSquircle &&
            model.Settings.OnlineRingEnabled &&
            model.Settings.UiFont == "Consolas" &&
            model.EffectiveMessageFontSize == 19 &&
            Math.Abs(model.WindowOpacity - .92) < .005 &&
            Math.Abs(model.EffectSpeed - 1.6) < .01 &&
            model.Settings.MessageAnimation == MessageMotion.Pop &&
            model.Settings.ChatCanvasGradient == ChatCanvasOverlay.Mesh &&
            model.Settings.ChatBackgroundBlur == 8 &&
            model.Settings.MatrixEnabled && model.Settings.ConstellationEnabled && model.Settings.BokehEnabled &&
            model.Settings.SavedPresets.Count == 1 && model.Settings.SavedPresets[0].Name == "Neon Lab",
            "Importing a look code and saving must persist all customization settings and presets.");
        Check(canvasLayer.IsVisible && matrixLayer.IsVisible && constellationLayer.IsVisible && bokehLayer.IsVisible,
            "Saved canvas gradient and Matrix/Constellation/Bokeh ambient layers should be active on MainWindow.");
        Capture(window, Path.Combine(artifacts, "custom-remix-chat-tested.png"));

        // 8. Reopening Settings returns to last configured page/tab; Profile opens AccountPage directly without overwriting it.
        var remembered = window.CreateSettingsWindow();
        remembered.Owner = window;
        remembered.Show();
        remembered.ShowPage("AppearancePage");
        remembered.ShowAppearanceTab("Bubbles");
        ClickNamed(remembered, "Save");
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        Check(model.LastSettingsPage == "AppearancePage" && model.LastAppearanceTab == "Bubbles" &&
            model.Settings.LastSettingsPage == "AppearancePage" && model.Settings.LastAppearanceTab == "Bubbles",
            "Saving Settings should remember the last configured page and Appearance tab.");

        var reopenedSettings = window.CreateSettingsWindow();
        reopenedSettings.Owner = window;
        reopenedSettings.Show();
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        Check(((FrameworkElement)reopenedSettings.FindName("AppearancePage")).IsVisible &&
            ((FrameworkElement)reopenedSettings.FindName("AppearanceBubblesSection")).IsVisible,
            "Reopening Preferences should land on the last configured page and sub-tab.");
        reopenedSettings.Close();

        var profileDialog = window.CreateSettingsWindow("AccountPage", rememberNavigation: false);
        profileDialog.Owner = window;
        profileDialog.Show();
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        Check(((FrameworkElement)profileDialog.FindName("AccountPage")).IsVisible &&
            !((FrameworkElement)profileDialog.FindName("AppearancePage")).IsVisible,
            "Opening Profile should go straight to AccountPage.");
        ClickNamed(profileDialog, "Save");
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        Check(model.LastSettingsPage == "AppearancePage" && model.LastAppearanceTab == "Bubbles",
            "Opening and saving Profile must not overwrite the Settings button's last configured tab.");

        var reopenedAfterProfile = window.CreateSettingsWindow();
        reopenedAfterProfile.Owner = window;
        reopenedAfterProfile.Show();
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        Check(((FrameworkElement)reopenedAfterProfile.FindName("AppearancePage")).IsVisible &&
            ((FrameworkElement)reopenedAfterProfile.FindName("AppearanceBubblesSection")).IsVisible,
            "Settings button should still restore the last configured tab after using Profile.");
        reopenedAfterProfile.Close();

        window.Close();
        ThemeManager.Apply(ThemeManager.Light, AppearanceOverrides.Default);
        AmbientEffect.SetSpeed(1.0);
        MessageMotion.Configure(MessageMotion.Off);
        Console.WriteLine("PASS  Custom color remix, bubbles, geometry, canvas gradients, new ambient effects, look codes & presets");
    }

    private static async Task TestFloatingLanSuiteAsync(string artifacts)
    {
        await using var model = new MainViewModel(preview: true);
        ThemeManager.Apply(ThemeManager.Pineapple, new AppearanceOverrides("", "", "", "", AppearanceOptions.BubbleSoft, AppearanceOptions.Default, AppearanceOptions.DensityCozy, AppearanceOptions.Default, AppearanceOptions.Default, "", 1.0, AppearanceOptions.ChatSurfaceSeparate, "", ColorFinish.Pastel, .75));
        var window = new MainWindow(model);
        window.Show();
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);

        Check(FindText(window, "Floating LAN") is null, "Floating LAN must not appear in MainWindow navigation.");

        using var controller = new FloatingPetController(model, () => App.ShowWindow(window));
        controller.SyncVisibility();
        Check(controller.PetWindow is null && !model.FloatingLanEnabled, "Floating pet should be off by default.");

        // 1. Toggle Floating LAN purely from the tray menu.
        var tray = new TrayMenuWindow(window, model);
        tray.ShowNear(new System.Drawing.Point(800, 600));
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        var floatingCheck = (FrameworkElement)tray.FindName("FloatingCheck");
        Check(floatingCheck.Visibility == Visibility.Collapsed, "Tray menu checkmark should start collapsed.");
        var floatingAction = (System.Windows.Controls.Button)tray.FindName("FloatingAction");
        floatingAction.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);

        Check(model.FloatingLanEnabled && controller.PetWindow is { IsVisible: true, ShowInTaskbar: false },
            "Clicking Floating LAN in the tray menu must show the floating pet off the taskbar.");

        int petExStyle = GetWindowLong(new WindowInteropHelper(controller.PetWindow!).Handle, -20);
        Check((petExStyle & 0x00000080) != 0 && (petExStyle & 0x00040000) == 0,
            "Floating pet window must have WS_EX_TOOLWINDOW and never WS_EX_APPWINDOW.");

        var trayOn = new TrayMenuWindow(window, model);
        trayOn.ShowNear(new System.Drawing.Point(800, 600));
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        Check(((FrameworkElement)trayOn.FindName("FloatingCheck")).Visibility == Visibility.Visible,
            "Tray menu should show an active checkmark when Floating LAN is enabled.");
        Capture(trayOn, Path.Combine(artifacts, "tray-floating-tested.png"));
        trayOn.CloseOnce();

        // 2. React to incoming messages while respecting NotificationPreviews privacy.
        var petWindow = controller.PetWindow!;
        var badge = (FrameworkElement)petWindow.FindName("UnreadBadge");
        var speech = (FrameworkElement)petWindow.FindName("SpeechBubble");
        var speechText = (System.Windows.Controls.TextBlock)petWindow.FindName("SpeechText");
        Check(model.TotalUnread == 2 && badge.IsVisible, "Pet unread badge must reflect unread LAN messages.");

        model.NotifyIncoming("Sofia Reyes", "Secret text", model.Peers[1].Id);
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        Check(speech.IsVisible && speechText.Text == "New message",
            "Speech bubble must hide message text when NotificationPreviews is false.");

        model.Settings.NotificationPreviews = true;
        model.NotifyIncoming("Sofia Reyes", "Check out the new build!", model.Peers[1].Id);
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        Check(speech.IsVisible && speechText.Text == "Check out the new build!",
            "Speech bubble should show the message snippet when NotificationPreviews is enabled.");
        Capture(petWindow, Path.Combine(artifacts, "floating-pet-pineapple-tested.png"));

        ThemeManager.Apply(ThemeManager.IronMan, AppearanceOverrides.Default);
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        Capture(petWindow, Path.Combine(artifacts, "floating-pet-ironman-tested.png"));

        // 3. Toggle compact quick-chat popup, verify active-chatters-only tab filter, send reply + shared Look card, and mark read.
        controller.ToggleQuickChat();
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        Check(controller.QuickChatWindow is { IsVisible: true, ShowInTaskbar: false },
            "Clicking the floating pet must open the compact quick-chat popup off the taskbar.");
        var quickChat = controller.QuickChatWindow!;
        int quickExStyle = GetWindowLong(new WindowInteropHelper(quickChat).Handle, -20);
        Check((quickExStyle & 0x00000080) != 0 && (quickExStyle & 0x00040000) == 0,
            "Quick chat popup must have WS_EX_TOOLWINDOW and stay out of Alt+Tab.");

        var quickPeersList = (System.Windows.Controls.ListBox)quickChat.FindName("QuickPeersList");
        Check(quickPeersList.Items.Count == 2 &&
            model.QuickChatPeers.Contains(model.Peers[0]) &&
            model.QuickChatPeers.Contains(model.Peers[1]) &&
            !model.QuickChatPeers.Contains(model.Peers[2]),
            "Quick chat tabs must show active/unread chatters only instead of every online contact.");

        model.Peers[2].Typing = "Typing…";
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        Check(quickPeersList.Items.Count == 3 && model.QuickChatPeers.Contains(model.Peers[2]),
            "An idle contact should appear in Quick chat tabs once they start typing.");
        model.Peers[2].Typing = "";
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        Check(quickPeersList.Items.Count == 2 && !model.QuickChatPeers.Contains(model.Peers[2]),
            "Contacts with no active conversation should stay out of Quick chat tabs.");

        var roamBtn = (System.Windows.Controls.Button)quickChat.FindName("RoamButton");
        roamBtn.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
        Check(model.FloatingLanRoam, "Roam toggle must enable roaming mode.");

        var quickComposer = (System.Windows.Controls.TextBox)quickChat.FindName("QuickComposer");
        model.SelectedPeer = model.Peers[1]; // Sofia Reyes (Unread = 2)
        quickComposer.Focus();
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        Check(model.TotalUnread == 0 && !badge.IsVisible,
            "Replying from quick chat must mark the peer read and clear the pet badge.");

        quickComposer.Text = "Quick reply from pet!";
        ClickNamed(quickChat, "Send message");
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        Check(model.Messages.Last().Text == "Quick reply from pet!" && model.Messages.Last().Delivery == "Preview only",
            "Quick chat composer should send directly through MainViewModel.");

        // Send a Look code carrying a background image and custom fonts, then apply it directly from the chat card.
        var lookBgVisual = new DrawingVisual();
        using (var lookDc = lookBgVisual.RenderOpen())
        {
            lookDc.DrawRectangle(new LinearGradientBrush(Color.FromRgb(0x16, 0x26, 0x45), Color.FromRgb(0x52, 0x2B, 0x6B), 35), null, new Rect(0, 0, 800, 500));
            lookDc.DrawEllipse(new SolidColorBrush(Color.FromArgb(145, 0xFF, 0xA8, 0x58)), null, new Point(590, 140), 130, 130);
        }
        var lookBgBmp = new RenderTargetBitmap(800, 500, 96, 96, PixelFormats.Pbgra32);
        lookBgBmp.Render(lookBgVisual);
        lookBgBmp.Freeze();
        string sharedLook = AppearanceCodec.Encode(new AppSettings
        {
            Theme = ThemeManager.Midnight,
            UiFont = "Consolas",
            MessageFont = "Verdana",
            ChatBackgroundOpacity = .40,
            ChatBackgroundBlur = 2
        }, lookBgBmp);
        quickComposer.Text = sharedLook;
        ClickNamed(quickChat, "Send message");
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        var quickApplyButton = FindButtons(quickChat, "Apply theme").LastOrDefault(b => b.IsVisible);
        Check(quickApplyButton is not null, "Quick chat should render a compact shared theme card with an Apply button.");
        quickApplyButton!.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        Check(model.Settings.Theme == ThemeManager.Midnight &&
            model.Settings.UiFont == "Consolas" &&
            model.Settings.MessageFont == "Verdana" &&
            model.ChatBackground is not null &&
            ((FontFamily)Application.Current.Resources["MessageFontFamily"]).Source.Contains("Verdana") &&
            FindButtons(window, "Apply theme").Any(b => b.IsVisible),
            "Applying a shared Look card must apply its theme, fonts, and embedded background image.");
        Capture(quickChat, Path.Combine(artifacts, "floating-quickchat-tested.png"));
        Capture(window, Path.Combine(artifacts, "shared-look-chat-tested.png"));

        // Verify context menu "Quick chat" stays open cleanly across input frames.
        quickChat.Hide();
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        var menu = (System.Windows.Controls.ContextMenu)petWindow.FindName("PetContextMenu");
        ((System.Windows.Controls.MenuItem)menu.Items[0]).RaiseEvent(new RoutedEventArgs(System.Windows.Controls.MenuItem.ClickEvent));
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        Check(quickChat.IsVisible, "Context menu Quick chat action must keep quick-chat open.");
        quickChat.Hide();
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);

        // 4. Companion customization catalog, Look code roundtrip, live preview/Cancel/Save, and multi-style visual verification.
        Check(FloatingPetStyle.CombinationCount >= 10_000,
            $"Companion combination count must exceed 10,000 (actual: {FloatingPetStyle.CombinationCount}).");
        var randomPet = FloatingPetStyle.Randomize(new Random(42));
        var randomSettings = new AppSettings { Theme = ThemeManager.Midnight };
        FloatingPetStyle.Apply(randomPet, randomSettings);
        string petLookCode = AppearanceCodec.Encode(randomSettings);
        Check(AppearanceCodec.TryDecode(petLookCode, out var decodedPetLook),
            "Look code must encode and decode cleanly without embedding companion traits.");
        var receiverPet = FloatingPetStyle.Presets.First(p => p.Name == "Shark").Appearance;
        var decodedPetSettings = new AppSettings();
        FloatingPetStyle.Apply(receiverPet, decodedPetSettings);
        AppearanceCodec.Apply(decodedPetLook, decodedPetSettings);
        Check(PetAppearance.From(decodedPetSettings) == receiverPet && decodedPetSettings.Theme == ThemeManager.Midnight,
            "Applying an appearance Look code from someone else must preserve the receiver's companion unchanged.");
        string savedPetCode = randomPet.ToWireCode();
        Check(PetAppearance.FromWireCode(savedPetCode) == randomPet,
            "Companion CYDPET1: code must round-trip all customized companion traits independently.");
        randomSettings.SavedCompanions = [new SavedCompanion { Name = "My Companion", Code = savedPetCode }];

        // Verify disk roundtrip via LocalStore.
        string tempStoreDir = Path.Combine(Path.GetTempPath(), "cydlan-pet-store-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            var store = new LocalStore(tempStoreDir);
            store.SaveSettings(randomSettings);
            var reloaded = store.LoadSettings();
            Check(PetAppearance.From(reloaded) == randomPet,
                "Saved companion customization must persist cleanly across LocalStore reload.");
        }
        finally
        {
            try { Directory.Delete(tempStoreDir, true); } catch { }
        }

        // Open Settings -> Appearance -> Companion studio and verify live preview + Cancel + Save + captures.
        var beforeStudio = model.PetAppearance;
        var studioSettings = window.CreateSettingsWindow("AppearancePage", rememberNavigation: false, appearanceTab: "Companion");
        studioSettings.Owner = window;
        studioSettings.Show();
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        var companionSection = (FrameworkElement)studioSettings.FindName("AppearanceCompanionSection");
        Check(companionSection.IsVisible, "Opening Settings directly on Companion tab must display Companion studio.");

        var presetBox = (System.Windows.Controls.Primitives.Selector)studioSettings.FindName("PetPresetBox");
        presetBox.SelectedValue = "Wizard";
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        Check(model.PetAppearance is { Body: "pear", Hat: "wizard", Back: "cape", Prop: "wand" },
            "Selecting a companion preset must live-preview onto MainViewModel and FloatingPetWindow.");
        Capture(studioSettings, Path.Combine(artifacts, "companion-studio-body.png"));
        Capture(petWindow, Path.Combine(artifacts, "companion-pet-wizard.png"));

        ((System.Windows.Controls.RadioButton)studioSettings.FindName("PetCatFace")).IsChecked = true;
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        Capture(studioSettings, Path.Combine(artifacts, "companion-studio-face.png"));

        ((System.Windows.Controls.RadioButton)studioSettings.FindName("PetCatGear")).IsChecked = true;
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        Capture(studioSettings, Path.Combine(artifacts, "companion-studio-gear.png"));

        ((System.Windows.Controls.RadioButton)studioSettings.FindName("PetCatColors")).IsChecked = true;
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        Capture(studioSettings, Path.Combine(artifacts, "companion-studio-colors.png"));

        ((System.Windows.Controls.RadioButton)studioSettings.FindName("PetCatMotion")).IsChecked = true;
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        Capture(studioSettings, Path.Combine(artifacts, "companion-studio-vibe.png"));

        ClickContentButton(studioSettings, "Cancel");
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        Check(model.PetAppearance == beforeStudio,
            "Canceling Settings must revert temporary companion live previews.");

        // Customize from scratch, Save, and verify other archetypes visually too.
        var saveSettings = window.CreateSettingsWindow("AppearancePage", rememberNavigation: false, appearanceTab: "Companion");
        saveSettings.Owner = window;
        saveSettings.Show();
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        ((System.Windows.Controls.ListBox)saveSettings.FindName("PetBodyBox")).SelectedValue = "star";
        ((System.Windows.Controls.ListBox)saveSettings.FindName("PetPatternBox")).SelectedValue = "gradient";
        ((System.Windows.Controls.ListBox)saveSettings.FindName("PetEarsBox")).SelectedValue = "bunny";
        ((System.Windows.Controls.ListBox)saveSettings.FindName("PetFeetBox")).SelectedValue = "hover";
        ((System.Windows.Controls.ListBox)saveSettings.FindName("PetEyesBox")).SelectedValue = "big";
        ((System.Windows.Controls.ListBox)saveSettings.FindName("PetMouthBox")).SelectedValue = "fang";
        ((System.Windows.Controls.ListBox)saveSettings.FindName("PetCheeksBox")).SelectedValue = "hearts";
        ((System.Windows.Controls.ListBox)saveSettings.FindName("PetGlassesBox")).SelectedValue = "star";
        ((System.Windows.Controls.ListBox)saveSettings.FindName("PetHatBox")).SelectedValue = "crown";
        ((System.Windows.Controls.ListBox)saveSettings.FindName("PetOutfitBox")).SelectedValue = "hoodie";
        ((System.Windows.Controls.ListBox)saveSettings.FindName("PetBackBox")).SelectedValue = "angel";
        ((System.Windows.Controls.ListBox)saveSettings.FindName("PetPropBox")).SelectedValue = "sparkles";
        ((System.Windows.Controls.ListBox)saveSettings.FindName("PetVibeBox")).SelectedValue = "zoomies";
        ((System.Windows.Controls.Slider)saveSettings.FindName("PetSizeBox")).Value = 1.25;
        ((System.Windows.Controls.ListBox)saveSettings.FindName("PetBodyColorBox")).SelectedValue = "#FCE7F3";
        ((System.Windows.Controls.ListBox)saveSettings.FindName("PetAccentColorBox")).SelectedValue = "#EC4899";
        ((System.Windows.Controls.ListBox)saveSettings.FindName("PetDetailColorBox")).SelectedValue = "#FACC15";
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        Capture(petWindow, Path.Combine(artifacts, "companion-pet-custom.png"));
        static System.Windows.Controls.RadioButton? FindTagRadio(DependencyObject root, string tag)
        {
            if (root is System.Windows.Controls.RadioButton rb && Equals(rb.Tag, tag)) return rb;
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
                if (FindTagRadio(VisualTreeHelper.GetChild(root, i), tag) is { } found) return found;
            return null;
        }

        (FindTagRadio(saveSettings, "Face") ?? throw new Exception("Missing Companion Face tab.")).IsChecked = true;
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        var browsBox = (System.Windows.Controls.ListBox)saveSettings.FindName("PetBrowsBox");
        browsBox.SelectedValue = "happy";
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        Check(browsBox.IsVisible, "Companion Face tab must display Eyebrows selector.");
        Capture(saveSettings, Path.Combine(artifacts, "companion-studio-face.png"));

        (FindTagRadio(saveSettings, "Motion") ?? throw new Exception("Missing Companion Vibe tab.")).IsChecked = true;
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        var auraBox = (System.Windows.Controls.ListBox)saveSettings.FindName("PetAuraBox");
        auraBox.SelectedValue = "stars";
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        Check(auraBox.IsVisible, "Companion Vibe tab must display Aura selector.");
        Capture(saveSettings, Path.Combine(artifacts, "companion-studio-vibe.png"));

        ClickNamed(saveSettings, "Save");
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        Check(model.Settings.PetBody == "star" && model.Settings.PetEars == "bunny" &&
            model.Settings.PetBrows == "happy" && model.Settings.PetAura == "stars" &&
            model.Settings.PetHat == "crown" && model.Settings.PetBack == "angel" &&
            model.Settings.PetFeet == "hover" && model.Settings.PetPattern == "gradient" &&
            model.Settings.PetBodyColor == "#FCE7F3" && Math.Abs(model.Settings.PetSize - 1.25) < .01,
            "Saving companion settings must commit every customized field into Settings.");
        Check(FloatingPetStyle.CombinationCount >= 10_000_000_000_000L &&
            PetAppearance.FromWireCode(model.PetAppearance.ToWireCode()) == model.PetAppearance,
            "Expanded companion catalog and wire roundtrip must work cleanly.");

        model.PreviewPetAppearance(FloatingPetStyle.Presets.First(p => p.Name == "Mecha Ace").Appearance);
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        Capture(petWindow, Path.Combine(artifacts, "companion-pet-mecha.png"));
        model.PreviewPetAppearance(FloatingPetStyle.Presets.First(p => p.Name == "Cyber DJ").Appearance);
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        Capture(petWindow, Path.Combine(artifacts, "companion-pet-cyberdj.png"));
        model.PreviewPetAppearance(FloatingPetStyle.Presets.First(p => p.Name == "Shark").Appearance);
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        Capture(petWindow, Path.Combine(artifacts, "companion-pet-shark.png"));
        model.PreviewPetAppearance(null);

        // 4b. Clicking profile avatar opens expanded profile showing Bio + CydLan companion,
        // and omits companion UI for legacy LAN Messenger peers.
        async Task OpenAndVerifyProfileAsync(Peer peer, string screenshotName, bool expectCompanion, bool adoptCompanion)
        {
            model.SelectedPeer = peer;
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            Exception? failure = null;
            _ = Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(async () =>
            {
                PeerProfileWindow? modal = null;
                try
                {
                    modal = System.Windows.Application.Current.Windows.OfType<PeerProfileWindow>().SingleOrDefault()
                        ?? throw new Exception("Expanded peer profile window did not open.");
                    await Task.Delay(90);
                    Check(((System.Windows.Controls.TextBlock)modal.FindName("DisplayNameText")).Text == peer.Name,
                        "Expanded profile must show the peer's display name.");
                    Check(((System.Windows.Controls.TextBlock)modal.FindName("ClientText")).Text == peer.Version,
                        "Expanded profile must show available client information.");
                    var bioSection = (FrameworkElement)modal.FindName("BioSection");
                    Check(bioSection.Visibility == (peer.HasAbout ? Visibility.Visible : Visibility.Collapsed),
                        "Bio section visibility must match whether peer shared About text.");
                    if (peer.HasAbout)
                        Check(((System.Windows.Controls.TextBlock)modal.FindName("BioText")).Text == peer.About,
                            "Bio text must match the peer's About text.");

                    var companionSection = (FrameworkElement)modal.FindName("CompanionSection");
                    Check(companionSection.Visibility == (expectCompanion ? Visibility.Visible : Visibility.Collapsed),
                        expectCompanion
                            ? "CydLan peers with a companion code must show their Companion character."
                            : "Legacy LAN Messenger peers must hide Companion section and show only available fields.");

                    if (expectCompanion)
                    {
                        var petVisual = (FloatingPetVisual)modal.FindName("PeerPetVisual");
                        Check(petVisual.PetStyle == PetAppearance.FromWireCode(peer.PetCode),
                            "Profile showcase must render the peer's exact custom companion.");
                    }

                    Capture(modal, Path.Combine(artifacts, screenshotName));

                    if (adoptCompanion)
                    {
                        var useBtn = (System.Windows.Controls.Button)modal.FindName("UseCompanionButton");
                        Check(useBtn.IsEnabled, "Use companion button must be enabled when peer companion differs.");
                        useBtn.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
                        await Task.Delay(120);
                        Check(model.PetAppearance == PetAppearance.FromWireCode(peer.PetCode),
                            "Clicking Use companion must adopt the peer's companion.");
                    }
                }
                catch (Exception ex)
                {
                    failure = ex;
                }
                finally
                {
                    modal?.Close();
                }
            }));

            var avatarButton = (System.Windows.Controls.Button)window.FindName("ConversationAvatarButton");
            avatarButton.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            if (failure is not null) throw new Exception("Peer profile verification failed", failure);
        }

        var cydPeer = model.Peers.First(p => p.Id == "2");
        Check(cydPeer.HasAbout && cydPeer.HasPetCode, "Preview CydLan peer must include About and PetCode.");
        await OpenAndVerifyProfileAsync(cydPeer, "peer-profile-cydlan-tested.png", expectCompanion: true, adoptCompanion: true);

        var legacyPeer = model.Peers.First(p => p.Id == "1");
        Check(!LanService.IsCydLanVersion(legacyPeer.Version) && !legacyPeer.HasPetCode,
            "Preview legacy peer must represent old LAN Messenger without PetCode.");
        await OpenAndVerifyProfileAsync(legacyPeer, "peer-profile-legacy-tested.png", expectCompanion: false, adoptCompanion: false);

        // 5. Turning off Floating LAN closes both quick chat and pet cleanly.
        model.UpdateFloatingState(enabled: false);
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        Check(!petWindow.IsVisible && !quickChat.IsVisible,
            "Turning Floating LAN off must hide both the pet and quick chat windows.");

        window.Close();
        ThemeManager.Apply(ThemeManager.Light, AppearanceOverrides.Default);
        Console.WriteLine("PASS  Tray-only Floating LAN pet, companion studio customization, Look code persistence, and quick chat");
    }

    private static async Task TestMediaModuleSuiteAsync(string artifacts)
    {
        Check(MediaCatalog.IsAudioFile("song.mp3") && MediaCatalog.IsAudioFile("tone.wav") &&
            MediaCatalog.IsVideoFile("clip.mp4") && MediaCatalog.IsVideoFile("demo.wmv") &&
            !MediaCatalog.IsMediaFile("notes.txt") && !MediaCatalog.IsMediaFile("photo.png"),
            "MediaCatalog extension detection must distinguish playable audio, video, and non-media files.");

        string tempDir = Path.Combine(Path.GetTempPath(), "cydlan-media-test-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(tempDir);
        string wav1 = Path.Combine(tempDir, "Morning Synth.wav");
        string wav2 = Path.Combine(tempDir, "Lan Lo-Fi Beat.wav");
        WriteSampleWav(wav1, 1.5);
        WriteSampleWav(wav2, 1.2);

        try
        {
            await using var model = new MainViewModel(preview: true);
            ThemeManager.Apply(ThemeManager.Midnight, AppearanceOverrides.Default);
            var window = new MainWindow(model);
            window.Show();
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);

            var mediaNav = (System.Windows.Controls.Button)window.FindName("MediaNav");
            mediaNav.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);

            var mediaSection = (FrameworkElement)window.FindName("MediaSectionGrid");
            var miniCard = (FrameworkElement)window.FindName("MiniPlayerCard");
            var player = (System.Windows.Controls.MediaElement)window.FindName("MediaPlayerElement");
            Check(model.IsMediaSection && mediaSection.IsVisible && model.NoMediaShown && !miniCard.IsVisible && player.Source is null,
                "Media rail button should open the empty Media module with zero loaded player handle before playback.");
            Capture(window, Path.Combine(artifacts, "media-empty-tested.png"));

            await model.AddMediaPathsAsync([wav1, wav2], autoPlayFirst: true);
            await Until(() => model.IsMediaPlaying && player.Source is not null);
            await Task.Delay(250);
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);

            Check(model.AllMediaCount == 2 && model.AudioMediaCount == 2 && model.VideoMediaCount == 0 &&
                model.CurrentMedia?.Path == wav1 && model.IsCurrentAudio && model.IsCurrentAudioPlaying,
                "Adding media files should populate the queue and load the player lazily.");

            model.CycleMediaSpeed();
            Check(model.MediaSpeedLabel == "1.25x" && Math.Abs(player.SpeedRatio - 1.25) < .01,
                "Cycling media speed must update playback speed.");
            model.MediaVolume = .45;
            Check(Math.Abs(player.Volume - .45) < .01 && Math.Abs(model.Settings.MediaVolume - .45) < .01,
                "Volume adjustment must update the active player and settings.");
            Capture(window, Path.Combine(artifacts, "media-audio-playing-tested.png"));

            model.NextMedia();
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            Check(model.CurrentMedia?.Path == wav2, "Next button must advance to the next track cleanly.");

            // Return to Chats while music plays: sidebar Mini Player should appear without displacing chat.
            var chatNav = (System.Windows.Controls.Button)window.FindName("ChatNav");
            chatNav.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            Check(model.ChatOpen && model.ShowMiniPlayer && miniCard.IsVisible,
                "Returning to Chats during playback must display the compact sidebar mini player.");
            Capture(window, Path.Combine(artifacts, "media-miniplayer-chat-tested.png"));

            model.TogglePlayPause();
            Check(!model.IsMediaPlaying, "TogglePlayPause must pause playback.");
            model.StopMedia();
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            Check(!model.HasCurrentMedia && !miniCard.IsVisible && player.Source is null,
                "Stopping media must clear current track, hide mini player, and release MediaElement.Source.");

            window.Close();
            ThemeManager.Apply(ThemeManager.Light, AppearanceOverrides.Default);
            Console.WriteLine("PASS  Lightweight Media module (rail stage, queue, lazy MediaElement, seek/speed/volume, mini player)");
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    private static void WriteSampleWav(string path, double seconds)
    {
        const int sampleRate = 8000;
        int samples = (int)(sampleRate * seconds);
        using var fs = File.Create(path);
        using var bw = new BinaryWriter(fs);
        bw.Write("RIFF"u8);
        bw.Write(36 + samples * 2);
        bw.Write("WAVE"u8);
        bw.Write("fmt "u8);
        bw.Write(16);
        bw.Write((short)1);
        bw.Write((short)1);
        bw.Write(sampleRate);
        bw.Write(sampleRate * 2);
        bw.Write((short)2);
        bw.Write((short)16);
        bw.Write("data"u8);
        bw.Write(samples * 2);
        for (int i = 0; i < samples; i++)
            bw.Write((short)(Math.Sin(i * 2.0 * Math.PI * 440.0 / sampleRate) * 2400));
    }

    private static void ClickContentButton(DependencyObject root, string content)
    {
        System.Windows.Controls.Button? Find(DependencyObject d)
        {
            if (d is System.Windows.Controls.Button b && Equals(b.Content, content)) return b;
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(d); i++) if (Find(VisualTreeHelper.GetChild(d, i)) is { } f) return f;
            return null;
        }
        (Find(root) ?? throw new Exception("No button with content " + content)).RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
    }

    private static void TypeInComposer(System.Windows.Controls.TextBox composer, string text)
    {
        Check(composer.IsKeyboardFocused, "The test composer must have keyboard focus.");
        System.Windows.Input.TextCompositionManager.StartComposition(
            new System.Windows.Input.TextComposition(System.Windows.Input.InputManager.Current, composer, text));
    }

    private static async Task TestOfflinePagingAsync(string directory)
    {
        var store = new LocalStore(directory);
        store.SaveSettings(new AppSettings
        {
            EffectIntensity = .55, PetalIntensity = .4, EngineIntensity = .9, SakuraPetalsEnabled = true, SteampunkEngineEnabled = true,
            FirefliesEnabled = true, FireflyIntensity = .9, RainEnabled = true, RainIntensity = .5, SnowEnabled = true, SnowIntensity = .8,
            StarfieldEnabled = true, StarfieldIntensity = .7, BubblesEnabled = true, BubblesIntensity = .6, EmbersEnabled = true, EmbersIntensity = .75,
            TypingEffect = "ink", InkBloomIntensity = .35, SendPulseEnabled = true, SendPulseIntensity = .45,
            HeaderAuroraEnabled = true, HeaderAuroraIntensity = .8, HeaderPulseEnabled = true, HeaderPulseIntensity = .7, TitleSparklesEnabled = true, TitleSparklesIntensity = .4,
            RailBeaconEnabled = true, RailBeaconIntensity = .75, RailStreamEnabled = true, RailStreamIntensity = .65, RailStardustEnabled = true, RailStardustIntensity = .55, RailScanEnabled = true, RailScanIntensity = .85,
            SidebarMotesEnabled = true, SidebarMotesIntensity = .7, SidebarGlowEnabled = true, SidebarGlowIntensity = .5,
            SidebarPrismEnabled = true, SidebarPrismIntensity = .6, SidebarSweepEnabled = true, SidebarSweepIntensity = .7,
            AmbientOnSidebarEnabled = true,
            ChromeTexture = ChromeTexture.Silk, ChromeTextureIntensity = .8,
            TargetAura = TargetAura.Fire, TargetAuraIntensity = .85, AuraSelectedModuleEnabled = true, AuraSelectedPersonEnabled = false, AuraHighlightButtonsEnabled = true,
            ButtonGlowEnabled = true, ButtonGlowIntensity = .8, ButtonSheenEnabled = true, ButtonSheenIntensity = .65, ButtonBounceEnabled = true, ButtonBounceIntensity = .7, ButtonRingEnabled = true, ButtonRingIntensity = .9,
            ButtonPulseEnabled = true, ButtonPulseIntensity = .75, ButtonLiftEnabled = true, ButtonLiftIntensity = .7, ButtonSparkEnabled = true, ButtonSparkIntensity = .85,
            TitleShimmerEnabled = true, TitleShimmerIntensity = .7, TitleCometEnabled = true, TitleCometIntensity = .75, TitleEqualizerEnabled = true, TitleEqualizerIntensity = .8, TitleLaserEnabled = true, TitleLaserIntensity = .85,
            HeaderPrismEnabled = true, HeaderPrismIntensity = .75, HeaderRipplesEnabled = true, HeaderRipplesIntensity = .7, HeaderCircuitEnabled = true, HeaderCircuitIntensity = .8, HeaderStarlightEnabled = true, HeaderStarlightIntensity = .65,
            CustomAccentColor = "#EC4899", CustomEffectColor = "#00FFAA", CustomOutgoingBubbleColor = "#6366F1", CustomIncomingBubbleColor = "#3B82F6",
            BubbleStyle = AppearanceOptions.BubbleGlass, BubbleShape = AppearanceOptions.ShapeTail, ChatDensity = AppearanceOptions.DensityCompact,
            MessageFontSizePx = 18, ChatCanvasGradient = ChatCanvasOverlay.Aurora, ChatCanvasGradientIntensity = .75, ChatBackgroundBlur = 6,
            ChatBackgroundTransition = BackgroundSideTransition.Left, ChatBackgroundTransitionStrength = .70,
            CornerStyle = AppearanceOptions.CornerRound, AvatarShape = AppearanceOptions.AvatarSquircle, OnlineRingEnabled = true,
            UiFont = "Consolas", WindowOpacity = .94, EffectSpeed = 1.4, MessageAnimation = MessageMotion.Slide,
            MatrixEnabled = true, MatrixIntensity = .7, ConstellationEnabled = true, ConstellationIntensity = .6, BokehEnabled = true, BokehIntensity = .8,
            WingPlumesEnabled = true, WingPlumesIntensity = .75, RepulsorEnabled = true, RepulsorIntensity = .85,
            TitleWingsEnabled = true, TitleWingsIntensity = .8, TitleReactorEnabled = true, TitleReactorIntensity = .7,
            HeaderBurstEnabled = true, HeaderBurstIntensity = .85, HeaderArcEnabled = true, HeaderArcIntensity = .75,
            RailWingsEnabled = true, RailWingsIntensity = .7, RailArcEnabled = true, RailArcIntensity = .8,
            SidebarWingsEnabled = true, SidebarWingsIntensity = .75, SidebarHudEnabled = true, SidebarHudIntensity = .85,
            SavedPresets = [new AppearancePreset { Id = "p1", Name = "Studio", Code = "CYDLAN1:test" }]
        });
        store.SavePeers([new Peer { Id = "peer", Name = "Offline contact" }, new Peer { Id = "second", Name = "Other contact" }]);
        store.SaveHistory(Enumerable.Range(1, 125).Select(i => new ChatMessage { PeerId = "peer", Text = $"Message {i:000}" })
            .Append(new ChatMessage { PeerId = "second", Text = "Other chat" }));
        store.SaveTransfers(Enumerable.Range(1, 105).Select(i => new FileTransfer
        {
            Id = i.ToString(), PeerId = "peer", Outgoing = i <= 100, Name = $"File {i:000}", State = "Received"
        }));
        await using var model = new MainViewModel(localStore: store);
        Check(model.EffectIntensity == .55 && model.PetalIntensity == .4 && model.EngineIntensity == .9 &&
            model.SakuraPetalsEnabled && model.SteampunkEngineEnabled,
            "Appearance intensity and effects should survive restart.");
        Check(model.TypingIntensity == .55 && model.ClickIntensity == .55,
            "Older profiles should keep their shared intensity for both new controls.");
        Check(model.Settings is
        {
            FirefliesEnabled: true, FireflyIntensity: .9, RainEnabled: true, RainIntensity: .5, SnowEnabled: true, SnowIntensity: .8,
            StarfieldEnabled: true, StarfieldIntensity: .7, BubblesEnabled: true, BubblesIntensity: .6, EmbersEnabled: true, EmbersIntensity: .75,
            TypingEffect: "ink", InkBloomIntensity: .35, SendPulseEnabled: true, SendPulseIntensity: .45,
            HeaderAuroraEnabled: true, HeaderAuroraIntensity: .8, HeaderPulseEnabled: true, HeaderPulseIntensity: .7, TitleSparklesEnabled: true, TitleSparklesIntensity: .4,
            RailBeaconEnabled: true, RailBeaconIntensity: .75, RailStreamEnabled: true, RailStreamIntensity: .65, RailStardustEnabled: true, RailStardustIntensity: .55, RailScanEnabled: true, RailScanIntensity: .85,
            SidebarMotesEnabled: true, SidebarMotesIntensity: .7, SidebarGlowEnabled: true, SidebarGlowIntensity: .5,
            SidebarPrismEnabled: true, SidebarPrismIntensity: .6, SidebarSweepEnabled: true, SidebarSweepIntensity: .7,
            AmbientOnSidebarEnabled: true,
            ChromeTexture: "silk", ChromeTextureIntensity: .8,
            TargetAura: "fire", TargetAuraIntensity: .85, AuraSelectedModuleEnabled: true, AuraSelectedPersonEnabled: false, AuraHighlightButtonsEnabled: true,
            ButtonGlowEnabled: true, ButtonGlowIntensity: .8, ButtonSheenEnabled: true, ButtonSheenIntensity: .65, ButtonBounceEnabled: true, ButtonBounceIntensity: .7, ButtonRingEnabled: true, ButtonRingIntensity: .9,
            ButtonPulseEnabled: true, ButtonPulseIntensity: .75, ButtonLiftEnabled: true, ButtonLiftIntensity: .7, ButtonSparkEnabled: true, ButtonSparkIntensity: .85,
            TitleShimmerEnabled: true, TitleShimmerIntensity: .7, TitleCometEnabled: true, TitleCometIntensity: .75, TitleEqualizerEnabled: true, TitleEqualizerIntensity: .8, TitleLaserEnabled: true, TitleLaserIntensity: .85,
            HeaderPrismEnabled: true, HeaderPrismIntensity: .75, HeaderRipplesEnabled: true, HeaderRipplesIntensity: .7, HeaderCircuitEnabled: true, HeaderCircuitIntensity: .8, HeaderStarlightEnabled: true, HeaderStarlightIntensity: .65,
            CustomAccentColor: "#EC4899", CustomEffectColor: "#00FFAA", CustomOutgoingBubbleColor: "#6366F1", CustomIncomingBubbleColor: "#3B82F6",
            BubbleStyle: "glass", BubbleShape: "tail", ChatDensity: "compact",
            MessageFontSizePx: 18, ChatCanvasGradient: "aurora", ChatCanvasGradientIntensity: .75, ChatBackgroundBlur: 6,
            ChatBackgroundTransition: "left", ChatBackgroundTransitionStrength: .70,
            CornerStyle: "round", AvatarShape: "squircle", OnlineRingEnabled: true,
            UiFont: "Consolas", WindowOpacity: .94, EffectSpeed: 1.4, MessageAnimation: "slide",
            MatrixEnabled: true, MatrixIntensity: .7, ConstellationEnabled: true, ConstellationIntensity: .6, BokehEnabled: true, BokehIntensity: .8,
            WingPlumesEnabled: true, WingPlumesIntensity: .75, RepulsorEnabled: true, RepulsorIntensity: .85,
            TitleWingsEnabled: true, TitleWingsIntensity: .8, TitleReactorEnabled: true, TitleReactorIntensity: .7,
            HeaderBurstEnabled: true, HeaderBurstIntensity: .85, HeaderArcEnabled: true, HeaderArcIntensity: .75,
            RailWingsEnabled: true, RailWingsIntensity: .7, RailArcEnabled: true, RailArcIntensity: .8,
            SidebarWingsEnabled: true, SidebarWingsIntensity: .75, SidebarHudEnabled: true, SidebarHudIntensity: .85
        } && model.Settings.SavedPresets.Count == 1 && model.Settings.SavedPresets[0].Name == "Studio", "The new effects and their settings should survive restart.");
        model.SelectedPeer = model.Peers[0];
        Check(model.Messages.Count == 40 && model.Messages[0].Text == "Message 086" && model.HasOlderMessages && !model.HasNewerMessages,
            "Opening chat should only populate the latest message page.");
        model.PageMessages(older: true);
        Check(model.Messages[0].Text == "Message 046" && model.Messages[^1].Text == "Message 085" && model.HasNewerMessages,
            "Previous message pages should appear in chronological order.");
        model.PageMessages(older: true);
        model.PageMessages(older: true);
        Check(model.Messages.Count == 5 && !model.HasOlderMessages && model.Messages[0].Text == "Message 001",
            "History must remain reachable all the way to the first message.");
        model.PageMessages(older: false);
        Check(model.Messages.Count == 40 && model.Messages[0].Text == "Message 006", "Moving forward from a partial page should skip no messages.");
        model.MessageSearch = "Message 002";
        Check(model.Messages.Single().Text == "Message 002", "Search must find messages outside the loaded page.");
        model.MessageSearch = "";
        model.SelectedPeer = model.Peers[1];
        Check(model.Messages.Single().Text == "Other chat", "Changing chats should reset paging.");
        model.SelectedPeer = model.Peers[0];
        Check(model.Messages[0].Text == "Message 086" && model.ConversationFiles.Count == 0,
            "Reopening a chat should start at its latest page without populating the files panel.");
        var window = new MainWindow(model);
        window.Show();
        ClickNamed(window, "Files in this conversation");
        model.ConversationFilesSent = true;
        Check(model.ConversationFiles.Count == 40 && model.HasOlderFiles && model.ConversationSentCount == 100,
            "The sent files panel should page results while showing the total count.");
        model.PageConversationFiles(older: true);
        Check(model.ConversationFiles[0].Name == "File 041", "The next file page should show the next set of results.");
        model.ConversationFilesSent = false;
        Check(model.ConversationFiles.Count == 5 && !model.HasOlderFiles && !model.HasNewerFiles,
            "Switching the file direction should reset paging.");
        ClickNamed(window, "Files in this conversation");
        Check(model.ConversationFiles.Count == 0, "Closing the files panel should release its displayed rows.");
        window.Close();
        Console.WriteLine("PASS  Offline stored history, message search, and on-demand conversation files");
    }

    private static async Task TestIsolatedAccountsAsync(string directory)
    {
        // Use distinct TCP/UDP ports and data directories for three desktop identities.
        var profiles = new[] { ("Test Alice", "ocean"), ("Test Bob", "pixel"), ("Test Carol", "ember") };
        var stores = profiles.Select(profile => new LocalStore(Path.Combine(directory, profile.Item1))).ToArray();
        using var alicePort = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        using var bobPort = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        using var carolPort = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        var reservations = new[] { alicePort, bobPort, carolPort };
        for (int i = 0; i < profiles.Length; i++)
        {
            var (name, theme) = profiles[i];
            stores[i].SaveSettings(new() {
                DisplayName = name, Theme = theme,
                About = i == 0 ? "LAN designer" : "",
                PetBody = i == 0 ? "heart" : "squircle",
                PetBrows = i == 0 ? "focused" : "none",
                PetAura = i == 0 ? "ring" : "none",
                Port = ((IPEndPoint)reservations[i].Client.LocalEndPoint!).Port,
                Notifications = false, Sounds = false
            });
        }
        foreach (var socket in reservations) socket.Dispose();

        await using var alice = new MainViewModel(localStore: stores[0]);
        await using var bob = new MainViewModel(localStore: stores[1]);
        await using var carol = new MainViewModel(localStore: stores[2]);
        var accounts = new[] { alice, bob, carol };
        Check(accounts.Select(account => account.Settings.UserId).Distinct().Count() == 3, "Account identities must stay separate.");
        foreach (var account in accounts) await account.StartAsync(true, IPAddress.Loopback);
        await alice.ConnectByAddressAsync($"127.0.0.1:{bob.Settings.Port}");
        await alice.ConnectByAddressAsync($"127.0.0.1:{carol.Settings.Port}");
        await bob.ConnectByAddressAsync($"127.0.0.1:{carol.Settings.Port}");
        await Until(() => accounts.All(account => account.OnlineCount == 2));
        await Until(() =>
        {
            var p = bob.Peers.Single(peer => peer.Id == alice.Settings.UserId);
            return p.About == "LAN designer" && p.HasPetCode;
        });
        var aliceFromBob = bob.Peers.Single(peer => peer.Id == alice.Settings.UserId);
        Check(PetAppearance.FromWireCode(aliceFromBob.PetCode) is { Body: "heart", Brows: "focused", Aura: "ring" },
            "Bob must receive Alice's custom companion setup over the desktop LAN session.");
        for (int i = 0; i < accounts.Length; i++)
        {
            var sender = accounts[i];
            var receiver = accounts[(i + 1) % accounts.Length];
            sender.SelectedPeer = sender.Peers.Single(peer => peer.Id == receiver.Settings.UserId);
            receiver.SelectedPeer = receiver.Peers.Single(peer => peer.Id == sender.Settings.UserId);
            string text = $"{sender.ProfileName} to {receiver.ProfileName}";
            sender.Draft = text;
            await sender.SendAsync();
            await Until(() => receiver.Messages.Any(message => message.Text == text));
            Check(sender.Messages.Single(message => message.Text == text).Delivery == "Delivered" &&
                receiver.Messages.Count(message => message.Text == text) == 1,
                "Expected one delivered message in both conversations.");
            var other = accounts.Single(account => account != sender && account != receiver);
            Check(!other.Peers.Any(peer => peer.Preview == text), "A direct message reached the third account.");
            Check(stores[i].LoadSettings().Theme == profiles[i].Item2, "Account themes must stay separate on disk.");
        }
        Console.WriteLine("PASS  Three isolated desktop accounts discover each other, exchange chat and retain independent themes");
    }

    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool IsIconic(IntPtr window);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")] private static extern int GetWindowLong(IntPtr window, int index);

    private static List<System.Windows.Controls.Button> FindButtons(DependencyObject root, string automationName)
    {
        var matches = new List<System.Windows.Controls.Button>();
        void Walk(DependencyObject d)
        {
            if (d is System.Windows.Controls.Button b && System.Windows.Automation.AutomationProperties.GetName(b) == automationName) matches.Add(b);
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(d); i++) Walk(VisualTreeHelper.GetChild(d, i));
        }
        Walk(root);
        return matches;
    }

    private static void ClickNamed(DependencyObject root, string automationName) =>
        (FindButtons(root, automationName).FirstOrDefault() ?? throw new Exception("No button named " + automationName)).RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));

    private static System.Windows.Controls.TextBlock? FindText(DependencyObject root, string text)
    {
        if (root is System.Windows.Controls.TextBlock block && block.Text == text) return block;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            if (FindText(VisualTreeHelper.GetChild(root, i), text) is { } found) return found;
        return null;
    }

    private static System.Windows.Controls.Image? FindChatImage(DependencyObject root)
    {
        if (root is System.Windows.Controls.Image { IsVisible: true, ContextMenu: not null } image) return image;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            if (FindChatImage(VisualTreeHelper.GetChild(root, i)) is { } found) return found;
        return null;
    }

    private static System.Windows.Controls.ProgressBar? FindTransferProgress(DependencyObject root, FileTransfer transfer)
    {
        if (root is System.Windows.Controls.ProgressBar bar && ReferenceEquals(bar.DataContext, transfer)) return bar;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            if (FindTransferProgress(VisualTreeHelper.GetChild(root, i), transfer) is { } found) return found;
        return null;
    }

    private static void Capture(Window window, string path)
    {
        window.UpdateLayout();
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(window.ActualWidth), (int)Math.Ceiling(window.ActualHeight), 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(window);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var file = File.Create(path); encoder.Save(file);
    }
}
