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
    public string Delivery { get => delivery; set => Set(ref delivery, value); }
    public string TimeLabel => Time.ToLocalTime().ToString("h:mm tt");
}

public sealed class FileTransfer : Observable
{
    public required string Id { get; init; }
    public required string PeerId { get; init; }
    public required string Name { get; init; }
    public long Size { get; init; }
    public bool Outgoing { get; init; }
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
    public string UserId { get; set; } = Guid.NewGuid().ToString("N");
    public string DisplayName { get; set; } = Environment.UserName;
    public string Status { get; set; } = "chat";
    public bool Notifications { get; set; } = true;
    public bool StartMinimized { get; set; }
    public int Port { get; set; } = 50000;
    public string Multicast { get; set; } = "239.255.100.100";
}

public sealed class LocalStore
{
    public string DirectoryPath { get; }
    private readonly JsonSerializerOptions options = new() { WriteIndented = true };
    public LocalStore(string directoryPath) { DirectoryPath = directoryPath; Directory.CreateDirectory(directoryPath); }
    public AppSettings LoadSettings() => Read<AppSettings>("settings.json") ?? new();
    public List<ChatMessage> LoadHistory() => Read<List<ChatMessage>>("history.json") ?? [];
    public List<Peer> LoadPeers() => Read<List<Peer>>("contacts.json") ?? [];
    public void SaveSettings(AppSettings settings) => Write("settings.json", settings);
    public void SaveHistory(IEnumerable<ChatMessage> messages) => Write("history.json", messages.TakeLast(10000).ToArray());
    public void SavePeers(IEnumerable<Peer> peers) => Write("contacts.json", peers.ToArray());
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
