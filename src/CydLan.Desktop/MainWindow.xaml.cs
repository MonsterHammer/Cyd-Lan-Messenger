using System.Runtime.InteropServices;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using CydLan.Core;
using Microsoft.Win32;

namespace CydLan.Desktop;

public partial class MainWindow : Window
{
    private readonly MainViewModel model;
    private DateTime lastTyping = DateTime.MinValue;
    private string? typingPeer;
    private bool sidebarVisible = true;
    private double sidebarApplied = -1;
    private bool searchOpen;
    private readonly DispatcherTimer typingTimer = new() { Interval = TimeSpan.FromSeconds(4) };
    public MainWindow(MainViewModel model)
    {
        InitializeComponent(); this.model = model; DataContext = model;
        model.ScrollRequested += force => {
            bool atEnd = MessageScroller.ScrollableHeight - MessageScroller.VerticalOffset < 40;
            double offset = MessageScroller.VerticalOffset;
            Dispatcher.BeginInvoke(() => { if (force || atEnd) MessageScroller.ScrollToEnd(); else MessageScroller.ScrollToVerticalOffset(offset); });
        };
        Loaded += (_, _) => MessageScroller.ScrollToEnd();
        // Opening a conversation (including from a notification) leaves it unread until the user clicks or types in the composer.
        Deactivated += (_, _) => model.Replying = false;
        Composer.PreviewMouseDown += (_, _) => model.Replying = true;
        Composer.PreviewTextInput += (_, _) => model.Replying = true;
        Closing += (_, args) => { if (System.Windows.Application.Current is App { IsQuitting: true }) return; args.Cancel = true; Hide(); };
        typingTimer.Tick += async (_, _) => { typingTimer.Stop(); await StopTypingAsync(); };
        SizeChanged += (_, _) => UpdateCompactLayout();
        SourceInitialized += (_, _) =>
        {
            PopupHost.RoundCorners(this);
            if (PresentationSource.FromVisual(this) is HwndSource source) source.AddHook(RespectMonitorWorkArea);
        };
        PreviewKeyDown += Window_PreviewKeyDown;
        // Paste images/files into the composer: covers Win+Shift+S screenshots, copied images and copied files.
        CommandManager.AddPreviewExecutedHandler(Composer, (_, e) => { if (e.Command == ApplicationCommands.Paste && TryPasteAttachments()) e.Handled = true; });
        // A TextBox only enables Paste for text, so a screenshot-only clipboard (Win+Shift+S) would never reach the handler above.
        CommandManager.AddPreviewCanExecuteHandler(Composer, (_, e) => { if (e.Command == ApplicationCommands.Paste && ClipboardHasAttachment()) { e.CanExecute = true; e.Handled = true; } });
        PreviewDragEnter += Window_DragOver; PreviewDragOver += Window_DragOver;
        PreviewDragLeave += (_, _) => DropOverlay.Visibility = Visibility.Collapsed;
        PreviewDrop += Window_Drop;
        model.PropertyChanged += async (_, args) => {
            if (args.PropertyName == nameof(model.HasPeer) && !model.HasPeer) { DetailsPanel.Visibility = Visibility.Collapsed; FilesPanel.Visibility = Visibility.Collapsed; UpdateCompactLayout(); }
            if (args.PropertyName == nameof(model.InGroup) && !model.InGroup) { GroupPanel.Visibility = Visibility.Collapsed; UpdateCompactLayout(); }
            if (args.PropertyName == nameof(model.SelectedPeer) || args.PropertyName == nameof(model.Broadcasting)) { typingTimer.Stop(); await StopTypingAsync(); }
        };
    }
    private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e) { if (e.ClickCount == 2) ToggleMaximize(); else if (e.LeftButton == MouseButtonState.Pressed) DragMove(); }
    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void Maximize_Click(object sender, RoutedEventArgs e) => ToggleMaximize();
    private void ToggleMaximize() => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private static IntPtr RespectMonitorWorkArea(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        const int getMinMaxInfo = 0x0024;
        if (message != getMinMaxInfo) return IntPtr.Zero;

        // WindowChrome extends our client area to the window edges. Keep those edges
        // inside the current monitor's work area so Windows can show its taskbar.
        var screen = System.Windows.Forms.Screen.FromHandle(hwnd);
        var bounds = screen.Bounds;
        var workArea = screen.WorkingArea;
        var limits = Marshal.PtrToStructure<MinMaxInfo>(lParam);
        limits.MaxPosition = new() { X = workArea.Left - bounds.Left, Y = workArea.Top - bounds.Top };
        limits.MaxSize = new() { X = workArea.Width, Y = workArea.Height };
        Marshal.StructureToPtr(limits, lParam, false);
        handled = true;
        return IntPtr.Zero;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint { public int X, Y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct MinMaxInfo
    {
        public NativePoint Reserved, MaxSize, MaxPosition, MinTrackSize, MaxTrackSize;
    }

    protected override void OnStateChanged(EventArgs e)
    {
        base.OnStateChanged(e);
        // Same as Windows: one square when restored (Maximize), two overlapping squares when maximized (Restore).
        bool maximized = WindowState == WindowState.Maximized;
        MaximizeButton.Content = maximized ? "" : "";
        MaximizeButton.ToolTip = maximized ? "Restore" : "Maximize";
        System.Windows.Automation.AutomationProperties.SetName(MaximizeButton, maximized ? "Restore" : "Maximize");
    }
    private void Close_Click(object sender, RoutedEventArgs e) => Hide();
    private void Chat_Click(object sender, RoutedEventArgs e) { model.TransfersOpen = false; model.Broadcasting = false; model.GroupsOpen = false; }
    private void Groups_Click(object sender, RoutedEventArgs e)
    {
        model.TransfersOpen = false; model.GroupsOpen = true;
        if (model.SelectedGroup is null && model.Groups.Count > 0) model.SelectedGroup = model.Groups[0];
    }
    private void SidebarPlus_Click(object sender, RoutedEventArgs e) { if (model.IsGroupsSection) NewGroup_Click(sender, e); else Settings_Click(sender, e); }
    private async void NewGroup_Click(object sender, RoutedEventArgs e)
    {
        var picker = new GroupPeopleWindow(model, "New group", [], askName: true);
        if (PopupHost.ShowModal(this, picker) == true) await model.CreateGroupAsync(picker.GroupName, picker.SelectedIds);
    }
    private async void AddPeople_Click(object sender, RoutedEventArgs e)
    {
        if (model.SelectedGroup is not { } group) return;
        var picker = new GroupPeopleWindow(model, "Add people", group.Members, askName: false);
        if (PopupHost.ShowModal(this, picker) == true) await model.AddGroupMembersAsync(group, picker.SelectedIds);
    }
    private async void LeaveGroup_Click(object sender, RoutedEventArgs e)
    {
        if (model.SelectedGroup is not { } group) return;
        if (System.Windows.MessageBox.Show(this, $"Leave \"{group.Name}\"? Its messages are removed from this computer.", "Leave group", MessageBoxButton.OKCancel) != MessageBoxResult.OK) return;
        GroupPanel.Visibility = Visibility.Collapsed; UpdateCompactLayout();
        await model.LeaveGroupAsync(group);
    }
    private void GroupName_KeyDown(object sender, System.Windows.Input.KeyEventArgs e) { if (e.Key == Key.Enter) { RenameFromBox(); Composer.Focus(); } }
    private void GroupName_LostFocus(object sender, KeyboardFocusChangedEventArgs e) => RenameFromBox();
    private void RenameFromBox() { if (model.SelectedGroup is { } group && GroupNameBox.Text.Trim() != group.Name) model.RenameGroup(group, GroupNameBox.Text); }
    private void TransferFilter_Checked(object sender, RoutedEventArgs e) { if (model is not null) model.TransferFilter = (string)((FrameworkElement)sender).Tag; }
    private void OpenReceivedFolder_Click(object sender, RoutedEventArgs e)
    {
        System.IO.Directory.CreateDirectory(model.ReceivedFilesFolder);
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe", $"\"{model.ReceivedFilesFolder}\"") { UseShellExecute = true });
    }
    // Programs and scripts received over the LAN are only revealed in Explorer, never launched from here.
    private static readonly string[] RunnableExtensions = [".exe", ".com", ".bat", ".cmd", ".ps1", ".vbs", ".js", ".jse", ".wsf", ".msi", ".msix", ".scr", ".lnk", ".reg", ".hta", ".cpl", ".jar"];
    private void OpenFile_Click(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is not FileTransfer { Path.Length: > 0 } transfer || !System.IO.File.Exists(transfer.Path)) { model.Toast = "File not found"; return; }
        if (RunnableExtensions.Contains(System.IO.Path.GetExtension(transfer.Path).ToLowerInvariant())) { ShowFile_Click(sender, e); return; }
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(transfer.Path) { UseShellExecute = true }); }
        catch (System.ComponentModel.Win32Exception ex) { model.Toast = ex.Message; }
    }
    private void ShowFile_Click(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is not FileTransfer { Path.Length: > 0 } transfer || !System.IO.File.Exists(transfer.Path)) { model.Toast = "File not found"; return; }
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe", $"/select,\"{transfer.Path}\"") { UseShellExecute = true });
    }
    // A file bubble in the conversation: clicking shows it in its folder; the menu offers the rest.
    private void MessageFile_Click(object sender, MouseButtonEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is ChatMessage message) RunFileAction(message, "Reveal");
    }
    private void FileMenu_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.MenuItem { DataContext: ChatMessage message, Tag: string action }) RunFileAction(message, action);
    }
    private void RunFileAction(ChatMessage message, string action)
    {
        string path = model.TransferFor(message)?.Path ?? "";
        if (path.Length == 0 || !File.Exists(path)) { model.Toast = "File not found"; return; }
        try
        {
            switch (action)
            {
                case "Open":
                    if (RunnableExtensions.Contains(Path.GetExtension(path).ToLowerInvariant())) goto case "Reveal";
                    Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
                    break;
                case "Reveal":
                    Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{Path.GetFullPath(path)}\"") { UseShellExecute = true });
                    break;
                case "Copy":
                    System.Windows.Clipboard.SetFileDropList([Path.GetFullPath(path)]);
                    break;
                case "Save":
                    var picker = new Microsoft.Win32.SaveFileDialog { Title = "Save a copy", FileName = Path.GetFileName(path) };
                    if (picker.ShowDialog(this) == true) File.Copy(path, picker.FileName, overwrite: true);
                    break;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ExternalException or System.ComponentModel.Win32Exception)
        {
            model.Toast = "File action failed";
        }
    }
    private void People_Click(object sender, RoutedEventArgs e) { Chat_Click(sender, e); sidebarVisible = true; UpdateCompactLayout(animate: true); PeopleSearch.Focus(); PeopleSearch.SelectAll(); }
    private void ToggleSidebar_Click(object sender, RoutedEventArgs e) { sidebarVisible = !sidebarVisible; UpdateCompactLayout(animate: true); }
    private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            switch (e.Key)
            {
                case Key.B: ToggleSidebar_Click(this, new()); e.Handled = true; break;
                case Key.K: People_Click(this, new()); e.Handled = true; break;
                case Key.F: SearchMessages_Click(this, new()); e.Handled = true; break;
                case Key.V when Keyboard.FocusedElement is not System.Windows.Controls.TextBox: if (TryPasteAttachments()) { e.Handled = true; Composer.Focus(); } break;
            }
        }
        else if (e.Key == Key.Escape)
        {
            DetailsPanel.Visibility = Visibility.Collapsed; FilesPanel.Visibility = Visibility.Collapsed;
            if (searchOpen) CloseSearch();
            UpdateCompactLayout(); Composer.Focus();
        }
    }
    private void Transfers_Click(object sender, RoutedEventArgs e) => model.TransfersOpen = true;
    private void Broadcast_Click(object sender, RoutedEventArgs e) { model.Broadcasting = true; model.TransfersOpen = false; Composer.Focus(); }
    private async void Refresh_Click(object sender, RoutedEventArgs e) => await model.RefreshAsync();
    // The header search icon grows into a compact search field in place, and shrinks back when emptied.
    private const double SearchOpenWidth = 220, SearchClosedWidth = 32;
    private void SearchMessages_Click(object sender, RoutedEventArgs e) { if (searchOpen && model.MessageSearch.Length == 0) CloseSearch(); else OpenSearch(); }
    private void OpenSearch()
    {
        searchOpen = true;
        MessageSearchBox.Visibility = Visibility.Visible;
        SearchPill.SetResourceReference(System.Windows.Controls.Border.BackgroundProperty, "Surface");
        Animate(SearchPill, WidthProperty, SearchOpenWidth, 220);
        Animate(MessageSearchBox, OpacityProperty, 1, 180);
        MessageSearchBox.Focus();
    }
    private void CloseSearch()
    {
        searchOpen = false; model.MessageSearch = "";
        Animate(MessageSearchBox, OpacityProperty, 0, 100);
        Animate(SearchPill, WidthProperty, SearchClosedWidth, 200, () => {
            if (searchOpen) return;
            MessageSearchBox.Visibility = Visibility.Collapsed;
            SearchPill.Background = System.Windows.Media.Brushes.Transparent;
        });
    }
    private void MessageSearch_LostFocus(object sender, KeyboardFocusChangedEventArgs e) { if (searchOpen && model.MessageSearch.Length == 0) CloseSearch(); }
    private static void Animate(UIElement element, DependencyProperty property, double to, int milliseconds, Action? completed = null)
    {
        var animation = new DoubleAnimation(to, TimeSpan.FromMilliseconds(milliseconds)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut } };
        if (completed is not null) animation.Completed += (_, _) => completed();
        element.BeginAnimation(property, animation);
    }
    private void Details_Click(object sender, RoutedEventArgs e)
    {
        if (model.InGroup) ShowSidePanel(GroupPanel);
        else if (model.HasPeer) ShowSidePanel(DetailsPanel);
    }
    private void ConversationFiles_Click(object sender, RoutedEventArgs e) { if (model.HasPeer) ShowSidePanel(FilesPanel); }
    // One side panel at a time; clicking the open one's button closes it.
    private void ShowSidePanel(FrameworkElement panel)
    {
        bool open = panel.Visibility != Visibility.Visible;
        foreach (var other in new FrameworkElement[] { DetailsPanel, GroupPanel, FilesPanel }) other.Visibility = Visibility.Collapsed;
        if (open) panel.Visibility = Visibility.Visible;
        UpdateCompactLayout();
    }
    // Side panels overlay the conversation, so working in the conversation closes them.
    private void Conversation_Interacted(object sender, RoutedEventArgs e)
    {
        var panels = new FrameworkElement[] { DetailsPanel, GroupPanel, FilesPanel };
        if (!panels.Any(p => p.Visibility == Visibility.Visible)) return;
        foreach (var panel in panels) panel.Visibility = Visibility.Collapsed;
        UpdateCompactLayout();
    }
    private void FilesTab_Checked(object sender, RoutedEventArgs e) { if (model is not null) model.ConversationFilesSent = sender == SentTab; }
    private void More_Click(object sender, RoutedEventArgs e)
    {
        var menu = MoreButton.ContextMenu;
        menu.DataContext = model; menu.PlacementTarget = MoreButton; menu.IsOpen = true;
    }
    private void DeleteChat_Click(object sender, RoutedEventArgs e)
    {
        if (!model.HasDetails) return;
        if (System.Windows.MessageBox.Show(this, $"Delete your chat with {model.ChatTitle}? Messages are removed from this computer.", "Delete chat", MessageBoxButton.OKCancel) == MessageBoxResult.OK)
            model.DeleteConversation();
    }
    private void DeleteAllChats_Click(object sender, RoutedEventArgs e)
    {
        if (System.Windows.MessageBox.Show(this, "Delete all chats? Messages are removed from this computer.", "Delete all chats", MessageBoxButton.OKCancel) == MessageBoxResult.OK)
            model.ClearMessageHistory();
    }
    private void DeleteRowChat_Click(object sender, RoutedEventArgs e)
    {
        switch (((FrameworkElement)sender).DataContext)
        {
            case Peer peer: model.SelectedPeer = peer; break;
            case GroupChat group: model.SelectedGroup = group; break;
            default: return;
        }
        DeleteChat_Click(sender, e);
    }
    internal void ShowConnectionDetails() { if (model.HasPeer) DetailsPanel.Visibility = Visibility.Visible; UpdateCompactLayout(); }
    private void UpdateCompactLayout(bool animate = false)
    {
        double full = ActualWidth < 900 ? 216 : ActualWidth < 1100 ? 244 : 274;
        double width = sidebarVisible ? full : 0;
        SidebarContent.Width = full; // content keeps its width and is clipped while the panel slides
        if (width != sidebarApplied)
        {
            sidebarApplied = width;
            if (sidebarVisible) SidebarPanel.Visibility = Visibility.Visible;
            if (animate)
            {
                Animate(SidebarPanel, WidthProperty, width, 240, () => { if (!sidebarVisible) SidebarPanel.Visibility = Visibility.Collapsed; });
                Animate(SidebarContent, OpacityProperty, sidebarVisible ? 1 : 0, sidebarVisible ? 260 : 160);
            }
            else
            {
                SidebarPanel.BeginAnimation(WidthProperty, null); SidebarContent.BeginAnimation(OpacityProperty, null);
                SidebarPanel.Width = width; SidebarContent.Opacity = sidebarVisible ? 1 : 0;
                SidebarPanel.Visibility = sidebarVisible ? Visibility.Visible : Visibility.Collapsed;
            }
        }
        // Details become a drawer on small windows instead of squeezing the chat.
        double conversationWidth = ActualWidth - 52 - width;
        bool panelOpen = DetailsPanel.Visibility == Visibility.Visible || GroupPanel.Visibility == Visibility.Visible || FilesPanel.Visibility == Visibility.Visible;
        ChatContentGrid.Margin = panelOpen && conversationWidth >= 790 ? new Thickness(0,0,242,0) : new Thickness(0);
    }
    private async void Send_Click(object sender, RoutedEventArgs e) => await model.SendAsync();
    private async void Retry_Click(object sender, RoutedEventArgs e) { if (((System.Windows.Controls.Button)sender).DataContext is ChatMessage message) await model.RetryAsync(message); }
    // Preferences › Messages: Enter sends (Shift+Enter = new line), or Ctrl+Enter sends (Enter = new line), like LAN Messenger.
    private async void Composer_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        bool ctrl = Keyboard.Modifiers.HasFlag(ModifierKeys.Control), shift = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);
        bool send = model.Settings.SendWithCtrlEnter ? ctrl : !shift && !ctrl;
        if (send) { e.Handled = true; await model.SendAsync(); }
        else if (ctrl) { e.Handled = true; Composer.SelectedText = Environment.NewLine; Composer.CaretIndex = Composer.SelectionStart + Composer.SelectionLength; Composer.SelectionLength = 0; }
    }
    private async void Composer_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (model is null) return;
        if (string.IsNullOrWhiteSpace(Composer.Text)) { typingTimer.Stop(); await StopTypingAsync(); return; }
        if (model.Broadcasting || model.SelectedPeer?.Online != true) return;
        typingTimer.Stop(); typingTimer.Start();
        if (typingPeer == model.SelectedPeer.Id && (DateTime.UtcNow - lastTyping).TotalSeconds < 3) return;
        typingPeer = model.SelectedPeer.Id;
        lastTyping = DateTime.UtcNow; await model.SendTypingAsync(true, typingPeer);
    }
    private async Task StopTypingAsync() { var peer = typingPeer; typingPeer = null; if (peer is not null) await model.SendTypingAsync(false, peer); }
    private void Emoji_Click(object sender, RoutedEventArgs e)
    {
        SmileyGrid.ItemsSource ??= CydLan.Core.Smileys.PickerCodes.Select((code, i) => new { Code = code, Image = SmileyImages.Get(i + 1) }).ToList();
        SmileyPopup.IsOpen = !SmileyPopup.IsOpen;
    }
    // Inserts the LAN Messenger code; both apps draw it as the picture.
    private void Smiley_Click(object sender, RoutedEventArgs e)
    {
        SmileyPopup.IsOpen = false;
        Composer.SelectedText = (string)((FrameworkElement)sender).Tag;
        Composer.CaretIndex = Composer.SelectionStart + Composer.SelectionLength; Composer.SelectionLength = 0;
        model.Replying = true; Composer.Focus();
    }
    private void CopyMessage_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: ChatMessage message }) try { System.Windows.Clipboard.SetText(message.Text); } catch (System.Runtime.InteropServices.ExternalException) { }
    }
    private void Attach_Click(object sender, RoutedEventArgs e)
    {
        if (model.SelectedPeer is null || !model.SelectedPeer.Online) { model.Toast = "No online contact selected"; return; }
        var picker = new Microsoft.Win32.OpenFileDialog { Title = "Attach images or files", CheckFileExists = true, Multiselect = true,
            Filter = "All files|*.*|Images|*.png;*.jpg;*.jpeg;*.gif;*.bmp;*.webp" };
        if (picker.ShowDialog(this) == true) AttachFiles(picker.FileNames);
        Composer.Focus();
    }
    private void AttachFiles(IEnumerable<string> paths)
    {
        foreach (var path in paths)
        {
            if (System.IO.Directory.Exists(path)) { model.Toast = "Folders are not supported"; continue; }
            if (model.AddAttachment(path) is { } problem) { model.Toast = problem; break; }
        }
    }
    private bool ClipboardHasAttachment()
    {
        if (!model.CanAttach) return false;
        try { return System.Windows.Clipboard.ContainsFileDropList() || (!System.Windows.Clipboard.ContainsText() && (System.Windows.Clipboard.ContainsImage() || System.Windows.Clipboard.ContainsData("PNG"))); }
        catch (System.Runtime.InteropServices.COMException) { return false; }
    }
    private bool TryPasteAttachments()
    {
        if (!model.CanAttach) return false;
        try
        {
            if (System.Windows.Clipboard.ContainsFileDropList())
            {
                var files = System.Windows.Clipboard.GetFileDropList().Cast<string>().ToList();
                if (files.Count == 0) return false;
                AttachFiles(files); return true;
            }
            // Word and web pages copy text plus a picture of it; in that case paste the text as usual.
            if (System.Windows.Clipboard.ContainsText()) return false;
            if (ClipboardImage() is { } image) { if (model.AddImage(image) is { } problem) model.Toast = problem; return true; }
        }
        catch (System.Runtime.InteropServices.COMException) { model.Toast = "Clipboard unavailable"; return true; }
        return false;
    }
    // Prefer the PNG clipboard format (keeps transparency); fall back to the bitmap, dropping its often-bogus alpha channel.
    private static BitmapSource? ClipboardImage()
    {
        if (System.Windows.Clipboard.GetData("PNG") is System.IO.Stream png)
        {
            try { var frame = BitmapFrame.Create(png, BitmapCreateOptions.None, BitmapCacheOption.OnLoad); frame.Freeze(); return frame; }
            catch (Exception e) when (e is NotSupportedException or System.IO.FileFormatException or ArgumentException) { }
        }
        if (!System.Windows.Clipboard.ContainsImage() || System.Windows.Clipboard.GetImage() is not { } bitmap) return null;
        var opaque = new FormatConvertedBitmap(bitmap, System.Windows.Media.PixelFormats.Bgr32, null, 0); opaque.Freeze();
        return opaque;
    }
    private void Window_DragOver(object sender, System.Windows.DragEventArgs e)
    {
        bool usable = model.CanAttach && e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop);
        e.Effects = usable ? System.Windows.DragDropEffects.Copy : System.Windows.DragDropEffects.None;
        DropOverlay.Visibility = usable && model.ChatOpen ? Visibility.Visible : Visibility.Collapsed;
        e.Handled = true;
    }
    private void Window_Drop(object sender, System.Windows.DragEventArgs e)
    {
        DropOverlay.Visibility = Visibility.Collapsed;
        if (!model.CanAttach) { model.Toast = "No online contact selected"; return; }
        if (e.Data.GetData(System.Windows.DataFormats.FileDrop) is string[] files) { model.TransfersOpen = false; AttachFiles(files); Composer.Focus(); e.Handled = true; }
    }
    private void RemoveAttachment_Click(object sender, RoutedEventArgs e) { if (((FrameworkElement)sender).DataContext is Attachment item) model.RemoveAttachment(item); }
    private void MessageImage_Click(object sender, MouseButtonEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is ChatMessage message) RunImageAction(message, "Open");
    }
    private void ImageMenu_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.MenuItem { DataContext: ChatMessage message, Tag: string action })
            RunImageAction(message, action);
    }
    private void RunImageAction(ChatMessage message, string action)
    {
        string path = message.ImagePath;
        if (!LanService.IsInlineImageName(path) || !File.Exists(path))
        {
            model.Toast = "Image unavailable";
            return;
        }

        try
        {
            switch (action)
            {
                case "Open":
                    if (Images.Load(path, 2048) is { } preview)
                        PopupHost.ShowModal(this, new ImagePreviewWindow(Path.GetFileName(path), preview));
                    else model.Toast = "Image could not be opened";
                    break;
                case "Attach":
                    if (model.AddAttachment(path) is { } problem) model.Toast = problem;
                    else Composer.Focus();
                    break;
                case "Copy":
                    if (Images.Load(path, 0) is { } image) System.Windows.Clipboard.SetImage(image);
                    else model.Toast = "Image could not be copied";
                    break;
                case "Reveal":
                    Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{Path.GetFullPath(path)}\""));
                    break;
                case "Save":
                    var picker = new Microsoft.Win32.SaveFileDialog { Title = "Save a copy", FileName = Path.GetFileName(path) };
                    if (picker.ShowDialog(this) == true) File.Copy(path, picker.FileName, overwrite: true);
                    break;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ExternalException)
        {
            model.Toast = "Image action failed";
        }
    }
    // Like LAN Messenger, an accepted file goes straight into the received files folder ("name [1].ext" when taken).
    private async void AcceptFile_Click(object sender, RoutedEventArgs e)
    {
        if (((System.Windows.Controls.Button)sender).DataContext is FileTransfer transfer) await model.AcceptFileAsync(transfer);
    }
    private async void CancelOffer_Click(object sender, RoutedEventArgs e) { if (((FrameworkElement)sender).DataContext is FileTransfer transfer) await model.CancelOfferAsync(transfer); }
    private async void DeclineFile_Click(object sender, RoutedEventArgs e) { if (((System.Windows.Controls.Button)sender).DataContext is FileTransfer transfer) await model.DeclineFileAsync(transfer); }
    private async void MessageAcceptFile_Click(object sender, RoutedEventArgs e) { if (((System.Windows.Controls.Button)sender).DataContext is ChatMessage message && model.TransferFor(message) is { } transfer) await model.AcceptFileAsync(transfer); }
    private async void MessageDeclineFile_Click(object sender, RoutedEventArgs e) { if (((System.Windows.Controls.Button)sender).DataContext is ChatMessage message && model.TransferFor(message) is { } transfer) await model.DeclineFileAsync(transfer); }
    private void Settings_Click(object sender, RoutedEventArgs e) => PopupHost.ShowModal(this, new SettingsWindow(model));
    public void SetScrim(bool visible)
    {
        var fade = new System.Windows.Media.Animation.DoubleAnimation(visible ? 1 : 0, TimeSpan.FromMilliseconds(visible ? 160 : 120));
        if (visible) Scrim.Visibility = Visibility.Visible;
        else fade.Completed += (_, _) => { if (Scrim.Opacity == 0) Scrim.Visibility = Visibility.Collapsed; };
        Scrim.BeginAnimation(OpacityProperty, fade);
    }
    private void DismissToast_Click(object sender, RoutedEventArgs e) => model.Toast = "";
}
