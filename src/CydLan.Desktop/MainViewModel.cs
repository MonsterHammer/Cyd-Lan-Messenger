using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Data;
using CydLan.Core;

namespace CydLan.Desktop;

public sealed class MainViewModel : Observable, IAsyncDisposable
{
    private readonly LocalStore? store;
    private readonly LanService? lan;
    private readonly List<ChatMessage> history = [];
    public AppSettings Settings { get; }
    public bool Preview { get; }
    public ObservableCollection<Peer> Peers { get; } = [];
    public ObservableCollection<ChatMessage> Messages { get; } = [];
    public ObservableCollection<FileTransfer> Transfers { get; } = [];
    public ObservableCollection<string> Diagnostics { get; } = [];
    public ICollectionView People { get; }
    private Peer? selected;
    private string search = "", messageSearch = "", draft = "", networkStatus = "Starting local network…", toast = "";
    private bool transfersOpen, broadcasting;
    public bool ConversationFocused { get; set; }
    public event Action<string, string>? Notify;
    public event Action? ScrollRequested;
    public Peer? SelectedPeer
    {
        get => selected;
        set { if (!Set(ref selected, value)) return; if (value is not null) value.Unread = 0; Broadcasting = false; TransfersOpen = false; MessageSearch = ""; RefreshMessages(); Changed(nameof(HasPeer)); Changed(nameof(NoPeer)); Changed(nameof(CanCompose)); Changed(nameof(ChatTitle)); Changed(nameof(ChatSubtitle)); }
    }
    public string Search { get => search; set { if (Set(ref search, value)) People.Refresh(); } }
    public string MessageSearch { get => messageSearch; set { if (Set(ref messageSearch, value)) RefreshMessages(); } }
    public string Draft { get => draft; set => Set(ref draft, value); }
    public string NetworkStatus { get => networkStatus; set => Set(ref networkStatus, value); }
    public string Toast { get => toast; set => Set(ref toast, value); }
    public bool TransfersOpen { get => transfersOpen; set { Set(ref transfersOpen, value); Changed(nameof(ChatOpen)); Changed(nameof(IsConversationSection)); Changed(nameof(IsBroadcastSection)); Changed(nameof(ChatTitle)); } }
    public bool ChatOpen => !TransfersOpen;
    public bool IsConversationSection => !TransfersOpen && !Broadcasting;
    public bool IsBroadcastSection => !TransfersOpen && Broadcasting;
    public bool Broadcasting { get => broadcasting; set { Set(ref broadcasting, value); Changed(nameof(HasPeer)); Changed(nameof(NoPeer)); Changed(nameof(CanCompose)); Changed(nameof(ChatTitle)); Changed(nameof(ChatSubtitle)); Changed(nameof(IsConversationSection)); Changed(nameof(IsBroadcastSection)); RefreshMessages(); } }
    public bool HasPeer => SelectedPeer is not null && !Broadcasting;
    public bool NoPeer => SelectedPeer is null && !Broadcasting;
    public bool CanCompose => Broadcasting || SelectedPeer?.Online == true;
    public string ChatTitle => TransfersOpen ? "File transfers" : Broadcasting ? "Broadcast preview" : SelectedPeer?.Name ?? "Your conversations";
    public string ChatSubtitle => Preview ? "Offline design mode · sample conversations" : Broadcasting ? $"Send an announcement to {OnlineCount} connected people" : SelectedPeer is { } p ? $"{p.Presence}  ·  Direct LAN connection" : "Your people. One network. No server.";
    public int OnlineCount => Peers.Count(p => p.Online);
    public string ProfileName => Settings.DisplayName;
    public string ProfileInitial => Settings.DisplayName.Length > 0 ? Settings.DisplayName[..1].ToUpperInvariant() : "C";
    public string ProfileStatus => Settings.Status switch { "away" => "Away", "busy" => "Busy", "dnd" => "Do not disturb", _ => "Available" };
    public string LocalAddress => lan?.LocalAddress ?? "Not connected";
    public string DeviceLabel => Preview ? "OFFLINE DESIGN MODE · NO LAN CONNECTION" : $"Your device: {LocalAddress}";
    public string ConnectionLabel => Preview ? "Sample contact" : "Direct peer connection";
    public string ConnectionPrivacy => Preview ? "This is a sample conversation. Preview messages stay in memory and no LAN connection is opened." : "Messages use LAN Messenger's legacy encryption. History is stored on this device.";

    public MainViewModel(bool preview = true, bool seedPreview = true)
    {
        Preview = preview;
        if (preview) Settings = new() { DisplayName = "Alex Morgan" };
        else
        {
            store = new(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CydLan"));
            Settings = store.LoadSettings();
            if (Settings.Port is < 1 or > 65535) Settings.Port = 50000;
            if (Settings.Status is not ("chat" or "away" or "busy" or "dnd")) Settings.Status = "chat";
            if (Settings.UserId.Length is < 1 or > 61 || Settings.UserId.Any(c => c < 33 || c > 126)) Settings.UserId = Guid.NewGuid().ToString("N");
            store.SaveSettings(Settings);
            history.AddRange(store.LoadHistory());
            foreach (var peer in store.LoadPeers()) { peer.Online = false; peer.Unread = 0; Peers.Add(peer); }
            lan = new(Settings);
            lan.PeerOnline += peer => UI(() => {
                var existing = Peers.FirstOrDefault(p => p.Id == peer.Id);
                if (existing is null) { existing = peer; Peers.Add(peer); }
                else { existing.Name = peer.Name; existing.Address = peer.Address; existing.Port = peer.Port; existing.Version = peer.Version; existing.Status = peer.Status; existing.Online = true; }
                existing.PropertyChanged -= PeerChanged; existing.PropertyChanged += PeerChanged;
                UpdateCounts(); Save();
            });
            lan.PeerOffline += id => UI(() => { if (Peers.FirstOrDefault(p => p.Id == id) is { } p) { p.Online = false; p.Typing = ""; } UpdateCounts(); });
            lan.MessageReceived += message => UI(() => {
                var peer = Peers.FirstOrDefault(p => p.Id == message.PeerId);
                if (peer is null) return;
                history.Add(message); peer.Preview = message.Text.ReplaceLineEndings(" ");
                if (SelectedPeer?.Id != peer.Id || TransfersOpen || Broadcasting || !ConversationFocused) peer.Unread++;
                RefreshMessages(); Save(); Notify?.Invoke(peer.Name, message.Text);
            });
            lan.TypingChanged += (id, typing) => UI(() => { if (Peers.FirstOrDefault(p => p.Id == id) is { } p) p.Typing = typing; });
            lan.TransferChanged += transfer => UI(() => { if (!Transfers.Contains(transfer)) { transfer.PeerName = Peers.FirstOrDefault(p => p.Id == transfer.PeerId)?.Name ?? transfer.PeerId; Transfers.Insert(0, transfer); if (!transfer.Outgoing) Notify?.Invoke(transfer.PeerName, transfer.Name); } });
            lan.Diagnostic += message => UI(() => { Diagnostics.Insert(0, $"{DateTime.Now:HH:mm:ss}  {message}"); while (Diagnostics.Count > 100) Diagnostics.RemoveAt(100); });
        }
        People = CollectionViewSource.GetDefaultView(Peers);
        People.Filter = obj => obj is Peer peer && (peer.Name.Contains(Search, StringComparison.OrdinalIgnoreCase) || peer.Address.Contains(Search, StringComparison.OrdinalIgnoreCase) || history.Any(m => m.PeerId == peer.Id && m.Text.Contains(Search, StringComparison.OrdinalIgnoreCase)));
        People.SortDescriptions.Add(new(nameof(Peer.Online), ListSortDirection.Descending));
        People.SortDescriptions.Add(new(nameof(Peer.Name), ListSortDirection.Ascending));
        if (preview && seedPreview) SeedPreview();
    }
    private void PeerChanged(object? sender, PropertyChangedEventArgs e) { if (sender == SelectedPeer) { Changed(nameof(ChatSubtitle)); Changed(nameof(ChatTitle)); Changed(nameof(CanCompose)); } }
    private void UpdateCounts() { Changed(nameof(OnlineCount)); Changed(nameof(LocalAddress)); Changed(nameof(CanCompose)); Changed(nameof(ChatSubtitle)); People.Refresh(); }
    private static void UI(Action action) => System.Windows.Application.Current.Dispatcher.BeginInvoke(action);
    public async Task StartAsync()
    {
        if (Preview) { NetworkStatus = "Design mode · LAN disconnected"; return; }
        try { await lan!.StartAsync(); NetworkStatus = "Local network connected"; Changed(nameof(LocalAddress)); }
        catch (Exception e) when (e is System.Net.Sockets.SocketException or ArgumentException or IOException)
        { NetworkStatus = "Network unavailable"; Toast = $"Could not start networking: {e.Message}. Close the old client on this PC if it uses port 50000, then restart CydLan."; Diagnostics.Add(Toast); }
    }
    public async Task RefreshAsync() { if (lan is not null) await RunAsync(() => lan.AnnounceAsync()); Toast = Preview ? "Design mode is offline. The people shown here are sample contacts." : "Looking for people on your local network…"; }
    public async Task SendAsync()
    {
        var text = Draft.Trim(); if (text.Length == 0 || !CanCompose) return;
        if (Preview)
        {
            if (Broadcasting) { Toast = "Announcement preview only · nothing was sent."; Draft = ""; return; }
            if (SelectedPeer is not { } sample) return;
            history.Add(new() { PeerId = sample.Id, Text = text, Outgoing = true, Delivery = "Preview only" });
            sample.Preview = text.ReplaceLineEndings(" "); Draft = ""; RefreshMessages();
            return;
        }
        if (Broadcasting)
        {
            await RunAsync(async () => { int count = await lan!.BroadcastAsync(text); Toast = $"Announcement sent to {count} connected people."; Draft = ""; }); return;
        }
        var peer = SelectedPeer!;
        var message = new ChatMessage { PeerId = peer.Id, Text = text, Outgoing = true, Delivery = "Sending…" };
        // Legacy peers parse message IDs as signed 64-bit decimal integers.
        message.Id = NextMessageId().ToString();
        history.Add(message); Draft = ""; peer.Preview = text.ReplaceLineEndings(" "); RefreshMessages(); Save();
        try { message.Delivery = await lan!.SendChatAsync(peer.Id, text, message.Id) ? "Delivered" : "Unconfirmed"; }
        catch (Exception e) when (e is IOException or System.Net.Sockets.SocketException or OperationCanceledException or InvalidOperationException or ArgumentException) { message.Delivery = "Failed"; Toast = e.Message; }
        Save();
    }
    private long messageSequence = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 100;
    private long NextMessageId() => Interlocked.Increment(ref messageSequence);
    public async Task SendTypingAsync(bool composing) { if (lan is not null && SelectedPeer?.Online == true && !Broadcasting) await RunAsync(() => lan.SendTypingAsync(SelectedPeer.Id, composing), false); }
    public async Task OfferFileAsync(string path)
    {
        if (Preview) { Toast = "Design preview — files are not sent."; return; }
        if (SelectedPeer is null) return;
        await RunAsync(async () => { await lan!.OfferFileAsync(SelectedPeer.Id, path); TransfersOpen = true; });
    }
    public async Task AcceptFileAsync(FileTransfer transfer, string path) { if (lan is not null) await RunAsync(() => lan.AcceptFileAsync(transfer, path)); }
    public async Task DeclineFileAsync(FileTransfer transfer) { if (lan is not null) await RunAsync(() => lan.DeclineFileAsync(transfer)); }
    public async Task SaveProfileAsync(string name, string status, bool notifications, bool minimized)
    {
        Settings.DisplayName = name; Settings.Status = status; Settings.Notifications = notifications; Settings.StartMinimized = minimized;
        store?.SaveSettings(Settings); Changed(nameof(ProfileName)); Changed(nameof(ProfileInitial)); Changed(nameof(ProfileStatus));
        if (lan is not null) await RunAsync(() => lan.UpdateProfileAsync());
    }
    private async Task RunAsync(Func<Task> action, bool report = true)
    {
        try { await action(); }
        catch (Exception e) when (e is IOException or System.Net.Sockets.SocketException or OperationCanceledException or UnauthorizedAccessException or InvalidOperationException)
        { if (report) Toast = e.Message; }
    }
    private void RefreshMessages()
    {
        Messages.Clear();
        if (!Broadcasting && SelectedPeer is not null)
            foreach (var message in history.Where(m => m.PeerId == SelectedPeer.Id && m.Text.Contains(MessageSearch, StringComparison.OrdinalIgnoreCase)).TakeLast(500)) Messages.Add(message);
        ScrollRequested?.Invoke();
    }
    private void Save()
    {
        try { store?.SaveHistory(history); store?.SavePeers(Peers); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { Toast = "Local history could not be saved: " + e.Message; }
    }
    public async ValueTask DisposeAsync() { Save(); if (lan is not null) await lan.DisposeAsync(); }
    private void SeedPreview()
    {
        var names = new[] { ("1", "Jamie Chen", "chat", "Thanks! That looks great."), ("2", "Sofia Reyes", "chat", "Sent you the updated assets"), ("3", "Marcus Taylor", "busy", "Let's sync after lunch"), ("4", "Olivia Wilson", "chat", "The network is all set ✓"), ("5", "Daniel Kim", "away", "Back in a few minutes") };
        foreach (var (id, name, status, preview) in names) Peers.Add(new() { Id = id, Name = name, Status = status, Online = true, Address = "192.168.1." + (110 + int.Parse(id)), Version = id == "1" ? "LAN Messenger 1.2.35" : "CydLan 0.1", Preview = preview, Unread = id == "2" ? 2 : 0 });
        void Add(string text, bool outgoing, int minutes) => history.Add(new() { PeerId = "1", Text = text, Outgoing = outgoing, Delivery = outgoing ? "Delivered" : "Received", Time = DateTimeOffset.Now.AddMinutes(-minutes) });
        Add("Hey Alex! I'm using the original LAN Messenger. Can we still chat and share files?", false, 9);
        Add("Yep! We can chat and share files directly, even with the older app.", true, 8);
        Add("Perfect. A fresh look, same local connection.", false, 6);
        Add("Exactly. Sending over the concept now.", true, 5);
        Add("Thanks! That looks great.", false, 2);
        SelectedPeer = Peers[0]; UpdateCounts();
        Transfers.Add(new() { Id = "preview", PeerId = "1", PeerName = "Jamie Chen", Name = "Workspace concepts.fig", Size = 4823000, Outgoing = true, State = "Preview example", Progress = 100 });
    }
}
