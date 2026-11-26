using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using FontFamily = System.Windows.Media.FontFamily;

namespace CydLan.Desktop;

public partial class SettingsWindow : Window
{
    private readonly MainViewModel model;
    private readonly string originalTheme;
    private string selectedTheme;
    private bool updatingThemeFilter;
    private bool saved;
    public SettingsWindow(MainViewModel model)
    {
        InitializeComponent(); this.model = model; DataContext = model;
        VersionText.Text = $"Version {typeof(SettingsWindow).Assembly.GetName().Version?.ToString(3)}";
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
        NotificationPreviewsBox.IsChecked = s.NotificationPreviews;
        SoundsBox.IsChecked = s.Sounds; SoundsBusyBox.IsChecked = s.SoundsWhenBusy; SoundsDndBox.IsChecked = s.SoundsWhenDnd;
        // Network
        PortBox.Text = s.Port.ToString(); MulticastBox.Text = s.Multicast;
        // Files
        FolderBox.Text = model.ReceivedFilesFolder; AutoAcceptBox.IsChecked = s.AutoAcceptFiles;
        // Appearance (live preview; Cancel restores)
        originalTheme = ThemeManager.Normalize(s.Theme);
        selectedTheme = originalTheme;
        FilterThemes("all");
        backgroundPath = s.ChatBackgroundPath;
        BackgroundOpacityBox.Value = model.ChatBackgroundOpacity;
        ShowBackground(model.ChatBackground);
        TypingEffectBox.SelectedValue = ChatEffects.Normalize(s.TypingEffect, typing: true);
        ClickEffectBox.SelectedValue = ChatEffects.Normalize(s.ClickEffect);
        SteampunkEngineBox.IsChecked = s.SteampunkEngineEnabled;
        SakuraPetalsBox.IsChecked = s.SakuraPetalsEnabled;
        TypingIntensityBox.Value = model.TypingIntensity;
        ClickIntensityBox.Value = model.ClickIntensity;
        ThemeDetailsBox.IsChecked = s.ThemeDetailsEnabled;
        EngineIntensityBox.Value = model.EngineIntensity;
        PetalIntensityBox.Value = model.PetalIntensity;
        FirefliesBox.IsChecked = s.FirefliesEnabled;
        FireflyIntensityBox.Value = s.FireflyIntensity;
        RainBox.IsChecked = s.RainEnabled;
        RainIntensityBox.Value = s.RainIntensity;
        SnowBox.IsChecked = s.SnowEnabled;
        SnowIntensityBox.Value = s.SnowIntensity;
        InkBloomIntensityBox.Value = s.InkBloomIntensity;
        SendPulseBox.IsChecked = s.SendPulseEnabled;
        SendPulseIntensityBox.Value = s.SendPulseIntensity;
        AmbientOnSidebarBox.IsChecked = s.AmbientOnSidebarEnabled;
        HeaderAuroraBox.IsChecked = s.HeaderAuroraEnabled;
        HeaderAuroraIntensityBox.Value = model.HeaderAuroraIntensity;
        TitleSparklesBox.IsChecked = s.TitleSparklesEnabled;
        TitleSparklesIntensityBox.Value = model.TitleSparklesIntensity;
        HeaderPulseBox.IsChecked = s.HeaderPulseEnabled;
        HeaderPulseIntensityBox.Value = model.HeaderPulseIntensity;
        RailBeaconBox.IsChecked = s.RailBeaconEnabled;
        RailBeaconIntensityBox.Value = model.RailBeaconIntensity;
        RailStreamBox.IsChecked = s.RailStreamEnabled;
        RailStreamIntensityBox.Value = model.RailStreamIntensity;
        RailStardustBox.IsChecked = s.RailStardustEnabled;
        RailStardustIntensityBox.Value = model.RailStardustIntensity;
        RailScanBox.IsChecked = s.RailScanEnabled;
        RailScanIntensityBox.Value = model.RailScanIntensity;
        SidebarMotesBox.IsChecked = s.SidebarMotesEnabled;
        SidebarMotesIntensityBox.Value = model.SidebarMotesIntensity;
        SidebarGlowBox.IsChecked = s.SidebarGlowEnabled;
        SidebarGlowIntensityBox.Value = model.SidebarGlowIntensity;
        SidebarPrismBox.IsChecked = s.SidebarPrismEnabled;
        SidebarPrismIntensityBox.Value = model.SidebarPrismIntensity;
        SidebarSweepBox.IsChecked = s.SidebarSweepEnabled;
        SidebarSweepIntensityBox.Value = model.SidebarSweepIntensity;
        StarfieldBox.IsChecked = s.StarfieldEnabled;
        StarfieldIntensityBox.Value = model.StarfieldIntensity;
        BubblesBox.IsChecked = s.BubblesEnabled;
        BubblesIntensityBox.Value = model.BubblesIntensity;
        EmbersBox.IsChecked = s.EmbersEnabled;
        EmbersIntensityBox.Value = model.EmbersIntensity;
        ButtonGlowBox.IsChecked = s.ButtonGlowEnabled;
        ButtonGlowIntensityBox.Value = model.ButtonGlowIntensity;
        ButtonSheenBox.IsChecked = s.ButtonSheenEnabled;
        ButtonSheenIntensityBox.Value = model.ButtonSheenIntensity;
        ButtonBounceBox.IsChecked = s.ButtonBounceEnabled;
        ButtonBounceIntensityBox.Value = model.ButtonBounceIntensity;
        ButtonRingBox.IsChecked = s.ButtonRingEnabled;
        ButtonRingIntensityBox.Value = model.ButtonRingIntensity;
        ChromeTextureBox.ItemsSource = ChromeTexture.Catalog;
        ChromeTextureBox.SelectedValue = ChromeTexture.Normalize(s.ChromeTexture);
        ChromeTextureIntensityBox.Value = model.ChromeTextureIntensity;
        TargetAuraBox.ItemsSource = TargetAura.Catalog;
        TargetAuraBox.SelectedValue = TargetAura.Normalize(s.TargetAura);
        TargetAuraIntensityBox.Value = Math.Clamp(s.TargetAuraIntensity, .1, 1);
        AuraModuleBox.IsChecked = s.AuraSelectedModuleEnabled;
        AuraPersonBox.IsChecked = s.AuraSelectedPersonEnabled;
        AuraButtonsBox.IsChecked = s.AuraHighlightButtonsEnabled;
        ApplyTargetAuraPreview();
        Closed += (_, _) =>
        {
            if (!saved)
            {
                ThemeManager.Apply(originalTheme);
                model.PreviewChromeTexture(null, null);
                ButtonEffects.Configure(
                    s.ButtonGlowEnabled, model.ButtonGlowIntensity,
                    s.ButtonSheenEnabled, model.ButtonSheenIntensity,
                    s.ButtonBounceEnabled, model.ButtonBounceIntensity,
                    s.ButtonRingEnabled, model.ButtonRingIntensity);
                TargetAura.Configure(
                    s.TargetAura, s.TargetAuraIntensity,
                    s.AuraSelectedModuleEnabled, s.AuraSelectedPersonEnabled, s.AuraHighlightButtonsEnabled);
            }
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
    private string backgroundPath = "";
    private BitmapImage? chosenBackground;
    private void ShowBackground(ImageSource? image)
    {
        BackgroundPreview.Source = image;
        BackgroundAdjustments.Visibility = image is null ? Visibility.Collapsed : Visibility.Visible;
        RemoveBackgroundButton.IsEnabled = image is not null || backgroundPath.Length > 0;
    }
    private void ChooseBackground_Click(object sender, RoutedEventArgs e)
    {
        var picker = new Microsoft.Win32.OpenFileDialog { Title = "Chat background", Filter = "Images|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.webp" };
        if (picker.ShowDialog(this) != true) return;
        if (Images.LoadChatBackground(picker.FileName) is not { } image)
        {
            BackgroundError.Text = "The image couldn't be opened.";
            return;
        }
        chosenBackground = image;
        BackgroundError.Text = "";
        ShowBackground(image);
    }
    private void RemoveBackground_Click(object sender, RoutedEventArgs e)
    {
        chosenBackground = null;
        backgroundPath = "";
        BackgroundError.Text = "";
        ShowBackground(null);
    }
    private void PreviewTyping_Click(object sender, RoutedEventArgs e) => PreviewEffect((FrameworkElement)sender, TypingEffectBox, typing: true);
    private void PreviewClick_Click(object sender, RoutedEventArgs e) => PreviewEffect((FrameworkElement)sender, ClickEffectBox, typing: false);
    private void PreviewSendPulse_Click(object sender, RoutedEventArgs e)
    {
        var button = (FrameworkElement)sender;
        var center = button.TranslatePoint(new System.Windows.Point(button.ActualWidth / 2, button.ActualHeight / 2), SendPulsePreview);
        SendPulsePreview.PlaySendPulse(center, SendPulseIntensityBox.Value);
    }
    private void PreviewEffect(FrameworkElement button, System.Windows.Controls.ComboBox choice, bool typing)
    {
        var position = button.TranslatePoint(new System.Windows.Point(button.ActualWidth / 2, button.ActualHeight / 2), EffectPreview);
        string? effect = choice.SelectedValue as string;
        double intensity = !typing ? ClickIntensityBox.Value : effect == "ink" ? InkBloomIntensityBox.Value : TypingIntensityBox.Value;
        EffectPreview.Play(position, effect, SelectedTheme, typing, intensity);
    }
    private string SelectedTheme => selectedTheme;
    private void Theme_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!IsInitialized || updatingThemeFilter) return;
        if (ThemeBox.SelectedValue is string theme)
        {
            selectedTheme = theme;
            ThemeManager.Apply(theme);
        }
    }
    private void ThemeFilter_Checked(object sender, RoutedEventArgs e)
    {
        if (!IsInitialized || sender is not FrameworkElement { Tag: string filter }) return;
        FilterThemes(filter);
    }
    private void FilterThemes(string filter)
    {
        updatingThemeFilter = true;
        IEnumerable<ThemeSwatch> items = filter switch
        {
            "dark" => ThemeManager.Catalog.Where(t => t.IsDark && !t.IsSystem),
            "light" => ThemeManager.Catalog.Where(t => !t.IsDark && !t.IsSystem),
            _ => ThemeManager.Catalog
        };
        ThemeBox.ItemsSource = items.ToList();
        ThemeBox.SelectedValue = selectedTheme;
        updatingThemeFilter = false;
    }
    private void AppearanceTab_Checked(object sender, RoutedEventArgs e)
    {
        if (!IsInitialized || AppearanceThemesSection is null || sender is not FrameworkElement { Tag: string tab }) return;
        SelectAppearanceTab(tab);
    }
    /// <summary>Switches the active sub-tab inside Appearance (Themes, Textures, Ambient, Headers, Panels, Aura, or Customize).</summary>
    public void ShowAppearanceTab(string name)
    {
        SelectAppearanceTab(name);
        foreach (var tab in AppearanceTabsPanel.Children.OfType<System.Windows.Controls.RadioButton>())
            if ((string)tab.Tag == name) tab.IsChecked = true;
    }
    private void SelectAppearanceTab(string name)
    {
        AppearanceThemesSection.Visibility = name == "Themes" ? Visibility.Visible : Visibility.Collapsed;
        AppearanceTexturesSection.Visibility = name == "Textures" ? Visibility.Visible : Visibility.Collapsed;
        AppearanceAmbientSection.Visibility = name == "Ambient" ? Visibility.Visible : Visibility.Collapsed;
        AppearanceHeadersSection.Visibility = name == "Headers" ? Visibility.Visible : Visibility.Collapsed;
        AppearancePanelsSection.Visibility = name == "Panels" ? Visibility.Visible : Visibility.Collapsed;
        AppearanceAuraSection.Visibility = name == "Aura" ? Visibility.Visible : Visibility.Collapsed;
        AppearanceCustomizeSection.Visibility = name == "Customize" ? Visibility.Visible : Visibility.Collapsed;
        PreferencesScroller.ScrollToTop();
    }
    private void Page_Checked(object sender, RoutedEventArgs e)
    {
        if (!IsInitialized || GeneralPage is null) return;
        SelectPage((string)((FrameworkElement)sender).Tag);
    }
    /// <summary>Shows a page by name (used by tests and screenshots).</summary>
    public void ShowPage(string name)
    {
        SelectPage(name);
        foreach (var item in NavPanel.Children.OfType<System.Windows.Controls.RadioButton>()) if ((string)item.Tag == name) item.IsChecked = true;
    }
    private void SelectPage(string name)
    {
        foreach (var page in new[] { GeneralPage, AccountPage, MessagesPage, HistoryPage, AlertsPage, NetworkPage, FilesPage, AppearancePage, AboutPage })
            page.Visibility = page.Name == name ? Visibility.Visible : Visibility.Collapsed;
        PreferencesScroller.ScrollToTop();
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
    private void OpenLogs_Click(object sender, RoutedEventArgs e)
    {
        if (!model.HasLogs) return;
        try
        {
            System.IO.Directory.CreateDirectory(model.LogsDirectory);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe", $"\"{model.LogsDirectory}\"") { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception) { model.Toast = "Logs could not be opened."; }
    }
    private async void Connect_Click(object sender, RoutedEventArgs e) => await model.ConnectByAddressAsync(AddressBox.Text);
    private void ButtonEffect_Changed(object sender, RoutedEventArgs e) => ApplyButtonPreview();
    private void ButtonEffect_SliderChanged(object sender, RoutedPropertyChangedEventArgs<double> e) => ApplyButtonPreview();
    private void ChromeTexture_Changed(object sender, SelectionChangedEventArgs e) => ApplyChromeTexturePreview();
    private void ChromeTexture_SliderChanged(object sender, RoutedPropertyChangedEventArgs<double> e) => ApplyChromeTexturePreview();
    private void ApplyChromeTexturePreview()
    {
        if (!IsInitialized) return;
        model.PreviewChromeTexture(ChromeTextureBox.SelectedValue as string, ChromeTextureIntensityBox.Value);
    }
    private void TargetAura_Changed(object sender, SelectionChangedEventArgs e) => ApplyTargetAuraPreview();
    private void TargetAuraTarget_Changed(object sender, RoutedEventArgs e) => ApplyTargetAuraPreview();
    private void TargetAura_SliderChanged(object sender, RoutedPropertyChangedEventArgs<double> e) => ApplyTargetAuraPreview();
    private void ApplyTargetAuraPreview()
    {
        if (!IsInitialized) return;
        string style = TargetAura.Normalize(TargetAuraBox.SelectedValue as string);
        double intensity = TargetAuraIntensityBox.Value;
        bool onModule = AuraModuleBox.IsChecked == true;
        bool onPerson = AuraPersonBox.IsChecked == true;
        bool onButtons = AuraButtonsBox.IsChecked == true;
        TargetAura.Configure(style, intensity, onModule, onPerson, onButtons);
        var kind = TargetAura.KindFor(style);
        TargetAuraPreviewPerson.Kind = kind;
        TargetAuraPreviewPerson.Intensity = intensity;
        TargetAuraPreviewPerson.Visibility = kind != TargetAuraKind.Off && onPerson ? Visibility.Visible : Visibility.Collapsed;
    }
    private void ApplyButtonPreview()
    {
        if (!IsInitialized) return;
        ButtonEffects.Configure(
            ButtonGlowBox.IsChecked == true, ButtonGlowIntensityBox.Value,
            ButtonSheenBox.IsChecked == true, ButtonSheenIntensityBox.Value,
            ButtonBounceBox.IsChecked == true, ButtonBounceIntensityBox.Value,
            ButtonRingBox.IsChecked == true, ButtonRingIntensityBox.Value);
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        var name = DisplayNameBox.Text.Trim();
        if (name.Length == 0) { ShowPage("AccountPage"); DisplayNameBox.Focus(); return; }
        if (!int.TryParse(PortBox.Text.Trim(), out int port) || port is < 1 or > 65535) { ShowPage("NetworkPage"); PortBox.Focus(); return; }
        var multicast = MulticastBox.Text.Trim();
        if (!IPAddress.TryParse(multicast, out var group) || group.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork || group.GetAddressBytes()[0] is < 224 or > 239)
        { ShowPage("NetworkPage"); MulticastBox.Focus(); return; }
        if (chosenBackground is not null)
        {
            try { backgroundPath = model.SaveChatBackground(chosenBackground); }
            catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException or NotSupportedException)
            {
                ShowPage("AppearancePage");
                ShowAppearanceTab("Customize");
                BackgroundError.Text = "The background couldn't be saved.";
                BackgroundError.BringIntoView();
                return;
            }
        }
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
            s.NotificationPreviews = NotificationPreviewsBox.IsChecked == true;
            s.Sounds = SoundsBox.IsChecked == true; s.SoundsWhenBusy = SoundsBusyBox.IsChecked == true; s.SoundsWhenDnd = SoundsDndBox.IsChecked == true;
            s.Port = port; s.Multicast = multicast;
            s.ReceivedFilesFolder = string.Equals(FolderBox.Text, defaultFolder, StringComparison.OrdinalIgnoreCase) ? "" : FolderBox.Text;
            s.AutoAcceptFiles = AutoAcceptBox.IsChecked == true;
            s.Theme = SelectedTheme;
            s.ChatBackgroundPath = backgroundPath;
            s.ChatBackgroundOpacity = BackgroundOpacityBox.Value;
            s.TypingEffect = ChatEffects.Normalize(TypingEffectBox.SelectedValue as string, typing: true);
            s.ClickEffect = ChatEffects.Normalize(ClickEffectBox.SelectedValue as string);
            s.SteampunkEngineEnabled = SteampunkEngineBox.IsChecked == true;
            s.SakuraPetalsEnabled = SakuraPetalsBox.IsChecked == true;
            s.TypingIntensity = TypingIntensityBox.Value;
            s.ClickIntensity = ClickIntensityBox.Value;
            s.ThemeDetailsEnabled = ThemeDetailsBox.IsChecked == true;
            s.EngineIntensity = EngineIntensityBox.Value;
            s.PetalIntensity = PetalIntensityBox.Value;
            s.FirefliesEnabled = FirefliesBox.IsChecked == true;
            s.FireflyIntensity = FireflyIntensityBox.Value;
            s.RainEnabled = RainBox.IsChecked == true;
            s.RainIntensity = RainIntensityBox.Value;
            s.SnowEnabled = SnowBox.IsChecked == true;
            s.SnowIntensity = SnowIntensityBox.Value;
            s.InkBloomIntensity = InkBloomIntensityBox.Value;
            s.SendPulseEnabled = SendPulseBox.IsChecked == true;
            s.SendPulseIntensity = SendPulseIntensityBox.Value;
            s.AmbientOnSidebarEnabled = AmbientOnSidebarBox.IsChecked == true;
            s.HeaderAuroraEnabled = HeaderAuroraBox.IsChecked == true;
            s.HeaderAuroraIntensity = HeaderAuroraIntensityBox.Value;
            s.TitleSparklesEnabled = TitleSparklesBox.IsChecked == true;
            s.TitleSparklesIntensity = TitleSparklesIntensityBox.Value;
            s.HeaderPulseEnabled = HeaderPulseBox.IsChecked == true;
            s.HeaderPulseIntensity = HeaderPulseIntensityBox.Value;
            s.RailBeaconEnabled = RailBeaconBox.IsChecked == true;
            s.RailBeaconIntensity = RailBeaconIntensityBox.Value;
            s.RailStreamEnabled = RailStreamBox.IsChecked == true;
            s.RailStreamIntensity = RailStreamIntensityBox.Value;
            s.RailStardustEnabled = RailStardustBox.IsChecked == true;
            s.RailStardustIntensity = RailStardustIntensityBox.Value;
            s.RailScanEnabled = RailScanBox.IsChecked == true;
            s.RailScanIntensity = RailScanIntensityBox.Value;
            s.SidebarMotesEnabled = SidebarMotesBox.IsChecked == true;
            s.SidebarMotesIntensity = SidebarMotesIntensityBox.Value;
            s.SidebarGlowEnabled = SidebarGlowBox.IsChecked == true;
            s.SidebarGlowIntensity = SidebarGlowIntensityBox.Value;
            s.SidebarPrismEnabled = SidebarPrismBox.IsChecked == true;
            s.SidebarPrismIntensity = SidebarPrismIntensityBox.Value;
            s.SidebarSweepEnabled = SidebarSweepBox.IsChecked == true;
            s.SidebarSweepIntensity = SidebarSweepIntensityBox.Value;
            s.StarfieldEnabled = StarfieldBox.IsChecked == true;
            s.StarfieldIntensity = StarfieldIntensityBox.Value;
            s.BubblesEnabled = BubblesBox.IsChecked == true;
            s.BubblesIntensity = BubblesIntensityBox.Value;
            s.EmbersEnabled = EmbersBox.IsChecked == true;
            s.EmbersIntensity = EmbersIntensityBox.Value;
            s.ButtonGlowEnabled = ButtonGlowBox.IsChecked == true;
            s.ButtonGlowIntensity = ButtonGlowIntensityBox.Value;
            s.ButtonSheenEnabled = ButtonSheenBox.IsChecked == true;
            s.ButtonSheenIntensity = ButtonSheenIntensityBox.Value;
            s.ButtonBounceEnabled = ButtonBounceBox.IsChecked == true;
            s.ButtonBounceIntensity = ButtonBounceIntensityBox.Value;
            s.ButtonRingEnabled = ButtonRingBox.IsChecked == true;
            s.ButtonRingIntensity = ButtonRingIntensityBox.Value;
            s.ChromeTexture = ChromeTexture.Normalize(ChromeTextureBox.SelectedValue as string);
            s.ChromeTextureIntensity = ChromeTextureIntensityBox.Value;
            s.TargetAura = TargetAura.Normalize(TargetAuraBox.SelectedValue as string);
            s.TargetAuraIntensity = TargetAuraIntensityBox.Value;
            s.AuraSelectedModuleEnabled = AuraModuleBox.IsChecked == true;
            s.AuraSelectedPersonEnabled = AuraPersonBox.IsChecked == true;
            s.AuraHighlightButtonsEnabled = AuraButtonsBox.IsChecked == true;
        });
        if (networkChanged) model.Toast = "Restart CydLan to use the new network settings.";
        saved = true; Close();
    }
}
