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
            Check(ThemeManager.Catalog.Count == 21 && themeBox.Items.Count == 21,
                $"Expected 21 theme sample boxes (20 themes + Match Windows), found {ThemeManager.Catalog.Count}/{themeBox.Items.Count}.");
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
        var reservation = new TcpListener(IPAddress.Loopback, 0); reservation.Start();
        int port = ((IPEndPoint)reservation.LocalEndpoint).Port; reservation.Stop();
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
                await Until(() => { lock (peerTransfers) return peerTransfers.Any(t => t.State == "Received"); });
                var inbound = peerTransfers.Single();
                Check(inbound.Inline && File.ReadAllBytes(inbound.Path).SequenceEqual(File.ReadAllBytes(png)), "The CydLan peer should auto-receive the exact image inline.");
                Check(Path.GetDirectoryName(inbound.Path) == peerImages, "Inline images must be stored only in the receiver's image folder.");
                await Until(() => model.Messages.Last().Delivery == "Sent");
                Check(model.Messages.Last().HasImage && File.Exists(model.Messages.Last().ImagePath) && model.Transfers.All(t => t.Inline), "The sent image should appear in the chat (and be listed in Files as inline).");
                await peer.OfferFileAsync(model.Settings.UserId, png, inlineImage: true);
                await Until(() => model.Messages.Last() is { HasImage: true, Outgoing: false });
                Check(File.Exists(model.Messages.Last().ImagePath) && model.Transfers.All(t => t.Inline), "An inline image from a CydLan peer should appear in the conversation.");
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
                var loading = new FileTransfer
                {
                    Id = "progress-ui-test", PeerId = model.SelectedPeer!.Id, Name = "Test attachment.bin",
                    Size = 100, State = "Accepted"
                };
                model.Transfers.Insert(0, loading);
                await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
                var progress = FindTransferProgress(window, loading);
                Check(progress is { IsVisible: true, IsIndeterminate: true }, "Accepted attachments should show connecting progress in chat.");
                loading.Progress = 40;
                loading.State = "Transferring";
                await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
                Check(progress is { IsIndeterminate: false, Value: 40 } && FindText(window, "Receiving…") is { IsVisible: true },
                    "An incoming attachment should show its live byte progress.");
                loading.State = "Received";
                await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
                Check(progress is { IsVisible: false }, "Completed attachments should stop showing loading.");
                model.Transfers.Remove(loading);
                string artifacts = Path.GetFullPath("artifacts"); Directory.CreateDirectory(artifacts);
                Capture(window, Path.Combine(artifacts, "live-chat-tested.png"));
                ClickNamed(window, "Files in this conversation");
                await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
                Check(((FrameworkElement)window.FindName("FilesPanel")).IsVisible, "The files button should open the files panel.");
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
                Check(previewImage.IsVisible && previewImage.Source == background.Source && opacityControl.IsVisible &&
                    opacityControl.Value == .4 && opacityControl.Template.FindName("PART_Track", opacityControl) is not null,
                    "The background settings should preview the saved background with its own opacity control.");
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
            Directory.Delete(full, true);
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
        Check(themeBox.IsVisible && themeBox.Items.Count == 21, "The visual theme sample boxes should be shown directly in Appearance.");
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
        Check(themeBox.Items.Count == 11, $"Expected 11 dark themes, got {themeBox.Items.Count}.");
        ((System.Windows.Controls.RadioButton)preferences.FindName("ThemeFilterLight")).IsChecked = true;
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        Check(themeBox.Items.Count == 9 && (string?)themeBox.SelectedValue == ThemeManager.Sakura,
            $"Expected 9 light themes keeping Sakura selected, got {themeBox.Items.Count} / {themeBox.SelectedValue}.");
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
            (ThemeManager.Matcha, DecorationDesign.Matcha), (ThemeManager.Frost, DecorationDesign.Frost)
        })
        {
            ThemeManager.Apply(theme);
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            Check(new[] { sidebar, rail, title, header, composer }.All(surface => surface.IsVisible && !surface.IsHitTestVisible && surface.Design == design),
                $"Theme surfaces should follow {theme} without intercepting chat controls.");
            if (theme is ThemeManager.Sakura or ThemeManager.Steampunk or ThemeManager.Forest or ThemeManager.Paper or
                ThemeManager.Anime or ThemeManager.Aurora or ThemeManager.Arcade or ThemeManager.Ocean or
                ThemeManager.Cyberpunk or ThemeManager.Midnight or ThemeManager.Sunset or ThemeManager.Mint or
                ThemeManager.Matcha or ThemeManager.Frost)
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
        var titleSparkles = (HeaderEffect)window.FindName("TitleBarSparkles");
        var railBeacon = (RailEffect)window.FindName("RailBeaconEffect");
        var railStream = (RailEffect)window.FindName("RailStreamEffect");
        var railStardust = (RailEffect)window.FindName("RailStardustEffect");
        var railScan = (RailEffect)window.FindName("RailScanEffect");
        var sidebarMotes = (SidebarEffect)window.FindName("SidebarMotesEffect");
        var sidebarGlow = (SidebarEffect)window.FindName("SidebarEdgeGlowEffect");
        var sidebarPrism = (SidebarEffect)window.FindName("SidebarPrismEffect");
        var sidebarSweep = (SidebarEffect)window.FindName("SidebarSweepEffect");
        var sidebarRain = (WeatherEffect)window.FindName("SidebarRain");
        var sidebarStarfield = (WeatherEffect)window.FindName("SidebarStarfield");
        var sidebarBubbles = (WeatherEffect)window.FindName("SidebarBubbles");
        var sidebarEmbers = (WeatherEffect)window.FindName("SidebarEmbers");
        var contentStarfield = (WeatherEffect)window.FindName("ConversationStarfield");
        var contentBubbles = (WeatherEffect)window.FindName("ConversationBubbles");
        var contentEmbers = (WeatherEffect)window.FindName("ConversationEmbers");
        var ambientEffects = new AmbientEffect[]
        {
            headerAurora, headerPulse, titleSparkles,
            railBeacon, railStream, railStardust, railScan,
            sidebarMotes, sidebarGlow, sidebarPrism, sidebarSweep,
            sidebarRain, sidebarStarfield, sidebarBubbles, sidebarEmbers,
            contentStarfield, contentBubbles, contentEmbers
        };
        Check(ambientEffects.All(effect => !effect.IsVisible) &&
            !ButtonEffects.GlowEnabled && !ButtonEffects.SheenEnabled && !ButtonEffects.BounceEnabled && !ButtonEffects.RingEnabled,
            "All header, rail, side panel, ambient, and highlight button effects must be opt-in.");

        var preferences = new SettingsWindow(model) { Owner = window };
        preferences.Show();
        preferences.ShowPage("AppearancePage");
        ((System.Windows.Controls.ListBox)preferences.FindName("ThemeBox")).SelectedValue = ThemeManager.Aurora;
        ((System.Windows.Controls.CheckBox)preferences.FindName("ThemeDetailsBox")).IsChecked = true;

        var defaultPreviews = new (string Tab, string[] Previews)[]
        {
            ("Headers", ["EnginePreview", "HeaderAuroraPreview", "TitleSparklesPreview", "HeaderPulsePreview"]),
            ("Panels", ["RailBeaconPreview", "RailStreamPreview", "RailStardustPreview", "RailScanPreview", "SidebarMotesPreview", "SidebarGlowPreview", "SidebarPrismPreview", "SidebarSweepPreview"]),
            ("Ambient", ["PetalsPreview", "StarfieldPreview", "BubblesPreview", "EmbersPreview"]),
            ("Aura", ["ButtonPreviewPrimary", "ButtonPreviewNav", "ButtonPreviewSend"])
        };
        foreach (var (tab, previews) in defaultPreviews)
        {
            preferences.ShowAppearanceTab(tab);
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            Check(previews.All(name => ((FrameworkElement)preferences.FindName(name)).IsVisible),
                $"All effect previews in the {tab} tab must be visible by default without checking a box.");
        }

        foreach (string name in new[]
        {
            "HeaderAurora", "HeaderPulse", "TitleSparkles",
            "RailBeacon", "RailStream", "RailStardust", "RailScan",
            "SidebarMotes", "SidebarGlow", "SidebarPrism", "SidebarSweep",
            "Rain", "Starfield", "Bubbles", "Embers", "AmbientOnSidebar",
            "ButtonGlow", "ButtonSheen", "ButtonBounce", "ButtonRing"
        })
            ((System.Windows.Controls.CheckBox)preferences.FindName(name + "Box")).IsChecked = true;
        foreach (var (name, value) in new[]
        {
            ("HeaderAurora", .8), ("HeaderPulse", .7), ("TitleSparkles", .55),
            ("RailBeacon", .75), ("RailStream", .7), ("RailStardust", .65), ("RailScan", .6),
            ("SidebarMotes", .75), ("SidebarGlow", .65), ("SidebarPrism", .7), ("SidebarSweep", .65),
            ("Starfield", .8), ("Bubbles", .7), ("Embers", .75),
            ("ButtonGlow", .85), ("ButtonSheen", .8), ("ButtonBounce", .7), ("ButtonRing", .85)
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
            headerAurora.Intensity == .8 && headerPulse.Intensity == .7 && titleSparkles.Intensity == .55 &&
            railBeacon.Intensity == .75 && railStream.Intensity == .7 && railStardust.Intensity == .65 && railScan.Intensity == .6 &&
            sidebarMotes.Intensity == .75 && sidebarGlow.Intensity == .65 && sidebarPrism.Intensity == .7 && sidebarSweep.Intensity == .65 &&
            contentStarfield.Intensity == .8 && contentBubbles.Intensity == .7 && contentEmbers.Intensity == .75 &&
            ButtonEffects.GlowEnabled && ButtonEffects.SheenEnabled && ButtonEffects.BounceEnabled && ButtonEffects.RingEnabled,
            "All 5 UI zone effects should activate with independent intensities and the active theme colour.");
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
        Check(ChromeTexture.Catalog.Count == 7, $"Expected 6 background textures + None, found {ChromeTexture.Catalog.Count}.");
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
        Check(textureBox.IsVisible && textureBox.Items.Count == 7 && (string)textureBox.SelectedValue == ChromeTexture.Off,
            "All 6 textures + None should appear directly on the Textures tab.");

        ((System.Windows.Controls.ListBox)preferences.FindName("ThemeBox")).SelectedValue = ThemeManager.Cyberpunk;
        textureBox.SelectedValue = ChromeTexture.Silk;
        intensityBox.Value = .85;
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        Check(textureLayer.IsVisible && textureLayer.Kind == ChromeTextureKind.Silk && textureLayer.Intensity == .85,
            "Selecting a chrome texture should live-preview across the unified shell.");
        Capture(preferences, Path.Combine(artifacts, "appearance-texture-prefs-tested.png"));
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

        foreach (var (theme, texture, expectedKind) in new[]
        {
            (ThemeManager.Cyberpunk, ChromeTexture.Hex, ChromeTextureKind.Hex),
            (ThemeManager.Midnight, ChromeTexture.Silk, ChromeTextureKind.Silk),
            (ThemeManager.Aurora, ChromeTexture.Topo, ChromeTextureKind.Topo),
            (ThemeManager.Frost, ChromeTexture.Grid, ChromeTextureKind.Grid),
            (ThemeManager.Matcha, ChromeTexture.Woven, ChromeTextureKind.Woven),
            (ThemeManager.Dark, ChromeTexture.Grain, ChromeTextureKind.Grain)
        })
        {
            await model.SavePreferencesAsync(s => { s.Theme = theme; s.ChromeTexture = texture; s.ChromeTextureIntensity = .78; });
            ThemeManager.Apply(theme);
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            Check(textureLayer.IsVisible && textureLayer.Kind == expectedKind, $"Chrome texture {texture} should render cleanly in {theme}.");
            Capture(window, Path.Combine(artifacts, $"chrome-texture-{theme}-{texture}-tested.png"));
        }

        window.Close();
        ThemeManager.Apply(ThemeManager.Light);
        Console.WriteLine("PASS  Uniform chrome shell with 6 live theme-adaptive background textures");
    }

    private static async Task TestTargetAuraAsync(string artifacts)
    {
        Check(TargetAura.Catalog.Count == 6, $"Expected 5 target aura styles + None, found {TargetAura.Catalog.Count}.");
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
        Check(auraBox.IsVisible && auraBox.Items.Count == 6 && (string)auraBox.SelectedValue == TargetAura.Off,
            "All 5 aura styles + None should appear directly on the Aura tab.");

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
            (ThemeManager.Frost, ChromeTexture.Grid, TargetAura.Arcane, TargetAuraKind.Arcane)
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
            ButtonGlowEnabled = true, ButtonGlowIntensity = .8, ButtonSheenEnabled = true, ButtonSheenIntensity = .65, ButtonBounceEnabled = true, ButtonBounceIntensity = .7, ButtonRingEnabled = true, ButtonRingIntensity = .9
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
            ButtonGlowEnabled: true, ButtonGlowIntensity: .8, ButtonSheenEnabled: true, ButtonSheenIntensity: .65, ButtonBounceEnabled: true, ButtonBounceIntensity: .7, ButtonRingEnabled: true, ButtonRingIntensity: .9
        }, "The new effects and their settings should survive restart.");
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

    private static void ClickNamed(DependencyObject root, string automationName)
    {
        System.Windows.Controls.Button? Find(DependencyObject d)
        {
            if (d is System.Windows.Controls.Button b && System.Windows.Automation.AutomationProperties.GetName(b) == automationName) return b;
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(d); i++) if (Find(VisualTreeHelper.GetChild(d, i)) is { } f) return f;
            return null;
        }
        (Find(root) ?? throw new Exception("No button named " + automationName)).RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
    }

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
