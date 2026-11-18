using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Net;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Color = System.Windows.Media.Color;
using FontFamily = System.Windows.Media.FontFamily;
using CydLan.Core;

namespace CydLan.Desktop;

public sealed class MainViewModel : Observable, IAsyncDisposable
{
    private readonly LocalStore? store;
    private readonly LanService? lan;
    private readonly DiagnosticLog? log;
    private readonly Dictionary<string, string> loggedTransferStates = [];
    public string LogsDirectory => log?.DirectoryPath ?? "";
    public bool HasLogs => log is not null;
    private readonly List<ChatMessage> history = [];
    private readonly Dictionary<string, string> drafts = [];
    private readonly Dictionary<string, ObservableCollection<Attachment>> attachments = [];
    private readonly Dictionary<string, ChatMessage> transferMessages = [];
    private readonly HashSet<string> receivedImages = [];
    public const int MaxAttachments = 10;
    /// <summary>Where sent and received images are kept so conversation history can show them later.</summary>
    public string ImagesDirectory { get; }
    public AppSettings Settings { get; }
    public bool Preview { get; }
    public ObservableCollection<Peer> Peers { get; } = [];
    public ObservableCollection<ChatMessage> Messages { get; } = [];
    public ObservableCollection<FileTransfer> Transfers { get; } = [];
    public ObservableCollection<GroupChat> Groups { get; } = [];
    /// <summary>The Files section: every transfer, or only received / only sent ones.</summary>
    public ICollectionView TransfersView { get; }
    public string TransferFilter { get => transferFilter; set { if (Set(ref transferFilter, value)) { TransfersView.Refresh(); Changed(nameof(NoTransfersShown)); } } }
    public int ReceivedCount => Transfers.Count(t => !t.Outgoing);
    public int SentCount => Transfers.Count(t => t.Outgoing);
    public bool NoTransfersShown => TransfersView.IsEmpty;
    /// <summary>The open conversation's files panel: what this person sent you, or what you sent them.</summary>
    public ICollectionView ConversationFiles { get; }
    public ICollectionView ActiveConversationTransfers { get; }
    private bool conversationFilesSent;
    public bool ConversationFilesSent { get => conversationFilesSent; set { if (Set(ref conversationFilesSent, value)) RefreshConversationFiles(); } }
    public int ConversationReceivedCount => SelectedPeer is { } p ? Transfers.Count(t => t.PeerId == p.Id && !t.Outgoing) : 0;
    public int ConversationSentCount => SelectedPeer is { } p ? Transfers.Count(t => t.PeerId == p.Id && t.Outgoing) : 0;
    public bool NoConversationFiles => ConversationFiles.IsEmpty;
    private void RefreshConversationFiles()
    {
        ConversationFiles.Refresh();
        ActiveConversationTransfers.Refresh();
        Changed(nameof(ConversationReceivedCount)); Changed(nameof(ConversationSentCount)); Changed(nameof(NoConversationFiles));
    }
    public ObservableCollection<string> Diagnostics { get; } = [];
    public ICollectionView People { get; }
    private Peer? selected;
    private GroupChat? selectedGroup;
    private bool groupsOpen;
    private string transferFilter = "all";
    private string search = "", messageSearch = "", networkStatus = "Starting local network…", toast = "";
    private bool transfersOpen, broadcasting, connected;
    private bool replying;
    /// <summary>The user has clicked or typed in the composer of the open conversation; until then its messages stay unread.</summary>
    public bool Replying { get => replying; set { replying = value; MarkConversationRead(); } }
    public event Action<string, string>? Notify;
    public event Action<bool>? ScrollRequested;
    public Peer? SelectedPeer
    {
        get => selected;
        set
        {
            if (!Set(ref selected, value)) return;
            // Picking a person always returns to one-to-one chat.
            if (value is not null) { broadcasting = false; groupsOpen = false; transfersOpen = false; }
            MessageSearch = ""; SectionChanged();
        }
    }
    public GroupChat? SelectedGroup
    {
        get => selectedGroup;
        set
        {
            if (!Set(ref selectedGroup, value)) return;
            if (value is not null) { groupsOpen = true; broadcasting = false; transfersOpen = false; }
            MessageSearch = ""; SectionChanged();
        }
    }
    public string Search { get => search; set { if (Set(ref search, value)) People.Refresh(); } }
    public string MessageSearch { get => messageSearch; set { if (Set(ref messageSearch, value)) RefreshMessages(); } }
    private string DraftKey => GroupsOpen ? (SelectedGroup is { } g ? "group:" + g.Id : "groups") : Broadcasting ? "broadcast" : SelectedPeer?.Id ?? "none";
    public string Draft { get => drafts.GetValueOrDefault(DraftKey, ""); set { drafts[DraftKey] = value; Changed(); Changed(nameof(CanSend)); } }
    public string NetworkStatus { get => networkStatus; set { Set(ref networkStatus, value); Changed(nameof(ConnectionSummary)); } }
    public string Toast { get => toast; set => Set(ref toast, value); }
    // Sections (left rail): Chats, Groups, Files, Broadcast. Exactly one is active.
    public bool TransfersOpen { get => transfersOpen; set { if (transfersOpen == value) return; transfersOpen = value; SectionChanged(); } }
    public bool Broadcasting { get => broadcasting; set { if (broadcasting == value) return; broadcasting = value; if (value) groupsOpen = false; SectionChanged(); } }
    public bool GroupsOpen { get => groupsOpen; set { if (groupsOpen == value) return; groupsOpen = value; if (value) broadcasting = false; SectionChanged(); } }
    public bool ChatOpen => !TransfersOpen;
    public bool IsConversationSection => !TransfersOpen && !Broadcasting && !GroupsOpen;
    public bool IsBroadcastSection => !TransfersOpen && Broadcasting;
    public bool IsGroupsSection => !TransfersOpen && GroupsOpen;
    public bool IsFilesSection => TransfersOpen;
    public bool ShowPeopleList => !TransfersOpen && !GroupsOpen;
    public bool ShowGroupList => !TransfersOpen && GroupsOpen;
    public bool InGroup => IsGroupsSection && SelectedGroup is not null;
    public bool HasPeer => SelectedPeer is not null && !Broadcasting && !GroupsOpen;
    public bool NoPeer => SelectedPeer is null && !Broadcasting && !GroupsOpen;
    public bool NoGroup => GroupsOpen && SelectedGroup is null;
    public bool HasDetails => HasPeer || InGroup;
    public IEnumerable<Peer> SelectedGroupMembers => SelectedGroup?.Members.Select(id => Peers.FirstOrDefault(p => p.Id == id) ?? new Peer { Id = id, Name = "Someone" }).ToList() ?? [];
    public string SectionTitle => TransfersOpen ? "Files" : Broadcasting ? "Broadcast" : GroupsOpen ? "Groups" : "Chats";
    public bool CanCompose => Broadcasting || (GroupsOpen ? SelectedGroup is { Left: false } : SelectedPeer is not null);
    private bool GroupReachable => SelectedGroup is { Left: false } g && g.Members.Any(id => Peers.Any(p => p.Id == id && p.Online));
    public bool CanSend => (!string.IsNullOrWhiteSpace(Draft) || HasAttachments) && (Broadcasting ? OnlineCount > 0 : GroupsOpen ? GroupReachable : SelectedPeer?.Online == true);
    public ObservableCollection<Attachment> Attachments
    {
        get
        {
            if (attachments.TryGetValue(DraftKey, out var list)) return list;
            list = []; list.CollectionChanged += (_, _) => { Changed(nameof(HasAttachments)); Changed(nameof(CanSend)); };
            return attachments[DraftKey] = list;
        }
    }
    public bool HasAttachments => HasPeer && Attachments.Count > 0;
    public bool CanAttach => HasPeer && SelectedPeer?.Online == true;
    public string ChatTitle => TransfersOpen ? "Files" : Broadcasting ? "Broadcast" : GroupsOpen ? SelectedGroup?.Name ?? "Groups" : SelectedPeer?.Name ?? "Conversations";
    public string ChatSubtitle => TransfersOpen ? "" : Broadcasting ? $"{OnlineCount} online" : GroupsOpen ? (SelectedGroup is { } g ? GroupPeopleLabel(g) : "") : SelectedPeer is { } p ? p.Presence : "";
    private string GroupPeopleLabel(GroupChat g) => g.Left ? "You left this group" : string.Join(", ", g.Members.Select(NameOf).Append("you"));
    private string NameOf(string peerId) => Peers.FirstOrDefault(p => p.Id == peerId)?.Name ?? "Someone";
    private void SectionChanged()
    {
        replying = false;
        foreach (var name in new[] { nameof(TransfersOpen), nameof(Broadcasting), nameof(GroupsOpen), nameof(ChatOpen), nameof(IsConversationSection), nameof(IsBroadcastSection),
            nameof(IsGroupsSection), nameof(IsFilesSection), nameof(ShowPeopleList), nameof(ShowGroupList), nameof(InGroup), nameof(HasPeer), nameof(NoPeer), nameof(NoGroup),
            nameof(HasDetails), nameof(SelectedGroupMembers), nameof(SectionTitle), nameof(ChatTitle), nameof(ChatSubtitle), nameof(Draft) })
            Changed(name);
        Changed(nameof(SelectedGroup)); Changed(nameof(SelectedPeer));
        AttachmentsSwitched(); ComposeChanged(); RefreshMessages(true); RefreshConversationFiles();
    }
    private void AttachmentsSwitched() { Changed(nameof(Attachments)); Changed(nameof(HasAttachments)); Changed(nameof(CanSend)); }
    public int OnlineCount => Peers.Count(p => p.Online);
    public string ProfileName => Settings.DisplayName;
    public string ProfileInitial => Settings.DisplayName.Length > 0 ? Settings.DisplayName[..1].ToUpperInvariant() : "C";
    public string ProfileAvatar => Settings.AvatarPath;
    private static void TryDelete(string path) { try { File.Delete(path); } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { } }
    /// <summary>Turns a chosen image into a profile picture file; null when it is not a usable image.</summary>
    public string? PrepareProfilePicture(string source) => Images.SaveProfilePicture(source, Path.Combine(ImagesDirectory, "profile"));
    public string ProfileStatus => Settings.Status switch { "away" => "Away", "busy" => "Busy", "dnd" => "Do not disturb", _ => "Available" };
    public string LocalAddress => lan?.LocalAddress ?? "Not connected";
    public string DeviceLabel => Preview ? "OFFLINE PREVIEW · NO LAN CONNECTION" : connected ? $"{LocalAddress} · TCP {lan!.ListeningPort}" : "Network unavailable";
    public string ModeBadge => Preview ? "PREVIEW" : "";
    public string ConnectionSummary => Preview ? "Offline preview" : NetworkStatus;
    public string PeopleSummary => Preview ? "Sample contacts only" : $"{OnlineCount} online · {Peers.Sum(p => p.Unread)} unread";
    public string NetworkEndpoint => Preview ? "" : connected ? $"{LocalAddress} · TCP {lan!.ListeningPort} · UDP {lan.DiscoveryPort}" : "Network unavailable";

    public MainViewModel(bool preview = false, bool seedPreview = true, LocalStore? localStore = null, DiagnosticLog? log = null)
    {
        Preview = preview;
        ImagesDirectory = preview ? Path.Combine(Path.GetTempPath(), "CydLan-preview", "images")
            : Path.Combine(localStore?.DirectoryPath ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CydLan"), "images");
        if (preview) Settings = new() { DisplayName = "Alex Morgan" };
        else
        {
            this.log = log ?? new DiagnosticLog(Path.Combine(localStore?.DirectoryPath ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CydLan"), "logs"));
            store = localStore ?? new(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CydLan"));
            Settings = store.LoadSettings();
            if (Settings.Port is < 1 or > 65535) Settings.Port = 50000;
            if (Settings.Status is not ("chat" or "away" or "busy" or "dnd")) Settings.Status = "chat";
            // Earlier builds used 32-character IDs, which LAN Messenger cannot match for incoming files.
            if (!AppSettings.IsValidUserId(Settings.UserId)) Settings.UserId = AppSettings.NewUserId();
            store.SaveSettings(Settings);
            history.AddRange(store.LoadHistory());
            foreach (var item in store.LoadDrafts()) drafts[item.Key] = item.Value;
            foreach (var message in history.Where(m => m.Outgoing && m.Delivery == "Sending…" && !m.HasImage && !m.HasFile)) message.Delivery = "Unconfirmed";
            // An image or file still pending when CydLan closed can no longer complete.
            foreach (var message in history.Where(m => m.Outgoing && (m.HasImage || m.HasFile) && (m.Delivery is "Sending…" or "Waiting for them to accept" or "Accepted" || m.Delivery.StartsWith("Accepted ·", StringComparison.Ordinal)))) message.Delivery = "Interrupted";
            foreach (var peer in store.LoadPeers()) { peer.Online = false; peer.Unread = 0; Peers.Add(peer); }
            foreach (var group in store.LoadGroups()) { group.Unread = 0; Groups.Add(group); }
            foreach (var transfer in store.LoadTransfers())
            {
                // Offers and transfers in progress cannot resume after a restart.
                if (transfer.State is "Waiting for approval" or "Accepted" or "Connecting" or "Transferring") transfer.State = "Interrupted";
                Transfers.Add(transfer);
            }
            lan = new(Settings, Peers, this.log) { InlineImageDirectory = Path.Combine(ImagesDirectory, "received"), AvatarDirectory = Path.Combine(ImagesDirectory, "avatars") };
            lan.PeerOnline += peer => UI(() => {
                var existing = Peers.FirstOrDefault(p => p.Id == peer.Id);
                if (existing is null) { existing = peer; Peers.Add(peer); }
                else
                {
                    existing.Name = peer.Name; existing.Address = peer.Address; existing.Port = peer.Port; existing.Version = peer.Version; existing.Status = peer.Status; existing.Online = true;
                    // CydLan peers include these in user data; LAN Messenger only sends them in reply to a query.
                    if (LanService.IsCydLanVersion(peer.Version)) { existing.FirstName = peer.FirstName; existing.LastName = peer.LastName; existing.About = peer.About; }
                }
                existing.PropertyChanged -= PeerChanged; existing.PropertyChanged += PeerChanged;
                UpdateCounts(); Save();
            });
            lan.ProfileReceived += (id, first, last, about) => UI(() => {
                if (Peers.FirstOrDefault(p => p.Id == id) is not { } peer) return;
                peer.FirstName = first; peer.LastName = last; peer.About = about; Save();
            });
            lan.AvatarReceived += (id, path) => UI(() => {
                if (Peers.FirstOrDefault(p => p.Id == id) is not { } peer) { TryDelete(path); return; }
                var previous = peer.AvatarPath; peer.AvatarPath = path; Save();
                if (previous.Length > 0 && previous != path && previous.StartsWith(Path.Combine(ImagesDirectory, "avatars"), StringComparison.OrdinalIgnoreCase)) TryDelete(previous);
            });
            lan.PeerOffline += id => UI(() => { if (Peers.FirstOrDefault(p => p.Id == id) is { } p) { p.Online = false; p.Typing = ""; } UpdateCounts(); });
            lan.MessageReceived += message => UI(() => {
                var peer = Peers.FirstOrDefault(p => p.Id == message.PeerId);
                if (peer is null) return;
                history.Add(message); peer.Preview = message.Text.ReplaceLineEndings(" ");
                if (SelectedPeer?.Id != peer.Id || !IsConversationSection || !Replying) peer.Unread++;
                peer.Typing = "";
                RefreshMessages(); Changed(nameof(PeopleSummary)); Save(); Notify?.Invoke(peer.Name, message.Text);
            });
            lan.TypingChanged += (id, typing) => UI(() => { if (Peers.FirstOrDefault(p => p.Id == id) is { } p) p.Typing = typing; });
            lan.TransferChanged += transfer => UI(() => OnTransferChanged(transfer));
            lan.GroupMessageReceived += (sender, thread, op, text) => UI(() => OnGroupMessage(sender, thread, op, text));
            lan.Diagnostic += message => UI(() => { Diagnostics.Insert(0, $"{DateTime.Now:HH:mm:ss}  {message}"); while (Diagnostics.Count > 100) Diagnostics.RemoveAt(100); });
        }
        TransfersView = new ListCollectionView(Transfers) { Filter = item => item is FileTransfer t && (TransferFilter == "all" || (TransferFilter == "received") != t.Outgoing) };
        ConversationFiles = new ListCollectionView(Transfers) { Filter = item => item is FileTransfer t && SelectedPeer is { } p && t.PeerId == p.Id && t.Outgoing == ConversationFilesSent };
        ActiveConversationTransfers = new ListCollectionView(Transfers) { Filter = item => item is FileTransfer { IsActive: true } t && HasPeer && t.PeerId == SelectedPeer?.Id };
        Transfers.CollectionChanged += (_, _) => { Changed(nameof(ReceivedCount)); Changed(nameof(SentCount)); Changed(nameof(NoTransfersShown)); RefreshConversationFiles(); };
        People = CollectionViewSource.GetDefaultView(Peers);
        People.Filter = obj => obj is Peer peer && (peer.Name.Contains(Search, StringComparison.OrdinalIgnoreCase) || peer.Address.Contains(Search, StringComparison.OrdinalIgnoreCase) || history.Any(m => m.PeerId == peer.Id && m.Text.Contains(Search, StringComparison.OrdinalIgnoreCase)));
        People.SortDescriptions.Add(new(nameof(Peer.Online), ListSortDirection.Descending));
        People.SortDescriptions.Add(new(nameof(Peer.Name), ListSortDirection.Ascending));
        foreach (var peer in Peers) peer.PropertyChanged += PeerChanged;
        if (preview && seedPreview) SeedPreview();
        ApplySettings();
    }
    private void ComposeChanged() { Changed(nameof(CanCompose)); Changed(nameof(CanSend)); Changed(nameof(CanAttach)); }
    private void MarkConversationRead()
    {
        if (!Replying) return;
        if (IsConversationSection && SelectedPeer is { } peer) peer.Unread = 0;
        if (IsGroupsSection && SelectedGroup is { } group) group.Unread = 0;
    }
    private void PeerChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender == SelectedPeer) { Changed(nameof(ChatSubtitle)); Changed(nameof(ChatTitle)); ComposeChanged(); }
        if (e.PropertyName == nameof(Peer.Unread)) Changed(nameof(PeopleSummary));
    }
    private void UpdateCounts() { Changed(nameof(OnlineCount)); Changed(nameof(LocalAddress)); Changed(nameof(DeviceLabel)); Changed(nameof(NetworkEndpoint)); Changed(nameof(PeopleSummary)); ComposeChanged(); Changed(nameof(ChatSubtitle)); People.Refresh(); }
    private static void UI(Action action) => System.Windows.Application.Current.Dispatcher.BeginInvoke(action);
    public async Task StartAsync(bool discovery = true, IPAddress? bindAddress = null)
    {
        if (Preview) { NetworkStatus = "Design mode · LAN disconnected"; return; }
        try { await lan!.StartAsync(discovery, bindAddress); connected = true; NetworkStatus = "Local network connected"; UpdateCounts(); }
        catch (Exception e) when (e is System.Net.Sockets.SocketException or ArgumentException or IOException)
        { log?.Write("network.start_failed", e); NetworkStatus = "Network unavailable"; Toast = $"Could not start networking: {e.Message}"; Diagnostics.Add(Toast); UpdateCounts(); }
    }
    public async Task RefreshAsync()
    {
        if (Preview) { Toast = "Offline preview"; return; }
        Toast = "Looking for people on your local network…";
        await RunAsync(() => lan!.RefreshAsync());
        UpdateCounts();
    }
    public async Task SendAsync()
    {
        var text = Settings.TrimMessages ? Draft.Trim() : Draft;
        if (string.IsNullOrWhiteSpace(text)) text = "";
        var files = HasPeer ? Attachments.ToList() : [];
        if (GroupsOpen) { if (text.Length > 0 && CanSend) await SendGroupMessageAsync(SelectedGroup!, text); return; }
        if ((text.Length == 0 && files.Count == 0) || !CanSend) return;
        if (Preview)
        {
            if (Broadcasting) { Toast = "Announcement preview only · nothing was sent."; Draft = ""; return; }
            if (SelectedPeer is not { } sample) return;
            foreach (var file in files)
                history.Add(new() { PeerId = sample.Id, Outgoing = true, Delivery = "Preview only", ImagePath = file.IsImage ? KeepCopy(file.Path) : "", FileName = file.IsImage ? "" : file.Name });
            if (text.Length > 0) history.Add(new() { PeerId = sample.Id, Text = text, Outgoing = true, Delivery = "Preview only" });
            sample.Preview = text.Length > 0 ? text.ReplaceLineEndings(" ") : files.Any(f => f.IsImage) ? "Photo" : files[0].Name;
            Attachments.Clear(); Draft = ""; RefreshMessages(true);
            return;
        }
        if (files.Count > 0) await SendAttachmentsAsync(SelectedPeer!, files);
        if (text.Length == 0) return;
        if (Broadcasting)
        {
            Draft = "";
            await RunAsync(async () => { int count = await lan!.BroadcastAsync(text); Toast = $"Announcement sent to {count} connected people."; }); return;
        }
        var peer = SelectedPeer!;
        var message = new ChatMessage { PeerId = peer.Id, Text = text, Outgoing = true, Delivery = "Sending…" };
        // Legacy peers parse message IDs as signed 64-bit decimal integers.
        message.Id = NextMessageId().ToString();
        history.Add(message); Draft = ""; peer.Preview = text.ReplaceLineEndings(" "); RefreshMessages(true); Save();
        await DeliverAsync(message);
    }
    public async Task RetryAsync(ChatMessage message)
    {
        if (Preview || !message.CanRetry) return;
        if (Peers.FirstOrDefault(p => p.Id == message.PeerId)?.Online != true) { Toast = "Contact offline"; return; }
        message.Delivery = "Sending…";
        await DeliverAsync(message);
    }
    private async Task DeliverAsync(ChatMessage message)
    {
        try { message.Delivery = await lan!.SendChatAsync(message.PeerId, message.Text, message.Id) ? "Delivered" : "Unconfirmed"; }
        catch (Exception e) when (e is IOException or System.Net.Sockets.SocketException or OperationCanceledException or InvalidOperationException or ArgumentException) { log?.Write("message.send_failed", e, ("peer", message.PeerId), ("id", message.Id)); message.Delivery = "Failed"; Toast = e.Message; }
        Save();
    }
    public async Task ConnectByAddressAsync(string endpoint)
    {
        if (Preview) { Toast = "Offline preview"; return; }
        var parts = endpoint.Trim().Split(':');
        int port = Settings.Port;
        if (parts.Length is < 1 or > 2 || !IPAddress.TryParse(parts[0], out var ip) || ip.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork ||
            (parts.Length == 2 && !int.TryParse(parts[1], out port)) || port is < 1 or > 65535)
        { Toast = "Invalid IP address or port"; return; }
        await RunAsync(async () => { await lan!.DiscoverAsync(ip, port); Toast = $"Connection requested · {ip}:{port}"; });
    }
    private long messageSequence = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 100;
    private long NextMessageId() => Interlocked.Increment(ref messageSequence);
    public async Task SendTypingAsync(bool composing, string? peerId = null)
    {
        peerId ??= SelectedPeer?.Id;
        if (lan is not null && peerId is not null && (!composing || !Broadcasting) && Peers.Any(p => p.Id == peerId && p.Online))
            await RunAsync(() => lan.SendTypingAsync(peerId, composing), false);
    }
    /// <summary>Queues a file or image in the composer. Returns a message for the user when it cannot be added.</summary>
    public string? AddAttachment(string path)
    {
        if (!CanAttach) return "No online contact selected";
        if (Attachments.Count >= MaxAttachments) return $"Attachment limit: {MaxAttachments}";
        try
        {
            var item = Attachment.From(path);
            if (Attachments.Any(a => string.Equals(a.Path, item.Path, StringComparison.OrdinalIgnoreCase))) return null;
            Attachments.Add(item); return null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException) { return "That file can't be attached: " + e.Message; }
    }
    /// <summary>Attaches a pasted bitmap (for example a Win+Shift+S screenshot) by saving it as PNG first.</summary>
    public string? AddImage(BitmapSource bitmap)
    {
        if (!CanAttach) return "No online contact selected";
        try { return AddAttachment(Images.SavePng(bitmap, Path.Combine(ImagesDirectory, "pasted"))); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or NotSupportedException) { return "The pasted image couldn't be saved: " + e.Message; }
    }
    public void RemoveAttachment(Attachment item) => Attachments.Remove(item);

    // Images are copied next to history so the conversation still shows them if the original is moved.
    // The original file name is kept because legacy clients display it in their file offer.
    private string KeepCopy(string path)
    {
        if (Path.GetFullPath(path).StartsWith(Path.GetFullPath(ImagesDirectory), StringComparison.OrdinalIgnoreCase)) return path;
        var folder = Path.Combine(ImagesDirectory, "sent", Guid.NewGuid().ToString("N")[..12]);
        Directory.CreateDirectory(folder);
        var copy = Path.Combine(folder, Path.GetFileName(path));
        File.Copy(path, copy);
        return copy;
    }
    private async Task SendAttachmentsAsync(Peer peer, List<Attachment> files)
    {
        Attachments.Clear();
        var queued = new List<(ChatMessage Message, string Path, bool Image)>();
        foreach (var file in files)
        {
            var message = new ChatMessage { PeerId = peer.Id, Outgoing = true, Delivery = "Sending…", Id = NextMessageId().ToString() };
            string path = file.Path;
            try { if (file.IsImage) { path = KeepCopy(file.Path); message.ImagePath = path; } else message.FileName = file.Name; }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { log?.Write("attachment.prepare_failed", e); Toast = $"{file.Name} couldn't be prepared: {e.Message}"; continue; }
            history.Add(message); queued.Add((message, path, file.IsImage));
        }
        if (queued.Count > 0) peer.Preview = queued[^1].Image ? "Photo" : queued[^1].Message.FileName;
        RefreshMessages(true); Save();
        foreach (var (message, path, image) in queued)
        {
            try
            {
                // CydLan peers get the image inline; legacy LAN Messenger receives an ordinary file offer.
                var transfer = await lan!.OfferFileAsync(peer.Id, path, inlineImage: image && LanService.IsCydLanVersion(peer.Version));
                message.TransferId = transfer.Id; transferMessages[transfer.Id] = message;
                message.Delivery = DeliveryFor(transfer);
            }
            catch (Exception e) when (e is IOException or System.Net.Sockets.SocketException or OperationCanceledException or InvalidOperationException)
            { log?.Write("attachment.offer_failed", e, ("peer", peer.Id)); message.Delivery = "Failed"; Toast = e.Message; }
        }
        Save();
    }
    // What the sender sees under an image or file, following the transfer the way LAN Messenger reports it.
    private static string DeliveryFor(FileTransfer t) => t.State switch
    {
        "Waiting for approval" => t.Inline ? "Sending…" : "Waiting for them to accept",
        "Connecting" => "Accepted",
        "Transferring" => $"Accepted · sending {t.Progress:0}%",
        "Sent · receipt unavailable" => "Sent",
        "Declined" or "Cancelled" or "Aborted" or "Interrupted" => t.State,
        var state when state.StartsWith("Failed", StringComparison.Ordinal) => state,
        _ => t.State,
    };
    private static bool IsFinished(string state) => state is "Received" or "Sent · receipt unavailable" or "Declined" or "Cancelled" or "Aborted" or "Interrupted" || state.StartsWith("Failed", StringComparison.Ordinal);
    /// <summary>Withdraws an offer the other person has not accepted yet.</summary>
    public async Task CancelOfferAsync(FileTransfer transfer) { if (lan is not null) await RunAsync(() => lan.CancelFileAsync(transfer)); }
    private void OnTransferChanged(FileTransfer transfer)
    {
        // Progress can fire for every network buffer. Persist state changes only, without file names or paths.
        string state = transfer.State.StartsWith("Failed", StringComparison.Ordinal) ? "Failed" : transfer.State;
        bool stateChanged = loggedTransferStates.GetValueOrDefault(transfer.Id) != state;
        if (stateChanged)
        {
            if (loggedTransferStates.Count >= 2000) loggedTransferStates.Clear();
            loggedTransferStates[transfer.Id] = state;
            log?.Write("transfer.state", null, ("peer", transfer.PeerId), ("id", transfer.Id), ("outgoing", transfer.Outgoing), ("bytes", transfer.Size), ("state", state));
        }
        if (!Transfers.Contains(transfer))
        {
            transfer.PeerName = Peers.FirstOrDefault(p => p.Id == transfer.PeerId)?.Name ?? transfer.PeerId; Transfers.Insert(0, transfer);
            if (!transfer.Outgoing && !transfer.Inline && transfer.State == "Waiting for approval") Notify?.Invoke(transfer.PeerName, "Wants to send you " + transfer.Name);
        }
        if (stateChanged) RefreshConversationFiles();
        if (transferMessages.TryGetValue(transfer.Id, out var sent))
        {
            var delivery = DeliveryFor(transfer);
            if (sent.Delivery != delivery) sent.Delivery = delivery;
        }
        if (IsFinished(transfer.State)) Save();
        var peer = Peers.FirstOrDefault(p => p.Id == transfer.PeerId);
        if (transfer.Inline)
        {
            // Inline images show in the conversation (and in Files, as a record).
            if (transfer.Outgoing || transfer.State != "Received" || peer is null || !receivedImages.Add(transfer.Id)) return;
            if (Images.Load(transfer.Path, 64) is null)
            {
                try { File.Delete(transfer.Path); } catch (IOException) { }
                Diagnostics.Insert(0, $"{DateTime.Now:HH:mm:ss}  Discarded an image from {peer.Name} that could not be decoded.");
                log?.Write("image.decode_failed", null, ("peer", peer.Id), ("id", transfer.Id));
                return;
            }
            history.Add(new() { Id = transfer.Id, PeerId = peer.Id, ImagePath = transfer.Path, Delivery = "Received" });
            peer.Preview = "Photo"; peer.Typing = "";
            if (SelectedPeer?.Id != peer.Id || !IsConversationSection || !Replying) peer.Unread++;
            RefreshMessages(); Changed(nameof(PeopleSummary)); Save(); Notify?.Invoke(peer.Name, "Sent you a photo");
        }
    }
    public async Task OfferFileAsync(string path)
    {
        if (Preview) { Toast = "Design preview — files are not sent."; return; }
        if (SelectedPeer is null) return;
        await RunAsync(async () => { await lan!.OfferFileAsync(SelectedPeer.Id, path); TransfersOpen = true; });
    }
    /// <summary>Saves an offered file straight into the received files folder, as LAN Messenger does.</summary>
    public async Task AcceptFileAsync(FileTransfer transfer) { if (lan is not null) await RunAsync(() => lan.AcceptFileIntoAsync(transfer, ReceivedFilesFolder)); }
    public async Task DeclineFileAsync(FileTransfer transfer) { if (lan is not null) await RunAsync(() => lan.DeclineFileAsync(transfer)); }
    // ---------------- Group chat (LAN Messenger "chat rooms") ----------------
    // Wire format: groupmessage with thread + groupmsgop (request | join | message | leave), one copy per participant, never acknowledged.
    public async Task<GroupChat?> CreateGroupAsync(string name, IEnumerable<string> memberIds)
    {
        var ids = memberIds.Distinct().Where(id => Peers.Any(p => p.Id == id)).ToList();
        if (ids.Count == 0) { Toast = "Choose at least one person for the group."; return null; }
        var group = new GroupChat { Id = Settings.UserId + Guid.NewGuid().ToString("N").ToUpperInvariant(), Members = ids,
            Name = string.IsNullOrWhiteSpace(name) ? string.Join(", ", ids.Take(3).Select(NameOf)) + (ids.Count > 3 ? $" +{ids.Count - 3}" : "") : name.Trim() };
        Groups.Insert(0, group); SelectedGroup = group; Save();
        await InviteAsync(group, ids);
        return group;
    }
    public async Task AddGroupMembersAsync(GroupChat group, IEnumerable<string> memberIds)
    {
        var added = memberIds.Where(id => !group.Members.Contains(id) && Peers.Any(p => p.Id == id)).Distinct().ToList();
        if (added.Count == 0) return;
        group.Members.AddRange(added); group.MembersChanged(); Changed(nameof(ChatSubtitle)); Changed(nameof(SelectedGroupMembers)); Save();
        await InviteAsync(group, added);
    }
    private async Task InviteAsync(GroupChat group, List<string> ids)
    {
        if (Preview || lan is null) return;
        foreach (var id in ids.Where(id => Peers.Any(p => p.Id == id && p.Online)))
            await RunAsync(() => lan.SendGroupAsync(id, group.Id, "request"));
        if (ids.Any(id => Peers.All(p => p.Id != id || !p.Online))) Toast = "People who are offline can be added again once they are online.";
    }
    public void RenameGroup(GroupChat group, string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return;
        group.Name = name.Trim(); Changed(nameof(ChatTitle)); Save();
    }
    public async Task LeaveGroupAsync(GroupChat group)
    {
        if (!Preview && lan is not null)
            foreach (var id in group.Members.Where(id => Peers.Any(p => p.Id == id && p.Online)).ToList())
                await RunAsync(() => lan.SendGroupAsync(id, group.Id, "leave"), false);
        Groups.Remove(group); history.RemoveAll(m => m.GroupId == group.Id);
        SelectedGroup = Groups.FirstOrDefault(); if (SelectedGroup is null) SectionChanged();
        Save();
    }
    private async Task SendGroupMessageAsync(GroupChat group, string text)
    {
        var message = new ChatMessage { GroupId = group.Id, Text = text, Outgoing = true, Delivery = "Sending…", Id = NextMessageId().ToString() };
        history.Add(message); Draft = ""; group.Preview = "You: " + text.ReplaceLineEndings(" "); RefreshMessages(true);
        if (Preview || lan is null) { message.Delivery = "Preview only"; return; }
        int sent = 0, total = group.Members.Count;
        foreach (var id in group.Members.Where(id => Peers.Any(p => p.Id == id && p.Online)).ToList())
        {
            try { await lan.SendGroupAsync(id, group.Id, "message", text); sent++; }
            catch (Exception e) when (e is IOException or System.Net.Sockets.SocketException or OperationCanceledException) { }
        }
        message.Delivery = sent == 0 ? "Not sent · no one online" : sent == total ? "Sent" : $"Sent to {sent} of {total}";
        Save();
    }
    private void OnGroupMessage(string senderId, string thread, string op, string text)
    {
        if (Peers.FirstOrDefault(p => p.Id == senderId) is not { } sender) return;
        var group = Groups.FirstOrDefault(g => g.Id == thread);
        switch (op)
        {
            case "request":
                if (group is null)
                {
                    group = new GroupChat { Id = thread, Name = $"Group with {sender.Name}", Preview = $"{sender.Name} added you" };
                    Groups.Insert(0, group);
                    if (!IsGroupsSection || SelectedGroup != group || !Replying) group.Unread++;
                    Notify?.Invoke(sender.Name, "Added you to a group chat");
                    _ = AnnounceJoinAsync(group);
                }
                AddMember(group, senderId);
                break;
            case "join": if (group is not null) AddMember(group, senderId); break;
            case "leave": if (group is not null && group.Members.Remove(senderId)) group.MembersChanged(); break;
            case "message":
                if (group is null) return; // like LAN Messenger: messages for rooms we are not in are dropped
                AddMember(group, senderId);
                history.Add(new() { GroupId = thread, PeerId = senderId, SenderName = sender.Name, Text = text, Delivery = "Received" });
                group.Preview = $"{sender.Name}: {text.ReplaceLineEndings(" ")}";
                if (SelectedGroup != group || !IsGroupsSection || !Replying) group.Unread++;
                if (SelectedGroup == group && IsGroupsSection) RefreshMessages();
                Notify?.Invoke($"{sender.Name} · {group.Name}", text);
                break;
        }
        if (group is not null && group == SelectedGroup) { Changed(nameof(ChatSubtitle)); Changed(nameof(SelectedGroupMembers)); ComposeChanged(); }
        Save();
    }
    private static void AddMember(GroupChat group, string id) { if (!group.Members.Contains(id)) { group.Members.Add(id); group.MembersChanged(); } }
    // LAN Messenger announces a join to every contact; only people already in the room act on it.
    private async Task AnnounceJoinAsync(GroupChat group)
    {
        if (lan is null) return;
        foreach (var peer in Peers.Where(p => p.Online).ToList()) await RunAsync(() => lan.SendGroupAsync(peer.Id, group.Id, "join"), false);
    }

    // ---------------- Preferences ----------------
    public string ReceivedFilesFolder => string.IsNullOrWhiteSpace(Settings.ReceivedFilesFolder) ? AppSettings.DefaultReceivedFilesFolder : Settings.ReceivedFilesFolder;
    public static double FontSizeFor(string size) => size switch { "small" => 13, "large" => 17, _ => 15 };
    public void ApplySettings()
    {
        if (System.Windows.Application.Current is { } app)
        {
            try { app.Resources["MessageFontFamily"] = new FontFamily(Settings.MessageFont + ", Segoe UI"); } catch (ArgumentException) { }
            app.Resources["MessageFontSize"] = FontSizeFor(Settings.MessageFontSize);
        }
        if (lan is not null) lan.AutoAcceptDirectory = Settings.AutoAcceptFiles ? ReceivedFilesFolder : null;
    }
    /// <summary>Applies a batch of preference changes, saves them and tells contacts about profile changes.</summary>
    public async Task SavePreferencesAsync(Action<AppSettings> change)
    {
        bool hadHistory = Settings.KeepHistory;
        string previousAvatar = Settings.AvatarPath;
        change(Settings);
        bool avatarChanged = previousAvatar != Settings.AvatarPath;
        if (avatarChanged && previousAvatar.StartsWith(Path.Combine(ImagesDirectory, "profile"), StringComparison.OrdinalIgnoreCase)) TryDelete(previousAvatar);
        Settings.Theme = ThemeManager.Normalize(Settings.Theme);
        if (Settings.MessageFontSize is not ("small" or "medium" or "large")) Settings.MessageFontSize = "medium";
        store?.SaveSettings(Settings);
        if (!Preview) StartupRegistration.Apply(Settings.StartWithWindows);
        ApplySettings();
        Changed(nameof(ProfileName)); Changed(nameof(ProfileInitial)); Changed(nameof(ProfileStatus)); Changed(nameof(ProfileAvatar));
        if (hadHistory != Settings.KeepHistory) Save();
        if (lan is not null) await RunAsync(() => lan.UpdateProfileAsync());
        // LAN Messenger has no "picture removed" message, so only a new picture is sent.
        if (lan is not null && avatarChanged && Settings.AvatarPath.Length > 0) await RunAsync(() => lan.SendAvatarAsync());
    }
    public bool ShouldAlert => Settings.Notifications && !(Settings.Status == "busy" && !Settings.AlertsWhenBusy) && !(Settings.Status == "dnd" && !Settings.AlertsWhenDnd);
    public bool ShouldPlaySound => Settings.Sounds && !(Settings.Status == "busy" && !Settings.SoundsWhenBusy) && !(Settings.Status == "dnd" && !Settings.SoundsWhenDnd);
    public void ClearMessageHistory() => DeleteMessages(_ => true);
    /// <summary>Deletes the open chat's messages (one person or one group) from this computer.</summary>
    public void DeleteConversation()
    {
        if (InGroup) { var id = SelectedGroup!.Id; DeleteMessages(m => m.GroupId == id); }
        else if (HasPeer) { var id = SelectedPeer!.Id; DeleteMessages(m => m.GroupId.Length == 0 && m.PeerId == id); }
    }
    private void DeleteMessages(Predicate<ChatMessage> match)
    {
        var removed = history.FindAll(match);
        history.RemoveAll(match);
        // Pictures are CydLan's own copies; files saved to the received files folder are kept.
        foreach (var image in removed.Select(m => m.ImagePath).Where(p => p.Length > 0 && Path.GetFullPath(p).StartsWith(Path.GetFullPath(ImagesDirectory), StringComparison.OrdinalIgnoreCase)))
            if (!history.Any(m => string.Equals(m.ImagePath, image, StringComparison.OrdinalIgnoreCase))) TryDelete(image);
        foreach (var peer in Peers.Where(p => !history.Any(m => m.GroupId.Length == 0 && m.PeerId == p.Id))) { peer.Preview = ""; peer.Unread = 0; }
        foreach (var group in Groups.Where(g => !history.Any(m => m.GroupId == g.Id))) { group.Preview = ""; group.Unread = 0; }
        RefreshMessages(); Changed(nameof(PeopleSummary)); Save();
    }
    public void ClearTransferHistory()
    {
        foreach (var t in Transfers.Where(t => t.State is not ("Waiting for approval" or "Accepted" or "Connecting" or "Transferring")).ToList()) Transfers.Remove(t);
        Save();
    }

    public async Task SaveProfileAsync(string name, string status, bool notifications, bool minimized, string? theme = null)
    {
        Settings.DisplayName = name; Settings.Status = status; Settings.Notifications = notifications; Settings.StartMinimized = minimized;
        if (theme is not null) Settings.Theme = ThemeManager.Normalize(theme);
        store?.SaveSettings(Settings); Changed(nameof(ProfileName)); Changed(nameof(ProfileInitial)); Changed(nameof(ProfileStatus));
        if (lan is not null) await RunAsync(() => lan.UpdateProfileAsync());
    }
    private async Task RunAsync(Func<Task> action, bool report = true, [CallerMemberName] string operation = "")
    {
        try { await action(); }
        catch (Exception e) when (e is IOException or System.Net.Sockets.SocketException or OperationCanceledException or UnauthorizedAccessException or InvalidOperationException)
        { log?.Write("operation.failed", e, ("operation", operation)); if (report) Toast = e.Message; }
    }
    private void RefreshMessages(bool forceScroll = false)
    {
        Messages.Clear();
        if (InGroup)
            foreach (var message in history.Where(m => m.GroupId == SelectedGroup!.Id && m.Text.Contains(MessageSearch, StringComparison.OrdinalIgnoreCase)).TakeLast(500)) Messages.Add(message);
        else if (HasPeer)
            foreach (var message in history.Where(m => m.PeerId == SelectedPeer!.Id && m.GroupId.Length == 0 && (m.Text.Contains(MessageSearch, StringComparison.OrdinalIgnoreCase) || m.FileName.Contains(MessageSearch, StringComparison.OrdinalIgnoreCase))).TakeLast(500)) Messages.Add(message);
        ScrollRequested?.Invoke(forceScroll);
    }
    private void Save()
    {
        try
        {
            store?.SaveHistory(Settings.KeepHistory ? history : []); store?.SavePeers(Peers); store?.SaveDrafts(drafts); store?.SaveGroups(Groups);
            store?.SaveTransfers(Settings.KeepFileHistory ? Transfers.Where(t => t.Id != "preview") : []);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { log?.Write("storage.save_failed", e); Toast = "Local history could not be saved: " + e.Message; }
    }
    public async ValueTask DisposeAsync() { Save(); if (lan is not null) await lan.DisposeAsync(); }
    private void SeedPreview()
    {
        var names = new[] { ("1", "Jamie Chen", "chat", "Thanks! That looks great."), ("2", "Sofia Reyes", "chat", "Sent you the updated assets"), ("3", "Marcus Taylor", "busy", "Let's sync after lunch"), ("4", "Olivia Wilson", "chat", "The network is all set ✓"), ("5", "Daniel Kim", "away", "Back in a few minutes") };
        foreach (var (id, name, status, preview) in names)
        {
            var peer = new Peer { Id = id, Name = name, Status = status, Online = true, Address = "192.168.1." + (110 + int.Parse(id)), Version = id == "1" ? "LAN Messenger 1.2.35" : "CydLan 0.1", Preview = preview, Unread = id == "2" ? 2 : 0 };
            peer.PropertyChanged += PeerChanged; Peers.Add(peer);
        }
        void Add(string text, bool outgoing, int minutes) => history.Add(new() { PeerId = "1", Text = text, Outgoing = outgoing, Delivery = outgoing ? "Delivered" : "Received", Time = DateTimeOffset.Now.AddMinutes(-minutes) });
        Add("Hey Alex! I'm using the original LAN Messenger. Can we still chat and share files?", false, 9);
        Add("Yep! We can chat and share files directly, even with the older app.", true, 8);
        Add("Perfect. A fresh look, same local connection.", false, 6);
        Add("Exactly. Sending over the concept now.", true, 5);
        try { history.Add(new() { PeerId = "1", Outgoing = true, Delivery = "Delivered", ImagePath = SampleImage(), Time = DateTimeOffset.Now.AddMinutes(-4) }); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        Add("Thanks! That looks great.", false, 2);
        var team = new GroupChat { Id = "preview-group", Name = "Design team", Members = ["2", "4"], Preview = "Olivia: Works on my side too ✓" };
        Groups.Add(team);
        history.Add(new() { GroupId = team.Id, PeerId = "2", SenderName = "Sofia Reyes", Text = "Uploaded the new icons to the shared folder.", Delivery = "Received", Time = DateTimeOffset.Now.AddMinutes(-30) });
        history.Add(new() { GroupId = team.Id, Text = "Great, I'll review them after lunch.", Outgoing = true, Delivery = "Sent", Time = DateTimeOffset.Now.AddMinutes(-28) });
        history.Add(new() { GroupId = team.Id, PeerId = "4", SenderName = "Olivia Wilson", Text = "Works on my side too ✓", Delivery = "Received", Time = DateTimeOffset.Now.AddMinutes(-20) });
        SelectedPeer = Peers[0]; UpdateCounts();
        string SampleImage()
        {
            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                dc.DrawRectangle(new LinearGradientBrush(Color.FromRgb(0x3B, 0x7A, 0x8C), Color.FromRgb(0x9C, 0xC6, 0xD2), 35), null, new Rect(0, 0, 480, 300));
                dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(0xE6, 0xFF, 0xFF, 0xFF)), null, new Rect(40, 60, 250, 180), 14, 14);
                dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(0x55, 0xFF, 0xFF, 0xFF)), null, new Rect(310, 60, 130, 80), 12, 12);
                dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(0x55, 0xFF, 0xFF, 0xFF)), null, new Rect(310, 160, 130, 80), 12, 12);
            }
            var bitmap = new RenderTargetBitmap(480, 300, 96, 96, PixelFormats.Pbgra32); bitmap.Render(visual);
            return Images.SavePng(bitmap, Path.Combine(ImagesDirectory, "sample"));
        }
        Transfers.Add(new() { Id = "preview", PeerId = "1", PeerName = "Jamie Chen", Name = "Workspace concepts.fig", Size = 4823000, Outgoing = true, State = "Preview example", Progress = 100 });
    }
}
