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
    private string firstName = "", lastName = "", about = "", avatarPath = "";
    public string FirstName { get => firstName; set { Set(ref firstName, value); Changed(nameof(FullName)); Changed(nameof(HasFullName)); } }
    public string LastName { get => lastName; set { Set(ref lastName, value); Changed(nameof(FullName)); Changed(nameof(HasFullName)); } }
    public string About { get => about; set { Set(ref about, value); Changed(nameof(HasAbout)); } }
    public string FullName => $"{FirstName} {LastName}".Trim();
    public bool HasFullName => FullName.Length > 0;
    public bool HasAbout => About.Length > 0;
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
    public string Delivery { get => delivery; set { Set(ref delivery, value); Changed(nameof(CanRetry)); Changed(nameof(DeliveryProblem)); } }
    /// <summary>The message (or its file) did not get through: shown in the warning colour.</summary>
    public bool DeliveryProblem => Delivery is "Declined" or "Cancelled" or "Aborted" or "Interrupted" or "Unconfirmed" || Delivery.StartsWith("Failed", StringComparison.Ordinal) || Delivery.StartsWith("Not sent", StringComparison.Ordinal);
    public bool CanRetry => Outgoing && !Broadcast && ImagePath.Length == 0 && FileName.Length == 0 && Delivery is "Failed" or "Unconfirmed";
    public string TimeLabel => Time.ToLocalTime().ToString("h:mm tt");
    /// <summary>Local copy of an image sent or received in this conversation; empty for text messages.</summary>
    public string ImagePath { get; set; } = "";
    /// <summary>Name of a non-image file offered in this conversation; empty otherwise.</summary>
    public string FileName { get; set; } = "";
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
    /// <summary>A profile picture (LAN Messenger "avatar" transfer); never listed with ordinary files.</summary>
    public bool Avatar { get; init; }
    public DateTimeOffset Time { get; init; } = DateTimeOffset.Now;
    public string TimeLabel => Time.ToLocalTime().ToString("MMM d · h:mm tt");
    public string PeerName { get; set; } = "LAN peer";
    public string DirectionLabel => $"{(Outgoing ? "To" : "From")} {PeerName}";
    public string Path { get; set; } = "";
    private string state = "Waiting for approval";
    private double progress;
    public string State { get => state; set => Set(ref state, value); }
    public double Progress { get => progress; set => Set(ref progress, value); }
    public string SizeLabel => Size < 1024 * 1024 ? $"{Size / 1024d:0.#} KB" : $"{Size / (1024d * 1024):0.#} MB";
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
    /// <summary>Desktop colour theme: "light", "dark" or "system".</summary>
    public string Theme { get; set; } = "light";
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
