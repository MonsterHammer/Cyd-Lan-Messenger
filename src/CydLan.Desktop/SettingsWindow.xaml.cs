using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using FontFamily = System.Windows.Media.FontFamily;

namespace CydLan.Desktop;

public partial class SettingsWindow : Window
{
    private readonly MainViewModel model;
    private readonly string originalTheme;
    private bool saved;
    public SettingsWindow(MainViewModel model)
    {
        InitializeComponent(); this.model = model; DataContext = model;
        PopupHost.EnableDrag(this, TitleBar); PopupHost.EnableDrag(this, DragArea);
        var s = model.Settings;
        // General
        StartWithWindowsBox.IsChecked = s.StartWithWindows; MinimizedBox.IsChecked = s.StartMinimized;
        StatusBox.SelectedItem = StatusBox.Items.Cast<ComboBoxItem>().FirstOrDefault(item => (string)item.Tag == s.Status) ?? StatusBox.Items[0];
        // Account
        DisplayNameBox.Text = s.DisplayName; FirstNameBox.Text = s.FirstName; LastNameBox.Text = s.LastName; AboutBox.Text = s.About;
        avatarPath = s.AvatarPath; ShowAvatar();
        DisplayNameBox.TextChanged += (_, _) => ShowAvatar();
        // Messages
        FontBox.ItemsSource = Fonts.SystemFontFamilies.Select(f => f.Source).Append("Segoe UI Variable Text").Distinct().OrderBy(n => n).ToList();
        FontBox.SelectedItem = s.MessageFont; if (FontBox.SelectedItem is null) FontBox.SelectedItem = "Segoe UI Variable Text";
        SizeBox.SelectedItem = SizeBox.Items.Cast<ComboBoxItem>().FirstOrDefault(i => (string)i.Tag == s.MessageFontSize) ?? SizeBox.Items[1];
        (s.SendWithCtrlEnter ? SendCtrlEnter : SendEnter).IsChecked = true;
        TrimBox.IsChecked = s.TrimMessages;
        // History
        KeepHistoryBox.IsChecked = s.KeepHistory; KeepFileHistoryBox.IsChecked = s.KeepFileHistory;
        // Alerts
        NotificationsBox.IsChecked = s.Notifications; AlertsBusyBox.IsChecked = s.AlertsWhenBusy; AlertsDndBox.IsChecked = s.AlertsWhenDnd;
        SoundsBox.IsChecked = s.Sounds; SoundsBusyBox.IsChecked = s.SoundsWhenBusy; SoundsDndBox.IsChecked = s.SoundsWhenDnd;
        // Network
        PortBox.Text = s.Port.ToString(); MulticastBox.Text = s.Multicast;
        // Files
        FolderBox.Text = model.ReceivedFilesFolder; AutoAcceptBox.IsChecked = s.AutoAcceptFiles;
        // Appearance (live preview; Cancel restores)
        originalTheme = ThemeManager.Normalize(s.Theme);
        (originalTheme switch { ThemeManager.Dark => ThemeDark, ThemeManager.FollowSystem => ThemeSystem, _ => ThemeLight }).IsChecked = true;
        Closed += (_, _) =>
        {
            if (!saved) ThemeManager.Apply(originalTheme);
            foreach (var path in unsavedAvatars.Where(p => p != model.Settings.AvatarPath)) try { System.IO.File.Delete(path); } catch (System.IO.IOException) { }
        };
        UpdateFontPreview();
    }
    // A picture chosen here is only applied on Save; an unsaved one is deleted when the window closes.
    private string avatarPath = "";
    private readonly List<string> unsavedAvatars = [];
    private void ShowAvatar()
    {
        AvatarPreview.Source = avatarPath.Length > 0 ? Images.Load(avatarPath, 112) : null;
        var name = DisplayNameBox.Text.Trim();
        AvatarInitial.Text = name.Length > 0 ? name[..1].ToUpperInvariant() : "C";
        RemoveAvatarButton.Visibility = avatarPath.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
    }
    private void ChooseAvatar_Click(object sender, RoutedEventArgs e)
    {
        var picker = new Microsoft.Win32.OpenFileDialog { Title = "Profile picture", Filter = "Images|*.png;*.jpg;*.jpeg;*.gif;*.bmp|All files|*.*" };
        if (picker.ShowDialog(this) != true) return;
        if (model.PrepareProfilePicture(picker.FileName) is not { } path) { model.Toast = "That file is not a supported image."; return; }
        unsavedAvatars.Add(path); avatarPath = path; ShowAvatar();
    }
    private void RemoveAvatar_Click(object sender, RoutedEventArgs e) { avatarPath = ""; ShowAvatar(); }
    private string SelectedTheme => ThemeDark.IsChecked == true ? ThemeManager.Dark : ThemeSystem.IsChecked == true ? ThemeManager.FollowSystem : ThemeManager.Light;
    private void Theme_Checked(object sender, RoutedEventArgs e) { if (IsInitialized && ThemeLight is not null) ThemeManager.Apply(SelectedTheme); }
    private void Page_Checked(object sender, RoutedEventArgs e)
    {
        if (!IsInitialized || GeneralPage is null) return;
        foreach (var page in new[] { GeneralPage, AccountPage, MessagesPage, HistoryPage, AlertsPage, NetworkPage, FilesPage, AppearancePage })
            page.Visibility = page.Name == (string)((FrameworkElement)sender).Tag ? Visibility.Visible : Visibility.Collapsed;
    }
    /// <summary>Shows a page by name (used by tests and screenshots).</summary>
    public void ShowPage(string name)
    {
        foreach (var page in new[] { GeneralPage, AccountPage, MessagesPage, HistoryPage, AlertsPage, NetworkPage, FilesPage, AppearancePage })
            page.Visibility = page.Name == name ? Visibility.Visible : Visibility.Collapsed;
        foreach (var item in NavPanel.Children.OfType<System.Windows.Controls.RadioButton>()) if ((string)item.Tag == name) item.IsChecked = true;
    }
    private void Font_Changed(object sender, SelectionChangedEventArgs e) => UpdateFontPreview();
    private void UpdateFontPreview()
    {
        if (FontPreview is null || SizeBox is null) return;
        try { FontPreview.FontFamily = new FontFamily((FontBox.SelectedItem as string ?? "Segoe UI") + ", Segoe UI"); } catch (ArgumentException) { }
        FontPreview.FontSize = MainViewModel.FontSizeFor((SizeBox.SelectedItem as ComboBoxItem)?.Tag as string ?? "medium");
    }
    private void ClearHistory_Click(object sender, RoutedEventArgs e)
    {
        if (System.Windows.MessageBox.Show(this, "Delete all conversation history on this computer?", "Clear history", MessageBoxButton.OKCancel) == MessageBoxResult.OK) model.ClearMessageHistory();
    }
    private void ClearFiles_Click(object sender, RoutedEventArgs e) => model.ClearTransferHistory();
    private void ChangeFolder_Click(object sender, RoutedEventArgs e)
    {
        var picker = new Microsoft.Win32.OpenFolderDialog { Title = "Save received files to", InitialDirectory = FolderBox.Text };
        if (picker.ShowDialog(this) == true) FolderBox.Text = picker.FolderName;
    }
    private void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        try { System.IO.Directory.CreateDirectory(FolderBox.Text); System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe", $"\"{FolderBox.Text}\"") { UseShellExecute = true }); }
        catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception) { model.Toast = ex.Message; }
    }
    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();
    private async void Connect_Click(object sender, RoutedEventArgs e) => await model.ConnectByAddressAsync(AddressBox.Text);
    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        var name = DisplayNameBox.Text.Trim();
        if (name.Length == 0) { ShowPage("AccountPage"); DisplayNameBox.Focus(); return; }
        if (!int.TryParse(PortBox.Text.Trim(), out int port) || port is < 1 or > 65535) { ShowPage("NetworkPage"); PortBox.Focus(); return; }
        var multicast = MulticastBox.Text.Trim();
        if (!IPAddress.TryParse(multicast, out var group) || group.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork || group.GetAddressBytes()[0] is < 224 or > 239)
        { ShowPage("NetworkPage"); MulticastBox.Focus(); return; }
        bool networkChanged = port != model.Settings.Port || multicast != model.Settings.Multicast;
        string defaultFolder = CydLan.Core.AppSettings.DefaultReceivedFilesFolder;
        await model.SavePreferencesAsync(s =>
        {
            s.StartWithWindows = StartWithWindowsBox.IsChecked == true; s.StartMinimized = MinimizedBox.IsChecked == true;
            s.Status = (string)((ComboBoxItem)StatusBox.SelectedItem).Tag;
            s.DisplayName = name; s.FirstName = FirstNameBox.Text.Trim(); s.LastName = LastNameBox.Text.Trim(); s.About = AboutBox.Text.Trim();
            s.AvatarPath = avatarPath;
            s.MessageFont = FontBox.SelectedItem as string ?? "Segoe UI Variable Text";
            s.MessageFontSize = (SizeBox.SelectedItem as ComboBoxItem)?.Tag as string ?? "medium";
            s.SendWithCtrlEnter = SendCtrlEnter.IsChecked == true; s.TrimMessages = TrimBox.IsChecked == true;
            s.KeepHistory = KeepHistoryBox.IsChecked == true; s.KeepFileHistory = KeepFileHistoryBox.IsChecked == true;
            s.Notifications = NotificationsBox.IsChecked == true; s.AlertsWhenBusy = AlertsBusyBox.IsChecked == true; s.AlertsWhenDnd = AlertsDndBox.IsChecked == true;
            s.Sounds = SoundsBox.IsChecked == true; s.SoundsWhenBusy = SoundsBusyBox.IsChecked == true; s.SoundsWhenDnd = SoundsDndBox.IsChecked == true;
            s.Port = port; s.Multicast = multicast;
            s.ReceivedFilesFolder = string.Equals(FolderBox.Text, defaultFolder, StringComparison.OrdinalIgnoreCase) ? "" : FolderBox.Text;
            s.AutoAcceptFiles = AutoAcceptBox.IsChecked == true;
            s.Theme = SelectedTheme;
        });
        if (networkChanged) model.Toast = "Restart CydLan to use the new network settings.";
        saved = true; Close();
    }
}
