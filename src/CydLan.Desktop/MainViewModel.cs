using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Net;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Color = System.Windows.Media.Color;
using FontFamily = System.Windows.Media.FontFamily;
using CydLan.Core;

namespace CydLan.Desktop;

public sealed partial class MainViewModel : Observable, IAsyncDisposable
{
    private readonly LocalStore? store;
    private readonly LanService? lan;
    private readonly DiagnosticLog? log;
    private readonly DispatcherTimer rosterSaveTimer = new(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(400) };
    private readonly HashSet<string> obsoleteAvatars = new(StringComparer.OrdinalIgnoreCase);
    private DispatcherOperation? queuedRosterRefresh;
    private Task? networkStartup;
    private bool disposed;
    private readonly Dictionary<string, string> loggedTransferStates = [];
    public string LogsDirectory => log?.DirectoryPath ?? "";
    public bool HasLogs => log is not null;
    private readonly List<ChatMessage> history = [];
    private readonly Dictionary<(bool Group, string Id), List<ChatMessage>> historyByChat = [];
    private const int HistoryPageSize = 40;
    private int? messagePageEnd;
    private int messageResultCount;
    private int messagePageStart;
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
    public ObservableCollection<FileTransfer> ConversationFiles { get; } = [];
    public ICollectionView ActiveConversationTransfers { get; }
    private readonly ObservableCollection<FileTransfer> activeConversationTransfers = [];
    private ILookup<(string Peer, bool Sent), FileTransfer> filesByChat = Array.Empty<FileTransfer>().ToLookup(t => (t.PeerId, t.Outgoing));
    private bool conversationFilesOpen;
    private int filePage;
    private bool conversationFilesSent;
    public bool ConversationFilesSent
    {
        get => conversationFilesSent;
        set { if (Set(ref conversationFilesSent, value)) { filePage = 0; RefreshConversationFiles(); } }
    }
    public int ConversationReceivedCount => SelectedPeer is { } p ? filesByChat[(p.Id, false)].Count() : 0;
    public int ConversationSentCount => SelectedPeer is { } p ? filesByChat[(p.Id, true)].Count() : 0;
    private int ConversationFileCount => ConversationFilesSent ? ConversationSentCount : ConversationReceivedCount;
    public bool NoConversationFiles => ConversationFileCount == 0;
    public bool HasOlderFiles => (filePage + 1) * HistoryPageSize < ConversationFileCount;
    public bool HasNewerFiles => filePage > 0;
    internal void SetConversationFilesOpen(bool value)
    {
        if (conversationFilesOpen == value) return;
        conversationFilesOpen = value;
        filePage = 0;
        RefreshConversationFiles();
    }
    internal void PageConversationFiles(bool older)
    {
        if (older ? !HasOlderFiles : !HasNewerFiles) return;
        filePage += older ? 1 : -1;
        RefreshConversationFiles();
    }
    private void RefreshConversationFiles()
    {
        filePage = Math.Min(filePage, Math.Max(0, (ConversationFileCount - 1) / HistoryPageSize));
        IEnumerable<FileTransfer> page = conversationFilesOpen && IsConversationSection && SelectedPeer is { } peer
            ? filesByChat[(peer.Id, ConversationFilesSent)].Skip(filePage * HistoryPageSize).Take(HistoryPageSize)
            : [];
        UpdateRows(ConversationFiles, page);
        Changed(nameof(ConversationReceivedCount)); Changed(nameof(ConversationSentCount)); Changed(nameof(NoConversationFiles));
        Changed(nameof(HasOlderFiles)); Changed(nameof(HasNewerFiles));
    }
    private void RefreshActiveTransfers()
    {
        IEnumerable<FileTransfer> active = IsConversationSection && SelectedPeer is { } peer
            ? filesByChat[(peer.Id, false)].Concat(filesByChat[(peer.Id, true)]).Where(t => t.IsActive).OrderByDescending(t => t.Time)
            : [];
        UpdateRows(activeConversationTransfers, active);
    }
    public ObservableCollection<string> Diagnostics { get; } = [];
    public ICollectionView People { get; }
    public ObservableCollection<Peer> QuickChatPeers { get; } = [];
    public bool HasQuickChatPeers => QuickChatPeers.Count > 0;
    public ObservableCollection<object> RecentChats { get; } = [];
    private readonly HashSet<string> activeSessionChatPeers = [];
    private bool showRecentChats;
    public bool ShowRecentChats
    {
        get => showRecentChats;
        set
        {
            if (!Set(ref showRecentChats, value)) return;
            Changed(nameof(ShowPeopleDirectory)); Changed(nameof(ShowRecentList));
        }
    }
    public bool ShowPeopleDirectory => ShowPeopleList && !ShowRecentChats;
    public bool ShowRecentList => ShowPeopleList && ShowRecentChats;
    public object? SelectedRecentChat
    {
        get => IsGroupsSection ? SelectedGroup : SelectedPeer;
        set
        {
            switch (value)
            {
                case Peer peer when peer == SelectedPeer: Broadcasting = false; break;
                case Peer peer: SelectedPeer = peer; break;
                case GroupChat group when group == SelectedGroup: GroupsOpen = true; break;
                case GroupChat group: SelectedGroup = group; break;
            }
        }
    }
    private Peer? selected;
    private GroupChat? selectedGroup;
    private bool groupsOpen;
    private string transferFilter = "all";
    private string search = "", messageSearch = "", networkStatus = "Starting local network…", toast = "";
    private bool transfersOpen, mediaOpen, broadcasting, connected;
    private bool replying;
    /// <summary>The user has clicked or typed in the composer of the open conversation; until then its messages stay unread.</summary>
    public bool Replying { get => replying; set { replying = value; MarkConversationRead(); } }
    public event Action<(string Sender, string Text)?>? Notify;
    public event Action<string, string, string?>? PetActivity;
    public event Action? FloatingStateChanged;
    public event Action<bool>? ScrollRequested;
    public Peer? SelectedPeer
    {
        get => selected;
        set
        {
            if (!Set(ref selected, value)) return;
            // Picking a person always returns to one-to-one chat.
            if (value is not null) { broadcasting = false; groupsOpen = false; transfersOpen = false; mediaOpen = false; }
            ClearMessageSearch(); SectionChanged();
        }
    }
    public GroupChat? SelectedGroup
    {
        get => selectedGroup;
        set
        {
            if (!Set(ref selectedGroup, value)) return;
            if (value is not null) { groupsOpen = true; broadcasting = false; transfersOpen = false; mediaOpen = false; }
            ClearMessageSearch(); SectionChanged();
        }
    }
    public string Search { get => search; set { if (Set(ref search, value)) { People.Refresh(); RefreshRecentChats(); RefreshMediaFilterIfLoaded(); } } }
    public string MessageSearch { get => messageSearch; set { if (Set(ref messageSearch, value)) RefreshMessages(true); } }
    private void ClearMessageSearch() => Set(ref messageSearch, "", nameof(MessageSearch));
    private string DraftKey => GroupsOpen ? (SelectedGroup is { } g ? "group:" + g.Id : "groups") : Broadcasting ? "broadcast" : SelectedPeer?.Id ?? "none";
    public string Draft { get => drafts.GetValueOrDefault(DraftKey, ""); set { drafts[DraftKey] = value; Changed(); Changed(nameof(CanSend)); } }
    public string NetworkStatus { get => networkStatus; set { Set(ref networkStatus, value); Changed(nameof(ConnectionSummary)); } }
    public string Toast { get => toast; set => Set(ref toast, value); }
    // Sections (left rail): Chats, Groups, Files, Media, Broadcast. Exactly one is active.
    public bool TransfersOpen { get => transfersOpen; set { if (transfersOpen == value) return; transfersOpen = value; if (value) mediaOpen = false; SectionChanged(); } }
    public bool MediaOpen { get => mediaOpen; set { if (mediaOpen == value) return; mediaOpen = value; if (value) { transfersOpen = false; broadcasting = false; groupsOpen = false; _ = EnsureMediaLoadedAsync(); } SectionChanged(); } }
    public bool Broadcasting { get => broadcasting; set { if (broadcasting == value) return; broadcasting = value; if (value) { groupsOpen = false; transfersOpen = false; mediaOpen = false; } SectionChanged(); } }
    public bool GroupsOpen { get => groupsOpen; set { if (groupsOpen == value) return; groupsOpen = value; if (value) { broadcasting = false; transfersOpen = false; mediaOpen = false; } SectionChanged(); } }
    public bool ChatOpen => !TransfersOpen && !MediaOpen;
    public bool IsConversationSection => ChatOpen && !Broadcasting && !GroupsOpen;
    public bool IsBroadcastSection => ChatOpen && Broadcasting;
    public bool IsGroupsSection => ChatOpen && GroupsOpen;
    public bool IsFilesSection => TransfersOpen && !MediaOpen;
    public bool IsMediaSection => MediaOpen;
    public bool ShowPeopleList => ChatOpen && !GroupsOpen;
    public bool ShowGroupList => ChatOpen && GroupsOpen;
    public bool InGroup => IsGroupsSection && SelectedGroup is not null;
    public bool HasPeer => SelectedPeer is not null && IsConversationSection;
    public bool NoPeer => SelectedPeer is null && IsConversationSection;
    public bool NoGroup => GroupsOpen && SelectedGroup is null && ChatOpen;
    public bool HasDetails => HasPeer || InGroup;
    public IEnumerable<Peer> SelectedGroupMembers => SelectedGroup?.Members.Select(id => Peers.FirstOrDefault(p => p.Id == id) ?? new Peer { Id = id, Name = "Someone" }).ToList() ?? [];
    public string SectionTitle => MediaOpen ? "Media" : TransfersOpen ? "Files" : Broadcasting ? "Broadcast" : GroupsOpen ? "Groups" : "Chats";
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
    public string ChatTitle => MediaOpen ? (CurrentMedia?.Title ?? "Media") : TransfersOpen ? "Files" : Broadcasting ? "Broadcast" : GroupsOpen ? SelectedGroup?.Name ?? "Groups" : SelectedPeer?.Name ?? "Conversations";
    public string ChatSubtitle => MediaOpen ? (CurrentMedia?.MetaLabel ?? "") : TransfersOpen ? "" : Broadcasting ? $"{OnlineCount} online" : GroupsOpen ? (SelectedGroup is { } g ? GroupPeopleLabel(g) : "") : SelectedPeer is { } p ? p.Presence : "";
    private string GroupPeopleLabel(GroupChat g) => g.Left ? "You left this group" : string.Join(", ", g.Members.Select(NameOf).Append("you"));
    private string NameOf(string peerId) => Peers.FirstOrDefault(p => p.Id == peerId)?.Name ?? "Someone";
    private void SectionChanged()
    {
        replying = false;
        foreach (var name in new[] { nameof(TransfersOpen), nameof(MediaOpen), nameof(Broadcasting), nameof(GroupsOpen), nameof(ChatOpen), nameof(IsConversationSection), nameof(IsBroadcastSection),
            nameof(IsGroupsSection), nameof(IsFilesSection), nameof(IsMediaSection), nameof(ShowPeopleList), nameof(ShowPeopleDirectory), nameof(ShowRecentList), nameof(ShowGroupList), nameof(InGroup), nameof(HasPeer), nameof(NoPeer), nameof(NoGroup),
            nameof(HasDetails), nameof(SelectedGroupMembers), nameof(SectionTitle), nameof(ChatTitle), nameof(ChatSubtitle), nameof(Draft), nameof(ShowMiniPlayer) })
            Changed(name);
        Changed(nameof(SelectedGroup)); Changed(nameof(SelectedPeer)); Changed(nameof(SelectedRecentChat));
        filePage = 0;
        AttachmentsSwitched(); ComposeChanged(); RefreshMessages(true); RefreshConversationFiles(); RefreshActiveTransfers(); RefreshQuickChatPeers();
    }
    private void AttachmentsSwitched() { Changed(nameof(Attachments)); Changed(nameof(HasAttachments)); Changed(nameof(CanSend)); }
    public int OnlineCount => Peers.Count(p => p.Online);
    public int TotalUnread => Peers.Sum(p => p.Unread) + Groups.Sum(g => g.Unread);
    public bool HasUnread => TotalUnread > 0;
    public bool FloatingLanEnabled => Settings.FloatingLanEnabled;
    public bool FloatingLanRoam => Settings.FloatingLanRoam;
    private PetAppearance? previewPetAppearance;
    public PetAppearance PetAppearance => previewPetAppearance ?? PetAppearance.From(Settings);
    public event Action? PetHopRequested;
    internal void RequestPetHop() => PetHopRequested?.Invoke();
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
        rosterSaveTimer.Tick += (_, _) => SaveRoster();
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
            LastSettingsPage = SettingsWindow.NormalizePage(Settings.LastSettingsPage);
            LastAppearanceTab = SettingsWindow.NormalizeAppearanceTab(Settings.LastAppearanceTab);
            // Earlier builds used 32-character IDs, which LAN Messenger cannot match for incoming files.
            if (!AppSettings.IsValidUserId(Settings.UserId)) Settings.UserId = AppSettings.NewUserId();
            store.SaveSettings(Settings);
            foreach (var message in store.LoadHistory()) AddMessage(message);
            foreach (var item in store.LoadDrafts()) drafts[item.Key] = item.Value;
            foreach (var message in history.Where(m => m.Outgoing && m.Delivery == "Sending…" && !m.HasImage && !m.HasFile)) message.Delivery = "Unconfirmed";
            // An image or file still pending when CydLan closed can no longer complete.
            foreach (var message in history.Where(m => m.Outgoing && (m.HasImage || m.HasFile) && (m.Delivery is "Sending…" or "Waiting for them to accept" or "Accepted" || m.Delivery.StartsWith("Accepted ·", StringComparison.Ordinal)))) message.Delivery = "Interrupted";
            foreach (var message in history.Where(m => !m.Outgoing && m.HasFile && (m.Delivery is "Waiting" or "Accepted" || m.Delivery.StartsWith("Receiving", StringComparison.Ordinal)))) message.Delivery = "Interrupted";
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
                if (disposed) return;
                var existing = Peers.FirstOrDefault(p => p.Id == peer.Id);
                if (existing is null) { existing = peer; Peers.Add(peer); rosterSaveTimer.Start(); }
                else
                {
                    var previous = (existing.Name, existing.Address, existing.Port, existing.Version, existing.Status, existing.FirstName, existing.LastName, existing.About, existing.PetCode);
                    existing.Name = peer.Name; existing.Address = peer.Address; existing.Port = peer.Port; existing.Version = peer.Version; existing.Status = peer.Status; existing.Online = true;
                    // CydLan peers include these in user data; LAN Messenger only sends them in reply to a query.
                    if (LanService.IsCydLanVersion(peer.Version)) { existing.FirstName = peer.FirstName; existing.LastName = peer.LastName; existing.About = peer.About; existing.PetCode = peer.PetCode; }
                    if (previous != (existing.Name, existing.Address, existing.Port, existing.Version, existing.Status, existing.FirstName, existing.LastName, existing.About, existing.PetCode))
                        rosterSaveTimer.Start();
                }
                existing.PropertyChanged -= PeerChanged; existing.PropertyChanged += PeerChanged;
                QueueRosterRefresh();
            });
            lan.ProfileReceived += (id, first, last, about) => UI(() => {
                if (disposed) return;
                if (Peers.FirstOrDefault(p => p.Id == id) is not { } peer) return;
                if ((peer.FirstName, peer.LastName, peer.About) == (first, last, about)) return;
                peer.FirstName = first; peer.LastName = last; peer.About = about; rosterSaveTimer.Start();
            });
            lan.PetCodeReceived += (id, petCode) => UI(() => {
                if (disposed) return;
                if (Peers.FirstOrDefault(p => p.Id == id) is not { } peer || peer.PetCode == petCode) return;
                peer.PetCode = petCode; rosterSaveTimer.Start();
            });
            lan.AvatarReceived += (id, path) => UI(() => {
                if (disposed) { TryDelete(path); return; }
                if (Peers.FirstOrDefault(p => p.Id == id) is not { } peer) { TryDelete(path); return; }
                var previous = peer.AvatarPath; peer.AvatarPath = path; rosterSaveTimer.Start();
                if (previous.Length > 0 && previous != path && previous.StartsWith(Path.Combine(ImagesDirectory, "avatars"), StringComparison.OrdinalIgnoreCase))
                    obsoleteAvatars.Add(previous);
            });
            lan.PeerOffline += id => UI(() => { if (Peers.FirstOrDefault(p => p.Id == id) is { } p) { p.Online = false; p.Typing = ""; } QueueRosterRefresh(); });
            lan.MessageReceived += message => UI(() => {
                var peer = Peers.FirstOrDefault(p => p.Id == message.PeerId);
                if (peer is null) return;
                activeSessionChatPeers.Add(peer.Id);
                AddMessage(message); peer.Preview = message.Text.ReplaceLineEndings(" ");
                RefreshRecentChats();
                if (SelectedPeer?.Id != peer.Id || !IsConversationSection || !Replying) peer.Unread++;
                peer.Typing = "";
                RefreshQuickChatPeers();
                RefreshMessages(); Changed(nameof(PeopleSummary)); Changed(nameof(TotalUnread)); Changed(nameof(HasUnread)); Save(); NotifyIncoming(peer.Name, message.Text, peer.Id);
            });
            lan.TypingChanged += (id, typing) => UI(() => { if (Peers.FirstOrDefault(p => p.Id == id) is { } p) { p.Typing = typing; RefreshQuickChatPeers(); } });
            lan.TransferChanged += transfer => UI(() => OnTransferChanged(transfer));
            lan.GroupMessageReceived += (sender, thread, op, text) => UI(() => OnGroupMessage(sender, thread, op, text));
            lan.Diagnostic += message => UI(() => { Diagnostics.Insert(0, $"{DateTime.Now:HH:mm:ss}  {message}"); while (Diagnostics.Count > 100) Diagnostics.RemoveAt(100); });
        }
        TransfersView = new ListCollectionView(Transfers) { Filter = item => item is FileTransfer t && (TransferFilter == "all" || (TransferFilter == "received") != t.Outgoing) };
        ActiveConversationTransfers = new ListCollectionView(activeConversationTransfers);
        void IndexTransfers() => filesByChat = Transfers.ToLookup(t => (t.PeerId, t.Outgoing));
        IndexTransfers();
        Transfers.CollectionChanged += (_, _) => { IndexTransfers(); Changed(nameof(ReceivedCount)); Changed(nameof(SentCount)); Changed(nameof(NoTransfersShown)); RefreshConversationFiles(); RefreshActiveTransfers(); };
        People = CollectionViewSource.GetDefaultView(Peers);
        People.Filter = obj => obj is Peer peer && (peer.Name.Contains(Search, StringComparison.OrdinalIgnoreCase) || peer.Address.Contains(Search, StringComparison.OrdinalIgnoreCase) || history.Any(m => m.PeerId == peer.Id && m.Text.Contains(Search, StringComparison.OrdinalIgnoreCase)));
        People.SortDescriptions.Add(new(nameof(Peer.Online), ListSortDirection.Descending));
        People.SortDescriptions.Add(new(nameof(Peer.Name), ListSortDirection.Ascending));
        foreach (var peer in Peers) peer.PropertyChanged += PeerChanged;
        if (preview && seedPreview) SeedPreview();
        RefreshRecentChats();
        RefreshQuickChatPeers();
        InitMedia();
        ApplySettings();
    }
    private void RefreshQuickChatPeers()
    {
        var cutoff = DateTimeOffset.Now - TimeSpan.FromHours(12);
        var active = new List<(Peer Peer, DateTimeOffset LastActivity)>();
        foreach (var peer in Peers)
        {
            var direct = ChatHistory(false, peer.Id);
            DateTimeOffset lastTime = direct.Count > 0 ? direct[^1].Time : DateTimeOffset.MinValue;
            bool chatting = peer.Unread > 0
                || peer.Typing.Length > 0
                || (HasPeer && peer == SelectedPeer)
                || (direct.Count > 0 && (activeSessionChatPeers.Contains(peer.Id) || lastTime >= cutoff));
            if (chatting) active.Add((peer, lastTime));
        }

        var ordered = active
            .OrderByDescending(x => x.Peer.Unread > 0)
            .ThenByDescending(x => x.Peer.Typing.Length > 0)
            .ThenByDescending(x => x.LastActivity)
            .ThenByDescending(x => HasPeer && x.Peer == SelectedPeer)
            .ThenBy(x => x.Peer.Name, StringComparer.OrdinalIgnoreCase)
            .Select(x => x.Peer)
            .ToList();

        var included = ordered.ToHashSet();
        for (int i = QuickChatPeers.Count - 1; i >= 0; i--)
            if (!included.Contains(QuickChatPeers[i])) QuickChatPeers.RemoveAt(i);
        for (int i = 0; i < ordered.Count; i++)
        {
            int previous = QuickChatPeers.IndexOf(ordered[i]);
            if (previous < 0) QuickChatPeers.Insert(i, ordered[i]);
            else if (previous != i) QuickChatPeers.Move(previous, i);
        }
        Changed(nameof(HasQuickChatPeers));
        Changed(nameof(SelectedPeer));
    }
    private void RefreshRecentChats()
    {
        bool ContainsSearch(string text) => text.Contains(Search, StringComparison.OrdinalIgnoreCase);
        var ordered = Peers.Select(p => (Chat: (object)p, Messages: ChatHistory(false, p.Id), Matches: ContainsSearch(p.Name) || ContainsSearch(p.Address)))
            .Concat(Groups.Select(g => (Chat: (object)g, Messages: ChatHistory(true, g.Id), Matches: ContainsSearch(g.Name))))
            .Where(c => c.Messages.Any() && (c.Matches || c.Messages.Any(m => ContainsSearch(m.Text) || ContainsSearch(m.FileName))))
            .OrderByDescending(c => c.Messages.Max(m => m.Time))
            .Select(c => c.Chat).ToList();

        // Keep selected rows when new activity changes their position.
        var included = ordered.ToHashSet();
        for (int i = RecentChats.Count - 1; i >= 0; i--)
            if (!included.Contains(RecentChats[i])) RecentChats.RemoveAt(i);
        for (int i = 0; i < ordered.Count; i++)
        {
            int previous = RecentChats.IndexOf(ordered[i]);
            if (previous < 0) RecentChats.Insert(i, ordered[i]);
            else if (previous != i) RecentChats.Move(previous, i);
        }
        Changed(nameof(SelectedRecentChat));
    }
    private void ComposeChanged() { Changed(nameof(CanCompose)); Changed(nameof(CanSend)); Changed(nameof(CanAttach)); }
    private void MarkConversationRead()
    {
        if (!Replying) return;
        if (IsConversationSection && SelectedPeer is { } peer) peer.Unread = 0;
        if (IsGroupsSection && SelectedGroup is { } group) group.Unread = 0;
        Changed(nameof(TotalUnread)); Changed(nameof(HasUnread));
    }
    private void PeerChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender == SelectedPeer) { Changed(nameof(ChatSubtitle)); Changed(nameof(ChatTitle)); ComposeChanged(); }
        if (e.PropertyName is nameof(Peer.Unread) or nameof(Peer.Online) or nameof(Peer.Typing)) RefreshQuickChatPeers();
        if (e.PropertyName == nameof(Peer.Unread)) { Changed(nameof(PeopleSummary)); Changed(nameof(TotalUnread)); Changed(nameof(HasUnread)); }
    }
    private void UpdateCounts() { Changed(nameof(OnlineCount)); Changed(nameof(TotalUnread)); Changed(nameof(HasUnread)); Changed(nameof(LocalAddress)); Changed(nameof(DeviceLabel)); Changed(nameof(NetworkEndpoint)); Changed(nameof(PeopleSummary)); ComposeChanged(); Changed(nameof(ChatSubtitle)); People.Refresh(); if (Search.Length > 0) RefreshRecentChats(); }
    private void QueueRosterRefresh()
    {
        if (disposed || queuedRosterRefresh is { Status: DispatcherOperationStatus.Pending }) return;
        queuedRosterRefresh = System.Windows.Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(UpdateCounts));
    }
    private static void UI(Action action) => System.Windows.Application.Current.Dispatcher.BeginInvoke(action);
    public Task StartAsync(bool discovery = true, IPAddress? bindAddress = null) =>
        networkStartup ??= StartNetworkAsync(discovery, bindAddress);

    private async Task StartNetworkAsync(bool discovery, IPAddress? bindAddress)
    {
        if (Preview) { NetworkStatus = "Design mode · LAN disconnected"; return; }
        try { await Task.Run(() => lan!.StartAsync(discovery, bindAddress)); connected = true; NetworkStatus = "Local network connected"; UpdateCounts(); }
        catch (Exception e) when (e is System.Net.Sockets.SocketException or ArgumentException or IOException)
        { log?.Write("network.start_failed", e); NetworkStatus = "Network unavailable"; Toast = $"Could not start networking: {e.Message}"; Diagnostics.Add(Toast); UpdateCounts(); }
    }
    public async Task RefreshAsync()
    {
        if (Preview) { Toast = "Offline preview"; return; }
        if (networkStartup is not null) await networkStartup;
        if (disposed) return;
        Toast = "Looking for people on your local network…";
        await RunAsync(() => lan!.RefreshAsync());
        UpdateCounts();
    }
    public async Task<bool> SendAsync()
    {
        var text = Settings.TrimMessages ? Draft.Trim() : Draft;
        if (string.IsNullOrWhiteSpace(text)) text = "";
        var files = HasPeer ? Attachments.ToList() : [];
        if (GroupsOpen) return text.Length > 0 && CanSend && await SendGroupMessageAsync(SelectedGroup!, text);
        if ((text.Length == 0 && files.Count == 0) || !CanSend) return false;
        if (Preview)
        {
            if (Broadcasting) { Toast = "Announcement preview only · nothing was sent."; Draft = ""; return true; }
            if (SelectedPeer is not { } sample) return false;
            activeSessionChatPeers.Add(sample.Id);
            foreach (var file in files)
                AddMessage(new() { PeerId = sample.Id, Outgoing = true, Delivery = "Preview only", ImagePath = file.IsImage ? KeepCopy(file.Path) : "", FileName = file.IsImage ? "" : file.Name, FolderAttachment = file.IsFolder });
            if (text.Length > 0) AddMessage(new() { PeerId = sample.Id, Text = text, Outgoing = true, Delivery = "Preview only" });
            sample.Preview = text.Length > 0 ? text.ReplaceLineEndings(" ") : files.Any(f => f.IsImage) ? "Photo" : files[0].Name;
            Attachments.Clear(); Draft = ""; RefreshRecentChats(); RefreshQuickChatPeers(); RefreshMessages(true);
            return text.Length > 0;
        }
        if (Broadcasting)
        {
            Draft = "";
            int count = 0;
            await RunAsync(async () => { count = await lan!.BroadcastAsync(text); Toast = $"Announcement sent to {count} connected people."; });
            return count > 0;
        }
        var peer = SelectedPeer!;
        if (text.Length > 0) Draft = "";
        if (files.Count > 0) await SendAttachmentsAsync(peer, files);
        if (text.Length == 0) return false;
        var message = new ChatMessage { PeerId = peer.Id, Text = text, Outgoing = true, Delivery = "Sending…" };
        // Legacy peers parse message IDs as signed 64-bit decimal integers.
        message.Id = NextMessageId().ToString();
        activeSessionChatPeers.Add(peer.Id);
        AddMessage(message); peer.Preview = text.ReplaceLineEndings(" "); RefreshRecentChats(); RefreshQuickChatPeers();
        if (IsConversationSection && SelectedPeer == peer) RefreshMessages(true);
        Save();
        await DeliverAsync(message);
        return message.Delivery == "Delivered";
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
        if (networkStartup is not null) await networkStartup;
        if (disposed) return;
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
        activeSessionChatPeers.Add(peer.Id);
        var queued = new List<(ChatMessage Message, string Path)>();
        foreach (var file in files)
        {
            var message = new ChatMessage { PeerId = peer.Id, Outgoing = true, Delivery = "Sending…", Id = NextMessageId().ToString(), FolderAttachment = file.IsFolder };
            string path = file.Path;
            try { if (file.IsImage && file.Size <= LanService.MaxInlineImageBytes) { path = KeepCopy(file.Path); message.ImagePath = path; } else message.FileName = file.Name; }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { log?.Write("attachment.prepare_failed", e); Toast = $"{file.Name} couldn't be prepared: {e.Message}"; continue; }
            AddMessage(message); queued.Add((message, path));
        }
        if (queued.Count > 0)
        {
            peer.Preview = queued[^1].Message.HasImage ? "Photo" : queued[^1].Message.FileName;
            RefreshRecentChats();
            RefreshQuickChatPeers();
        }
        RefreshMessages(true); Save();
        foreach (var (message, path) in queued)
        {
            try
            {
                // CydLan peers get the image inline; legacy LAN Messenger receives an ordinary file offer.
                var transfer = message.FolderAttachment
                    ? await lan!.OfferFolderAsync(peer.Id, path)
                    : await lan!.OfferFileAsync(peer.Id, path, inlineImage: message.HasImage && LanService.IsCydLanVersion(peer.Version));
                if (message.FolderAttachment) message.FileName = transfer.Name;
                message.TransferId = transfer.Id; transferMessages[transfer.Id] = message;
                message.Delivery = DeliveryFor(transfer);
                message.CanCancel = transfer.CanCancel;
            }
            catch (Exception e) when (e is IOException or System.Net.Sockets.SocketException or OperationCanceledException or InvalidOperationException)
            { log?.Write("attachment.offer_failed", e, ("peer", peer.Id)); message.Delivery = "Failed"; Toast = e.Message; }
        }
        Save();
    }
    // What the sender sees under an image or file, following the transfer the way LAN Messenger reports it.
    private static string DeliveryFor(FileTransfer t) => !t.Outgoing ? t.State switch
    {
        // What the receiver sees under a file someone offered.
        "Waiting for approval" => "Waiting",
        "Accepted" or "Connecting" => "Accepted",
        "Transferring" => $"Receiving {t.DisplayPercent}% · {FileTransfer.FormatBytes(t.TransferredBytes)} / {t.SizeLabel}",
        var state when state.StartsWith("Failed", StringComparison.Ordinal) => state,
        _ => t.State,
    } : t.State switch
    {
        "Waiting for approval" => "Waiting for them to accept",
        "Connecting" => "Accepted",
        "Transferring" => $"Accepted · sending {t.DisplayPercent}% · {FileTransfer.FormatBytes(t.TransferredBytes)} / {t.SizeLabel}",
        "Sent · receipt unavailable" => "Sent",
        "Declined" or "Cancelled" or "Aborted" or "Interrupted" => t.State,
        var state when state.StartsWith("Failed", StringComparison.Ordinal) => state,
        _ => t.State,
    };
    private static bool IsFinished(string state) => state is "Received" or "Sent · receipt unavailable" or "Declined" or "Cancelled" or "Aborted" or "Interrupted" || state.StartsWith("Failed", StringComparison.Ordinal);
    /// <summary>Withdraws a pending offer or cancels an active transfer.</summary>
    public async Task CancelOfferAsync(FileTransfer transfer)
    {
        if (lan is null) return;
        try { await lan.CancelFileAsync(transfer); }
        catch (InvalidOperationException)
        {
            if (transfer.CanCancel)
            {
                transfer.State = "Cancelled";
                OnTransferChanged(transfer);
            }
        }
        catch (Exception e) when (e is IOException or System.Net.Sockets.SocketException or OperationCanceledException or UnauthorizedAccessException)
        {
            log?.Write("operation.failed", e, ("operation", nameof(CancelOfferAsync)));
            Toast = e.Message;
        }
    }
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
            if (!transfer.Outgoing)
            {
                if (!transfer.Inline || !Settings.AutoAcceptFiles)
                    NotifyIncoming(transfer.PeerName, "Wants to send you " + transfer.Name, transfer.PeerId);
                // Offered files and images belong in the conversation timeline so the recipient can accept or decline.
                if (Peers.FirstOrDefault(p => p.Id == transfer.PeerId) is { } sender && !transferMessages.ContainsKey(transfer.Id))
                {
                    var offer = new ChatMessage { Id = transfer.Id, PeerId = sender.Id, FileName = transfer.Name, FolderAttachment = transfer.Folder, TransferId = transfer.Id, Delivery = DeliveryFor(transfer) };
                    activeSessionChatPeers.Add(sender.Id);
                    AddMessage(offer); transferMessages[transfer.Id] = offer;
                    sender.Preview = transfer.Name; sender.Typing = "";
                    RefreshRecentChats();
                    if (SelectedPeer?.Id != sender.Id || !IsConversationSection || !Replying) sender.Unread++;
                    RefreshQuickChatPeers();
                    RefreshMessages(); Changed(nameof(PeopleSummary)); Changed(nameof(TotalUnread)); Changed(nameof(HasUnread)); Save();
                }
            }
        }
        else if (!transfer.Outgoing && stateChanged && transfer.State is "Received" or "Cancelled")
            Changed(nameof(TotalUnread));
        if (stateChanged) RefreshActiveTransfers();
        if (transferMessages.TryGetValue(transfer.Id, out var sent))
        {
            var delivery = DeliveryFor(transfer);
            if (sent.Delivery != delivery) sent.Delivery = delivery;
            if (sent.CanCancel != transfer.CanCancel) sent.CanCancel = transfer.CanCancel;
        }
        if (IsFinished(transfer.State))
        {
            Save();
            SyncMediaFromTransferIfLoaded(transfer);
        }
        var peer = Peers.FirstOrDefault(p => p.Id == transfer.PeerId);
        if (transfer.Inline)
        {
            // Accepted inline images upgrade their offer bubble into the photo in the conversation.
            if (transfer.Outgoing || transfer.State != "Received" || peer is null || !receivedImages.Add(transfer.Id)) return;
            if (Images.Load(transfer.Path, 64) is null)
            {
                try { File.Delete(transfer.Path); } catch (IOException) { }
                Diagnostics.Insert(0, $"{DateTime.Now:HH:mm:ss}  Discarded an image from {peer.Name} that could not be decoded.");
                log?.Write("image.decode_failed", null, ("peer", peer.Id), ("id", transfer.Id));
                if (transferMessages.TryGetValue(transfer.Id, out var invalidOffer))
                {
                    invalidOffer.Delivery = "Failed";
                    Save();
                }
                return;
            }
            if (transferMessages.TryGetValue(transfer.Id, out var offerMessage))
            {
                offerMessage.ImagePath = transfer.Path;
                offerMessage.FileName = "";
                offerMessage.Delivery = "Received";
                peer.Preview = "Photo"; peer.Typing = "";
                RefreshRecentChats();
                Save();
                if (Settings.AutoAcceptFiles) NotifyIncoming(peer.Name, "Sent you a photo");
            }
            else
            {
                AddMessage(new() { Id = transfer.Id, PeerId = peer.Id, ImagePath = transfer.Path, Delivery = "Received" });
                peer.Preview = "Photo"; peer.Typing = "";
                RefreshRecentChats();
                if (SelectedPeer?.Id != peer.Id || !IsConversationSection || !Replying) peer.Unread++;
                RefreshMessages(); Changed(nameof(PeopleSummary)); Changed(nameof(TotalUnread)); Changed(nameof(HasUnread)); Save(); NotifyIncoming(peer.Name, "Sent you a photo");
            }
        }
    }
    public async Task OfferFileAsync(string path)
    {
        if (Preview) { Toast = "Design preview — files are not sent."; return; }
        if (SelectedPeer is null) return;
        await RunAsync(async () => { await lan!.OfferFileAsync(SelectedPeer.Id, path); TransfersOpen = true; });
    }
    /// <summary>Saves an offered file straight into the received files folder, as LAN Messenger does.</summary>
    public async Task AcceptFileAsync(FileTransfer transfer)
    {
        if (lan is null) return;
        try { await lan.AcceptFileIntoAsync(transfer, ReceivedFilesFolder); }
        catch (InvalidOperationException e)
        {
            if (transfer.State == "Waiting for approval")
            {
                transfer.State = "Interrupted";
                OnTransferChanged(transfer);
            }
            Toast = e.Message;
        }
        catch (Exception e) when (e is IOException or System.Net.Sockets.SocketException or OperationCanceledException or UnauthorizedAccessException)
        {
            log?.Write("operation.failed", e, ("operation", nameof(AcceptFileAsync)));
            Toast = e.Message;
        }
    }
    public FileTransfer? TransferFor(ChatMessage message) => Transfers.FirstOrDefault(t => t.Id == message.TransferId && t.PeerId == message.PeerId);
    public async Task DeclineFileAsync(FileTransfer transfer)
    {
        if (lan is null) return;
        try { await lan.DeclineFileAsync(transfer); }
        catch (InvalidOperationException)
        {
            if (transfer.State == "Waiting for approval")
            {
                transfer.State = "Declined";
                OnTransferChanged(transfer);
            }
        }
        catch (Exception e) when (e is IOException or System.Net.Sockets.SocketException or OperationCanceledException or UnauthorizedAccessException)
        {
            log?.Write("operation.failed", e, ("operation", nameof(DeclineFileAsync)));
            Toast = e.Message;
        }
    }
    public async Task AcceptMessageFileAsync(ChatMessage message)
    {
        if (TransferFor(message) is { } transfer && transfer.State == "Waiting for approval")
        {
            await AcceptFileAsync(transfer);
            return;
        }
        message.Delivery = TransferFor(message) is { } stale ? DeliveryFor(stale) : "Interrupted";
        Save();
    }
    public async Task DeclineMessageFileAsync(ChatMessage message)
    {
        if (TransferFor(message) is { } transfer && transfer.State == "Waiting for approval")
        {
            await DeclineFileAsync(transfer);
            return;
        }
        message.Delivery = TransferFor(message) is { } stale ? DeliveryFor(stale) : "Declined";
        Save();
    }
    public async Task CancelMessageFileAsync(ChatMessage message)
    {
        if (TransferFor(message) is { } transfer && transfer.CanCancel)
        {
            await CancelOfferAsync(transfer);
            return;
        }
        message.CanCancel = false;
        if (TransferFor(message) is { } stale) message.Delivery = DeliveryFor(stale);
        Save();
    }
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
        group.Name = name.Trim(); Changed(nameof(ChatTitle)); if (Search.Length > 0) RefreshRecentChats(); Save();
    }
    public async Task LeaveGroupAsync(GroupChat group)
    {
        if (!Preview && lan is not null)
            foreach (var id in group.Members.Where(id => Peers.Any(p => p.Id == id && p.Online)).ToList())
                await RunAsync(() => lan.SendGroupAsync(id, group.Id, "leave"), false);
        Groups.Remove(group); history.RemoveAll(m => m.GroupId == group.Id);
        historyByChat.Remove((true, group.Id));
        RefreshRecentChats();
        SelectedGroup = Groups.FirstOrDefault(); if (SelectedGroup is null) SectionChanged();
        Save();
    }
    private async Task<bool> SendGroupMessageAsync(GroupChat group, string text)
    {
        var message = new ChatMessage { GroupId = group.Id, Text = text, Outgoing = true, Delivery = "Sending…", Id = NextMessageId().ToString() };
        AddMessage(message); Draft = ""; group.Preview = "You: " + text.ReplaceLineEndings(" "); RefreshRecentChats(); RefreshMessages(true);
        if (Preview || lan is null) { message.Delivery = "Preview only"; return true; }
        int sent = 0, total = group.Members.Count;
        foreach (var id in group.Members.Where(id => Peers.Any(p => p.Id == id && p.Online)).ToList())
        {
            try { await lan.SendGroupAsync(id, group.Id, "message", text); sent++; }
            catch (Exception e) when (e is IOException or System.Net.Sockets.SocketException or OperationCanceledException) { }
        }
        message.Delivery = sent == 0 ? "Not sent · no one online" : sent == total ? "Sent" : $"Sent to {sent} of {total}";
        Save();
        return sent > 0;
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
                    NotifyIncoming(sender.Name, "Added you to a group chat");
                    _ = AnnounceJoinAsync(group);
                }
                AddMember(group, senderId);
                break;
            case "join": if (group is not null) AddMember(group, senderId); break;
            case "leave": if (group is not null && group.Members.Remove(senderId)) group.MembersChanged(); break;
            case "message":
                if (group is null) return; // like LAN Messenger: messages for rooms we are not in are dropped
                AddMember(group, senderId);
                AddMessage(new() { GroupId = thread, PeerId = senderId, SenderName = sender.Name, Text = text, Delivery = "Received" });
                group.Preview = $"{sender.Name}: {text.ReplaceLineEndings(" ")}";
                RefreshRecentChats();
                if (SelectedGroup != group || !IsGroupsSection || !Replying) group.Unread++;
                if (SelectedGroup == group && IsGroupsSection) RefreshMessages();
                NotifyIncoming($"{sender.Name} · {group.Name}", text);
                break;
        }
        if (group is not null && group == SelectedGroup) { Changed(nameof(ChatSubtitle)); Changed(nameof(SelectedGroupMembers)); ComposeChanged(); }
        Changed(nameof(TotalUnread)); Changed(nameof(HasUnread));
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
    public string LastSettingsPage { get; set; } = "GeneralPage";
    public string LastAppearanceTab { get; set; } = "Themes";
    public string ReceivedFilesFolder => string.IsNullOrWhiteSpace(Settings.ReceivedFilesFolder) ? AppSettings.DefaultReceivedFilesFolder : Settings.ReceivedFilesFolder;
    private string BackgroundsDirectory => Path.Combine(ImagesDirectory, "backgrounds");
    private BitmapSource? savedChatBackground;
    private BitmapSource? savedChatBackgroundGray;
    private AnimatedGifSequence? savedChatBackgroundGif;
    private AnimatedGifSequence? savedChatBackgroundGrayGif;
    private bool previewingChatBackground;
    private BitmapSource? previewChatBackground;
    private BitmapSource? previewChatBackgroundGray;
    private AnimatedGifSequence? previewChatBackgroundGif;
    private AnimatedGifSequence? previewChatBackgroundGrayGif;
    private double? previewChatBackgroundOpacity;
    private double? previewBackgroundDim;
    private double? previewBackgroundScale;
    private string? previewBackgroundFit;
    private string? previewBackgroundAnchor;
    private bool? previewBackgroundGrayscale;
    private BitmapSource? ResolveBackgroundFallback(BitmapSource? color, ref BitmapSource? gray) =>
        color is null ? null : ChatBackgroundGrayscale ? (gray ??= Images.ToGrayscale(color)) : color;
    private AnimatedGifSequence? ResolveBackgroundGif(AnimatedGifSequence? color, ref AnimatedGifSequence? gray) =>
        color is null ? null : ChatBackgroundGrayscale ? (gray ??= color.ToGrayscale()) : color;
    public BitmapSource? SavedChatBackground => ResolveBackgroundFallback(savedChatBackground, ref savedChatBackgroundGray);
    internal BitmapSource? RawSavedChatBackground => savedChatBackground;
    public AnimatedGifSequence? SavedChatBackgroundGif => ResolveBackgroundGif(savedChatBackgroundGif, ref savedChatBackgroundGrayGif);
    internal AnimatedGifSequence? RawSavedChatBackgroundGif => savedChatBackgroundGif;
    public BitmapSource? ChatBackground => previewingChatBackground
        ? ResolveBackgroundFallback(previewChatBackground, ref previewChatBackgroundGray)
        : SavedChatBackground;
    public AnimatedGifSequence? ChatBackgroundGif => previewingChatBackground
        ? ResolveBackgroundGif(previewChatBackgroundGif, ref previewChatBackgroundGrayGif)
        : SavedChatBackgroundGif;
    public double ChatBackgroundOpacity
    {
        get
        {
            double value = previewChatBackgroundOpacity ?? Settings.ChatBackgroundOpacity;
            return double.IsFinite(value) ? Math.Clamp(value, .10, 1.0) : .26;
        }
    }
    public double ChatBackgroundDim
    {
        get
        {
            double value = previewBackgroundDim ?? Settings.ChatBackgroundDim;
            return double.IsFinite(value) ? Math.Clamp(value, 0, .80) : 0;
        }
    }
    public double ChatBackgroundScale
    {
        get
        {
            double value = previewBackgroundScale ?? Settings.ChatBackgroundScale;
            return double.IsFinite(value) ? Math.Clamp(value, 1.0, 1.6) : 1.0;
        }
    }
    public string ChatBackgroundFit => AppearanceCodec.NormalizeBgFit(previewBackgroundFit ?? Settings.ChatBackgroundFit);
    public string ChatBackgroundAnchor => BackgroundAnchor.Normalize(previewBackgroundAnchor ?? Settings.ChatBackgroundAnchor);
    public System.Windows.Media.Stretch ChatBackgroundStretch => ChatBackgroundFit switch
    {
        "fit" => System.Windows.Media.Stretch.Uniform,
        "stretch" => System.Windows.Media.Stretch.Fill,
        "center" => System.Windows.Media.Stretch.None,
        _ => System.Windows.Media.Stretch.UniformToFill
    };
    public bool ChatBackgroundGrayscale => previewBackgroundGrayscale ?? Settings.ChatBackgroundGrayscale;
    internal string SaveChatBackground(BitmapSource image) => Images.SavePng(image, BackgroundsDirectory);
    internal string SaveChatBackgroundBytes(byte[] bytes, string extension) => Images.SaveBytes(bytes, extension, BackgroundsDirectory);
    public void PreviewChatBackground(bool active, BitmapSource? image, double? opacity, AnimatedGifSequence? gif = null)
    {
        previewingChatBackground = active;
        if (!ReferenceEquals(previewChatBackground, image)) previewChatBackgroundGray = null;
        previewChatBackground = image;
        if (!ReferenceEquals(previewChatBackgroundGif, gif)) previewChatBackgroundGrayGif = null;
        previewChatBackgroundGif = gif;
        previewChatBackgroundOpacity = active ? opacity : null;
        Changed(nameof(ChatBackground));
        Changed(nameof(ChatBackgroundGif));
        Changed(nameof(ChatBackgroundOpacity));
    }
    public void PreviewBackgroundOptions(double? dim, double? scale, string? fit, bool? grayscale, string? anchor = null)
    {
        previewBackgroundDim = dim;
        previewBackgroundScale = scale;
        previewBackgroundFit = fit;
        previewBackgroundAnchor = anchor;
        previewBackgroundGrayscale = grayscale;
        Changed(nameof(ChatBackgroundDim));
        Changed(nameof(ChatBackgroundScale));
        Changed(nameof(ChatBackgroundFit));
        Changed(nameof(ChatBackgroundAnchor));
        Changed(nameof(ChatBackgroundStretch));
        Changed(nameof(ChatBackgroundGrayscale));
        Changed(nameof(ChatBackground));
        Changed(nameof(ChatBackgroundGif));
    }
    private string? previewBgTransition;
    private double? previewBgTransitionStrength;
    public string ChatBackgroundTransition => BackgroundSideTransition.Normalize(previewBgTransition ?? Settings.ChatBackgroundTransition);
    public double ChatBackgroundTransitionStrength
    {
        get
        {
            double value = previewBgTransitionStrength ?? Settings.ChatBackgroundTransitionStrength;
            return double.IsFinite(value) ? Math.Clamp(value, .1, 1.0) : .55;
        }
    }
    public System.Windows.Media.Brush? ChatBackgroundHorizontalMask =>
        BackgroundSideTransition.CreateHorizontalMask(BackgroundSideTransition.KindFor(ChatBackgroundTransition), ChatBackgroundTransitionStrength);
    public System.Windows.Media.Brush? ChatBackgroundVerticalMask =>
        BackgroundSideTransition.CreateVerticalMask(BackgroundSideTransition.KindFor(ChatBackgroundTransition), ChatBackgroundTransitionStrength);
    public void PreviewBackgroundTransition(string? transition, double? strength)
    {
        previewBgTransition = transition;
        previewBgTransitionStrength = strength;
        Changed(nameof(ChatBackgroundTransition));
        Changed(nameof(ChatBackgroundTransitionStrength));
        Changed(nameof(ChatBackgroundHorizontalMask));
        Changed(nameof(ChatBackgroundVerticalMask));
    }
    public void PreviewMessageTypography(string? font, string? sizeTag, double? px)
    {
        if (System.Windows.Application.Current is not { } app) return;
        string family = string.IsNullOrWhiteSpace(font) ? Settings.MessageFont : font.Trim();
        double resolvedSize = px is { } custom && double.IsFinite(custom)
            ? Math.Clamp(Math.Round(custom), 11, 22)
            : sizeTag is not null ? FontSizeFor(sizeTag) : EffectiveMessageFontSize;
        try { app.Resources["MessageFontFamily"] = new FontFamily(family + ", Segoe UI"); } catch (ArgumentException) { }
        app.Resources["MessageFontSize"] = resolvedSize;
    }
    public async Task<bool> ApplyLookCodeAsync(string code)
    {
        if (!AppearanceCodec.TryDecode(code, out var look)) return false;
        string bgPath = "";
        var (bgImage, gifBytes) = AppearanceCodec.TryDecodeBackground(look);
        if (gifBytes is not null || bgImage is not null)
        {
            try
            {
                bgPath = gifBytes is not null
                    ? SaveChatBackgroundBytes(gifBytes, ".gif")
                    : SaveChatBackground(bgImage!);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or NotSupportedException)
            {
                Toast = "The background couldn't be saved.";
                return false;
            }
        }
        await SavePreferencesAsync(s =>
        {
            AppearanceCodec.Apply(look, s);
            s.ChatBackgroundPath = bgPath;
        });
        Toast = "Theme applied";
        return true;
    }
    public static double FontSizeFor(string size) => size switch { "small" => 13, "large" => 17, _ => 15 };
    public bool SteampunkEngineEnabled => Settings.SteampunkEngineEnabled;
    public bool SakuraPetalsEnabled => Settings.SakuraPetalsEnabled;
    public bool ThemeDetailsEnabled => Settings.ThemeDetailsEnabled;
    public bool FirefliesEnabled => Settings.FirefliesEnabled;
    public double FireflyIntensity => Math.Clamp(Settings.FireflyIntensity, .1, 1);
    public bool RainEnabled => Settings.RainEnabled;
    public double RainIntensity => Math.Clamp(Settings.RainIntensity, .1, 1);
    public bool SnowEnabled => Settings.SnowEnabled;
    public double SnowIntensity => Math.Clamp(Settings.SnowIntensity, .1, 1);
    public bool StarfieldEnabled => Settings.StarfieldEnabled;
    public bool BubblesEnabled => Settings.BubblesEnabled;
    public bool EmbersEnabled => Settings.EmbersEnabled;
    public bool MatrixEnabled => Settings.MatrixEnabled;
    public double MatrixIntensity => Math.Clamp(Settings.MatrixIntensity, .1, 1);
    public bool ConstellationEnabled => Settings.ConstellationEnabled;
    public double ConstellationIntensity => Math.Clamp(Settings.ConstellationIntensity, .1, 1);
    public bool BokehEnabled => Settings.BokehEnabled;
    public double BokehIntensity => Math.Clamp(Settings.BokehIntensity, .1, 1);
    public bool WingPlumesEnabled => Settings.WingPlumesEnabled;
    public double WingPlumesIntensity => Math.Clamp(Settings.WingPlumesIntensity, .1, 1);
    public bool RepulsorEnabled => Settings.RepulsorEnabled;
    public double RepulsorIntensity => Math.Clamp(Settings.RepulsorIntensity, .1, 1);
    public double EffectSpeed => double.IsFinite(Settings.EffectSpeed) ? Math.Clamp(Settings.EffectSpeed, .4, 2.0) : 1.0;
    public double WindowOpacity => double.IsFinite(Settings.WindowOpacity) ? Math.Clamp(Settings.WindowOpacity, .80, 1.0) : 1.0;
    public double EffectiveMessageFontSize => Settings.MessageFontSizePx is { } px && double.IsFinite(px)
        ? Math.Clamp(Math.Round(px), 11, 22)
        : FontSizeFor(Settings.MessageFontSize);
    private string? previewChatCanvasGradient;
    private double? previewChatCanvasIntensity;
    private bool? previewSeamlessComposer;
    private double? previewBackgroundBlur;
    private bool? previewOnlineRing;
    private string? previewAvatarShape;
    private System.Windows.Media.Effects.BlurEffect? cachedBlurEffect;
    private double cachedBlurRadius = -1;
    public string ChatCanvasGradient => ChatCanvasOverlay.Normalize(previewChatCanvasGradient ?? Settings.ChatCanvasGradient);
    public bool ChatCanvasGradientEnabled => ChatCanvasGradient != ChatCanvasOverlay.Off;
    public double ChatCanvasGradientIntensity => Math.Clamp(previewChatCanvasIntensity ?? Settings.ChatCanvasGradientIntensity, .1, 1);
    public bool SeamlessComposerEnabled => previewSeamlessComposer ?? Settings.SeamlessComposerEnabled;
    public int ChatBackdropRowSpan => SeamlessComposerEnabled ? 4 : 1;
    public double ChatBackgroundBlur => Math.Clamp(previewBackgroundBlur ?? Settings.ChatBackgroundBlur, 0, 24);
    public System.Windows.Media.Effects.Effect? ChatBackgroundBlurEffect
    {
        get
        {
            double r = ChatBackgroundBlur;
            if (r < .1) return null;
            if (cachedBlurEffect is null || Math.Abs(cachedBlurRadius - r) > .05)
            {
                cachedBlurRadius = r;
                cachedBlurEffect = new System.Windows.Media.Effects.BlurEffect { Radius = r };
            }
            return cachedBlurEffect;
        }
    }
    public bool OnlineRingEnabled => previewOnlineRing ?? Settings.OnlineRingEnabled;
    public bool UseCustomAvatarShape => AppearanceOptions.NormalizeAvatarShape(previewAvatarShape ?? Settings.AvatarShape) is not (AppearanceOptions.Default or AppearanceOptions.AvatarCircle);
    public bool UseCircularHeaderAvatar => !UseCustomAvatarShape;
    public double EffectIntensity => Math.Clamp(Settings.EffectIntensity, .1, 1);
    public double TypingIntensity => Math.Clamp(Settings.TypingIntensity ?? EffectIntensity, .1, 1);
    public double ClickIntensity => Math.Clamp(Settings.ClickIntensity ?? EffectIntensity, .1, 1);
    public double EngineIntensity => Math.Clamp(Settings.EngineIntensity, .1, 1);
    public double PetalIntensity => Math.Clamp(Settings.PetalIntensity, .1, 1);
    public bool HeaderAuroraEnabled => Settings.HeaderAuroraEnabled;
    public double HeaderAuroraIntensity => Math.Clamp(Settings.HeaderAuroraIntensity, .1, 1);
    public bool TitleSparklesEnabled => Settings.TitleSparklesEnabled;
    public double TitleSparklesIntensity => Math.Clamp(Settings.TitleSparklesIntensity, .1, 1);
    public bool HeaderPulseEnabled => Settings.HeaderPulseEnabled;
    public double HeaderPulseIntensity => Math.Clamp(Settings.HeaderPulseIntensity, .1, 1);
    public bool TitleShimmerEnabled => Settings.TitleShimmerEnabled;
    public double TitleShimmerIntensity => Math.Clamp(Settings.TitleShimmerIntensity, .1, 1);
    public bool TitleCometEnabled => Settings.TitleCometEnabled;
    public double TitleCometIntensity => Math.Clamp(Settings.TitleCometIntensity, .1, 1);
    public bool TitleEqualizerEnabled => Settings.TitleEqualizerEnabled;
    public double TitleEqualizerIntensity => Math.Clamp(Settings.TitleEqualizerIntensity, .1, 1);
    public bool TitleLaserEnabled => Settings.TitleLaserEnabled;
    public double TitleLaserIntensity => Math.Clamp(Settings.TitleLaserIntensity, .1, 1);
    public bool TitleAuroraEnabled => Settings.TitleAuroraEnabled;
    public double TitleAuroraIntensity => Math.Clamp(Settings.TitleAuroraIntensity, .1, 1);
    public bool TitleWaveEnabled => Settings.TitleWaveEnabled;
    public double TitleWaveIntensity => Math.Clamp(Settings.TitleWaveIntensity, .1, 1);
    public bool TitleWingsEnabled => Settings.TitleWingsEnabled;
    public double TitleWingsIntensity => Math.Clamp(Settings.TitleWingsIntensity, .1, 1);
    public bool TitleReactorEnabled => Settings.TitleReactorEnabled;
    public double TitleReactorIntensity => Math.Clamp(Settings.TitleReactorIntensity, .1, 1);
    public bool HeaderPrismEnabled => Settings.HeaderPrismEnabled;
    public double HeaderPrismIntensity => Math.Clamp(Settings.HeaderPrismIntensity, .1, 1);
    public bool HeaderRipplesEnabled => Settings.HeaderRipplesEnabled;
    public double HeaderRipplesIntensity => Math.Clamp(Settings.HeaderRipplesIntensity, .1, 1);
    public bool HeaderCircuitEnabled => Settings.HeaderCircuitEnabled;
    public double HeaderCircuitIntensity => Math.Clamp(Settings.HeaderCircuitIntensity, .1, 1);
    public bool HeaderStarlightEnabled => Settings.HeaderStarlightEnabled;
    public double HeaderStarlightIntensity => Math.Clamp(Settings.HeaderStarlightIntensity, .1, 1);
    public bool HeaderNebulaEnabled => Settings.HeaderNebulaEnabled;
    public double HeaderNebulaIntensity => Math.Clamp(Settings.HeaderNebulaIntensity, .1, 1);
    public bool HeaderSilkEnabled => Settings.HeaderSilkEnabled;
    public double HeaderSilkIntensity => Math.Clamp(Settings.HeaderSilkIntensity, .1, 1);
    public bool HeaderPlasmaEnabled => Settings.HeaderPlasmaEnabled;
    public double HeaderPlasmaIntensity => Math.Clamp(Settings.HeaderPlasmaIntensity, .1, 1);
    public bool HeaderBurstEnabled => Settings.HeaderBurstEnabled;
    public double HeaderBurstIntensity => Math.Clamp(Settings.HeaderBurstIntensity, .1, 1);
    public bool HeaderArcEnabled => Settings.HeaderArcEnabled;
    public double HeaderArcIntensity => Math.Clamp(Settings.HeaderArcIntensity, .1, 1);
    public bool RailBeaconEnabled => Settings.RailBeaconEnabled;
    public double RailBeaconIntensity => Math.Clamp(Settings.RailBeaconIntensity, .1, 1);
    public bool RailStreamEnabled => Settings.RailStreamEnabled;
    public double RailStreamIntensity => Math.Clamp(Settings.RailStreamIntensity, .1, 1);
    public bool RailStardustEnabled => Settings.RailStardustEnabled;
    public double RailStardustIntensity => Math.Clamp(Settings.RailStardustIntensity, .1, 1);
    public bool RailScanEnabled => Settings.RailScanEnabled;
    public double RailScanIntensity => Math.Clamp(Settings.RailScanIntensity, .1, 1);
    public bool RailAuroraEnabled => Settings.RailAuroraEnabled;
    public double RailAuroraIntensity => Math.Clamp(Settings.RailAuroraIntensity, .1, 1);
    public bool RailPulseEnabled => Settings.RailPulseEnabled;
    public double RailPulseIntensity => Math.Clamp(Settings.RailPulseIntensity, .1, 1);
    public bool RailPlasmaEnabled => Settings.RailPlasmaEnabled;
    public double RailPlasmaIntensity => Math.Clamp(Settings.RailPlasmaIntensity, .1, 1);
    public bool RailCascadeEnabled => Settings.RailCascadeEnabled;
    public double RailCascadeIntensity => Math.Clamp(Settings.RailCascadeIntensity, .1, 1);
    public bool RailWingsEnabled => Settings.RailWingsEnabled;
    public double RailWingsIntensity => Math.Clamp(Settings.RailWingsIntensity, .1, 1);
    public bool RailArcEnabled => Settings.RailArcEnabled;
    public double RailArcIntensity => Math.Clamp(Settings.RailArcIntensity, .1, 1);
    public bool SidebarMotesEnabled => Settings.SidebarMotesEnabled;
    public double SidebarMotesIntensity => Math.Clamp(Settings.SidebarMotesIntensity, .1, 1);
    public bool SidebarGlowEnabled => Settings.SidebarGlowEnabled;
    public double SidebarGlowIntensity => Math.Clamp(Settings.SidebarGlowIntensity, .1, 1);
    public bool SidebarPrismEnabled => Settings.SidebarPrismEnabled;
    public double SidebarPrismIntensity => Math.Clamp(Settings.SidebarPrismIntensity, .1, 1);
    public bool SidebarSweepEnabled => Settings.SidebarSweepEnabled;
    public double SidebarSweepIntensity => Math.Clamp(Settings.SidebarSweepIntensity, .1, 1);
    public bool SidebarAuroraEnabled => Settings.SidebarAuroraEnabled;
    public double SidebarAuroraIntensity => Math.Clamp(Settings.SidebarAuroraIntensity, .1, 1);
    public bool SidebarNebulaEnabled => Settings.SidebarNebulaEnabled;
    public double SidebarNebulaIntensity => Math.Clamp(Settings.SidebarNebulaIntensity, .1, 1);
    public bool SidebarWavesEnabled => Settings.SidebarWavesEnabled;
    public double SidebarWavesIntensity => Math.Clamp(Settings.SidebarWavesIntensity, .1, 1);
    public bool SidebarRipplesEnabled => Settings.SidebarRipplesEnabled;
    public double SidebarRipplesIntensity => Math.Clamp(Settings.SidebarRipplesIntensity, .1, 1);
    public bool SidebarCircuitEnabled => Settings.SidebarCircuitEnabled;
    public double SidebarCircuitIntensity => Math.Clamp(Settings.SidebarCircuitIntensity, .1, 1);
    public bool SidebarWingsEnabled => Settings.SidebarWingsEnabled;
    public double SidebarWingsIntensity => Math.Clamp(Settings.SidebarWingsIntensity, .1, 1);
    public bool SidebarHudEnabled => Settings.SidebarHudEnabled;
    public double SidebarHudIntensity => Math.Clamp(Settings.SidebarHudIntensity, .1, 1);
    public double StarfieldIntensity => Math.Clamp(Settings.StarfieldIntensity, .1, 1);
    public double BubblesIntensity => Math.Clamp(Settings.BubblesIntensity, .1, 1);
    public double EmbersIntensity => Math.Clamp(Settings.EmbersIntensity, .1, 1);
    public double ButtonGlowIntensity => Math.Clamp(Settings.ButtonGlowIntensity, .1, 1);
    public double ButtonSheenIntensity => Math.Clamp(Settings.ButtonSheenIntensity, .1, 1);
    public double ButtonBounceIntensity => Math.Clamp(Settings.ButtonBounceIntensity, .1, 1);
    public double ButtonRingIntensity => Math.Clamp(Settings.ButtonRingIntensity, .1, 1);
    public double ButtonPulseIntensity => Math.Clamp(Settings.ButtonPulseIntensity, .1, 1);
    public double ButtonLiftIntensity => Math.Clamp(Settings.ButtonLiftIntensity, .1, 1);
    public double ButtonSparkIntensity => Math.Clamp(Settings.ButtonSparkIntensity, .1, 1);
    private string? previewChromeTexture;
    private double? previewChromeTextureIntensity;
    public string ChromeTexture => CydLan.Desktop.ChromeTexture.Normalize(previewChromeTexture ?? Settings.ChromeTexture);
    public bool ChromeTextureEnabled => ChromeTexture != CydLan.Desktop.ChromeTexture.Off;
    public double ChromeTextureIntensity => Math.Clamp(previewChromeTextureIntensity ?? Settings.ChromeTextureIntensity, .1, 1);
    public bool AmbientOnSidebarEnabled => Settings.AmbientOnSidebarEnabled;
    public bool SidebarPetalsVisible => SakuraPetalsEnabled && Settings.AmbientOnSidebarEnabled;
    public bool SidebarFirefliesVisible => Settings.FirefliesEnabled && Settings.AmbientOnSidebarEnabled;
    public bool SidebarRainVisible => Settings.RainEnabled && Settings.AmbientOnSidebarEnabled;
    public bool SidebarSnowVisible => Settings.SnowEnabled && Settings.AmbientOnSidebarEnabled;
    public bool SidebarStarfieldVisible => Settings.StarfieldEnabled && Settings.AmbientOnSidebarEnabled;
    public bool SidebarBubblesVisible => Settings.BubblesEnabled && Settings.AmbientOnSidebarEnabled;
    public bool SidebarEmbersVisible => Settings.EmbersEnabled && Settings.AmbientOnSidebarEnabled;
    public bool SidebarMatrixVisible => Settings.MatrixEnabled && Settings.AmbientOnSidebarEnabled;
    public bool SidebarConstellationVisible => Settings.ConstellationEnabled && Settings.AmbientOnSidebarEnabled;
    public bool SidebarBokehVisible => Settings.BokehEnabled && Settings.AmbientOnSidebarEnabled;
    public bool SidebarWingPlumesVisible => Settings.WingPlumesEnabled && Settings.AmbientOnSidebarEnabled;
    public bool SidebarRepulsorVisible => Settings.RepulsorEnabled && Settings.AmbientOnSidebarEnabled;

    public void PreviewChromeTexture(string? texture, double? intensity)
    {
        previewChromeTexture = texture;
        previewChromeTextureIntensity = intensity;
        Changed(nameof(ChromeTexture));
        Changed(nameof(ChromeTextureEnabled));
        Changed(nameof(ChromeTextureIntensity));
    }

    public void PreviewChatCanvas(string? gradient, double? intensity)
    {
        previewChatCanvasGradient = gradient;
        previewChatCanvasIntensity = intensity;
        Changed(nameof(ChatCanvasGradient));
        Changed(nameof(ChatCanvasGradientEnabled));
        Changed(nameof(ChatCanvasGradientIntensity));
    }

    public void PreviewSeamlessComposer(bool? seamless)
    {
        previewSeamlessComposer = seamless;
        Changed(nameof(SeamlessComposerEnabled));
        Changed(nameof(ChatBackdropRowSpan));
    }

    public void PreviewBackgroundBlur(double? blur)
    {
        previewBackgroundBlur = blur;
        Changed(nameof(ChatBackgroundBlur));
        Changed(nameof(ChatBackgroundBlurEffect));
    }

    public void PreviewAvatarOptions(string? avatarShape, bool? onlineRing)
    {
        previewAvatarShape = avatarShape;
        previewOnlineRing = onlineRing;
        Changed(nameof(UseCustomAvatarShape));
        Changed(nameof(UseCircularHeaderAvatar));
        Changed(nameof(OnlineRingEnabled));
    }

    public void PreviewPetAppearance(PetAppearance? preview)
    {
        previewPetAppearance = preview;
        Changed(nameof(PetAppearance));
    }

    public void ApplySettings()
    {
        if (System.Windows.Application.Current is { } app)
        {
            try { app.Resources["MessageFontFamily"] = new FontFamily(Settings.MessageFont + ", Segoe UI"); } catch (ArgumentException) { }
            app.Resources["MessageFontSize"] = EffectiveMessageFontSize;
        }
        AmbientEffect.SetSpeed(EffectSpeed);
        AmbientEffect.SetAnimateWhenUnfocused(Settings.AnimateWhenUnfocused);
        MessageMotion.Configure(Settings.MessageAnimation);
        ThemeManager.Apply(Settings.Theme, AppearanceOverrides.From(Settings));
        savedChatBackground = Settings.ChatBackgroundPath.Length == 0 ? null : Images.LoadChatBackground(Settings.ChatBackgroundPath);
        savedChatBackgroundGray = null;
        savedChatBackgroundGif = Settings.ChatBackgroundPath.Length == 0 ? null : AnimatedGifSequence.TryLoadFromFile(Settings.ChatBackgroundPath);
        savedChatBackgroundGrayGif = null;
        previewingChatBackground = false;
        previewChatBackground = null;
        previewChatBackgroundGray = null;
        previewChatBackgroundGif = null;
        previewChatBackgroundGrayGif = null;
        previewChatBackgroundOpacity = null;
        Changed(nameof(ChatBackground)); Changed(nameof(ChatBackgroundGif)); Changed(nameof(ChatBackgroundOpacity));
        Changed(nameof(SteampunkEngineEnabled));
        Changed(nameof(SakuraPetalsEnabled));
        Changed(nameof(ThemeDetailsEnabled));
        Changed(nameof(FirefliesEnabled));
        Changed(nameof(FireflyIntensity));
        Changed(nameof(RainEnabled));
        Changed(nameof(RainIntensity));
        Changed(nameof(SnowEnabled));
        Changed(nameof(SnowIntensity));
        Changed(nameof(StarfieldEnabled));
        Changed(nameof(BubblesEnabled));
        Changed(nameof(EmbersEnabled));
        Changed(nameof(MatrixEnabled));
        Changed(nameof(MatrixIntensity));
        Changed(nameof(ConstellationEnabled));
        Changed(nameof(ConstellationIntensity));
        Changed(nameof(BokehEnabled));
        Changed(nameof(BokehIntensity));
        Changed(nameof(WingPlumesEnabled));
        Changed(nameof(WingPlumesIntensity));
        Changed(nameof(RepulsorEnabled));
        Changed(nameof(RepulsorIntensity));
        Changed(nameof(EffectSpeed));
        Changed(nameof(WindowOpacity));
        Changed(nameof(EffectiveMessageFontSize));
        Changed(nameof(EffectIntensity));
        Changed(nameof(EngineIntensity));
        Changed(nameof(PetalIntensity));
        Changed(nameof(HeaderAuroraEnabled));
        Changed(nameof(HeaderAuroraIntensity));
        Changed(nameof(TitleSparklesEnabled));
        Changed(nameof(TitleSparklesIntensity));
        Changed(nameof(HeaderPulseEnabled));
        Changed(nameof(HeaderPulseIntensity));
        Changed(nameof(TitleShimmerEnabled));
        Changed(nameof(TitleShimmerIntensity));
        Changed(nameof(TitleCometEnabled));
        Changed(nameof(TitleCometIntensity));
        Changed(nameof(TitleEqualizerEnabled));
        Changed(nameof(TitleEqualizerIntensity));
        Changed(nameof(TitleLaserEnabled));
        Changed(nameof(TitleLaserIntensity));
        Changed(nameof(TitleAuroraEnabled));
        Changed(nameof(TitleAuroraIntensity));
        Changed(nameof(TitleWaveEnabled));
        Changed(nameof(TitleWaveIntensity));
        Changed(nameof(TitleWingsEnabled));
        Changed(nameof(TitleWingsIntensity));
        Changed(nameof(TitleReactorEnabled));
        Changed(nameof(TitleReactorIntensity));
        Changed(nameof(HeaderPrismEnabled));
        Changed(nameof(HeaderPrismIntensity));
        Changed(nameof(HeaderRipplesEnabled));
        Changed(nameof(HeaderRipplesIntensity));
        Changed(nameof(HeaderCircuitEnabled));
        Changed(nameof(HeaderCircuitIntensity));
        Changed(nameof(HeaderStarlightEnabled));
        Changed(nameof(HeaderStarlightIntensity));
        Changed(nameof(HeaderNebulaEnabled));
        Changed(nameof(HeaderNebulaIntensity));
        Changed(nameof(HeaderSilkEnabled));
        Changed(nameof(HeaderSilkIntensity));
        Changed(nameof(HeaderPlasmaEnabled));
        Changed(nameof(HeaderPlasmaIntensity));
        Changed(nameof(HeaderBurstEnabled));
        Changed(nameof(HeaderBurstIntensity));
        Changed(nameof(HeaderArcEnabled));
        Changed(nameof(HeaderArcIntensity));
        Changed(nameof(RailBeaconEnabled));
        Changed(nameof(RailBeaconIntensity));
        Changed(nameof(RailStreamEnabled));
        Changed(nameof(RailStreamIntensity));
        Changed(nameof(RailStardustEnabled));
        Changed(nameof(RailStardustIntensity));
        Changed(nameof(RailScanEnabled));
        Changed(nameof(RailScanIntensity));
        Changed(nameof(RailAuroraEnabled));
        Changed(nameof(RailAuroraIntensity));
        Changed(nameof(RailPulseEnabled));
        Changed(nameof(RailPulseIntensity));
        Changed(nameof(RailPlasmaEnabled));
        Changed(nameof(RailPlasmaIntensity));
        Changed(nameof(RailCascadeEnabled));
        Changed(nameof(RailCascadeIntensity));
        Changed(nameof(RailWingsEnabled));
        Changed(nameof(RailWingsIntensity));
        Changed(nameof(RailArcEnabled));
        Changed(nameof(RailArcIntensity));
        Changed(nameof(SidebarMotesEnabled));
        Changed(nameof(SidebarMotesIntensity));
        Changed(nameof(SidebarGlowEnabled));
        Changed(nameof(SidebarGlowIntensity));
        Changed(nameof(SidebarPrismEnabled));
        Changed(nameof(SidebarPrismIntensity));
        Changed(nameof(SidebarSweepEnabled));
        Changed(nameof(SidebarSweepIntensity));
        Changed(nameof(SidebarAuroraEnabled));
        Changed(nameof(SidebarAuroraIntensity));
        Changed(nameof(SidebarNebulaEnabled));
        Changed(nameof(SidebarNebulaIntensity));
        Changed(nameof(SidebarWavesEnabled));
        Changed(nameof(SidebarWavesIntensity));
        Changed(nameof(SidebarRipplesEnabled));
        Changed(nameof(SidebarRipplesIntensity));
        Changed(nameof(SidebarCircuitEnabled));
        Changed(nameof(SidebarCircuitIntensity));
        Changed(nameof(SidebarWingsEnabled));
        Changed(nameof(SidebarWingsIntensity));
        Changed(nameof(SidebarHudEnabled));
        Changed(nameof(SidebarHudIntensity));
        Changed(nameof(StarfieldIntensity));
        Changed(nameof(BubblesIntensity));
        Changed(nameof(EmbersIntensity));
        Changed(nameof(ButtonGlowIntensity));
        Changed(nameof(ButtonSheenIntensity));
        Changed(nameof(ButtonBounceIntensity));
        Changed(nameof(ButtonRingIntensity));
        Changed(nameof(ButtonPulseIntensity));
        Changed(nameof(ButtonLiftIntensity));
        Changed(nameof(ButtonSparkIntensity));
        previewChromeTexture = null;
        previewChromeTextureIntensity = null;
        previewChatCanvasGradient = null;
        previewChatCanvasIntensity = null;
        previewSeamlessComposer = null;
        previewBackgroundBlur = null;
        previewBgTransition = null;
        previewBgTransitionStrength = null;
        previewBackgroundDim = null;
        previewBackgroundScale = null;
        previewBackgroundFit = null;
        previewBackgroundAnchor = null;
        previewBackgroundGrayscale = null;
        previewOnlineRing = null;
        previewAvatarShape = null;
        previewPetAppearance = null;
        Changed(nameof(PetAppearance));
        Changed(nameof(ChromeTexture));
        Changed(nameof(ChromeTextureEnabled));
        Changed(nameof(ChromeTextureIntensity));
        Changed(nameof(ChatCanvasGradient));
        Changed(nameof(ChatCanvasGradientEnabled));
        Changed(nameof(ChatCanvasGradientIntensity));
        Changed(nameof(SeamlessComposerEnabled));
        Changed(nameof(ChatBackdropRowSpan));
        Changed(nameof(ChatBackgroundBlur));
        Changed(nameof(ChatBackgroundBlurEffect));
        Changed(nameof(ChatBackgroundTransition));
        Changed(nameof(ChatBackgroundTransitionStrength));
        Changed(nameof(ChatBackgroundHorizontalMask));
        Changed(nameof(ChatBackgroundVerticalMask));
        Changed(nameof(ChatBackgroundDim));
        Changed(nameof(ChatBackgroundScale));
        Changed(nameof(ChatBackgroundFit));
        Changed(nameof(ChatBackgroundAnchor));
        Changed(nameof(ChatBackgroundStretch));
        Changed(nameof(ChatBackgroundGrayscale));
        Changed(nameof(ChatBackground));
        Changed(nameof(ChatBackgroundGif));
        Changed(nameof(OnlineRingEnabled));
        Changed(nameof(UseCustomAvatarShape));
        Changed(nameof(UseCircularHeaderAvatar));
        Changed(nameof(AmbientOnSidebarEnabled));
        Changed(nameof(SidebarPetalsVisible));
        Changed(nameof(SidebarFirefliesVisible));
        Changed(nameof(SidebarRainVisible));
        Changed(nameof(SidebarSnowVisible));
        Changed(nameof(SidebarStarfieldVisible));
        Changed(nameof(SidebarBubblesVisible));
        Changed(nameof(SidebarEmbersVisible));
        Changed(nameof(SidebarMatrixVisible));
        Changed(nameof(SidebarConstellationVisible));
        Changed(nameof(SidebarBokehVisible));
        Changed(nameof(SidebarWingPlumesVisible));
        Changed(nameof(SidebarRepulsorVisible));
        Changed(nameof(Settings));
        ButtonEffects.Configure(
            Settings.ButtonGlowEnabled, ButtonGlowIntensity,
            Settings.ButtonSheenEnabled, ButtonSheenIntensity,
            Settings.ButtonBounceEnabled, ButtonBounceIntensity,
            Settings.ButtonRingEnabled, ButtonRingIntensity,
            Settings.ButtonPulseEnabled, ButtonPulseIntensity,
            Settings.ButtonLiftEnabled, ButtonLiftIntensity,
            Settings.ButtonSparkEnabled, ButtonSparkIntensity);
        TargetAura.Configure(
            Settings.TargetAura, Settings.TargetAuraIntensity,
            Settings.AuraSelectedModuleEnabled, Settings.AuraSelectedPersonEnabled, Settings.AuraHighlightButtonsEnabled);
        if (lan is not null) lan.AutoAcceptDirectory = Settings.AutoAcceptFiles ? ReceivedFilesFolder : null;
    }
    /// <summary>Applies a batch of preference changes, saves them and tells contacts about profile changes.</summary>
    public async Task SavePreferencesAsync(Action<AppSettings> change)
    {
        bool hadHistory = Settings.KeepHistory;
        string previousAvatar = Settings.AvatarPath;
        string previousBackground = Settings.ChatBackgroundPath;
        var previousProfile = (Settings.DisplayName, Settings.Status, Settings.FirstName, Settings.LastName, Settings.About, CompanionProfileCodec.Encode(Settings));
        change(Settings);
        Changed(nameof(ShouldShowNotificationPopup));
        bool avatarChanged = previousAvatar != Settings.AvatarPath;
        if (avatarChanged && previousAvatar.StartsWith(Path.Combine(ImagesDirectory, "profile"), StringComparison.OrdinalIgnoreCase)) TryDelete(previousAvatar);
        Settings.Theme = ThemeManager.Normalize(Settings.Theme);
        Settings.ColorFinish = ColorFinish.Normalize(Settings.ColorFinish);
        Settings.ColorFinishIntensity = Math.Clamp(double.IsFinite(Settings.ColorFinishIntensity) ? Settings.ColorFinishIntensity : .65, .1, 1);
        Settings.CustomAccentColor = AppearanceOptions.NormalizeHex(Settings.CustomAccentColor);
        Settings.CustomEffectColor = AppearanceOptions.NormalizeHex(Settings.CustomEffectColor);
        Settings.CustomOutgoingBubbleColor = AppearanceOptions.NormalizeHex(Settings.CustomOutgoingBubbleColor);
        Settings.CustomIncomingBubbleColor = AppearanceOptions.NormalizeHex(Settings.CustomIncomingBubbleColor);
        Settings.BubbleStyle = AppearanceOptions.NormalizeBubbleStyle(Settings.BubbleStyle);
        Settings.BubbleShape = AppearanceOptions.NormalizeBubbleShape(Settings.BubbleShape);
        Settings.ChatDensity = AppearanceOptions.NormalizeChatDensity(Settings.ChatDensity);
        Settings.ChatCanvasGradient = ChatCanvasOverlay.Normalize(Settings.ChatCanvasGradient);
        Settings.ChatCanvasGradientIntensity = Math.Clamp(double.IsFinite(Settings.ChatCanvasGradientIntensity) ? Settings.ChatCanvasGradientIntensity : .55, .1, 1);
        Settings.ChatBackgroundBlur = Math.Clamp(double.IsFinite(Settings.ChatBackgroundBlur) ? Settings.ChatBackgroundBlur : 0, 0, 24);
        Settings.ChatBackgroundOpacity = double.IsFinite(Settings.ChatBackgroundOpacity) ? Math.Clamp(Settings.ChatBackgroundOpacity, .10, 1.0) : .26;
        Settings.ChatBackgroundTransition = BackgroundSideTransition.Normalize(Settings.ChatBackgroundTransition);
        Settings.ChatBackgroundTransitionStrength = Math.Clamp(double.IsFinite(Settings.ChatBackgroundTransitionStrength) ? Settings.ChatBackgroundTransitionStrength : .55, .1, 1.0);
        Settings.ChatBackgroundDim = double.IsFinite(Settings.ChatBackgroundDim) ? Math.Clamp(Settings.ChatBackgroundDim, 0, .80) : 0;
        Settings.ChatBackgroundScale = double.IsFinite(Settings.ChatBackgroundScale) ? Math.Clamp(Settings.ChatBackgroundScale, 1.0, 1.6) : 1.0;
        Settings.ChatBackgroundFit = AppearanceCodec.NormalizeBgFit(Settings.ChatBackgroundFit);
        Settings.ChatBackgroundAnchor = BackgroundAnchor.Normalize(Settings.ChatBackgroundAnchor);
        Settings.CornerStyle = AppearanceOptions.NormalizeCornerStyle(Settings.CornerStyle);
        Settings.AvatarShape = AppearanceOptions.NormalizeAvatarShape(Settings.AvatarShape);
        Settings.UiFont = (Settings.UiFont ?? "").Trim();
        Settings.WindowOpacity = Math.Clamp(double.IsFinite(Settings.WindowOpacity) ? Settings.WindowOpacity : 1.0, .80, 1.0);
        Settings.EffectSpeed = Math.Clamp(double.IsFinite(Settings.EffectSpeed) ? Settings.EffectSpeed : 1.0, .4, 2.0);
        Settings.MessageAnimation = MessageMotion.Normalize(Settings.MessageAnimation);
        Settings.MessageFontSizePx = Settings.MessageFontSizePx is { } px && double.IsFinite(px) ? Math.Clamp(Math.Round(px), 11, 22) : null;
        Settings.SavedPresets ??= [];
        Settings.LastSettingsPage = SettingsWindow.NormalizePage(Settings.LastSettingsPage);
        Settings.LastAppearanceTab = SettingsWindow.NormalizeAppearanceTab(Settings.LastAppearanceTab);
        Settings.TypingEffect = ChatEffects.Normalize(Settings.TypingEffect, typing: true);
        Settings.ClickEffect = ChatEffects.Normalize(Settings.ClickEffect);
        FloatingPetStyle.Apply(PetAppearance.From(Settings), Settings);
        if (Settings.MessageFontSize is not ("small" or "medium" or "large")) Settings.MessageFontSize = "medium";
        store?.SaveSettings(Settings);
        if (previousBackground.Length > 0 && previousBackground != Settings.ChatBackgroundPath &&
            string.Equals(Path.GetDirectoryName(previousBackground), BackgroundsDirectory, StringComparison.OrdinalIgnoreCase))
            TryDelete(previousBackground);
        if (!Preview) StartupRegistration.Apply(Settings.StartWithWindows);
        ApplySettings();
        Changed(nameof(ProfileName)); Changed(nameof(ProfileInitial)); Changed(nameof(ProfileStatus)); Changed(nameof(ProfileAvatar));
        if (hadHistory != Settings.KeepHistory) Save();
        if (lan is not null && previousProfile != (Settings.DisplayName, Settings.Status, Settings.FirstName, Settings.LastName, Settings.About, CompanionProfileCodec.Encode(Settings)))
            await RunAsync(() => lan.UpdateProfileAsync());
        // LAN Messenger has no "picture removed" message, so only a new picture is sent.
        if (lan is not null && avatarChanged && Settings.AvatarPath.Length > 0) await RunAsync(() => lan.SendAvatarAsync());
    }
    public bool ShouldAlert => Settings.Notifications && !(Settings.Status == "busy" && !Settings.AlertsWhenBusy) && !(Settings.Status == "dnd" && !Settings.AlertsWhenDnd);
    public bool ShouldShowNotificationPopup => ShouldAlert && Settings.NotificationPreviews;
    public bool ShouldPlaySound => Settings.Sounds && !(Settings.Status == "busy" && !Settings.SoundsWhenBusy) && !(Settings.Status == "dnd" && !Settings.SoundsWhenDnd);
    internal void NotifyIncoming(string sender, string text, string? peerId = null)
    {
        Notify?.Invoke(ShouldShowNotificationPopup ? (sender, text) : null);
        if (ShouldAlert)
            PetActivity?.Invoke(sender, ShouldShowNotificationPopup ? text : "New message", peerId);
    }

    public void UpdateFloatingState(bool? enabled = null, bool? roam = null, double? left = null, double? top = null)
    {
        if (enabled is { } e) Settings.FloatingLanEnabled = e;
        if (roam is { } r) Settings.FloatingLanRoam = r;
        if (left is { } l && double.IsFinite(l)) Settings.FloatingLanLeft = Math.Round(l, 1);
        if (top is { } t && double.IsFinite(t)) Settings.FloatingLanTop = Math.Round(t, 1);
        Changed(nameof(FloatingLanEnabled));
        Changed(nameof(FloatingLanRoam));
        if (!Preview) store?.SaveSettings(Settings);
        FloatingStateChanged?.Invoke();
    }
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
        foreach (var messages in historyByChat.Values) messages.RemoveAll(match);
        foreach (var message in removed)
            if (message.TransferId.Length > 0) transferMessages.Remove(message.TransferId);
        RefreshRecentChats();
        // Pictures are CydLan's own copies; files saved to the received files folder are kept.
        foreach (var image in removed.Select(m => m.ImagePath).Where(p => p.Length > 0 && Path.GetFullPath(p).StartsWith(Path.GetFullPath(ImagesDirectory), StringComparison.OrdinalIgnoreCase)))
            if (!history.Any(m => string.Equals(m.ImagePath, image, StringComparison.OrdinalIgnoreCase))) TryDelete(image);
        foreach (var peer in Peers.Where(p => !history.Any(m => m.GroupId.Length == 0 && m.PeerId == p.Id))) { peer.Preview = ""; peer.Unread = 0; activeSessionChatPeers.Remove(peer.Id); }
        foreach (var group in Groups.Where(g => !history.Any(m => m.GroupId == g.Id))) { group.Preview = ""; group.Unread = 0; }
        RefreshQuickChatPeers();
        RefreshMessages(); Changed(nameof(PeopleSummary)); Changed(nameof(TotalUnread)); Changed(nameof(HasUnread)); Save();
    }
    public void ClearTransferHistory()
    {
        foreach (var t in Transfers.Where(t => t.State is not ("Waiting for approval" or "Accepted" or "Connecting" or "Transferring")).ToList())
        {
            Transfers.Remove(t);
            transferMessages.Remove(t.Id);
            receivedImages.Remove(t.Id);
        }
        Save();
    }

    public async Task SaveProfileAsync(string name, string status, bool notifications, bool minimized, string? theme = null)
    {
        Settings.DisplayName = name; Settings.Status = status; Settings.Notifications = notifications; Settings.StartMinimized = minimized;
        Changed(nameof(ShouldShowNotificationPopup));
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
    private IReadOnlyList<ChatMessage> ChatHistory(bool group, string id) =>
        historyByChat.TryGetValue((group, id), out var messages) ? messages : [];

    private void AddMessage(ChatMessage message)
    {
        history.Add(message);
        bool group = message.GroupId.Length > 0;
        var key = (group, group ? message.GroupId : message.PeerId);
        if (!historyByChat.TryGetValue(key, out var messages)) historyByChat[key] = messages = [];
        messages.Add(message);
    }

    public bool HasOlderMessages => messagePageStart > 0;
    public bool HasNewerMessages => messagePageEnd is not null && messagePageEnd < messageResultCount;

    internal void PageMessages(bool older)
    {
        if (older ? !HasOlderMessages : !HasNewerMessages) return;
        int nextEnd = older ? messagePageStart : messagePageEnd!.Value + HistoryPageSize;
        messagePageEnd = nextEnd < messageResultCount ? nextEnd : null;
        RefreshMessages();
    }

    private void RefreshMessages(bool forceScroll = false)
    {
        if (forceScroll) messagePageEnd = null;
        var source = InGroup ? ChatHistory(true, SelectedGroup!.Id)
            : IsConversationSection && SelectedPeer is { } peer ? ChatHistory(false, peer.Id) : [];
        if (MessageSearch.Length > 0)
            source = source.Where(m => m.Text.Contains(MessageSearch, StringComparison.OrdinalIgnoreCase)
                || m.FileName.Contains(MessageSearch, StringComparison.OrdinalIgnoreCase)).ToArray();
        messageResultCount = source.Count;
        if (messagePageEnd >= messageResultCount) messagePageEnd = null;
        int end = messagePageEnd ?? messageResultCount;
        messagePageStart = Math.Max(0, end - HistoryPageSize);
        UpdateRows(Messages, source.Skip(messagePageStart).Take(end - messagePageStart));
        Changed(nameof(HasOlderMessages)); Changed(nameof(HasNewerMessages));
        if (forceScroll || messagePageEnd is null) ScrollRequested?.Invoke(forceScroll);
    }

    private static void UpdateRows<T>(ObservableCollection<T> rows, IEnumerable<T> source) where T : class
    {
        var next = source.ToList();
        if (rows.SequenceEqual(next)) return;
        if (rows.Count > 0 && next.Count > 0)
        {
            int trim = rows.IndexOf(next[0]);
            if (trim >= 0 && rows.Skip(trim).SequenceEqual(next.Take(rows.Count - trim)))
            {
                while (trim-- > 0) rows.RemoveAt(0);
                foreach (var item in next.Skip(rows.Count)) rows.Add(item);
                return;
            }
        }
        rows.Clear();
        foreach (var item in next) rows.Add(item);
    }
    private void Save()
    {
        try
        {
            WriteRoster();
            store?.SaveHistory(Settings.KeepHistory ? history : []); store?.SaveDrafts(drafts); store?.SaveGroups(Groups);
            store?.SaveTransfers(Settings.KeepFileHistory ? Transfers.Where(t => t.Id != "preview") : []);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { log?.Write("storage.save_failed", e); Toast = "Local history could not be saved: " + e.Message; }
    }
    private void SaveRoster()
    {
        rosterSaveTimer.Stop();
        if (disposed) return;
        try { WriteRoster(); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        { log?.Write("storage.contacts_save_failed", e); Toast = "Contacts could not be saved: " + e.Message; }
    }
    private void WriteRoster()
    {
        if (store is null) return;
        store.SavePeers(Peers);
        rosterSaveTimer.Stop();
        foreach (string path in obsoleteAvatars)
            if (!Peers.Any(peer => string.Equals(peer.AvatarPath, path, StringComparison.OrdinalIgnoreCase))) TryDelete(path);
        obsoleteAvatars.Clear();
    }
    public async ValueTask DisposeAsync()
    {
        if (disposed) return;
        disposed = true;
        rosterSaveTimer.Stop();
        queuedRosterRefresh?.Abort();
        try { if (networkStartup is not null) await networkStartup; }
        finally { Save(); if (lan is not null) await lan.DisposeAsync(); rosterSaveTimer.Stop(); }
    }
    private void SeedPreview()
    {
        var names = new[] { ("1", "Jamie Chen", "chat", "Thanks! That looks great."), ("2", "Sofia Reyes", "chat", "Sent you the updated assets"), ("3", "Marcus Taylor", "busy", "Let's sync after lunch"), ("4", "Olivia Wilson", "chat", "The network is all set ✓"), ("5", "Daniel Kim", "away", "Back in a few minutes") };
        foreach (var (id, name, status, preview) in names)
        {
            var peer = new Peer
            {
                Id = id,
                Name = name,
                Status = status,
                Online = true,
                Address = "192.168.1." + (110 + int.Parse(id)),
                Version = id == "1" ? "LAN Messenger 1.2.35" : "CydLan 0.1",
                Preview = preview,
                Unread = id == "2" ? 2 : 0,
                About = id switch
                {
                    "1" => "Still running classic LAN Messenger at the front desk.",
                    "2" => "UI designer & tea enthusiast. Drop assets anytime!",
                    "3" => "Backend & homelab builds.",
                    _ => ""
                },
                PetCode = id switch
                {
                    "2" => FloatingPetStyle.Presets.First(p => p.Name == "Sakura").Appearance.ToWireCode(),
                    "3" => FloatingPetStyle.Presets.First(p => p.Name == "Mecha Ace").Appearance.ToWireCode(),
                    "4" => FloatingPetStyle.Presets.First(p => p.Name == "Wizard").Appearance.ToWireCode(),
                    _ => ""
                }
            };
            peer.PropertyChanged += PeerChanged; Peers.Add(peer);
        }
        void Add(string text, bool outgoing, int minutes) => AddMessage(new() { PeerId = "1", Text = text, Outgoing = outgoing, Delivery = outgoing ? "Delivered" : "Received", Time = DateTimeOffset.Now.AddMinutes(-minutes) });
        Add("Hey Alex! I'm using the original LAN Messenger. Can we still chat and share files?", false, 9);
        Add("Yep! We can chat and share files directly, even with the older app.", true, 8);
        Add("Perfect. A fresh look, same local connection.", false, 6);
        Add("Exactly. Sending over the concept now.", true, 5);
        try { AddMessage(new() { PeerId = "1", Outgoing = true, Delivery = "Delivered", ImagePath = SampleImage(), Time = DateTimeOffset.Now.AddMinutes(-4) }); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        Add("Thanks! That looks great.", false, 2);
        var team = new GroupChat { Id = "preview-group", Name = "Design team", Members = ["2", "4"], Preview = "Olivia: Works on my side too ✓" };
        Groups.Add(team);
        AddMessage(new() { GroupId = team.Id, PeerId = "2", SenderName = "Sofia Reyes", Text = "Uploaded the new icons to the shared folder.", Delivery = "Received", Time = DateTimeOffset.Now.AddMinutes(-30) });
        AddMessage(new() { GroupId = team.Id, Text = "Great, I'll review them after lunch.", Outgoing = true, Delivery = "Sent", Time = DateTimeOffset.Now.AddMinutes(-28) });
        AddMessage(new() { GroupId = team.Id, PeerId = "4", SenderName = "Olivia Wilson", Text = "Works on my side too ✓", Delivery = "Received", Time = DateTimeOffset.Now.AddMinutes(-20) });
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
