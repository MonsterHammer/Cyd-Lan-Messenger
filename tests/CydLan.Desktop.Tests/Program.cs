using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Windows;
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
    private static int Main()
    {
        int exitCode = 0;
        TestApplication? app = null;
        app = new TestApplication { RunTests = async () => {
            try { await RunAsync(); }
            catch (Exception e) { Console.WriteLine("FAIL  " + e); exitCode = 1; }
            finally { await app!.QuitAsync(); }
        } };
        app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        app.Resources = new ResourceDictionary { Source = new Uri("/CydLan;component/Theme.xaml", UriKind.Relative) };
        app.Run();
        return exitCode;
    }

    private static void Check(bool ok, string reason) { if (!ok) throw new Exception(reason); }
    private static async Task Until(Func<bool> predicate)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        while (!predicate()) await Task.Delay(15, deadline.Token);
    }

    private static async Task RunAsync()
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
            Console.WriteLine("PASS  Offline preview, separate contact/broadcast drafts, honest receipts");

            // Switching theme while windows are open must recolour inherited text, not only explicit brushes.
            var themed = new MainWindow(preview); themed.Show();
            var prefs = new SettingsWindow(preview) { Owner = themed }; prefs.Show();
            foreach (var (mode, ink) in new[] { ("dark", "#FFECECEC"), ("light", "#FF1D2326"), ("dark", "#FFECECEC") })
            {
                ThemeManager.Apply(mode);
                await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
                foreach (Window w in new Window[] { themed, prefs })
                {
                    string actual = ((SolidColorBrush)w.Foreground).Color.ToString();
                    Check(actual == ink, $"{w.GetType().Name} text stayed {actual} after switching to {mode}.");
                    var heading = w is SettingsWindow ? FindText(w, "Preferences") : (System.Windows.Controls.TextBlock)w.FindName("ConversationTitle");
                    string inherited = ((SolidColorBrush)heading!.Foreground).Color.ToString();
                    Check(inherited == ink, $"{w.GetType().Name} inherited text stayed {inherited} after switching to {mode}.");
                }
            }
            prefs.Close(); themed.Close(); ThemeManager.Apply("light");
            Console.WriteLine("PASS  Live light/dark switching recolours open windows");

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
            ClickNamed(motion, "Toggle conversations sidebar"); await Task.Delay(100);
            double midWidth = ((FrameworkElement)motion.FindName("SidebarPanel")).ActualWidth;
            Check(midWidth > 0 && midWidth < openWidth, $"Sidebar should be mid-slide after 100 ms (was {midWidth} of {openWidth}).");
            await Task.Delay(400);
            Check(sidebar.Visibility == Visibility.Collapsed, "Sidebar did not finish closing.");
            ClickNamed(motion, "Toggle conversations sidebar"); await Task.Delay(500);
            Check(Math.Abs(sidebar.ActualWidth - openWidth) < 0.5, $"Sidebar reopened to {sidebar.ActualWidth}, expected {openWidth}.");
            var pill = (FrameworkElement)motion.FindName("SearchPill");
            ClickNamed(motion, "Search this conversation"); await Task.Delay(450);
            Check(Math.Abs(pill.ActualWidth - 220) < 0.5, $"Search should expand in place to 220 (was {pill.ActualWidth}).");
            Check(((UIElement)motion.FindName("MessageSearchBox")).IsKeyboardFocusWithin, "Expanded search should take keyboard focus.");
            Capture(motion, Path.Combine(Path.GetFullPath("artifacts"), "search-open-tested.png"));
            motion.RaiseEvent(new System.Windows.Input.KeyEventArgs(System.Windows.Input.Keyboard.PrimaryDevice, PresentationSource.FromVisual(motion)!, 0, System.Windows.Input.Key.Escape) { RoutedEvent = UIElement.PreviewKeyDownEvent });
            await Task.Delay(400);
            Check(Math.Abs(pill.ActualWidth - 32) < 0.5, $"Escape should shrink search back to its icon (was {pill.ActualWidth}).");
            motion.Close();
            Console.WriteLine("PASS  Compact default size, sliding sidebar and in-place search");
        }

        string directory = Path.Combine(Path.GetTempPath(), "CydLan-desktop-" + Guid.NewGuid().ToString("N"));
        var store = new LocalStore(directory);
        var reservation = new TcpListener(IPAddress.Loopback, 0); reservation.Start();
        int port = ((IPEndPoint)reservation.LocalEndpoint).Port; reservation.Stop();
        store.SaveSettings(new() { DisplayName = "Home desktop", Port = port });
        var remoteSettings = new AppSettings { DisplayName = "Experiment peer", Port = 0 };
        try
        {
            await using (var peer = new LanService(remoteSettings))
            await using (var model = new MainViewModel(localStore: store))
            {
                Check(!model.Preview && model.Peers.Count == 0, "Default desktop mode must use real contacts.");
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
                Check(await peer.SendChatAsync(model.Settings.UserId, "Peer → desktop ✅\nMessages and receipts are working.", "14001"), "Desktop did not acknowledge inbound chat.");
                await Until(() => model.Messages.Count == 2);
                Check(model.SelectedPeer.Unread == 0, "A focused conversation should not accumulate unread messages.");
                model.TransfersOpen = true;
                Check(await peer.SendChatAsync(model.Settings.UserId, "A message while viewing file transfers", "14003"), "Transfers view stopped receiving chat.");
                await Until(() => model.SelectedPeer.Unread == 1);
                model.TransfersOpen = false;
                Check(model.SelectedPeer.Unread == 1, "Opening a conversation must not mark it read before the user replies.");
                model.Replying = true;
                Check(model.SelectedPeer.Unread == 0, "Engaging the composer must clear the unread count.");
                model.Replying = false;
                Check(await peer.SendChatAsync(model.Settings.UserId, "A message while the window is inactive", "14002"), "Inactive window stopped receiving chat.");
                await Until(() => model.SelectedPeer.Unread == 1);
                Check(model.PeopleSummary.Contains("1 unread"), "Unread totals did not update.");
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
                model.ConversationFilesSent = false;
                Check(model.ConversationFiles.Cast<FileTransfer>().Any() && model.ConversationFiles.Cast<FileTransfer>().All(t => !t.Outgoing && t.PeerId == model.SelectedPeer!.Id), "The Received tab lists only files from this person.");
                model.ConversationFilesSent = true;
                Check(model.ConversationFiles.Cast<FileTransfer>().All(t => t.Outgoing) && model.ConversationSentCount == model.Transfers.Count(t => t.Outgoing), "The Sent tab lists only files sent to this person.");
                model.ConversationFilesSent = false;
                Console.WriteLine("PASS  Inline images both ways; accepted files saved like LAN Messenger; files panel tabs");

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
                Console.WriteLine("PASS  Files filters and preferences (font size, profile, send key)");

                var window = new MainWindow(model); window.Show();
                await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
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
            Check(restored.Messages.Count(m => m.HasText && m.GroupId.Length == 0) == 4 && restored.Messages.First().CanRetry, "History or retry state did not survive restart.");
            Check(restored.Messages.Count(m => m.HasImage && File.Exists(m.ImagePath)) == 2, "Sent and received images did not survive restart.");
            Check(restored.Groups.Count == 2 && restored.Groups.Any(g => g.Name == "Lunch"), "Groups did not survive restart.");
            Check(restored.Transfers.Count >= 3 && restored.Settings.FirstName == "Home", "File history and profile did not survive restart.");
            Check(restored.Settings.DisplayName == "Home desktop updated", "Preferences were not persisted.");
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
            Console.WriteLine("12/12 desktop test groups passed. Live networking was loopback-only, with isolated temporary application data.");
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

    private static void Capture(Window window, string path)
    {
        window.UpdateLayout();
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(window.ActualWidth), (int)Math.Ceiling(window.ActualHeight), 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(window);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var file = File.Create(path); encoder.Save(file);
    }
}
