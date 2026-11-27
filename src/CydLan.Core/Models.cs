using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace CydLan.Core;

public abstract class Observable : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    protected bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        Changed(name);
        return true;
    }
    public void Changed([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new(name));
}

public sealed class Peer : Observable
{
    public required string Id { get; init; }
    private string name = "Connecting…", address = "", status = "gone", version = "", preview = "Say hello", typing = "";
    private bool online;
    private int unread;
    public string Name { get => name; set { Set(ref name, value); Changed(nameof(Initials)); } }
    public string Address { get => address; set => Set(ref address, value); }
    public int Port { get; set; } = 50000;
    public string Status { get => status; set { Set(ref status, value); Changed(nameof(Presence)); } }
    public string Version { get => version; set => Set(ref version, value); }
    public bool Online { get => online; set { Set(ref online, value); Changed(nameof(Presence)); } }
    public string Preview { get => preview; set => Set(ref preview, value); }
    public int Unread { get => unread; set => Set(ref unread, value); }
    public string Typing { get => typing; set => Set(ref typing, value); }
    private string firstName = "", lastName = "", about = "", avatarPath = "", petCode = "";
    public string FirstName { get => firstName; set { Set(ref firstName, value); Changed(nameof(FullName)); Changed(nameof(HasFullName)); } }
    public string LastName { get => lastName; set { Set(ref lastName, value); Changed(nameof(FullName)); Changed(nameof(HasFullName)); } }
    public string About { get => about; set { Set(ref about, value); Changed(nameof(HasAbout)); } }
    public string FullName => $"{FirstName} {LastName}".Trim();
    public bool HasFullName => FullName.Length > 0;
    public bool HasAbout => About.Length > 0;
    /// <summary>Compact companion profile shared by CydLan peers; empty for legacy LAN Messenger peers.</summary>
    public string PetCode { get => petCode; set { Set(ref petCode, value); Changed(nameof(HasPetCode)); } }
    [System.Text.Json.Serialization.JsonIgnore]
    public bool HasPetCode => petCode.Length > 0 && LanService.IsCydLanVersion(Version);
    /// <summary>Local copy of this contact's profile picture; empty when they have not sent one.</summary>
    public string AvatarPath { get => avatarPath; set => Set(ref avatarPath, value); }
    public string Presence => !Online ? "Offline" : Status switch { "busy" => "Busy", "dnd" => "Do not disturb", "brb" or "away" => "Away", _ => "Online" };
    public string Initials => string.Concat(Name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2).Select(s => char.ToUpperInvariant(s[0])));
}

public sealed class ChatMessage : Observable
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string PeerId { get; set; } = "";
    public string Text { get; set; } = "";
    public bool Outgoing { get; set; }
    public bool Broadcast { get; set; }
    public DateTimeOffset Time { get; set; } = DateTimeOffset.Now;
    private string delivery = "Received";
    private bool canCancel;
    public string Delivery { get => delivery; set { Set(ref delivery, value); Changed(nameof(CanRetry)); Changed(nameof(DeliveryProblem)); Changed(nameof(AwaitingAnswer)); } }
    /// <summary>An incoming file offer that has not been accepted or declined yet.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool AwaitingAnswer => !Outgoing && HasFile && Delivery == "Waiting";
    /// <summary>An outgoing file offer or active transfer that can currently be cancelled.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool CanCancel { get => canCancel; set { if (Set(ref canCancel, value)) Changed(nameof(CanCancelInline)); } }
    [System.Text.Json.Serialization.JsonIgnore]
    public bool CanCancelInline => !HasFile && CanCancel;
    /// <summary>The message (or its file) did not get through: shown in the warning colour.</summary>
    public bool DeliveryProblem => Delivery is "Declined" or "Cancelled" or "Aborted" or "Interrupted" or "Unconfirmed" || Delivery.StartsWith("Failed", StringComparison.Ordinal) || Delivery.StartsWith("Not sent", StringComparison.Ordinal);
    public bool CanRetry => Outgoing && !Broadcast && ImagePath.Length == 0 && FileName.Length == 0 && Delivery is "Failed" or "Unconfirmed";
    public string TimeLabel => Time.ToLocalTime().ToString("h:mm tt");
    /// <summary>Local copy of an image sent or received in this conversation; empty for text messages.</summary>
    private string imagePath = "";
    public string ImagePath { get => imagePath; set { if (Set(ref imagePath, value)) { Changed(nameof(HasImage)); Changed(nameof(CanRetry)); } } }
    /// <summary>Name of a non-image file offered in this conversation; empty otherwise.</summary>
    private string fileName = "";
    public string FileName { get => fileName; set { if (Set(ref fileName, value)) { Changed(nameof(HasFile)); Changed(nameof(CanCancelInline)); } } }
    public bool FolderAttachment { get; set; }
    /// <summary>File-transfer ID backing an image or file message, so its delivery state can follow the transfer.</summary>
    public string TransferId { get; set; } = "";
    /// <summary>Legacy group-chat thread this message belongs to; empty for one-to-one chat.</summary>
    public string GroupId { get; set; } = "";
    /// <summary>Who wrote an incoming group message (group threads mix several senders).</summary>
    public string SenderName { get; set; } = "";
    public bool ShowSender => GroupId.Length > 0 && !Outgoing && SenderName.Length > 0;
    public bool HasImage => ImagePath.Length > 0;
    public bool HasFile => FileName.Length > 0;
    public bool HasText => Text.Length > 0;
}

public sealed class FileTransfer : Observable
{
    public required string Id { get; init; }
    public required string PeerId { get; init; }
    public required string Name { get; init; }
    public long Size { get; init; }
    public bool Outgoing { get; init; }
    /// <summary>A CydLan-to-CydLan image shown in the conversation instead of the transfers list.</summary>
    public bool Inline { get; init; }
    public bool Folder { get; init; }
    /// <summary>A profile picture (LAN Messenger "avatar" transfer); never listed with ordinary files.</summary>
    public bool Avatar { get; init; }
    public DateTimeOffset Time { get; init; } = DateTimeOffset.Now;
    public string TimeLabel => Time.ToLocalTime().ToString("MMM d · h:mm tt");
    public string PeerName { get; set; } = "LAN peer";
    public string DirectionLabel => $"{(Outgoing ? "To" : "From")} {PeerName}";
    public string Path { get; set; } = "";
    internal FolderArchive? Archive { get; init; }
    private string state = "Waiting for approval";
    private double progress;
    private long transferredBytes;
    public string State
    {
        get => state;
        set
        {
            if (!Set(ref state, value)) return;
            Changed(nameof(IsActive));
            Changed(nameof(IsConnecting));
            Changed(nameof(CanCancel));
            Changed(nameof(ActiveLabel));
            Changed(nameof(StatusLabel));
        }
    }
    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsActive => IsConnecting || State == "Transferring";
    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsConnecting => State is "Accepted" or "Connecting";
    [System.Text.Json.Serialization.JsonIgnore]
    public bool CanCancel => IsActive || (Outgoing && State == "Waiting for approval");
    [System.Text.Json.Serialization.JsonIgnore]
    public int DisplayPercent => State is "Received" or "Sent · receipt unavailable" ? 100 : (int)Math.Floor(Math.Clamp(Progress, 0, 99));
    [System.Text.Json.Serialization.JsonIgnore]
    public string ActiveLabel => IsConnecting
        ? "Connecting…"
        : $"{(Outgoing ? "Sending" : "Receiving")} {DisplayPercent}% · {FormatBytes(TransferredBytes)} / {SizeLabel}";
    [System.Text.Json.Serialization.JsonIgnore]
    public string StatusLabel => State == "Transferring"
        ? $"{(Outgoing ? "Sending" : "Receiving")} {DisplayPercent}% · {FormatBytes(TransferredBytes)}"
        : State;
    public double Progress
    {
        get => progress;
        set
        {
            if (!Set(ref progress, value)) return;
            Changed(nameof(DisplayPercent));
            Changed(nameof(ActiveLabel));
            Changed(nameof(StatusLabel));
        }
    }
    [System.Text.Json.Serialization.JsonIgnore]
    public long TransferredBytes
    {
        get => transferredBytes;
        set
        {
            if (!Set(ref transferredBytes, value)) return;
            Changed(nameof(ActiveLabel));
            Changed(nameof(StatusLabel));
        }
    }
    public string SizeLabel => FormatBytes(Size);
    public static string FormatBytes(long bytes) => bytes switch
    {
        >= 1024L * 1024 * 1024 => $"{bytes / (1024d * 1024 * 1024):0.##} GB",
        >= 1024L * 1024 => $"{bytes / (1024d * 1024):0.#} MB",
        _ => $"{bytes / 1024d:0.#} KB"
    };
}

public sealed class AppSettings
{
    /// <summary>
    /// LAN Messenger reads a file stream's greeting with a single 64-byte read: "FILE" (4), the file ID (32),
    /// then the sender ID. A longer sender ID is truncated, the transfer is not found, and the file never starts.
    /// </summary>
    public const int MaxUserIdLength = 28;
    public static string NewUserId() => Guid.NewGuid().ToString("N")[..24];
    public static bool IsValidUserId(string id) => id.Length is >= 1 and <= MaxUserIdLength && id.All(c => c is > ' ' and <= '~');
    public string UserId { get; set; } = NewUserId();
    public string DisplayName { get; set; } = Environment.UserName;
    public string Status { get; set; } = "chat";
    public bool Notifications { get; set; } = true;
    public bool StartMinimized { get; set; }
    public bool FloatingLanEnabled { get; set; }
    public bool FloatingLanRoam { get; set; }
    /// <summary>The companion wanders anywhere on screen instead of along the bottom edge.</summary>
    public bool PetFreeRoam { get; set; }
    public PetProgress PetGame { get; set; } = new();
    public double? FloatingLanLeft { get; set; }
    public double? FloatingLanTop { get; set; }
    public string PetBody { get; set; } = "squircle";
    public string PetEars { get; set; } = "auto";
    public string PetEyes { get; set; } = "dot";
    public string PetMouth { get; set; } = "smile";
    public string PetCheeks { get; set; } = "blush";
    public string PetGlasses { get; set; } = "none";
    public string PetHat { get; set; } = "auto";
    public string PetOutfit { get; set; } = "auto";
    public string PetBack { get; set; } = "none";
    public string PetProp { get; set; } = "none";
    public string PetFeet { get; set; } = "paws";
    public string PetPattern { get; set; } = "solid";
    public string PetVibe { get; set; } = "bouncy";
    public string PetBrows { get; set; } = "none";
    public string PetAura { get; set; } = "none";
    public List<string> PetTraits { get; set; } = [];
    public string PetBodyColor { get; set; } = "";
    public string PetAccentColor { get; set; } = "";
    public string PetDetailColor { get; set; } = "";
    public double PetSize { get; set; } = 1.0;
    /// <summary>Desktop colour theme: "light", "dark" or "system".</summary>
    public string Theme { get; set; } = "light";
    public string ChatBackgroundPath { get; set; } = "";
    public double ChatBackgroundOpacity { get; set; } = .26;
    public double ChatBackgroundDim { get; set; }
    public double ChatBackgroundScale { get; set; } = 1.0;
    public string ChatBackgroundFit { get; set; } = "fill";
    public string ChatBackgroundAnchor { get; set; } = "center";
    public bool ChatBackgroundGrayscale { get; set; }
    public string ChatBackgroundTransition { get; set; } = "off";
    public double ChatBackgroundTransitionStrength { get; set; } = .55;
    public bool AnimateWhenUnfocused { get; set; }
    public string TypingEffect { get; set; } = "off";
    public string ClickEffect { get; set; } = "off";
    public bool SteampunkEngineEnabled { get; set; }
    public bool SakuraPetalsEnabled { get; set; }
    public double EffectIntensity { get; set; } = .75;
    public double? TypingIntensity { get; set; }
    public double? ClickIntensity { get; set; }
    public bool ThemeDetailsEnabled { get; set; }
    public double EngineIntensity { get; set; } = .85;
    public double PetalIntensity { get; set; } = .75;
    public bool FirefliesEnabled { get; set; }
    public double FireflyIntensity { get; set; } = .65;
    public bool RainEnabled { get; set; }
    public double RainIntensity { get; set; } = .65;
    public bool SnowEnabled { get; set; }
    public double SnowIntensity { get; set; } = .65;
    public double InkBloomIntensity { get; set; } = .65;
    public bool SendPulseEnabled { get; set; }
    public double SendPulseIntensity { get; set; } = .65;
    public bool HeaderAuroraEnabled { get; set; }
    public double HeaderAuroraIntensity { get; set; } = .65;
    public bool TitleSparklesEnabled { get; set; }
    public double TitleSparklesIntensity { get; set; } = .65;
    public bool HeaderPulseEnabled { get; set; }
    public double HeaderPulseIntensity { get; set; } = .65;
    public bool RailBeaconEnabled { get; set; }
    public double RailBeaconIntensity { get; set; } = .65;
    public bool RailStreamEnabled { get; set; }
    public double RailStreamIntensity { get; set; } = .65;
    public bool RailStardustEnabled { get; set; }
    public double RailStardustIntensity { get; set; } = .65;
    public bool RailScanEnabled { get; set; }
    public double RailScanIntensity { get; set; } = .65;
    public bool RailAuroraEnabled { get; set; }
    public double RailAuroraIntensity { get; set; } = .65;
    public bool RailPulseEnabled { get; set; }
    public double RailPulseIntensity { get; set; } = .65;
    public bool RailPlasmaEnabled { get; set; }
    public double RailPlasmaIntensity { get; set; } = .65;
    public bool RailCascadeEnabled { get; set; }
    public double RailCascadeIntensity { get; set; } = .65;
    public bool SidebarMotesEnabled { get; set; }
    public double SidebarMotesIntensity { get; set; } = .65;
    public bool SidebarGlowEnabled { get; set; }
    public double SidebarGlowIntensity { get; set; } = .65;
    public bool SidebarPrismEnabled { get; set; }
    public double SidebarPrismIntensity { get; set; } = .65;
    public bool SidebarSweepEnabled { get; set; }
    public double SidebarSweepIntensity { get; set; } = .65;
    public bool SidebarAuroraEnabled { get; set; }
    public double SidebarAuroraIntensity { get; set; } = .65;
    public bool SidebarNebulaEnabled { get; set; }
    public double SidebarNebulaIntensity { get; set; } = .65;
    public bool SidebarWavesEnabled { get; set; }
    public double SidebarWavesIntensity { get; set; } = .65;
    public bool SidebarRipplesEnabled { get; set; }
    public double SidebarRipplesIntensity { get; set; } = .65;
    public bool SidebarCircuitEnabled { get; set; }
    public double SidebarCircuitIntensity { get; set; } = .65;
    public bool AmbientOnSidebarEnabled { get; set; }
    public bool StarfieldEnabled { get; set; }
    public double StarfieldIntensity { get; set; } = .65;
    public bool BubblesEnabled { get; set; }
    public double BubblesIntensity { get; set; } = .65;
    public bool EmbersEnabled { get; set; }
    public double EmbersIntensity { get; set; } = .65;
    public bool ButtonGlowEnabled { get; set; }
    public double ButtonGlowIntensity { get; set; } = .65;
    public bool ButtonSheenEnabled { get; set; }
    public double ButtonSheenIntensity { get; set; } = .65;
    public bool ButtonBounceEnabled { get; set; }
    public double ButtonBounceIntensity { get; set; } = .65;
    public bool ButtonRingEnabled { get; set; }
    public double ButtonRingIntensity { get; set; } = .65;
    public bool ButtonPulseEnabled { get; set; }
    public double ButtonPulseIntensity { get; set; } = .65;
    public bool ButtonLiftEnabled { get; set; }
    public double ButtonLiftIntensity { get; set; } = .65;
    public bool ButtonSparkEnabled { get; set; }
    public double ButtonSparkIntensity { get; set; } = .65;
    // Additional separated Title bar & Chat header effects
    public bool TitleShimmerEnabled { get; set; }
    public double TitleShimmerIntensity { get; set; } = .65;
    public bool TitleCometEnabled { get; set; }
    public List<string> TitleFx { get; set; } = [];
    public double TitleFxIntensity { get; set; } = .65;
    public List<string> HeaderFx { get; set; } = [];
    public double HeaderFxIntensity { get; set; } = .65;
    public double TitleCometIntensity { get; set; } = .65;
    public bool TitleEqualizerEnabled { get; set; }
    public double TitleEqualizerIntensity { get; set; } = .65;
    public bool TitleLaserEnabled { get; set; }
    public double TitleLaserIntensity { get; set; } = .65;
    public bool TitleAuroraEnabled { get; set; }
    public double TitleAuroraIntensity { get; set; } = .65;
    public bool TitleWaveEnabled { get; set; }
    public double TitleWaveIntensity { get; set; } = .65;
    public bool HeaderPrismEnabled { get; set; }
    public double HeaderPrismIntensity { get; set; } = .65;
    public bool HeaderRipplesEnabled { get; set; }
    public double HeaderRipplesIntensity { get; set; } = .65;
    public bool HeaderCircuitEnabled { get; set; }
    public double HeaderCircuitIntensity { get; set; } = .65;
    public bool HeaderStarlightEnabled { get; set; }
    public double HeaderStarlightIntensity { get; set; } = .65;
    public bool HeaderNebulaEnabled { get; set; }
    public double HeaderNebulaIntensity { get; set; } = .65;
    public bool HeaderSilkEnabled { get; set; }
    public double HeaderSilkIntensity { get; set; } = .65;
    public bool HeaderPlasmaEnabled { get; set; }
    public double HeaderPlasmaIntensity { get; set; } = .65;
    public string ChromeTexture { get; set; } = "off";
    public double ChromeTextureIntensity { get; set; } = .65;
    public bool TextureMotionEnabled { get; set; }
    public List<string> AmbientFx { get; set; } = [];
    public double AmbientFxIntensity { get; set; } = .65;
    public List<string> RailFx { get; set; } = [];
    public double RailFxIntensity { get; set; } = .65;
    public List<string> SidebarFx { get; set; } = [];
    public double SidebarFxIntensity { get; set; } = .65;
    public bool AuraOverflowEnabled { get; set; }
    public string WordPopColor { get; set; } = "";
    public string HoverEffect { get; set; } = "off";
    public double HoverIntensity { get; set; } = .65;
    public string CursorEffect { get; set; } = "off";
    public double CursorIntensity { get; set; } = .65;
    public bool SolidComposerEnabled { get; set; }
    public string TargetAura { get; set; } = "off";
    public double TargetAuraIntensity { get; set; } = .70;
    public bool AuraSelectedModuleEnabled { get; set; } = true;
    public bool AuraSelectedPersonEnabled { get; set; } = true;
    public bool AuraHighlightButtonsEnabled { get; set; } = true;
    // Opt-in appearance remixing (empty / "default" / "off" = use bundled theme)
    public string ColorFinish { get; set; } = "off";
    public double ColorFinishIntensity { get; set; } = .65;
    public string CustomAccentColor { get; set; } = "";
    public string CustomEffectColor { get; set; } = "";
    public string CustomOutgoingBubbleColor { get; set; } = "";
    public string CustomIncomingBubbleColor { get; set; } = "";
    public string CustomTitleColor { get; set; } = "";
    public string CustomHeaderColor { get; set; } = "";
    public string CustomRailColor { get; set; } = "";
    public string CustomSidebarColor { get; set; } = "";
    public string LogoColor { get; set; } = "";
    public string LogoStyle { get; set; } = "classic";
    public string LogoEffect { get; set; } = "none";
    public string LogoDotColor { get; set; } = "";
    public string LogoDotEffect { get; set; } = "none";
    public string IconColor { get; set; } = "";
    public string ChatSurfaceMode { get; set; } = "separate";
    public string CustomChatBackgroundColor { get; set; } = "";
    public string BubbleStyle { get; set; } = "soft";
    public string BubbleShape { get; set; } = "default";
    public string ChatDensity { get; set; } = "cozy";
    public double? MessageFontSizePx { get; set; }
    public string ChatCanvasGradient { get; set; } = "off";
    public double ChatCanvasGradientIntensity { get; set; } = .55;
    public bool SeamlessComposerEnabled { get; set; }
    public double ChatBackgroundBlur { get; set; }
    public string CornerStyle { get; set; } = "default";
    public string AvatarShape { get; set; } = "default";
    public bool OnlineRingEnabled { get; set; }
    public string UiFont { get; set; } = "";
    public List<string> ImportedFonts { get; set; } = [];
    public double WindowOpacity { get; set; } = 1.0;
    public double EffectSpeed { get; set; } = 1.0;
    public string MessageAnimation { get; set; } = "off";
    public bool MatrixEnabled { get; set; }
    public double MatrixIntensity { get; set; } = .65;
    public bool ConstellationEnabled { get; set; }
    public double ConstellationIntensity { get; set; } = .65;
    public bool BokehEnabled { get; set; }
    public double BokehIntensity { get; set; } = .65;
    public bool WingPlumesEnabled { get; set; }
    public double WingPlumesIntensity { get; set; } = .65;
    public bool RepulsorEnabled { get; set; }
    public double RepulsorIntensity { get; set; } = .65;
    public bool TitleWingsEnabled { get; set; }
    public double TitleWingsIntensity { get; set; } = .65;
    public bool TitleReactorEnabled { get; set; }
    public double TitleReactorIntensity { get; set; } = .65;
    public bool HeaderBurstEnabled { get; set; }
    public double HeaderBurstIntensity { get; set; } = .65;
    public bool HeaderArcEnabled { get; set; }
    public double HeaderArcIntensity { get; set; } = .65;
    public bool RailWingsEnabled { get; set; }
    public double RailWingsIntensity { get; set; } = .65;
    public bool RailArcEnabled { get; set; }
    public double RailArcIntensity { get; set; } = .65;
    public bool SidebarWingsEnabled { get; set; }
    public double SidebarWingsIntensity { get; set; } = .65;
    public bool SidebarHudEnabled { get; set; }
    public double SidebarHudIntensity { get; set; } = .65;
    public List<AppearancePreset> SavedPresets { get; set; } = [];
    public List<SavedCompanion> SavedCompanions { get; set; } = [];
    public List<SceneEffectItem> SceneEffects { get; set; } = [];
    public int Port { get; set; } = 50000;
    public string Multicast { get; set; } = "239.255.100.100";

    // Account (sent to contacts the way LAN Messenger does)
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string About { get; set; } = "";
    /// <summary>Your profile picture: a small PNG in the CydLan data folder, sent to contacts as a LAN Messenger avatar.</summary>
    public string AvatarPath { get; set; } = "";
    // General
    public bool StartWithWindows { get; set; }
    // Messages
    public string MessageFont { get; set; } = "Segoe UI Variable Text";
    /// <summary>"small", "medium" or "large".</summary>
    public string MessageFontSize { get; set; } = "medium";
    public bool SendWithCtrlEnter { get; set; }
    public bool TrimMessages { get; set; } = true;
    // History
    public bool KeepHistory { get; set; } = true;
    public bool KeepFileHistory { get; set; } = true;
    // Alerts
    public bool NotificationPreviews { get; set; }
    public bool AlertsWhenBusy { get; set; } = true;
    public bool AlertsWhenDnd { get; set; }
    public bool Sounds { get; set; } = true;
    public bool SoundsWhenBusy { get; set; } = true;
    public bool SoundsWhenDnd { get; set; }
    // File transfer
    /// <summary>Folder for received files; empty means <see cref="DefaultReceivedFilesFolder"/>.</summary>
    public string ReceivedFilesFolder { get; set; } = "";
    /// <summary>LAN Messenger's default storage folder (Documents\Received Files), so both apps save to the same place.</summary>
    public static string DefaultReceivedFilesFolder => System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Received Files");
    public bool AutoAcceptFiles { get; set; }
    // Preferences dialog navigation memory
    public string LastSettingsPage { get; set; } = "GeneralPage";
    public string LastAppearanceTab { get; set; } = "Themes";
    public List<string> MediaFolders { get; set; } = [];
    public List<string> MediaFiles { get; set; } = [];
    public double MediaVolume { get; set; } = .8;
}

/// <summary>A user-saved appearance configuration encoded as a compact look code.</summary>
public sealed class AppearancePreset
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..12];
    public string Name { get; set; } = "";
    public string Code { get; set; } = "";
    public override string ToString() => Name;
}

/// <summary>A user-saved floating companion encoded as a CYDPET1: code.</summary>
public sealed class SavedCompanion
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..12];
    public string Name { get; set; } = "";
    public string Code { get; set; } = "";
    public override string ToString() => Name;
}

/// <summary>A placed, customizable animated effect on a CydLan window zone.</summary>
public sealed class SceneEffectItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string Kind { get; set; } = "grass";
    public string Zone { get; set; } = "content";
    public double X { get; set; } = .5;
    public double Y { get; set; } = .88;
    public double Scale { get; set; } = 1.0;
    public double Opacity { get; set; } = .90;
    public double Speed { get; set; } = 1.0;
    public double Rotation { get; set; }
    public bool FlipX { get; set; }
    public string ColorHex { get; set; } = "";
    public double Seed { get; set; }

    public SceneEffectItem Clone(bool newId = false) => new()
    {
        Id = newId ? Guid.NewGuid().ToString("N")[..8] : Id,
        Kind = Kind,
        Zone = Zone,
        X = X,
        Y = Y,
        Scale = Scale,
        Opacity = Opacity,
        Speed = Speed,
        Rotation = Rotation,
        FlipX = FlipX,
        ColorHex = ColorHex,
        Seed = Seed
    };
}

/// <summary>A LAN Messenger group conversation ("chat room"), identified by its thread ID.</summary>
public sealed class GroupChat : Observable
{
    public required string Id { get; init; }
    private string name = "Group", preview = "";
    private int unread;
    private bool left;
    public string Name { get => name; set => Set(ref name, value); }
    public List<string> Members { get; set; } = [];
    public string Preview { get => preview; set => Set(ref preview, value); }
    public int Unread { get => unread; set => Set(ref unread, value); }
    public bool Left { get => left; set => Set(ref left, value); }
    public void MembersChanged() { Changed(nameof(Members)); Changed(nameof(MemberLabel)); }
    public string MemberLabel => Left ? "You left this group" : Members.Count == 1 ? "1 other person" : $"{Members.Count} other people";
}

public sealed class LocalStore
{
    public string DirectoryPath { get; }
    private readonly JsonSerializerOptions options = new() { WriteIndented = true };
    public LocalStore(string directoryPath) { DirectoryPath = directoryPath; Directory.CreateDirectory(directoryPath); }
    public AppSettings LoadSettings() => Read<AppSettings>("settings.json") ?? new();
    public List<ChatMessage> LoadHistory() => Read<List<ChatMessage>>("history.json") ?? [];
    public List<Peer> LoadPeers() => Read<List<Peer>>("contacts.json") ?? [];
    public Dictionary<string, string> LoadDrafts() => Read<Dictionary<string, string>>("drafts.json") ?? [];
    public void SaveSettings(AppSettings settings) => Write("settings.json", settings);
    public void SaveHistory(IEnumerable<ChatMessage> messages) => Write("history.json", messages.TakeLast(10000).ToArray());
    public void SavePeers(IEnumerable<Peer> peers) => Write("contacts.json", peers.ToArray());
    public void SaveDrafts(IReadOnlyDictionary<string, string> drafts) => Write("drafts.json", drafts.Where(d => !string.IsNullOrEmpty(d.Value)).ToDictionary());
    public List<GroupChat> LoadGroups() => Read<List<GroupChat>>("groups.json") ?? [];
    public void SaveGroups(IEnumerable<GroupChat> groups) => Write("groups.json", groups.ToArray());
    public List<FileTransfer> LoadTransfers() => Read<List<FileTransfer>>("transfers.json") ?? [];
    public void SaveTransfers(IEnumerable<FileTransfer> transfers) => Write("transfers.json", transfers.Take(2000).ToArray());
    public List<Reminder> LoadReminders() => Read<List<Reminder>>("reminders.json") ?? [];
    public void SaveReminders(IEnumerable<Reminder> reminders) => Write("reminders.json", reminders.ToArray());
    private T? Read<T>(string name)
    {
        var path = System.IO.Path.Combine(DirectoryPath, name);
        if (!File.Exists(path)) return default;
        try { return JsonSerializer.Deserialize<T>(File.ReadAllText(path), options); }
        catch (JsonException) { File.Copy(path, path + ".corrupt-" + DateTime.UtcNow.Ticks); return default; }
    }
    private void Write<T>(string name, T value)
    {
        var path = System.IO.Path.Combine(DirectoryPath, name);
        File.WriteAllText(path + ".tmp", JsonSerializer.Serialize(value, options));
        File.Move(path + ".tmp", path, true);
    }
}
