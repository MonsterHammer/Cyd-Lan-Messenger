using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using CydLan.Core;
using Microsoft.Win32;

namespace CydLan.Desktop;

public partial class MainWindow : Window
{
    private readonly MainViewModel model;
    private DateTime lastTyping = DateTime.MinValue;
    private readonly DispatcherTimer typingTimer = new() { Interval = TimeSpan.FromSeconds(4) };
    public MainWindow(MainViewModel model)
    {
        InitializeComponent(); this.model = model; DataContext = model;
        model.ScrollRequested += () => Dispatcher.BeginInvoke(() => MessageScroller.ScrollToEnd());
        Loaded += (_, _) => MessageScroller.ScrollToEnd();
        Activated += (_, _) => { model.ConversationFocused = true; if (model.ChatOpen && !model.Broadcasting && model.SelectedPeer is { } peer) peer.Unread = 0; };
        Deactivated += (_, _) => model.ConversationFocused = false;
        Closing += (_, args) => { args.Cancel = true; Hide(); };
        typingTimer.Tick += async (_, _) => { typingTimer.Stop(); await model.SendTypingAsync(false); };
        if (model.Preview && model.HasPeer) DetailsColumn.Width = new GridLength(230);
    }
    private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e) { if (e.ClickCount == 2) ToggleMaximize(); else if (e.LeftButton == MouseButtonState.Pressed) DragMove(); }
    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void Maximize_Click(object sender, RoutedEventArgs e) => ToggleMaximize();
    private void ToggleMaximize() => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    private void Close_Click(object sender, RoutedEventArgs e) => Hide();
    private void Chat_Click(object sender, RoutedEventArgs e) { model.TransfersOpen = false; model.Broadcasting = false; }
    private void People_Click(object sender, RoutedEventArgs e) { PeopleSearch.Focus(); PeopleSearch.SelectAll(); }
    private void Transfers_Click(object sender, RoutedEventArgs e) => model.TransfersOpen = true;
    private void Broadcast_Click(object sender, RoutedEventArgs e) { model.Broadcasting = true; model.TransfersOpen = false; DetailsColumn.Width = new GridLength(0); Composer.Focus(); }
    private async void Refresh_Click(object sender, RoutedEventArgs e) => await model.RefreshAsync();
    private void SearchMessages_Click(object sender, RoutedEventArgs e) { MessageSearchBox.Visibility = MessageSearchBox.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible; if (MessageSearchBox.Visibility == Visibility.Visible) MessageSearchBox.Focus(); else model.MessageSearch = ""; }
    private void Details_Click(object sender, RoutedEventArgs e) { if (model.HasPeer) DetailsColumn.Width = new GridLength(DetailsColumn.Width.Value == 0 ? 230 : 0); }
    private async void Send_Click(object sender, RoutedEventArgs e) => await model.SendAsync();
    private async void Composer_KeyDown(object sender, System.Windows.Input.KeyEventArgs e) { if (e.Key == Key.Enter && !Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) { e.Handled = true; await model.SendAsync(); } }
    private async void Composer_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (model is null) return;
        typingTimer.Stop(); typingTimer.Start();
        if ((DateTime.UtcNow - lastTyping).TotalSeconds < 3) return;
        lastTyping = DateTime.UtcNow; await model.SendTypingAsync(true);
    }
    private void Emoji_Click(object sender, RoutedEventArgs e) { Composer.SelectedText = " 🙂 "; Composer.CaretIndex = Composer.Text.Length; Composer.Focus(); }
    private async void Attach_Click(object sender, RoutedEventArgs e)
    {
        if (model.SelectedPeer is null || !model.SelectedPeer.Online) { model.Toast = "Choose an online person first."; return; }
        var picker = new Microsoft.Win32.OpenFileDialog { Title = "Share a file over LAN", CheckFileExists = true };
        if (picker.ShowDialog(this) == true) await model.OfferFileAsync(picker.FileName);
    }
    private async void AcceptFile_Click(object sender, RoutedEventArgs e)
    {
        if (((System.Windows.Controls.Button)sender).DataContext is not FileTransfer transfer) return;
        var picker = new Microsoft.Win32.SaveFileDialog { Title = "Save incoming LAN file", FileName = transfer.Name, OverwritePrompt = false };
        if (picker.ShowDialog(this) == true)
        {
            if (System.IO.File.Exists(picker.FileName)) { model.Toast = "Choose a new filename so an existing file is not replaced."; return; }
            await model.AcceptFileAsync(transfer, picker.FileName);
        }
    }
    private async void DeclineFile_Click(object sender, RoutedEventArgs e) { if (((System.Windows.Controls.Button)sender).DataContext is FileTransfer transfer) await model.DeclineFileAsync(transfer); }
    private void Settings_Click(object sender, RoutedEventArgs e) => new SettingsWindow(model) { Owner = this }.ShowDialog();
    private void DismissToast_Click(object sender, RoutedEventArgs e) => model.Toast = "";
}
