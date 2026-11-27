using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using CydLan.Core;
using FontFamily = System.Windows.Media.FontFamily;

namespace CydLan.Desktop;

public partial class SettingsWindow : Window
{
    private const string UiFontThemeDefault = "Theme default";
    private readonly MainViewModel model;
    private readonly string originalTheme;
    private readonly AppearanceOverrides originalOverrides;
    private readonly List<AppearancePreset> savedPresets;
    private string selectedTheme;
    private string currentThemeFilter = "all";
    private string accentHex = "", effectHex = "", outgoingBubbleHex = "", incomingBubbleHex = "", chatBackgroundHex = "";
    private double? customFontPx;
    private bool updatingThemeFilter;
    private bool applyingAppearance;
    private bool restoringNavigation;
    private bool closing;
    private bool saved;

    public static string NormalizePage(string? name) => name switch
    {
        "AccountPage" or "MessagesPage" or "HistoryPage" or "AlertsPage" or "NetworkPage" or "FilesPage" or "AppearancePage" or "AboutPage" => name,
        _ => "GeneralPage"
    };

    public static string NormalizeAppearanceTab(string? name) => name switch
    {
        "Colors" or "Bubbles" or "Textures" or "Ambient" or "Headers" or "Panels" or "Aura" or "Companion" or "Customize" => name,
        _ => "Themes"
    };

    public SettingsWindow(MainViewModel model, string? initialPage = null, string? initialAppearanceTab = null, bool rememberNavigation = false)
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
        var systemFonts = Fonts.SystemFontFamilies.Select(f => f.Source).Append("Segoe UI Variable Text").Distinct().OrderBy(n => n).ToList();
        FontBox.ItemsSource = systemFonts;
        UiFontBox.ItemsSource = systemFonts.Prepend(UiFontThemeDefault).ToList();
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
        originalOverrides = AppearanceOverrides.From(s);
        selectedTheme = originalTheme;
        savedPresets = (s.SavedPresets ?? []).Select(p => new AppearancePreset { Id = p.Id, Name = p.Name, Code = p.Code }).ToList();

        applyingAppearance = true;
        ColorFinishBox.ItemsSource = ColorFinish.Catalog;
        AccentColorBox.ItemsSource = AppearanceOptions.AccentSwatches;
        EffectColorBox.ItemsSource = AppearanceOptions.EffectSwatches;
        OutgoingBubbleColorBox.ItemsSource = AppearanceOptions.BubbleSwatches;
        IncomingBubbleColorBox.ItemsSource = AppearanceOptions.BubbleSwatches;
        ChatBackgroundColorBox.ItemsSource = AppearanceOptions.ChatBackgroundSwatches;
        BubbleStyleBox.ItemsSource = AppearanceOptions.BubbleStyles;
        BubbleShapeBox.ItemsSource = AppearanceOptions.BubbleShapes;
        CornerStyleBox.ItemsSource = AppearanceOptions.CornerStyles;
        AvatarShapeBox.ItemsSource = AppearanceOptions.AvatarShapes;
        ChromeTextureBox.ItemsSource = ChromeTexture.Catalog;
        TargetAuraBox.ItemsSource = TargetAura.Catalog;
        ChatCanvasBox.ItemsSource = ChatCanvasOverlay.Catalog;
        BackgroundTransitionBox.ItemsSource = BackgroundSideTransition.Catalog;
        InitCompanionControls();

        backgroundPath = s.ChatBackgroundPath;
        BackgroundOpacityBox.Value = model.ChatBackgroundOpacity;

        LoadAppearanceControls(s);
        RefreshPresets();
        applyingAppearance = false;

        ShowBackground();
        ApplyTargetAuraPreview();
        ApplyBackgroundBlurPreview();
        ApplyBackgroundTransitionPreview();
        SyncBackgroundPreviewVisuals();
        UpdateFontPreview();
        ApplyCompanionPreview();

        if (initialAppearanceTab is not null || initialPage is not null)
        {
            restoringNavigation = true;
            if (initialAppearanceTab is not null) ShowAppearanceTab(NormalizeAppearanceTab(initialAppearanceTab));
            if (initialPage is not null) ShowPage(NormalizePage(initialPage));
            restoringNavigation = false;
        }
        if (rememberNavigation && initialPage is not null) model.LastSettingsPage = NormalizePage(initialPage);
        if (rememberNavigation && initialAppearanceTab is not null) model.LastAppearanceTab = NormalizeAppearanceTab(initialAppearanceTab);

        Closed += (_, _) =>
        {
            if (!saved)
            {
                ThemeManager.Apply(originalTheme, originalOverrides);
                AmbientEffect.SetSpeed(model.EffectSpeed);
                AmbientEffect.SetAnimateWhenUnfocused(s.AnimateWhenUnfocused);
                MessageMotion.Configure(s.MessageAnimation);
                model.PreviewChromeTexture(null, null);
                model.PreviewChatCanvas(null, null);
                model.PreviewSeamlessComposer(null);
                model.PreviewBackgroundBlur(null);
                model.PreviewChatBackground(false, null, null, null);
                model.PreviewBackgroundTransition(null, null);
                model.PreviewBackgroundOptions(null, null, null, null);
                model.PreviewMessageTypography(null, null, null);
                model.PreviewAvatarOptions(null, null);
                model.PreviewPetAppearance(null);
                ButtonEffects.Configure(
                    s.ButtonGlowEnabled, model.ButtonGlowIntensity,
                    s.ButtonSheenEnabled, model.ButtonSheenIntensity,
                    s.ButtonBounceEnabled, model.ButtonBounceIntensity,
                    s.ButtonRingEnabled, model.ButtonRingIntensity,
                    s.ButtonPulseEnabled, model.ButtonPulseIntensity,
                    s.ButtonLiftEnabled, model.ButtonLiftIntensity,
                    s.ButtonSparkEnabled, model.ButtonSparkIntensity);
                TargetAura.Configure(
                    s.TargetAura, s.TargetAuraIntensity,
                    s.AuraSelectedModuleEnabled, s.AuraSelectedPersonEnabled, s.AuraHighlightButtonsEnabled);
            }
            foreach (var path in unsavedAvatars.Where(p => p != model.Settings.AvatarPath)) try { System.IO.File.Delete(path); } catch (System.IO.IOException) { }
            closing = true;
            updatingThemeFilter = true;
            applyingAppearance = true;
            ThemeBox.ItemsSource = null;
            ColorFinishBox.ItemsSource = null;
            AccentColorBox.ItemsSource = null;
            EffectColorBox.ItemsSource = null;
            OutgoingBubbleColorBox.ItemsSource = null;
            IncomingBubbleColorBox.ItemsSource = null;
            ChatBackgroundColorBox.ItemsSource = null;
            BubbleStyleBox.ItemsSource = null;
            BubbleShapeBox.ItemsSource = null;
            CornerStyleBox.ItemsSource = null;
            AvatarShapeBox.ItemsSource = null;
            ChromeTextureBox.ItemsSource = null;
            TargetAuraBox.ItemsSource = null;
            ChatCanvasBox.ItemsSource = null;
            BackgroundTransitionBox.ItemsSource = null;
            FontBox.ItemsSource = null;
            UiFontBox.ItemsSource = null;
            PresetBox.ItemsSource = null;
            BackgroundPreviewHorizontalMask.OpacityMask = null;
            BackgroundPreviewVerticalMask.OpacityMask = null;
            BackgroundPreview.Effect = null;
            AnimatedGifBehavior.SetSequence(BackgroundPreview, null);
            AnimatedGifBehavior.SetFallbackSource(BackgroundPreview, null);
            DisposeCompanionControls();
            AvatarPreview.Source = null;
            chosenBackground = null;
            chosenBackgroundBytes = null;
            chosenBackgroundGif = null;
            DataContext = null;
        };
    }

    private void LoadAppearanceControls(AppSettings s)
    {
        selectedTheme = ThemeManager.Normalize(s.Theme);
        FilterThemes(currentThemeFilter);
        if (ThemeBox.SelectedValue is not string matched || matched != selectedTheme)
        {
            ThemeFilterAll.IsChecked = true;
            FilterThemes("all");
        }
        ThemeDetailsBox.IsChecked = s.ThemeDetailsEnabled;

        ColorFinishBox.SelectedValue = ColorFinish.Normalize(s.ColorFinish);
        ColorFinishIntensityBox.Value = ClampUnit(s.ColorFinishIntensity, .65);
        SetColorChannel(AccentColorBox, AccentHexBox, ref accentHex, s.CustomAccentColor);
        SetColorChannel(EffectColorBox, EffectHexBox, ref effectHex, s.CustomEffectColor);
        SetColorChannel(OutgoingBubbleColorBox, OutgoingBubbleHexBox, ref outgoingBubbleHex, s.CustomOutgoingBubbleColor);
        SetColorChannel(IncomingBubbleColorBox, IncomingBubbleHexBox, ref incomingBubbleHex, s.CustomIncomingBubbleColor);
        bool matchThemeChat = AppearanceOptions.NormalizeChatSurfaceMode(s.ChatSurfaceMode) == AppearanceOptions.ChatSurfaceTheme;
        ChatSurfaceTheme.IsChecked = matchThemeChat;
        ChatSurfaceSeparate.IsChecked = !matchThemeChat;
        TextureOnChatBox.IsChecked = matchThemeChat;
        SetColorChannel(ChatBackgroundColorBox, ChatBackgroundHexBox, ref chatBackgroundHex, s.CustomChatBackgroundColor);

        BubbleStyleBox.SelectedValue = AppearanceOptions.NormalizeBubbleStyle(s.BubbleStyle);
        BubbleShapeBox.SelectedValue = AppearanceOptions.NormalizeBubbleShape(s.BubbleShape);
        switch (AppearanceOptions.NormalizeChatDensity(s.ChatDensity))
        {
            case AppearanceOptions.DensityCompact: DensityCompact.IsChecked = true; break;
            case AppearanceOptions.DensitySpacious: DensitySpacious.IsChecked = true; break;
            default: DensityCozy.IsChecked = true; break;
        }
        switch (MessageMotion.Normalize(s.MessageAnimation))
        {
            case MessageMotion.Fade: AnimFade.IsChecked = true; break;
            case MessageMotion.Slide: AnimSlide.IsChecked = true; break;
            case MessageMotion.Pop: AnimPop.IsChecked = true; break;
            default: AnimOff.IsChecked = true; break;
        }
        CornerStyleBox.SelectedValue = AppearanceOptions.NormalizeCornerStyle(s.CornerStyle);
        AvatarShapeBox.SelectedValue = AppearanceOptions.NormalizeAvatarShape(s.AvatarShape);
        OnlineRingBox.IsChecked = s.OnlineRingEnabled;

        TypingEffectBox.SelectedValue = ChatEffects.Normalize(s.TypingEffect, typing: true);
        ClickEffectBox.SelectedValue = ChatEffects.Normalize(s.ClickEffect);
        SteampunkEngineBox.IsChecked = s.SteampunkEngineEnabled;
        SakuraPetalsBox.IsChecked = s.SakuraPetalsEnabled;
        TypingIntensityBox.Value = ClampUnit(s.TypingIntensity ?? s.EffectIntensity, .75);
        ClickIntensityBox.Value = ClampUnit(s.ClickIntensity ?? s.EffectIntensity, .75);
        EngineIntensityBox.Value = ClampUnit(s.EngineIntensity, .85);
        PetalIntensityBox.Value = ClampUnit(s.PetalIntensity, .75);
        FirefliesBox.IsChecked = s.FirefliesEnabled;
        FireflyIntensityBox.Value = ClampUnit(s.FireflyIntensity, .65);
        RainBox.IsChecked = s.RainEnabled;
        RainIntensityBox.Value = ClampUnit(s.RainIntensity, .65);
        SnowBox.IsChecked = s.SnowEnabled;
        SnowIntensityBox.Value = ClampUnit(s.SnowIntensity, .65);
        StarfieldBox.IsChecked = s.StarfieldEnabled;
        StarfieldIntensityBox.Value = ClampUnit(s.StarfieldIntensity, .65);
        BubblesBox.IsChecked = s.BubblesEnabled;
        BubblesIntensityBox.Value = ClampUnit(s.BubblesIntensity, .65);
        EmbersBox.IsChecked = s.EmbersEnabled;
        EmbersIntensityBox.Value = ClampUnit(s.EmbersIntensity, .65);
        MatrixBox.IsChecked = s.MatrixEnabled;
        MatrixIntensityBox.Value = ClampUnit(s.MatrixIntensity, .65);
        ConstellationBox.IsChecked = s.ConstellationEnabled;
        ConstellationIntensityBox.Value = ClampUnit(s.ConstellationIntensity, .65);
        BokehBox.IsChecked = s.BokehEnabled;
        BokehIntensityBox.Value = ClampUnit(s.BokehIntensity, .65);
        WingPlumesBox.IsChecked = s.WingPlumesEnabled;
        WingPlumesIntensityBox.Value = ClampUnit(s.WingPlumesIntensity, .65);
        RepulsorBox.IsChecked = s.RepulsorEnabled;
        RepulsorIntensityBox.Value = ClampUnit(s.RepulsorIntensity, .65);
        EffectSpeedBox.Value = double.IsFinite(s.EffectSpeed) ? Math.Clamp(s.EffectSpeed, .4, 2.0) : 1.0;
        InkBloomIntensityBox.Value = ClampUnit(s.InkBloomIntensity, .65);
        SendPulseBox.IsChecked = s.SendPulseEnabled;
        SendPulseIntensityBox.Value = ClampUnit(s.SendPulseIntensity, .65);
        AmbientOnSidebarBox.IsChecked = s.AmbientOnSidebarEnabled;
        AnimateWhenUnfocusedBox.IsChecked = s.AnimateWhenUnfocused;

        HeaderAuroraBox.IsChecked = s.HeaderAuroraEnabled;
        HeaderAuroraIntensityBox.Value = ClampUnit(s.HeaderAuroraIntensity, .65);
        TitleSparklesBox.IsChecked = s.TitleSparklesEnabled;
        TitleSparklesIntensityBox.Value = ClampUnit(s.TitleSparklesIntensity, .65);
        HeaderPulseBox.IsChecked = s.HeaderPulseEnabled;
        HeaderPulseIntensityBox.Value = ClampUnit(s.HeaderPulseIntensity, .65);
        TitleShimmerBox.IsChecked = s.TitleShimmerEnabled;
        TitleShimmerIntensityBox.Value = ClampUnit(s.TitleShimmerIntensity, .65);
        TitleCometBox.IsChecked = s.TitleCometEnabled;
        TitleCometIntensityBox.Value = ClampUnit(s.TitleCometIntensity, .65);
        TitleEqualizerBox.IsChecked = s.TitleEqualizerEnabled;
        TitleEqualizerIntensityBox.Value = ClampUnit(s.TitleEqualizerIntensity, .65);
        TitleLaserBox.IsChecked = s.TitleLaserEnabled;
        TitleLaserIntensityBox.Value = ClampUnit(s.TitleLaserIntensity, .65);
        TitleAuroraBox.IsChecked = s.TitleAuroraEnabled;
        TitleAuroraIntensityBox.Value = ClampUnit(s.TitleAuroraIntensity, .65);
        TitleWaveBox.IsChecked = s.TitleWaveEnabled;
        TitleWaveIntensityBox.Value = ClampUnit(s.TitleWaveIntensity, .65);
        TitleWingsBox.IsChecked = s.TitleWingsEnabled;
        TitleWingsIntensityBox.Value = ClampUnit(s.TitleWingsIntensity, .65);
        TitleReactorBox.IsChecked = s.TitleReactorEnabled;
        TitleReactorIntensityBox.Value = ClampUnit(s.TitleReactorIntensity, .65);
        HeaderPrismBox.IsChecked = s.HeaderPrismEnabled;
        HeaderPrismIntensityBox.Value = ClampUnit(s.HeaderPrismIntensity, .65);
        HeaderRipplesBox.IsChecked = s.HeaderRipplesEnabled;
        HeaderRipplesIntensityBox.Value = ClampUnit(s.HeaderRipplesIntensity, .65);
        HeaderCircuitBox.IsChecked = s.HeaderCircuitEnabled;
        HeaderCircuitIntensityBox.Value = ClampUnit(s.HeaderCircuitIntensity, .65);
        HeaderStarlightBox.IsChecked = s.HeaderStarlightEnabled;
        HeaderStarlightIntensityBox.Value = ClampUnit(s.HeaderStarlightIntensity, .65);
        HeaderNebulaBox.IsChecked = s.HeaderNebulaEnabled;
        HeaderNebulaIntensityBox.Value = ClampUnit(s.HeaderNebulaIntensity, .65);
        HeaderSilkBox.IsChecked = s.HeaderSilkEnabled;
        HeaderSilkIntensityBox.Value = ClampUnit(s.HeaderSilkIntensity, .65);
        HeaderPlasmaBox.IsChecked = s.HeaderPlasmaEnabled;
        HeaderPlasmaIntensityBox.Value = ClampUnit(s.HeaderPlasmaIntensity, .65);
        HeaderBurstBox.IsChecked = s.HeaderBurstEnabled;
        HeaderBurstIntensityBox.Value = ClampUnit(s.HeaderBurstIntensity, .65);
        HeaderArcBox.IsChecked = s.HeaderArcEnabled;
        HeaderArcIntensityBox.Value = ClampUnit(s.HeaderArcIntensity, .65);

        RailBeaconBox.IsChecked = s.RailBeaconEnabled;
        RailBeaconIntensityBox.Value = ClampUnit(s.RailBeaconIntensity, .65);
        RailStreamBox.IsChecked = s.RailStreamEnabled;
        RailStreamIntensityBox.Value = ClampUnit(s.RailStreamIntensity, .65);
        RailStardustBox.IsChecked = s.RailStardustEnabled;
        RailStardustIntensityBox.Value = ClampUnit(s.RailStardustIntensity, .65);
        RailScanBox.IsChecked = s.RailScanEnabled;
        RailScanIntensityBox.Value = ClampUnit(s.RailScanIntensity, .65);
        RailAuroraBox.IsChecked = s.RailAuroraEnabled;
        RailAuroraIntensityBox.Value = ClampUnit(s.RailAuroraIntensity, .65);
        RailPulseBox.IsChecked = s.RailPulseEnabled;
        RailPulseIntensityBox.Value = ClampUnit(s.RailPulseIntensity, .65);
        RailPlasmaBox.IsChecked = s.RailPlasmaEnabled;
        RailPlasmaIntensityBox.Value = ClampUnit(s.RailPlasmaIntensity, .65);
        RailCascadeBox.IsChecked = s.RailCascadeEnabled;
        RailCascadeIntensityBox.Value = ClampUnit(s.RailCascadeIntensity, .65);
        RailWingsBox.IsChecked = s.RailWingsEnabled;
        RailWingsIntensityBox.Value = ClampUnit(s.RailWingsIntensity, .65);
        RailArcBox.IsChecked = s.RailArcEnabled;
        RailArcIntensityBox.Value = ClampUnit(s.RailArcIntensity, .65);

        SidebarMotesBox.IsChecked = s.SidebarMotesEnabled;
        SidebarMotesIntensityBox.Value = ClampUnit(s.SidebarMotesIntensity, .65);
        SidebarGlowBox.IsChecked = s.SidebarGlowEnabled;
        SidebarGlowIntensityBox.Value = ClampUnit(s.SidebarGlowIntensity, .65);
        SidebarPrismBox.IsChecked = s.SidebarPrismEnabled;
        SidebarPrismIntensityBox.Value = ClampUnit(s.SidebarPrismIntensity, .65);
        SidebarSweepBox.IsChecked = s.SidebarSweepEnabled;
        SidebarSweepIntensityBox.Value = ClampUnit(s.SidebarSweepIntensity, .65);
        SidebarAuroraBox.IsChecked = s.SidebarAuroraEnabled;
        SidebarAuroraIntensityBox.Value = ClampUnit(s.SidebarAuroraIntensity, .65);
        SidebarNebulaBox.IsChecked = s.SidebarNebulaEnabled;
        SidebarNebulaIntensityBox.Value = ClampUnit(s.SidebarNebulaIntensity, .65);
        SidebarWavesBox.IsChecked = s.SidebarWavesEnabled;
        SidebarWavesIntensityBox.Value = ClampUnit(s.SidebarWavesIntensity, .65);
        SidebarRipplesBox.IsChecked = s.SidebarRipplesEnabled;
        SidebarRipplesIntensityBox.Value = ClampUnit(s.SidebarRipplesIntensity, .65);
        SidebarCircuitBox.IsChecked = s.SidebarCircuitEnabled;
        SidebarCircuitIntensityBox.Value = ClampUnit(s.SidebarCircuitIntensity, .65);
        SidebarWingsBox.IsChecked = s.SidebarWingsEnabled;
        SidebarWingsIntensityBox.Value = ClampUnit(s.SidebarWingsIntensity, .65);
        SidebarHudBox.IsChecked = s.SidebarHudEnabled;
        SidebarHudIntensityBox.Value = ClampUnit(s.SidebarHudIntensity, .65);

        ButtonGlowBox.IsChecked = s.ButtonGlowEnabled;
        ButtonGlowIntensityBox.Value = ClampUnit(s.ButtonGlowIntensity, .65);
        ButtonSheenBox.IsChecked = s.ButtonSheenEnabled;
        ButtonSheenIntensityBox.Value = ClampUnit(s.ButtonSheenIntensity, .65);
        ButtonBounceBox.IsChecked = s.ButtonBounceEnabled;
        ButtonBounceIntensityBox.Value = ClampUnit(s.ButtonBounceIntensity, .65);
        ButtonRingBox.IsChecked = s.ButtonRingEnabled;
        ButtonRingIntensityBox.Value = ClampUnit(s.ButtonRingIntensity, .65);
        ButtonPulseBox.IsChecked = s.ButtonPulseEnabled;
        ButtonPulseIntensityBox.Value = ClampUnit(s.ButtonPulseIntensity, .65);
        ButtonLiftBox.IsChecked = s.ButtonLiftEnabled;
        ButtonLiftIntensityBox.Value = ClampUnit(s.ButtonLiftIntensity, .65);
        ButtonSparkBox.IsChecked = s.ButtonSparkEnabled;
        ButtonSparkIntensityBox.Value = ClampUnit(s.ButtonSparkIntensity, .65);

        ChromeTextureBox.SelectedValue = ChromeTexture.Normalize(s.ChromeTexture);
        ChromeTextureIntensityBox.Value = ClampUnit(s.ChromeTextureIntensity, .65);
        TargetAuraBox.SelectedValue = TargetAura.Normalize(s.TargetAura);
        TargetAuraIntensityBox.Value = ClampUnit(s.TargetAuraIntensity, .70);
        AuraModuleBox.IsChecked = s.AuraSelectedModuleEnabled;
        AuraPersonBox.IsChecked = s.AuraSelectedPersonEnabled;
        AuraButtonsBox.IsChecked = s.AuraHighlightButtonsEnabled;

        string uiFont = (s.UiFont ?? "").Trim();
        SelectFontItem(UiFontBox, uiFont.Length == 0 ? UiFontThemeDefault : uiFont, UiFontThemeDefault);
        SelectFontItem(FontBox, s.MessageFont, "Segoe UI Variable Text");
        SizeBox.SelectedItem = SizeBox.Items.Cast<ComboBoxItem>().FirstOrDefault(i => (string)i.Tag == s.MessageFontSize) ?? SizeBox.Items[1];
        customFontPx = s.MessageFontSizePx is { } px && double.IsFinite(px) ? Math.Clamp(Math.Round(px), 11, 22) : null;
        MessageFontPxBox.Value = customFontPx ?? MainViewModel.FontSizeFor(SelectedFontSizeTag);
        WindowOpacityBox.Value = double.IsFinite(s.WindowOpacity) ? Math.Clamp(s.WindowOpacity, .80, 1.0) : 1.0;

        ChatCanvasBox.SelectedValue = ChatCanvasOverlay.Normalize(s.ChatCanvasGradient);
        ChatCanvasIntensityBox.Value = ClampUnit(s.ChatCanvasGradientIntensity, .55);
        SeamlessComposerBox.IsChecked = s.SeamlessComposerEnabled;
        BackgroundBlurBox.Value = double.IsFinite(s.ChatBackgroundBlur) ? Math.Clamp(s.ChatBackgroundBlur, 0, 24) : 0;
        BackgroundTransitionBox.SelectedValue = BackgroundSideTransition.Normalize(s.ChatBackgroundTransition);
        BackgroundTransitionStrengthBox.Value = ClampUnit(s.ChatBackgroundTransitionStrength, .55);
        BackgroundDimBox.Value = double.IsFinite(s.ChatBackgroundDim) ? Math.Clamp(s.ChatBackgroundDim, 0, .80) : 0;
        BackgroundScaleBox.Value = double.IsFinite(s.ChatBackgroundScale) ? Math.Clamp(s.ChatBackgroundScale, 1.0, 1.6) : 1.0;
        switch (AppearanceCodec.NormalizeBgFit(s.ChatBackgroundFit))
        {
            case "fit": BgFitFit.IsChecked = true; break;
            case "stretch": BgFitStretch.IsChecked = true; break;
            case "center": BgFitCenter.IsChecked = true; break;
            default: BgFitFill.IsChecked = true; break;
        }
        string anchor = BackgroundAnchor.Normalize(s.ChatBackgroundAnchor);
        (anchor switch
        {
            BackgroundAnchor.TopLeft => BgAnchorTopLeft,
            BackgroundAnchor.Top => BgAnchorTop,
            BackgroundAnchor.TopRight => BgAnchorTopRight,
            BackgroundAnchor.Left => BgAnchorLeft,
            BackgroundAnchor.Right => BgAnchorRight,
            BackgroundAnchor.BottomLeft => BgAnchorBottomLeft,
            BackgroundAnchor.Bottom => BgAnchorBottom,
            BackgroundAnchor.BottomRight => BgAnchorBottomRight,
            _ => BgAnchorCenter
        }).IsChecked = true;
        BackgroundAnchorLabel.Text = BackgroundAnchor.DisplayName(anchor);
        BackgroundGrayscaleBox.IsChecked = s.ChatBackgroundGrayscale;
        LoadCompanionControls(s);
    }

    private static void SelectFontItem(System.Windows.Controls.ComboBox box, string? requested, string fallback)
    {
        string target = (requested ?? "").Trim();
        var items = box.Items.OfType<string>().ToList();
        string? match = target.Length > 0
            ? items.FirstOrDefault(f => string.Equals(f, target, StringComparison.OrdinalIgnoreCase))
            : null;
        box.SelectedItem = match ?? items.FirstOrDefault(f => string.Equals(f, fallback, StringComparison.OrdinalIgnoreCase)) ?? fallback;
    }

    private void WriteAppearanceTo(AppSettings s)
    {
        s.Theme = SelectedTheme;
        s.ThemeDetailsEnabled = ThemeDetailsBox.IsChecked == true;
        s.ColorFinish = ColorFinish.Normalize(ColorFinishBox.SelectedValue as string);
        s.ColorFinishIntensity = ColorFinishIntensityBox.Value;
        s.CustomAccentColor = accentHex;
        s.CustomEffectColor = effectHex;
        s.CustomOutgoingBubbleColor = outgoingBubbleHex;
        s.CustomIncomingBubbleColor = incomingBubbleHex;
        s.ChatSurfaceMode = SelectedChatSurfaceMode;
        s.CustomChatBackgroundColor = chatBackgroundHex;
        s.BubbleStyle = AppearanceOptions.NormalizeBubbleStyle(BubbleStyleBox.SelectedValue as string);
        s.BubbleShape = AppearanceOptions.NormalizeBubbleShape(BubbleShapeBox.SelectedValue as string);
        s.ChatDensity = SelectedChatDensity;
        s.CornerStyle = AppearanceOptions.NormalizeCornerStyle(CornerStyleBox.SelectedValue as string);
        s.AvatarShape = AppearanceOptions.NormalizeAvatarShape(AvatarShapeBox.SelectedValue as string);
        s.OnlineRingEnabled = OnlineRingBox.IsChecked == true;
        s.UiFont = SelectedUiFont;
        s.MessageFont = FontBox.SelectedItem as string ?? "Segoe UI Variable Text";
        s.MessageFontSize = SelectedFontSizeTag;
        s.MessageFontSizePx = customFontPx;
        s.WindowOpacity = WindowOpacityBox.Value;
        s.EffectSpeed = EffectSpeedBox.Value;
        s.MessageAnimation = SelectedMessageAnimation;
        s.ChatCanvasGradient = ChatCanvasOverlay.Normalize(ChatCanvasBox.SelectedValue as string);
        s.ChatCanvasGradientIntensity = ChatCanvasIntensityBox.Value;
        s.SeamlessComposerEnabled = SeamlessComposerBox.IsChecked == true;
        s.ChatBackgroundBlur = BackgroundBlurBox.Value;
        s.ChatBackgroundOpacity = double.IsFinite(BackgroundOpacityBox.Value) ? Math.Clamp(BackgroundOpacityBox.Value, .10, 1.0) : .26;
        s.ChatBackgroundTransition = BackgroundSideTransition.Normalize(BackgroundTransitionBox.SelectedValue as string);
        s.ChatBackgroundTransitionStrength = ClampUnit(BackgroundTransitionStrengthBox.Value, .55);
        s.ChatBackgroundDim = BackgroundDimBox.Value;
        s.ChatBackgroundScale = BackgroundScaleBox.Value;
        s.ChatBackgroundFit = SelectedBackgroundFit;
        s.ChatBackgroundAnchor = SelectedBackgroundAnchor;
        s.ChatBackgroundGrayscale = IsGrayscaleChecked;
        WriteCompanionTo(s);
        s.TypingEffect = ChatEffects.Normalize(TypingEffectBox.SelectedValue as string, typing: true);
        s.ClickEffect = ChatEffects.Normalize(ClickEffectBox.SelectedValue as string);
        s.SteampunkEngineEnabled = SteampunkEngineBox.IsChecked == true;
        s.SakuraPetalsEnabled = SakuraPetalsBox.IsChecked == true;
        s.TypingIntensity = TypingIntensityBox.Value;
        s.ClickIntensity = ClickIntensityBox.Value;
        s.EngineIntensity = EngineIntensityBox.Value;
        s.PetalIntensity = PetalIntensityBox.Value;
        s.FirefliesEnabled = FirefliesBox.IsChecked == true;
        s.FireflyIntensity = FireflyIntensityBox.Value;
        s.RainEnabled = RainBox.IsChecked == true;
        s.RainIntensity = RainIntensityBox.Value;
        s.SnowEnabled = SnowBox.IsChecked == true;
        s.SnowIntensity = SnowIntensityBox.Value;
        s.StarfieldEnabled = StarfieldBox.IsChecked == true;
        s.StarfieldIntensity = StarfieldIntensityBox.Value;
        s.BubblesEnabled = BubblesBox.IsChecked == true;
        s.BubblesIntensity = BubblesIntensityBox.Value;
        s.EmbersEnabled = EmbersBox.IsChecked == true;
        s.EmbersIntensity = EmbersIntensityBox.Value;
        s.MatrixEnabled = MatrixBox.IsChecked == true;
        s.MatrixIntensity = MatrixIntensityBox.Value;
        s.ConstellationEnabled = ConstellationBox.IsChecked == true;
        s.ConstellationIntensity = ConstellationIntensityBox.Value;
        s.BokehEnabled = BokehBox.IsChecked == true;
        s.BokehIntensity = BokehIntensityBox.Value;
        s.WingPlumesEnabled = WingPlumesBox.IsChecked == true;
        s.WingPlumesIntensity = WingPlumesIntensityBox.Value;
        s.RepulsorEnabled = RepulsorBox.IsChecked == true;
        s.RepulsorIntensity = RepulsorIntensityBox.Value;
        s.InkBloomIntensity = InkBloomIntensityBox.Value;
        s.SendPulseEnabled = SendPulseBox.IsChecked == true;
        s.SendPulseIntensity = SendPulseIntensityBox.Value;
        s.AmbientOnSidebarEnabled = AmbientOnSidebarBox.IsChecked == true;
        s.AnimateWhenUnfocused = AnimateWhenUnfocusedBox.IsChecked == true;
        s.HeaderAuroraEnabled = HeaderAuroraBox.IsChecked == true;
        s.HeaderAuroraIntensity = HeaderAuroraIntensityBox.Value;
        s.TitleSparklesEnabled = TitleSparklesBox.IsChecked == true;
        s.TitleSparklesIntensity = TitleSparklesIntensityBox.Value;
        s.HeaderPulseEnabled = HeaderPulseBox.IsChecked == true;
        s.HeaderPulseIntensity = HeaderPulseIntensityBox.Value;
        s.TitleShimmerEnabled = TitleShimmerBox.IsChecked == true;
        s.TitleShimmerIntensity = TitleShimmerIntensityBox.Value;
        s.TitleCometEnabled = TitleCometBox.IsChecked == true;
        s.TitleCometIntensity = TitleCometIntensityBox.Value;
        s.TitleEqualizerEnabled = TitleEqualizerBox.IsChecked == true;
        s.TitleEqualizerIntensity = TitleEqualizerIntensityBox.Value;
        s.TitleLaserEnabled = TitleLaserBox.IsChecked == true;
        s.TitleLaserIntensity = TitleLaserIntensityBox.Value;
        s.TitleAuroraEnabled = TitleAuroraBox.IsChecked == true;
        s.TitleAuroraIntensity = TitleAuroraIntensityBox.Value;
        s.TitleWaveEnabled = TitleWaveBox.IsChecked == true;
        s.TitleWaveIntensity = TitleWaveIntensityBox.Value;
        s.TitleWingsEnabled = TitleWingsBox.IsChecked == true;
        s.TitleWingsIntensity = TitleWingsIntensityBox.Value;
        s.TitleReactorEnabled = TitleReactorBox.IsChecked == true;
        s.TitleReactorIntensity = TitleReactorIntensityBox.Value;
        s.HeaderPrismEnabled = HeaderPrismBox.IsChecked == true;
        s.HeaderPrismIntensity = HeaderPrismIntensityBox.Value;
        s.HeaderRipplesEnabled = HeaderRipplesBox.IsChecked == true;
        s.HeaderRipplesIntensity = HeaderRipplesIntensityBox.Value;
        s.HeaderCircuitEnabled = HeaderCircuitBox.IsChecked == true;
        s.HeaderCircuitIntensity = HeaderCircuitIntensityBox.Value;
        s.HeaderStarlightEnabled = HeaderStarlightBox.IsChecked == true;
        s.HeaderStarlightIntensity = HeaderStarlightIntensityBox.Value;
        s.HeaderNebulaEnabled = HeaderNebulaBox.IsChecked == true;
        s.HeaderNebulaIntensity = HeaderNebulaIntensityBox.Value;
        s.HeaderSilkEnabled = HeaderSilkBox.IsChecked == true;
        s.HeaderSilkIntensity = HeaderSilkIntensityBox.Value;
        s.HeaderPlasmaEnabled = HeaderPlasmaBox.IsChecked == true;
        s.HeaderPlasmaIntensity = HeaderPlasmaIntensityBox.Value;
        s.HeaderBurstEnabled = HeaderBurstBox.IsChecked == true;
        s.HeaderBurstIntensity = HeaderBurstIntensityBox.Value;
        s.HeaderArcEnabled = HeaderArcBox.IsChecked == true;
        s.HeaderArcIntensity = HeaderArcIntensityBox.Value;
        s.RailBeaconEnabled = RailBeaconBox.IsChecked == true;
        s.RailBeaconIntensity = RailBeaconIntensityBox.Value;
        s.RailStreamEnabled = RailStreamBox.IsChecked == true;
        s.RailStreamIntensity = RailStreamIntensityBox.Value;
        s.RailStardustEnabled = RailStardustBox.IsChecked == true;
        s.RailStardustIntensity = RailStardustIntensityBox.Value;
        s.RailScanEnabled = RailScanBox.IsChecked == true;
        s.RailScanIntensity = RailScanIntensityBox.Value;
        s.RailAuroraEnabled = RailAuroraBox.IsChecked == true;
        s.RailAuroraIntensity = RailAuroraIntensityBox.Value;
        s.RailPulseEnabled = RailPulseBox.IsChecked == true;
        s.RailPulseIntensity = RailPulseIntensityBox.Value;
        s.RailPlasmaEnabled = RailPlasmaBox.IsChecked == true;
        s.RailPlasmaIntensity = RailPlasmaIntensityBox.Value;
        s.RailCascadeEnabled = RailCascadeBox.IsChecked == true;
        s.RailCascadeIntensity = RailCascadeIntensityBox.Value;
        s.RailWingsEnabled = RailWingsBox.IsChecked == true;
        s.RailWingsIntensity = RailWingsIntensityBox.Value;
        s.RailArcEnabled = RailArcBox.IsChecked == true;
        s.RailArcIntensity = RailArcIntensityBox.Value;
        s.SidebarMotesEnabled = SidebarMotesBox.IsChecked == true;
        s.SidebarMotesIntensity = SidebarMotesIntensityBox.Value;
        s.SidebarGlowEnabled = SidebarGlowBox.IsChecked == true;
        s.SidebarGlowIntensity = SidebarGlowIntensityBox.Value;
        s.SidebarPrismEnabled = SidebarPrismBox.IsChecked == true;
        s.SidebarPrismIntensity = SidebarPrismIntensityBox.Value;
        s.SidebarSweepEnabled = SidebarSweepBox.IsChecked == true;
        s.SidebarSweepIntensity = SidebarSweepIntensityBox.Value;
        s.SidebarAuroraEnabled = SidebarAuroraBox.IsChecked == true;
        s.SidebarAuroraIntensity = SidebarAuroraIntensityBox.Value;
        s.SidebarNebulaEnabled = SidebarNebulaBox.IsChecked == true;
        s.SidebarNebulaIntensity = SidebarNebulaIntensityBox.Value;
        s.SidebarWavesEnabled = SidebarWavesBox.IsChecked == true;
        s.SidebarWavesIntensity = SidebarWavesIntensityBox.Value;
        s.SidebarRipplesEnabled = SidebarRipplesBox.IsChecked == true;
        s.SidebarRipplesIntensity = SidebarRipplesIntensityBox.Value;
        s.SidebarCircuitEnabled = SidebarCircuitBox.IsChecked == true;
        s.SidebarCircuitIntensity = SidebarCircuitIntensityBox.Value;
        s.SidebarWingsEnabled = SidebarWingsBox.IsChecked == true;
        s.SidebarWingsIntensity = SidebarWingsIntensityBox.Value;
        s.SidebarHudEnabled = SidebarHudBox.IsChecked == true;
        s.SidebarHudIntensity = SidebarHudIntensityBox.Value;
        s.ButtonGlowEnabled = ButtonGlowBox.IsChecked == true;
        s.ButtonGlowIntensity = ButtonGlowIntensityBox.Value;
        s.ButtonSheenEnabled = ButtonSheenBox.IsChecked == true;
        s.ButtonSheenIntensity = ButtonSheenIntensityBox.Value;
        s.ButtonBounceEnabled = ButtonBounceBox.IsChecked == true;
        s.ButtonBounceIntensity = ButtonBounceIntensityBox.Value;
        s.ButtonRingEnabled = ButtonRingBox.IsChecked == true;
        s.ButtonRingIntensity = ButtonRingIntensityBox.Value;
        s.ButtonPulseEnabled = ButtonPulseBox.IsChecked == true;
        s.ButtonPulseIntensity = ButtonPulseIntensityBox.Value;
        s.ButtonLiftEnabled = ButtonLiftBox.IsChecked == true;
        s.ButtonLiftIntensity = ButtonLiftIntensityBox.Value;
        s.ButtonSparkEnabled = ButtonSparkBox.IsChecked == true;
        s.ButtonSparkIntensity = ButtonSparkIntensityBox.Value;
        s.ChromeTexture = ChromeTexture.Normalize(ChromeTextureBox.SelectedValue as string);
        s.ChromeTextureIntensity = ChromeTextureIntensityBox.Value;
        s.TargetAura = TargetAura.Normalize(TargetAuraBox.SelectedValue as string);
        s.TargetAuraIntensity = TargetAuraIntensityBox.Value;
        s.AuraSelectedModuleEnabled = AuraModuleBox.IsChecked == true;
        s.AuraSelectedPersonEnabled = AuraPersonBox.IsChecked == true;
        s.AuraHighlightButtonsEnabled = AuraButtonsBox.IsChecked == true;
    }

    private string SelectedChatSurfaceMode =>
        ChatSurfaceTheme.IsChecked == true ? AppearanceOptions.ChatSurfaceTheme : AppearanceOptions.ChatSurfaceSeparate;

    private string SelectedChatDensity =>
        DensityCompact.IsChecked == true ? AppearanceOptions.DensityCompact
        : DensitySpacious.IsChecked == true ? AppearanceOptions.DensitySpacious
        : AppearanceOptions.DensityCozy;

    private string SelectedMessageAnimation =>
        AnimFade.IsChecked == true ? MessageMotion.Fade
        : AnimSlide.IsChecked == true ? MessageMotion.Slide
        : AnimPop.IsChecked == true ? MessageMotion.Pop
        : MessageMotion.Off;

    private string SelectedUiFont => UiFontBox.SelectedItem is string font && font != UiFontThemeDefault ? font : "";
    private string SelectedFontSizeTag => (SizeBox.SelectedItem as ComboBoxItem)?.Tag as string ?? "medium";
    private static double ClampUnit(double value, double fallback) => double.IsFinite(value) ? Math.Clamp(value, .1, 1) : fallback;

    private AppearanceOverrides BuildCurrentOverrides() => new(
        accentHex,
        effectHex,
        outgoingBubbleHex,
        incomingBubbleHex,
        AppearanceOptions.NormalizeBubbleStyle(BubbleStyleBox.SelectedValue as string),
        AppearanceOptions.NormalizeBubbleShape(BubbleShapeBox.SelectedValue as string),
        SelectedChatDensity,
        AppearanceOptions.NormalizeCornerStyle(CornerStyleBox.SelectedValue as string),
        AppearanceOptions.NormalizeAvatarShape(AvatarShapeBox.SelectedValue as string),
        SelectedUiFont,
        WindowOpacityBox.Value,
        SelectedChatSurfaceMode,
        chatBackgroundHex,
        ColorFinish.Normalize(ColorFinishBox.SelectedValue as string),
        ColorFinishIntensityBox.Value);

    private void ApplyLiveThemeOverrides()
    {
        if (!IsInitialized || closing || applyingAppearance) return;
        var overrides = BuildCurrentOverrides();
        ThemeManager.Apply(SelectedTheme, overrides);
        model.PreviewAvatarOptions(overrides.AvatarShape, OnlineRingBox.IsChecked == true);
    }

    private void ApplyAllLivePreviews()
    {
        if (!IsInitialized || closing) return;
        ApplyLiveThemeOverrides();
        AmbientEffect.SetSpeed(EffectSpeedBox.Value);
        AmbientEffect.SetAnimateWhenUnfocused(AnimateWhenUnfocusedBox.IsChecked == true);
        MessageMotion.Configure(SelectedMessageAnimation);
        ApplyChromeTexturePreview();
        ApplyChatCanvasPreview();
        ApplySeamlessComposerPreview();
        ApplyBackgroundBlurPreview();
        ApplyBackgroundTransitionPreview();
        ApplyBackgroundOptionsPreview();
        ApplyChatBackgroundPreview();
        ApplyButtonPreview();
        ApplyTargetAuraPreview();
        UpdateFontPreview();
        ApplyMessageTypographyPreview();
        ApplyCompanionPreview();
        ShowBackground();
    }

    private void SetColorChannel(System.Windows.Controls.ListBox box, System.Windows.Controls.TextBox hexBox, ref string backingField, string? value)
    {
        string normalized = AppearanceOptions.NormalizeHex(value);
        backingField = normalized;
        hexBox.Text = normalized;
        if (normalized.Length == 0)
            box.SelectedValue = "";
        else
        {
            box.SelectedValue = normalized;
            if (box.SelectedValue as string != normalized) box.SelectedItem = null;
        }
    }

    private void ColorOverride_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!IsInitialized || closing || applyingAppearance) return;
        if (sender is not System.Windows.Controls.ListBox box || box.SelectedValue is not string value) return;
        applyingAppearance = true;
        if (box == AccentColorBox) { accentHex = value; AccentHexBox.Text = value; }
        else if (box == EffectColorBox) { effectHex = value; EffectHexBox.Text = value; }
        else if (box == OutgoingBubbleColorBox) { outgoingBubbleHex = value; OutgoingBubbleHexBox.Text = value; }
        else if (box == IncomingBubbleColorBox) { incomingBubbleHex = value; IncomingBubbleHexBox.Text = value; }
        else if (box == ChatBackgroundColorBox) { chatBackgroundHex = value; ChatBackgroundHexBox.Text = value; }
        applyingAppearance = false;
        ApplyLiveThemeOverrides();
    }

    private void ChatSurface_Checked(object sender, RoutedEventArgs e)
    {
        if (!IsInitialized || closing || applyingAppearance) return;
        applyingAppearance = true;
        TextureOnChatBox.IsChecked = ChatSurfaceTheme.IsChecked == true;
        applyingAppearance = false;
        ApplyLiveThemeOverrides();
    }

    private void TextureOnChat_Changed(object sender, RoutedEventArgs e)
    {
        if (!IsInitialized || closing || applyingAppearance) return;
        bool onChat = TextureOnChatBox.IsChecked == true;
        applyingAppearance = true;
        ChatSurfaceTheme.IsChecked = onChat;
        ChatSurfaceSeparate.IsChecked = !onChat;
        applyingAppearance = false;
        ApplyLiveThemeOverrides();
    }

    private void ColorHex_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        CommitHexInput(sender);
        e.Handled = true;
    }

    private void ColorHex_Committed(object sender, KeyboardFocusChangedEventArgs e) => CommitHexInput(sender);

    private void CommitHexInput(object sender)
    {
        if (!IsInitialized || closing || applyingAppearance || sender is not System.Windows.Controls.TextBox box) return;
        string raw = box.Text.Trim();
        string normalized;
        if (raw.Length == 0) normalized = "";
        else if (AppearanceOptions.TryParseHex(raw, out _)) normalized = AppearanceOptions.NormalizeHex(raw);
        else
        {
            applyingAppearance = true;
            box.Text = box == AccentHexBox ? accentHex
                : box == EffectHexBox ? effectHex
                : box == OutgoingBubbleHexBox ? outgoingBubbleHex
                : box == IncomingBubbleHexBox ? incomingBubbleHex
                : chatBackgroundHex;
            applyingAppearance = false;
            return;
        }
        applyingAppearance = true;
        if (box == AccentHexBox) SetColorChannel(AccentColorBox, AccentHexBox, ref accentHex, normalized);
        else if (box == EffectHexBox) SetColorChannel(EffectColorBox, EffectHexBox, ref effectHex, normalized);
        else if (box == OutgoingBubbleHexBox) SetColorChannel(OutgoingBubbleColorBox, OutgoingBubbleHexBox, ref outgoingBubbleHex, normalized);
        else if (box == IncomingBubbleHexBox) SetColorChannel(IncomingBubbleColorBox, IncomingBubbleHexBox, ref incomingBubbleHex, normalized);
        else if (box == ChatBackgroundHexBox) SetColorChannel(ChatBackgroundColorBox, ChatBackgroundHexBox, ref chatBackgroundHex, normalized);
        applyingAppearance = false;
        ApplyLiveThemeOverrides();
    }

    private void StyleOverride_Changed(object sender, SelectionChangedEventArgs e) => ApplyLiveThemeOverrides();
    private void ColorFinish_Changed(object sender, SelectionChangedEventArgs e) => ApplyLiveThemeOverrides();
    private void ColorFinish_SliderChanged(object sender, RoutedPropertyChangedEventArgs<double> e) => ApplyLiveThemeOverrides();
    private void Density_Checked(object sender, RoutedEventArgs e) => ApplyLiveThemeOverrides();
    private void AvatarOption_Changed(object sender, RoutedEventArgs e) => ApplyLiveThemeOverrides();
    private void UiFont_Changed(object sender, SelectionChangedEventArgs e) => ApplyLiveThemeOverrides();

    private void MessageAnim_Checked(object sender, RoutedEventArgs e)
    {
        if (!IsInitialized || closing || applyingAppearance) return;
        MessageMotion.Configure(SelectedMessageAnimation);
    }

    private void EffectSpeed_SliderChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!IsInitialized || closing || applyingAppearance) return;
        AmbientEffect.SetSpeed(EffectSpeedBox.Value);
    }

    private void AnimateUnfocused_Changed(object sender, RoutedEventArgs e)
    {
        if (!IsInitialized || closing || applyingAppearance) return;
        AmbientEffect.SetAnimateWhenUnfocused(AnimateWhenUnfocusedBox.IsChecked == true);
    }

    private void WindowOpacity_SliderChanged(object sender, RoutedPropertyChangedEventArgs<double> e) => ApplyLiveThemeOverrides();

    private void ChatCanvas_Changed(object sender, SelectionChangedEventArgs e) => ApplyChatCanvasPreview();
    private void ChatCanvas_SliderChanged(object sender, RoutedPropertyChangedEventArgs<double> e) => ApplyChatCanvasPreview();
    private void ApplyChatCanvasPreview()
    {
        if (!IsInitialized || closing || applyingAppearance) return;
        model.PreviewChatCanvas(ChatCanvasBox.SelectedValue as string, ChatCanvasIntensityBox.Value);
    }

    private void SeamlessComposer_Changed(object sender, RoutedEventArgs e) => ApplySeamlessComposerPreview();
    private void ApplySeamlessComposerPreview()
    {
        if (!IsInitialized || closing || applyingAppearance) return;
        model.PreviewSeamlessComposer(SeamlessComposerBox.IsChecked == true);
    }

    private string SelectedBackgroundFit =>
        BgFitFit.IsChecked == true ? "fit"
        : BgFitStretch.IsChecked == true ? "stretch"
        : BgFitCenter.IsChecked == true ? "center"
        : "fill";
    private string SelectedBackgroundAnchor =>
        BgAnchorTopLeft.IsChecked == true ? BackgroundAnchor.TopLeft
        : BgAnchorTop.IsChecked == true ? BackgroundAnchor.Top
        : BgAnchorTopRight.IsChecked == true ? BackgroundAnchor.TopRight
        : BgAnchorLeft.IsChecked == true ? BackgroundAnchor.Left
        : BgAnchorRight.IsChecked == true ? BackgroundAnchor.Right
        : BgAnchorBottomLeft.IsChecked == true ? BackgroundAnchor.BottomLeft
        : BgAnchorBottom.IsChecked == true ? BackgroundAnchor.Bottom
        : BgAnchorBottomRight.IsChecked == true ? BackgroundAnchor.BottomRight
        : BackgroundAnchor.Center;
    private bool IsGrayscaleChecked => BackgroundGrayscaleBox.IsChecked == true;
    private void BackgroundBlur_SliderChanged(object sender, RoutedPropertyChangedEventArgs<double> e) => ApplyBackgroundBlurPreview();
    private void BackgroundOpacity_SliderChanged(object sender, RoutedPropertyChangedEventArgs<double> e) => ApplyChatBackgroundPreview();
    private void BackgroundTransition_Changed(object sender, SelectionChangedEventArgs e) => ApplyBackgroundTransitionPreview();
    private void BackgroundTransition_SliderChanged(object sender, RoutedPropertyChangedEventArgs<double> e) => ApplyBackgroundTransitionPreview();
    private void BackgroundOptions_SliderChanged(object sender, RoutedPropertyChangedEventArgs<double> e) => ApplyBackgroundOptionsPreview();
    private void BackgroundFit_Checked(object sender, RoutedEventArgs e) => ApplyBackgroundOptionsPreview();
    private void BackgroundAnchor_Checked(object sender, RoutedEventArgs e) => ApplyBackgroundOptionsPreview();
    private void BackgroundGrayscale_Changed(object sender, RoutedEventArgs e) => ApplyBackgroundOptionsPreview();
    private void ApplyBackgroundBlurPreview()
    {
        if (!IsInitialized || closing || applyingAppearance) return;
        double blur = BackgroundBlurBox.Value;
        model.PreviewBackgroundBlur(blur);
        BackgroundPreview.Effect = blur >= .5 ? new BlurEffect { Radius = blur } : null;
    }

    private void SyncBackgroundPreviewVisuals()
    {
        string fit = SelectedBackgroundFit;
        string anchor = SelectedBackgroundAnchor;
        BackgroundAnchorLabel.Text = BackgroundAnchor.DisplayName(anchor);
        BackgroundPreviewViewport.Fit = fit;
        BackgroundPreviewViewport.Anchor = anchor;
        BackgroundPreviewViewport.ImageScale = BackgroundScaleBox.Value;
        BackgroundPreviewDimOverlay.Opacity = BackgroundDimBox.Value;
    }

    private void ApplyBackgroundOptionsPreview()
    {
        if (!IsInitialized || closing || applyingAppearance) return;
        model.PreviewBackgroundOptions(BackgroundDimBox.Value, BackgroundScaleBox.Value, SelectedBackgroundFit, IsGrayscaleChecked, SelectedBackgroundAnchor);
        SyncBackgroundPreviewVisuals();
        ShowBackground();
    }

    private void ApplyBackgroundTransitionPreview()
    {
        if (!IsInitialized || closing || applyingAppearance) return;
        string transition = BackgroundSideTransition.Normalize(BackgroundTransitionBox.SelectedValue as string);
        double strength = ClampUnit(BackgroundTransitionStrengthBox.Value, .55);
        model.PreviewBackgroundTransition(transition, strength);
        var kind = BackgroundSideTransition.KindFor(transition);
        BackgroundPreviewHorizontalMask.OpacityMask = BackgroundSideTransition.CreateHorizontalMask(kind, strength);
        BackgroundPreviewVerticalMask.OpacityMask = BackgroundSideTransition.CreateVerticalMask(kind, strength);
    }

    private BitmapSource? CurrentColorBackgroundSource() =>
        chosenBackground ?? (backgroundPath.Length > 0 ? (model.RawSavedChatBackground ?? Images.LoadChatBackground(backgroundPath)) : null);

    private AnimatedGifSequence? CurrentColorBackgroundGif() =>
        chosenBackground is not null ? chosenBackgroundGif : (backgroundPath.Length > 0 ? model.RawSavedChatBackgroundGif : null);

    private void ApplyChatBackgroundPreview()
    {
        if (!IsInitialized || closing || applyingAppearance) return;
        model.PreviewChatBackground(true, CurrentColorBackgroundSource(), BackgroundOpacityBox.Value, CurrentColorBackgroundGif());
    }

    private void RefreshPresets(string? selectId = null)
    {
        PresetBox.ItemsSource = null;
        PresetBox.ItemsSource = savedPresets.ToList();
        if (selectId is not null) PresetBox.SelectedValue = selectId;
        else if (savedPresets.Count > 0) PresetBox.SelectedIndex = 0;
        bool hasAny = savedPresets.Count > 0;
        LoadPresetButton.IsEnabled = hasAny;
        DeletePresetButton.IsEnabled = hasAny;
    }

    private string EncodeCurrentLook()
    {
        var temp = new AppSettings();
        WriteAppearanceTo(temp);
        temp.ChatBackgroundPath = backgroundPath;
        return AppearanceCodec.Encode(temp, CurrentColorBackgroundSource(), chosenBackgroundBytes);
    }

    private void ApplyLookData(AppearanceCodec.LookData look)
    {
        var temp = new AppSettings();
        AppearanceCodec.Apply(look, temp);
        var (decodedImage, decodedRawBytes, decodedExt, isAnimatedGif) = AppearanceCodec.TryDecodeBackgroundRaw(look);
        chosenBackground = decodedImage;
        chosenBackgroundBytes = decodedRawBytes;
        chosenBackgroundExtension = decodedExt;
        chosenBackgroundGif = isAnimatedGif && decodedRawBytes is not null ? AnimatedGifSequence.TryLoadFromBytes(decodedRawBytes) : null;
        backgroundPath = "";
        applyingAppearance = true;
        BackgroundOpacityBox.Value = temp.ChatBackgroundOpacity;
        LoadAppearanceControls(temp);
        applyingAppearance = false;
        ApplyAllLivePreviews();
    }

    private void SavePreset_Click(object sender, RoutedEventArgs e)
    {
        string name = PresetNameBox.Text.Trim();
        if (name.Length == 0) name = $"Look {savedPresets.Count + 1}";
        string code = EncodeCurrentLook();
        var existing = savedPresets.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
        string selectedId;
        if (existing is not null)
        {
            existing.Name = name;
            existing.Code = code;
            selectedId = existing.Id;
        }
        else
        {
            var preset = new AppearancePreset { Name = name, Code = code };
            savedPresets.Add(preset);
            selectedId = preset.Id;
        }
        PresetNameBox.Text = "";
        RefreshPresets(selectedId);
        PresetStatusText.Text = "Saved";
    }

    private void LoadPreset_Click(object sender, RoutedEventArgs e)
    {
        if (PresetBox.SelectedItem is not AppearancePreset preset) return;
        if (!AppearanceCodec.TryDecode(preset.Code, out var look)) { PresetStatusText.Text = "Invalid code"; return; }
        ApplyLookData(look);
        PresetStatusText.Text = "Loaded";
    }

    private void DeletePreset_Click(object sender, RoutedEventArgs e)
    {
        if (PresetBox.SelectedItem is not AppearancePreset preset) return;
        savedPresets.RemoveAll(p => p.Id == preset.Id);
        RefreshPresets();
        PresetStatusText.Text = "Deleted";
    }

    private void CopyLookCode_Click(object sender, RoutedEventArgs e)
    {
        string code = EncodeCurrentLook();
        LookCodeBox.Text = code;
        try { System.Windows.Clipboard.SetText(code); } catch (System.Runtime.InteropServices.ExternalException) { }
        PresetStatusText.Text = "Copied";
    }

    private void ImportLookCode_Click(object sender, RoutedEventArgs e)
    {
        if (!AppearanceCodec.TryDecode(LookCodeBox.Text, out var look)) { PresetStatusText.Text = "Invalid code"; return; }
        ApplyLookData(look);
        PresetStatusText.Text = "Imported";
    }

    private void RandomizeLook_Click(object sender, RoutedEventArgs e)
    {
        ApplyLookData(AppearanceCodec.Randomize());
        PresetStatusText.Text = "Randomized";
    }

    private void ResetAppearance_Click(object sender, RoutedEventArgs e)
    {
        var temp = new AppSettings { Theme = SelectedTheme };
        AppearanceCodec.ResetDefaults(temp);
        chosenBackground = null;
        chosenBackgroundBytes = null;
        chosenBackgroundGif = null;
        backgroundPath = "";
        applyingAppearance = true;
        BackgroundOpacityBox.Value = temp.ChatBackgroundOpacity;
        LoadAppearanceControls(temp);
        applyingAppearance = false;
        ApplyAllLivePreviews();
        PresetStatusText.Text = "Reset";
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
    private BitmapSource? chosenBackground;
    private byte[]? chosenBackgroundBytes;
    private string chosenBackgroundExtension = ".png";
    private AnimatedGifSequence? chosenBackgroundGif;
    private void ShowBackground()
    {
        BitmapSource? image = model.ChatBackground;
        AnimatedGifSequence? gif = model.ChatBackgroundGif;
        AnimatedGifBehavior.SetFallbackSource(BackgroundPreview, image);
        AnimatedGifBehavior.SetSequence(BackgroundPreview, gif);
        BackgroundAdjustments.Visibility = image is null ? Visibility.Collapsed : Visibility.Visible;
        RemoveBackgroundButton.IsEnabled = image is not null || backgroundPath.Length > 0;
    }
    private static byte[]? TryReadImageBytes(string path)
    {
        try
        {
            var info = new System.IO.FileInfo(path);
            if (!info.Exists || info.Length is < 6 or > 20 * 1024 * 1024) return null;
            return System.IO.File.ReadAllBytes(path);
        }
        catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return null;
        }
    }
    private void ChooseBackground_Click(object sender, RoutedEventArgs e)
    {
        var picker = new Microsoft.Win32.OpenFileDialog { Title = "Chat background", Filter = "Images|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.webp" };
        if (picker.ShowDialog(this) != true) return;
        byte[]? rawBytes = TryReadImageBytes(picker.FileName);
        AnimatedGifSequence? gif = rawBytes is not null && Images.HasGifSignature(rawBytes)
            ? AnimatedGifSequence.TryLoadFromBytes(rawBytes)
            : null;
        BitmapSource? image = (rawBytes is not null ? Images.LoadChatBackgroundFromBytes(rawBytes) : null)
            ?? Images.LoadChatBackground(picker.FileName)
            ?? (gif is { Frames.Count: > 0 } ? gif.Frames[0].Bitmap : null);
        if (image is null)
        {
            BackgroundError.Text = "The image couldn't be opened.";
            return;
        }
        chosenBackground = image;
        chosenBackgroundBytes = rawBytes;
        chosenBackgroundExtension = rawBytes is not null ? Images.DetectImageExtension(rawBytes) : ".png";
        chosenBackgroundGif = gif;
        backgroundPath = "";
        BackgroundError.Text = "";
        ApplyChatBackgroundPreview();
        ApplyBackgroundOptionsPreview();
        ShowBackground();
    }
    private void RemoveBackground_Click(object sender, RoutedEventArgs e)
    {
        chosenBackground = null;
        chosenBackgroundBytes = null;
        chosenBackgroundExtension = ".png";
        chosenBackgroundGif = null;
        backgroundPath = "";
        BackgroundError.Text = "";
        ApplyChatBackgroundPreview();
        ApplyBackgroundOptionsPreview();
        ShowBackground();
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
        if (!IsInitialized || updatingThemeFilter || applyingAppearance || closing) return;
        if (ThemeBox.SelectedValue is string theme)
        {
            selectedTheme = theme;
            ApplyLiveThemeOverrides();
        }
    }
    private void ThemeFilter_Checked(object sender, RoutedEventArgs e)
    {
        if (!IsInitialized || sender is not FrameworkElement { Tag: string filter }) return;
        FilterThemes(filter);
    }
    private void FilterThemes(string filter)
    {
        currentThemeFilter = filter;
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
    /// <summary>Switches the active sub-tab inside Appearance.</summary>
    public void ShowAppearanceTab(string name)
    {
        string normalized = NormalizeAppearanceTab(name);
        SelectAppearanceTab(normalized);
        foreach (var tab in AppearanceTabsPanel.Children.OfType<System.Windows.Controls.RadioButton>())
            if ((string)tab.Tag == normalized) tab.IsChecked = true;
    }
    private void SelectAppearanceTab(string name)
    {
        string normalized = NormalizeAppearanceTab(name);
        AppearanceThemesSection.Visibility = normalized == "Themes" ? Visibility.Visible : Visibility.Collapsed;
        AppearanceColorsSection.Visibility = normalized == "Colors" ? Visibility.Visible : Visibility.Collapsed;
        AppearanceBubblesSection.Visibility = normalized == "Bubbles" ? Visibility.Visible : Visibility.Collapsed;
        AppearanceTexturesSection.Visibility = normalized == "Textures" ? Visibility.Visible : Visibility.Collapsed;
        AppearanceAmbientSection.Visibility = normalized == "Ambient" ? Visibility.Visible : Visibility.Collapsed;
        AppearanceHeadersSection.Visibility = normalized == "Headers" ? Visibility.Visible : Visibility.Collapsed;
        AppearancePanelsSection.Visibility = normalized == "Panels" ? Visibility.Visible : Visibility.Collapsed;
        AppearanceAuraSection.Visibility = normalized == "Aura" ? Visibility.Visible : Visibility.Collapsed;
        AppearanceCompanionSection.Visibility = normalized == "Companion" ? Visibility.Visible : Visibility.Collapsed;
        AppearanceCustomizeSection.Visibility = normalized == "Customize" ? Visibility.Visible : Visibility.Collapsed;
        if (!restoringNavigation && !closing) model.LastAppearanceTab = normalized;
        UpdateCompanionTimerState();
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
        string normalized = NormalizePage(name);
        SelectPage(normalized);
        foreach (var item in NavPanel.Children.OfType<System.Windows.Controls.RadioButton>()) if ((string)item.Tag == normalized) item.IsChecked = true;
    }
    private void SelectPage(string name)
    {
        string normalized = NormalizePage(name);
        foreach (var page in new[] { GeneralPage, AccountPage, MessagesPage, HistoryPage, AlertsPage, NetworkPage, FilesPage, AppearancePage, AboutPage })
            page.Visibility = page.Name == normalized ? Visibility.Visible : Visibility.Collapsed;
        if (!restoringNavigation && !closing) model.LastSettingsPage = normalized;
        UpdateCompanionTimerState();
        PreferencesScroller.ScrollToTop();
    }
    private void Font_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!IsInitialized || closing || applyingAppearance) return;
        if (sender == SizeBox)
        {
            customFontPx = null;
            applyingAppearance = true;
            MessageFontPxBox.Value = MainViewModel.FontSizeFor(SelectedFontSizeTag);
            applyingAppearance = false;
        }
        UpdateFontPreview();
        ApplyMessageTypographyPreview();
    }
    private void MessageFontPx_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!IsInitialized || closing || applyingAppearance) return;
        double px = Math.Round(MessageFontPxBox.Value);
        double presetPx = MainViewModel.FontSizeFor(SelectedFontSizeTag);
        customFontPx = Math.Abs(px - presetPx) < .01 ? null : px;
        UpdateFontPreview();
        ApplyMessageTypographyPreview();
    }
    private void ApplyMessageTypographyPreview()
    {
        if (!IsInitialized || closing || applyingAppearance) return;
        model.PreviewMessageTypography(FontBox.SelectedItem as string, SelectedFontSizeTag, customFontPx);
    }
    private void UpdateFontPreview()
    {
        if (FontPreview is null || SizeBox is null) return;
        try { FontPreview.FontFamily = new FontFamily((FontBox.SelectedItem as string ?? "Segoe UI") + ", Segoe UI"); } catch (ArgumentException) { }
        FontPreview.FontSize = customFontPx ?? MainViewModel.FontSizeFor(SelectedFontSizeTag);
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
        if (!IsInitialized || closing || applyingAppearance) return;
        model.PreviewChromeTexture(ChromeTextureBox.SelectedValue as string, ChromeTextureIntensityBox.Value);
    }
    private void TargetAura_Changed(object sender, SelectionChangedEventArgs e) => ApplyTargetAuraPreview();
    private void TargetAuraTarget_Changed(object sender, RoutedEventArgs e) => ApplyTargetAuraPreview();
    private void TargetAura_SliderChanged(object sender, RoutedPropertyChangedEventArgs<double> e) => ApplyTargetAuraPreview();
    private void ApplyTargetAuraPreview()
    {
        if (!IsInitialized || closing || applyingAppearance) return;
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
        if (!IsInitialized || closing || applyingAppearance) return;
        ButtonEffects.Configure(
            ButtonGlowBox.IsChecked == true, ButtonGlowIntensityBox.Value,
            ButtonSheenBox.IsChecked == true, ButtonSheenIntensityBox.Value,
            ButtonBounceBox.IsChecked == true, ButtonBounceIntensityBox.Value,
            ButtonRingBox.IsChecked == true, ButtonRingIntensityBox.Value,
            ButtonPulseBox.IsChecked == true, ButtonPulseIntensityBox.Value,
            ButtonLiftBox.IsChecked == true, ButtonLiftIntensityBox.Value,
            ButtonSparkBox.IsChecked == true, ButtonSparkIntensityBox.Value);
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        var name = DisplayNameBox.Text.Trim();
        if (name.Length == 0) { ShowPage("AccountPage"); DisplayNameBox.Focus(); return; }
        if (!int.TryParse(PortBox.Text.Trim(), out int port) || port is < 1 or > 65535) { ShowPage("NetworkPage"); PortBox.Focus(); return; }
        var multicast = MulticastBox.Text.Trim();
        if (!IPAddress.TryParse(multicast, out var group) || group.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork || group.GetAddressBytes()[0] is < 224 or > 239)
        { ShowPage("NetworkPage"); MulticastBox.Focus(); return; }
        if (chosenBackgroundBytes is { Length: > 0 })
        {
            try { backgroundPath = model.SaveChatBackgroundBytes(chosenBackgroundBytes, chosenBackgroundExtension); }
            catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException or NotSupportedException)
            {
                ShowPage("AppearancePage");
                ShowAppearanceTab("Customize");
                BackgroundError.Text = "The background couldn't be saved.";
                BackgroundError.BringIntoView();
                return;
            }
        }
        else if (chosenBackground is not null)
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
        CommitHexInput(AccentHexBox);
        CommitHexInput(EffectHexBox);
        CommitHexInput(OutgoingBubbleHexBox);
        CommitHexInput(IncomingBubbleHexBox);
        CommitHexInput(ChatBackgroundHexBox);
        CommitCompanionHexInputs();
        bool networkChanged = port != model.Settings.Port || multicast != model.Settings.Multicast;
        string defaultFolder = AppSettings.DefaultReceivedFilesFolder;
        await model.SavePreferencesAsync(s =>
        {
            s.StartWithWindows = StartWithWindowsBox.IsChecked == true; s.StartMinimized = MinimizedBox.IsChecked == true;
            s.Status = (string)((ComboBoxItem)StatusBox.SelectedItem).Tag;
            s.DisplayName = name; s.FirstName = FirstNameBox.Text.Trim(); s.LastName = LastNameBox.Text.Trim(); s.About = AboutBox.Text.Trim();
            s.AvatarPath = avatarPath;
            s.SendWithCtrlEnter = SendCtrlEnter.IsChecked == true; s.TrimMessages = TrimBox.IsChecked == true;
            s.KeepHistory = KeepHistoryBox.IsChecked == true; s.KeepFileHistory = KeepFileHistoryBox.IsChecked == true;
            s.Notifications = NotificationsBox.IsChecked == true; s.AlertsWhenBusy = AlertsBusyBox.IsChecked == true; s.AlertsWhenDnd = AlertsDndBox.IsChecked == true;
            s.NotificationPreviews = NotificationPreviewsBox.IsChecked == true;
            s.Sounds = SoundsBox.IsChecked == true; s.SoundsWhenBusy = SoundsBusyBox.IsChecked == true; s.SoundsWhenDnd = SoundsDndBox.IsChecked == true;
            s.Port = port; s.Multicast = multicast;
            s.ReceivedFilesFolder = string.Equals(FolderBox.Text, defaultFolder, StringComparison.OrdinalIgnoreCase) ? "" : FolderBox.Text;
            s.AutoAcceptFiles = AutoAcceptBox.IsChecked == true;
            s.ChatBackgroundPath = backgroundPath;
            s.ChatBackgroundOpacity = double.IsFinite(BackgroundOpacityBox.Value) ? Math.Clamp(BackgroundOpacityBox.Value, .10, 1.0) : .26;
            WriteAppearanceTo(s);
            s.SavedPresets = savedPresets.Select(p => new AppearancePreset { Id = p.Id, Name = p.Name, Code = p.Code }).ToList();
            s.LastSettingsPage = NormalizePage(model.LastSettingsPage);
            s.LastAppearanceTab = NormalizeAppearanceTab(model.LastAppearanceTab);
        });
        if (networkChanged) model.Toast = "Restart CydLan to use the new network settings.";
        saved = true; Close();
    }
}
