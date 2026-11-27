using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using System.Windows.Threading;
using CydLan.Core;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;
using Point = System.Windows.Point;
using Size = System.Windows.Size;
using Rectangle = System.Windows.Shapes.Rectangle;
using ColorConverter = System.Windows.Media.ColorConverter;

namespace CydLan.Desktop;

/// <summary>Draws one mini-game food for the arena's shop tiles.</summary>
public sealed class FoodIcon : FrameworkElement
{
    public static readonly DependencyProperty FoodProperty = DependencyProperty.Register(nameof(Food), typeof(string), typeof(FoodIcon),
        new FrameworkPropertyMetadata("berry", FrameworkPropertyMetadataOptions.AffectsRender));
    public string Food { get => (string)GetValue(FoodProperty); set => SetValue(FoodProperty, value); }
    protected override void OnRender(DrawingContext dc)
    {
        double s = Math.Min(ActualWidth, ActualHeight) / 14.0;
        FloatingPetVisual.DrawFood(dc, Food ?? "berry", ActualWidth / 2, ActualHeight / 2, s);
    }
}

/// <summary>The Pets module: care, shop, gear, deck and the animated battle stage.</summary>
public partial class PetArenaView : System.Windows.Controls.UserControl
{
    private sealed record StatRow(string Key, string Label, int Total, int Bonus, bool CanAdd)
    {
        public string AddName => "Add point to " + Label;
        public string BonusText => Bonus > 0 ? $"+{Bonus}" : "";
    }
    private sealed record FoodRow(string Id, string Name, string Effect, int Count, int Price, bool CanFeed, bool CanBuy)
    {
        public string CountText => Count.ToString();
        public string PriceText => Price.ToString();
        public string FeedName => "Feed " + Name;
        public string BuyName => $"Buy {Name} for {Price} coins";
    }
    private sealed record FriendRow(Peer Peer, string Name, PetAppearance Appearance, bool Pending)
    {
        public string ActionText => Pending ? "Cancel" : "Duel";
        public string ActionName => (Pending ? "Cancel duel challenge to " : "Invite ") + Name + (Pending ? "" : " to a duel");
    }

    private sealed class StagePet
    {
        public required FloatingPetVisual Visual { get; init; }
        public required TranslateTransform Move { get; init; }
        public double Facing;
        public double Hop;
        public string TempMood = "";
        public DateTime TempUntil;
        public int ShownHp, MaxHp;
        public bool Fainted;
        public double Spin, Lift;
    }

    private readonly MainViewModel model;
    private readonly PetArena arena;
    private readonly DispatcherTimer anim = new(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(33) };
    private readonly DispatcherTimer slow = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly StagePet left, right;
    private double breath, blink, blinkClock, nextBlink = 2.8;
    private bool blinkDown;
    private double lobbyHop;
    private PetBattleSession? session;
    private readonly Queue<IReadOnlyList<BattleEvent>> pending = new();
    private bool playing;
    private string? lastOpponentPeer;

    public PetArenaView(MainViewModel model)
    {
        InitializeComponent();
        this.model = model;
        arena = model.Arena;
        DataContext = model;
        arena.EnsureStarted();

        left = new StagePet { Visual = LeftPet, Move = LeftMove, Facing = 1 };
        right = new StagePet { Visual = RightPet, Move = RightMove, Facing = -1 };
        CollectionFilter.ItemsSource = CollectionFilters.Select(f => f.Name).ToList();
        CollectionFilter.SelectedIndex = 0;
        InviteList.ItemsSource = arena.Invites;

        arena.PropertyChanged += ArenaChanged;
        arena.BattleStarted += ShowBattle;
        arena.CardBattleStarted += ShowCardBattle;
        arena.Invites.CollectionChanged += InvitesChanged;
        anim.Tick += (_, _) => Animate();
        slow.Tick += (_, _) =>
        {
            if (LobbyPanel.IsVisible) RefreshFriends();
            RefreshBattleControls();
        };
        // The module only animates while it is on screen.
        IsVisibleChanged += (_, _) =>
        {
            if (IsVisible)
            {
                anim.Start(); slow.Start();
                if (arena.CurrentCard is { } card) { if (cardSession != card) ShowCardBattle(card); }
                else if (arena.Current is { } current) { if (session != current) ShowBattle(current); }
                else ShowLobby();
            }
            else { anim.Stop(); slow.Stop(); }
        };
    }

    /// <summary>Stops listening to the arena when the view is thrown away.</summary>
    public void Release()
    {
        anim.Stop(); slow.Stop();
        arena.PropertyChanged -= ArenaChanged;
        arena.BattleStarted -= ShowBattle;
        arena.CardBattleStarted -= ShowCardBattle;
        arena.Invites.CollectionChanged -= InvitesChanged;
        Detach();
        DetachCard();
    }

    private void ArenaChanged(object? sender, PropertyChangedEventArgs e)
    {
        if ((e.PropertyName == nameof(PetArena.Current) && arena.Current is null) || (e.PropertyName == nameof(PetArena.CurrentCard) && arena.CurrentCard is null)) ShowLobby();
        else if (LobbyPanel.Visibility == Visibility.Visible) RefreshLobby();
    }

    private void InvitesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        InvitesSection.Visibility = arena.Invites.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    // ── Lobby ────────────────────────────────────────────────────────────

    private void RefreshLobby()
    {
        var p = arena.Progress;
        MyNameText.Text = model.Settings.DisplayName;
        var element = PetGame.ElementOf(p.Element);
        ElementBadgeText.Text = element.Name;
        ElementBadge.Background = BrushOf(element.Color);
        MatchupText.Text = $"Strong vs {Names(PetGame.BeatenBy(p.Element))} · Weak to {Names(PetGame.WeakTo(p.Element))}";
        int need = PetGame.XpToNext(p.Level);
        XpText.Text = p.Level >= PetGame.MaxLevel ? "Max" : $"{p.Xp} / {need} XP";
        SetBar(XpFill, XpTrack, p.Level >= PetGame.MaxLevel ? 1 : p.Xp / (double)need);
        int nextPoint = PetGame.NextBonusLevel(p.Level);
        NextPointText.Text = nextPoint > 0 ? $"Next stat point at level {nextPoint}" : $"All {PetGame.MaxBonusPoints} stat points earned";
        RecordText.Text = $"{p.CardWins} wins · {p.CardLosses} losses";
        BoostText.Text = p.Boost switch { "power" => "Power boost ready", "guard" => "Guard boost ready", "speed" => "Speed boost ready", _ => "" };
        BoostText.Visibility = p.Boost.Length > 0 ? Visibility.Visible : Visibility.Collapsed;


        int free = PetGame.FreePoints(p);
        bool busy = arena.CurrentCard is { Finished: false };
        PointsTitle.Text = free > 0 ? $"Stats · {free} to place" : $"Stats · {PetGame.SpentPoints(p)}/{PetGame.MaxBonusPoints} bonus";
        ResetPointsButton.IsEnabled = PetGame.SpentPoints(p) > 0 && !busy;
        RerollButton.Content = $"Re-roll · {PetGame.RerollPrice}";
        RerollButton.IsEnabled = p.Coins >= PetGame.RerollPrice && !busy;
        StatRows.ItemsSource = new[]
        {
            new StatRow("power", "Power", PetCards.TotalPower(p), p.Power, free > 0),
            new StatRow("guard", "Guard", PetCards.TotalGuard(p), p.Guard, free > 0),
            new StatRow("speed", "Speed", PetCards.TotalSpeed(p), p.Speed, free > 0),
            new StatRow("vitality", "Vitality", PetCards.TotalVitality(p), p.Vitality, free > 0)
        };
        StatsLine.Text = $"{PetCards.MaxHpFor(PetCards.TotalVitality(p))} HP";

        DailyButton.IsEnabled = p.LastDailyDay != DateTime.Now.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

        StatusText.Text = arena.Status;
        StatusText.Visibility = arena.Status.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        RefreshFriends();
        RefreshCards();
    }

    private static string Names(IEnumerable<string> elements) => string.Join(", ", elements.Select(e => PetGame.ElementOf(e).Name));

    private void RefreshFriends()
    {
        var rows = arena.BattleFriends.Select(peer => new FriendRow(peer, peer.Name,
            PetAppearance.FromWireCode(peer.PetCode) ?? PetAppearance.Default,
            arena.CardChallengePeerId == peer.Id || arena.ChallengePeerId == peer.Id)).ToList();
        if (FriendList.ItemsSource is not List<FriendRow> old || !old.SequenceEqual(rows)) FriendList.ItemsSource = rows;
        NoFriendsText.Visibility = rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        InvitesSection.Visibility = arena.Invites.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private const string BarTag = "bar";
    private const string HpBarTag = "hpbar";

    /// <summary>
    /// Sizes a progress bar. The fraction is kept on the fill, and a hidden or not-yet-measured track applies it when it
    /// gets a size, so nothing ever retries in a loop.
    /// </summary>
    private static void SetBar(Border fill, Border track, double fraction)
    {
        fill.Tag = Math.Clamp(double.IsFinite(fraction) ? fraction : 0, 0, 1);
        if (!Equals(track.Tag, BarTag))
        {
            track.Tag = BarTag;
            track.SizeChanged += (_, _) => { if (fill.Tag is double f) fill.Width = Math.Max(0, track.ActualWidth - 2) * f; };
        }
        double width = Math.Max(0, track.ActualWidth - 2);
        if (width > 0) fill.Width = width * (double)fill.Tag;
    }

    /// <summary>
    /// Updates a battle HP bar: player is green/amber/red, enemy is always red, and taking damage flashes a bright
    /// white trail at the old HP before smoothly draining down to the new width.
    /// </summary>
    private static void SetHpBar(Border fill, Border trail, Border track, double fraction, bool isEnemy, bool instant = false)
    {
        double next = Math.Clamp(double.IsFinite(fraction) ? fraction : 0, 0, 1);
        double prev = fill.Tag is double old ? old : next;
        fill.Tag = next;
        fill.Background = isEnemy
            ? (next > .5 ? BrushOf("#EF4444") : next > .25 ? BrushOf("#DC2626") : BrushOf("#B91C1C"))
            : (next > .5 ? BrushOf("#22C55E") : next > .25 ? BrushOf("#F59E0B") : BrushOf("#EF4444"));
        if (!Equals(track.Tag, HpBarTag))
        {
            track.Tag = HpBarTag;
            track.SizeChanged += (_, _) =>
            {
                if (fill.Tag is not double f) return;
                double w = Math.Max(0, track.ActualWidth - 2) * f;
                fill.BeginAnimation(WidthProperty, null);
                trail.BeginAnimation(WidthProperty, null);
                fill.Width = w;
                trail.Width = w;
            };
        }
        double fullWidth = Math.Max(0, track.ActualWidth - 2);
        if (fullWidth <= 0) return;
        double targetW = fullWidth * next;
        double startW = fullWidth * prev;
        if (!instant && next < prev)
        {
            fill.BeginAnimation(WidthProperty, null);
            fill.Width = targetW;
            trail.Background = System.Windows.Media.Brushes.White;
            trail.Opacity = 1;
            double fromW = Math.Max(trail.Width, startW);
            trail.BeginAnimation(WidthProperty, null);
            trail.Width = fromW;
            var drain = new DoubleAnimation(fromW, targetW, TimeSpan.FromMilliseconds(420))
            {
                BeginTime = TimeSpan.FromMilliseconds(160),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            trail.BeginAnimation(WidthProperty, drain);
        }
        else
        {
            fill.BeginAnimation(WidthProperty, null);
            trail.BeginAnimation(WidthProperty, null);
            fill.Width = targetW;
            trail.Width = targetW;
        }
    }

    private static SolidColorBrush BrushOf(string hex)
    {
        var b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        b.Freeze();
        return b;
    }

    private void AddPoint_Click(object sender, RoutedEventArgs e) { if (sender is FrameworkElement { Tag: string key }) arena.AddPoint(key); }
    private void ResetPoints_Click(object sender, RoutedEventArgs e) => arena.ResetPoints();
    private void Daily_Click(object sender, RoutedEventArgs e) { if (arena.ClaimDaily()) lobbyHop = .02; }
    private void Buy_Click(object sender, RoutedEventArgs e) { if (sender is FrameworkElement { Tag: string id }) arena.Buy(id); }

    private void Feed_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: string id }) return;
        if (arena.Feed(id) == PetGame.FeedResult.Fed)
        {
            MyPetVisual.SetMood(PetMood.Eating);
            MyPetVisual.SetHeldFood(id);
            lobbyMoodUntil = DateTime.UtcNow.AddSeconds(2.6);
        }
    }

    private DateTime lobbyMoodUntil;
    private void MyPet_Click(object sender, MouseButtonEventArgs e)
    {
        lobbyHop = .02;
        MyPetVisual.SetMood(PetMood.Laughing);
        lobbyMoodUntil = DateTime.UtcNow.AddSeconds(1.6);
    }

    private void Practice_Click(object sender, RoutedEventArgs e) => arena.StartPractice();

    private async void Challenge_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: FriendRow row }) return;
        if (row.Pending) { await arena.CancelCardChallengeAsync(); await arena.CancelChallengeAsync(); }
        else await arena.ChallengeCardDuelAsync(row.Peer);
        RefreshFriends();
    }

    private async void AcceptInvite_Click(object sender, RoutedEventArgs e) { if (sender is FrameworkElement { Tag: PetInvite i }) await arena.AcceptAsync(i); }
    private async void DeclineInvite_Click(object sender, RoutedEventArgs e) { if (sender is FrameworkElement { Tag: PetInvite i }) await arena.DeclineAsync(i); }

    // ── Battle ───────────────────────────────────────────────────────────

    private void ShowLobby()
    {
        Detach();
        DetachCard();
        CardBar.Visibility = Visibility.Collapsed;
        MoveRow.Visibility = Visibility.Visible;
        LeftChargeTrack.Visibility = RightChargeTrack.Visibility = Visibility.Visible;
        BattlePanel.Visibility = Visibility.Collapsed;
        LobbyPanel.Visibility = Visibility.Visible;
        RefreshLobby();
    }

    private void Detach()
    {
        if (session is null) return;
        session.TurnResolved -= OnTurn;
        session.Ended -= OnEnded;
        session.StateChanged -= OnState;
        session = null;
        pending.Clear();
    }

    private void ShowBattle(PetBattleSession s)
    {
        Detach();
        session = s;
        lastOpponentPeer = s.PeerId;
        s.TurnResolved += OnTurn;
        s.Ended += OnEnded;
        s.StateChanged += OnState;
        LobbyPanel.Visibility = Visibility.Collapsed;
        BattlePanel.Visibility = Visibility.Visible;
        ResultCard.Visibility = Visibility.Collapsed;
        CardBar.Visibility = Visibility.Collapsed;
        MoveRow.Visibility = Visibility.Visible;
        LeftChargeTrack.Visibility = RightChargeTrack.Visibility = Visibility.Visible;
        FxLayer.Children.Clear();

        LeftPet.PetStyle = model.PetAppearance;
        RightPet.PetStyle = PetAppearance.FromWireCode(s.Them.Card.PetCode) ?? PetAppearance.Default;
        foreach (var (pet, fighter) in new[] { (left, s.Me), (right, s.Them) })
        {
            pet.MaxHp = fighter.Stats.MaxHp; pet.ShownHp = fighter.Hp; pet.Fainted = fighter.Hp <= 0;
            pet.Hop = 0; pet.Spin = 0; pet.Lift = 0; pet.Move.X = 0; pet.Move.Y = 0;
            pet.Visual.Opacity = 1;
            pet.TempMood = PetMood.Fired; pet.TempUntil = DateTime.UtcNow.AddSeconds(2);
        }
        LeftName.Text = s.Me.Card.Name.Length > 0 ? s.Me.Card.Name : model.Settings.DisplayName;
        RightName.Text = s.OpponentName;
        LeftLevel.Text = $"Lv {s.Me.Card.Level} · {PetGame.ElementOf(s.Me.Card.Element).Name}";
        RightLevel.Text = $"Lv {s.Them.Card.Level} · {PetGame.ElementOf(s.Them.Card.Element).Name}";
        LogText.Text = s.IsPractice ? $"{s.OpponentName} appears!" : $"{s.OpponentName} is ready!";
        PaintTerrain(s.Them.Card.Element, "", s.Them.Card.Level, false);
        LeaveButton.Visibility = Visibility.Visible;
        StrikeButton.Content = "Strike";
        SpecialButton.Content = PetGame.ElementOf(s.Me.Card.Element).Special;
        GuardButton.Content = "Guard";
        HealButton.Content = "Heal";
        UltimateButton.Content = "Ultimate";
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () => { LayoutStage(); UpdateBars(instant: true); });
        RefreshBattleControls();
        if (s.Finished) ShowResult();
    }

    private void OnTurn(IReadOnlyList<BattleEvent> events)
    {
        pending.Enqueue(events);
        if (!playing) _ = PlayQueueAsync();
    }

    private void OnEnded()
    {
        if (!playing) ShowResult();
    }

    private void OnState() => RefreshBattleControls();

    private void RefreshBattleControls()
    {
        if (session is not { } s || BattlePanel.Visibility != Visibility.Visible) return;
        bool canPick = !playing && !s.Finished && s.MyMove is null;
        int side = s.MySide;
        var me = s.Me;
        StrikeButton.IsEnabled = canPick;
        SpecialButton.IsEnabled = canPick && s.Battle.CanUse(side, "special");
        SpecialButton.Content = me.SpecialCooldown > 0 ? $"{PetGame.ElementOf(me.Card.Element).Special} · {me.SpecialCooldown}" : PetGame.ElementOf(me.Card.Element).Special;
        GuardButton.IsEnabled = canPick;
        HealButton.Visibility = me.Card.Level >= 3 ? Visibility.Visible : Visibility.Collapsed;
        HealButton.IsEnabled = canPick && s.Battle.CanUse(side, "heal");
        HealButton.Content = me.HealCooldown > 0 ? $"Heal · {me.HealCooldown}" : "Heal";
        UltimateButton.Visibility = me.Card.Level >= 6 ? Visibility.Visible : Visibility.Collapsed;
        UltimateButton.IsEnabled = canPick && s.Battle.CanUse(side, "ultimate");
        UltimateButton.Content = me.Charge >= PetGame.MaxCharge ? "Ultimate" : $"Ultimate · {me.Charge}%";
        LeaveButton.IsEnabled = !s.Finished;
        TurnText.Text = s.Finished || playing ? ""
            : s.WaitingForThem ? (s.IsPractice ? "" : $"Waiting for {s.OpponentName}…")
            : $"Turn {s.Battle.Turn + 1} · {s.SecondsLeft}s";
    }

    private async void Move_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: string move } || session is null) return;
        await arena.SubmitMoveAsync(move);
        RefreshBattleControls();
    }

    private async void Leave_Click(object sender, RoutedEventArgs e) => await arena.LeaveAsync();

    private void Done_Click(object sender, RoutedEventArgs e)
    {
        if (cardSession is not null) { arena.DismissCardBattle(); ShowLobby(); return; }
        arena.Dismiss();
        ShowLobby();
    }

    private async void Rematch_Click(object sender, RoutedEventArgs e)
    {
        if (cardSession is not null)
        {
            string? duelPeerId = cardSession.PeerId;
            arena.DismissCardBattle();
            ShowLobby();
            if (duelPeerId is null) arena.StartCardBattle();
            else if (model.Peers.FirstOrDefault(p => p.Id == duelPeerId) is { } peer) await arena.ChallengeCardDuelAsync(peer);
            return;
        }
        string? peerId = lastOpponentPeer;
        arena.Dismiss();
        ShowLobby();
        if (peerId is null) arena.StartPractice();
        else if (model.Peers.FirstOrDefault(p => p.Id == peerId) is { } peer) await arena.ChallengeAsync(peer);
    }

    private void ShowResult()
    {
        if (session is not { Finished: true } s) return;
        RefreshBattleControls();
        UpdateBars();
        ResultTitle.Text = s.Reward is null ? "Battle ended" : s.Outcome > 0 ? "You win!" : s.Outcome < 0 ? "You lost" : "Draw";
        ResultReason.Text = s.EndReason;
        ResultReason.Visibility = s.EndReason.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        ResultReward.Text = s.Reward is { } r ? $"+{r.Xp} XP · +{r.Coins} coins" + (r.LevelsGained > 0 ? $" · Level {arena.Progress.Level}!" : "") : "";
        RematchButton.Visibility = s.IsPractice || model.Peers.Any(p => p.Id == s.PeerId && p.Online) ? Visibility.Visible : Visibility.Collapsed;
        ResultCard.Visibility = Visibility.Visible;
        CenterResult();
        bool won = s.Outcome > 0, lost = s.Outcome < 0 && s.Reward is not null;
        SetTemp(left, won ? PetMood.Laughing : lost ? PetMood.Crying : PetMood.Surprised, 600_000);
        SetTemp(right, won ? PetMood.Crying : lost ? PetMood.Laughing : PetMood.Surprised, 600_000);
        if (won) Confetti();
    }

    private void CenterResult()
    {
        ResultCard.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        Canvas.SetLeft(ResultCard, Math.Max(0, (Stage.ActualWidth - ResultCard.DesiredSize.Width) / 2));
        Canvas.SetTop(ResultCard, Math.Max(4, (Stage.ActualHeight - ResultCard.DesiredSize.Height) / 2));
    }

    // The stage is drawn at a reference size and scaled to fit, so the fight reads the same at any window size.
    private const double StageRefWidth = 560, StageRefHeight = 300;
    private double StageScale => WorldScale.ScaleX > 0 ? WorldScale.ScaleX : 1;
    private double StageW => Stage.ActualWidth / StageScale;
    private double StageH => Stage.ActualHeight / StageScale;

    private void Stage_SizeChanged(object sender, SizeChangedEventArgs e) { LayoutStage(); if (ResultCard.Visibility == Visibility.Visible) CenterResult(); }

    private void LayoutStage()
    {
        if (Stage.ActualWidth <= 0 || Stage.ActualHeight <= 0) return;
        double scale = Math.Clamp(Math.Min(Stage.ActualHeight / StageRefHeight, Stage.ActualWidth / StageRefWidth), .3, 1.5);
        WorldScale.ScaleX = WorldScale.ScaleY = scale;
        double w = StageW, h = StageH;
        double ground = h * .68 + 44;
        Canvas.SetLeft(LeftPet, w * .25 - LeftPet.Width / 2);
        Canvas.SetTop(LeftPet, ground - LeftPet.Height);
        Canvas.SetLeft(RightPet, w * .75 - RightPet.Width / 2);
        Canvas.SetTop(RightPet, ground - RightPet.Height);
        RedrawTerrain();
    }

    private Point CenterOf(StagePet pet)
    {
        // A battle can start before the stage has been measured; place the pets first.
        if (double.IsNaN(Canvas.GetLeft(pet.Visual)) || double.IsNaN(Canvas.GetTop(pet.Visual)))
        {
            Stage.UpdateLayout();
            LayoutStage();
        }
        return new(Canvas.GetLeft(pet.Visual) + pet.Visual.Width / 2 + pet.Move.X, Canvas.GetTop(pet.Visual) + pet.Visual.Height * .62 + pet.Move.Y);
    }

    private void UpdateBars(bool instant = false)
    {
        if (session is not { } s) return;
        foreach (var (pet, fill, trail, track, text, charge, chargeTrack, status, fighter, isEnemy) in new[]
        {
            (left, LeftHpFill, LeftHpTrail, LeftHpTrack, LeftHpText, LeftChargeFill, LeftChargeTrack, LeftStatus, s.Me, false),
            (right, RightHpFill, RightHpTrail, RightHpTrack, RightHpText, RightChargeFill, RightChargeTrack, RightStatus, s.Them, true)
        })
        {
            double frac = pet.MaxHp > 0 ? pet.ShownHp / (double)pet.MaxHp : 0;
            SetHpBar(fill, trail, track, frac, isEnemy, instant);
            text.Text = $"{pet.ShownHp} / {pet.MaxHp}";
            SetBar(charge, chargeTrack, fighter.Charge / (double)PetGame.MaxCharge);
            var tags = new List<string>();
            if (fighter.Burn > 0) tags.Add("Burned");
            if (fighter.Chill > 0) tags.Add("Chilled");
            if (fighter.Soak > 0) tags.Add("Soaked");
            status.Text = string.Join(" · ", tags);
        }
    }

    // ── Playback ─────────────────────────────────────────────────────────

    private async Task PlayQueueAsync()
    {
        playing = true;
        RefreshBattleControls();
        while (pending.Count > 0 && session is not null)
        {
            var events = pending.Dequeue();
            foreach (var ev in events)
            {
                if (session is null) break;
                await PlayEventAsync(ev);
            }
            if (session is { } s)
            {
                left.ShownHp = s.Me.Hp; right.ShownHp = s.Them.Hp;
                UpdateBars();
            }
        }
        playing = false;
        arena.RestartTurnTimer();
        RefreshBattleControls();
        if (session is { Finished: true }) ShowResult();
    }

    private StagePet PetFor(int side) => session is { } s && side == s.MySide ? left : right;
    private PetBattle.Fighter FighterFor(int side) => session!.Battle.Fighters[side];
    private string NameFor(int side) => PetFor(side) == left ? LeftName.Text : RightName.Text;

    private async Task PlayEventAsync(BattleEvent ev)
    {
        if (session is null) return;
        var actor = ev.Actor is 0 or 1 ? PetFor(ev.Actor) : left;
        var target = actor == left ? right : left;
        string element = ev.Actor is 0 or 1 ? FighterFor(ev.Actor).Card.Element : "blaze";
        Color elementColor = (Color)ColorConverter.ConvertFromString(PetGame.ElementOf(element).Color);
        switch (ev.Kind)
        {
            case "guard":
                LogText.Text = $"{NameFor(ev.Actor)} guards.";
                SetTemp(actor, PetMood.Fired, 700);
                Shield(actor, elementColor);
                if (ev.Amount > 0) { actor.ShownHp = Math.Min(actor.MaxHp, actor.ShownHp + ev.Amount); UpdateBars(); }
                await Task.Delay(520);
                break;
            case "heal":
                LogText.Text = $"{NameFor(ev.Actor)} heals {ev.Amount}.";
                SetTemp(actor, PetMood.Happy, 900);
                actor.Hop = .02;
                Sparkles(actor, Color.FromRgb(0x22, 0xC5, 0x5E));
                Popup($"+{ev.Amount}", CenterOf(actor), Color.FromRgb(0x16, 0xA3, 0x4A), 22);
                actor.ShownHp = Math.Min(actor.MaxHp, actor.ShownHp + ev.Amount);
                UpdateBars();
                await Task.Delay(750);
                break;
            case "hit":
            {
                string moveName = PetGame.MoveName(ev.Move, element);
                LogText.Text = $"{NameFor(ev.Actor)} uses {moveName}!";
                double dir = actor == left ? 1 : -1;
                if (ev.Move == "strike")
                {
                    SetTemp(actor, PetMood.Fired, 500);
                    await Lunge(actor, dir * 70, 170);
                }
                else if (ev.Move == "special")
                {
                    SetTemp(actor, PetMood.Fired, 900);
                    actor.Hop = .02;
                    await Task.Delay(140);
                    await Projectile(CenterOf(actor), CenterOf(target), elementColor, 15, 300);
                }
                else
                {
                    SetTemp(actor, PetMood.Fired, 1400);
                    await Spin(actor, 420);
                    await Beam(CenterOf(actor), CenterOf(target), elementColor);
                }
                Impact(target, ev, elementColor);
                target.ShownHp = Math.Max(0, target.ShownHp - ev.Amount);
                UpdateBars();
                if (ev.Move == "strike") await Lunge(actor, 0, 160);
                string extra = ev.TypePercent > 100 ? "Super effective!" : ev.TypePercent < 100 ? "Not very effective…" : "";
                string status = ev.Status switch
                {
                    "burn" => $"{NameFor(1 - ev.Actor)} is burned!",
                    "chill" => $"{NameFor(1 - ev.Actor)} is chilled!",
                    "soak" => $"{NameFor(1 - ev.Actor)} is soaked!",
                    "drain" => $"{NameFor(ev.Actor)} drains energy.",
                    "glow" => $"{NameFor(ev.Actor)} glows.",
                    _ => ""
                };
                if (ev.Status is "drain" or "glow") Sparkles(actor, Color.FromRgb(0x22, 0xC5, 0x5E));
                string line = string.Join(" ", new[] { ev.Crit ? "Critical hit!" : "", extra, status }.Where(t => t.Length > 0));
                if (line.Length > 0) LogText.Text = line;
                await Task.Delay(620);
                break;
            }
            case "burn":
                LogText.Text = $"{NameFor(ev.Actor)} is hurt by the burn.";
                Burst(CenterOf(actor), Color.FromRgb(0xF9, 0x73, 0x16), 10);
                Popup($"-{ev.Amount}", CenterOf(actor), Color.FromRgb(0xEA, 0x58, 0x0C), 18);
                SetTemp(actor, PetMood.Hurt, 500);
                actor.ShownHp = Math.Max(0, actor.ShownHp - ev.Amount);
                UpdateBars();
                await Task.Delay(560);
                break;
            case "faint":
                LogText.Text = $"{NameFor(ev.Actor)} fainted!";
                actor.Fainted = true;
                Fade(actor.Visual, .55, 600);
                await Task.Delay(800);
                break;
            case "timeout":
                LogText.Text = "Time's up!";
                await Task.Delay(500);
                break;
        }
    }

    private void SetTemp(StagePet pet, string mood, int ms) { pet.TempMood = mood; pet.TempUntil = DateTime.UtcNow.AddMilliseconds(ms); }

    private void Impact(StagePet target, BattleEvent ev, Color color)
    {
        SetTemp(target, PetMood.Hurt, 650);
        Burst(CenterOf(target), color, ev.Move == "ultimate" ? 22 : ev.Move == "special" ? 14 : 8);
        Ring(CenterOf(target), color, ev.Move == "ultimate" ? 90 : 52);
        var flash = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromMilliseconds(360) };
        flash.KeyFrames.Add(new LinearDoubleKeyFrame(.35, KeyTime.FromPercent(.25)));
        flash.KeyFrames.Add(new LinearDoubleKeyFrame(1, KeyTime.FromPercent(.5)));
        flash.KeyFrames.Add(new LinearDoubleKeyFrame(.35, KeyTime.FromPercent(.75)));
        flash.KeyFrames.Add(new LinearDoubleKeyFrame(1, KeyTime.FromPercent(1)));
        target.Visual.BeginAnimation(OpacityProperty, flash);
        var shake = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromMilliseconds(320) };
        double k = ev.Move == "ultimate" ? 10 : 6;
        foreach (var (v, t) in new[] { (k, .15), (-k, .35), (k * .6, .55), (-k * .4, .75), (0.0, 1.0) })
            shake.KeyFrames.Add(new LinearDoubleKeyFrame(v, KeyTime.FromPercent(t)));
        var stageShake = new TranslateTransform();
        if (ev.Move == "ultimate" || ev.Crit)
        {
            Stage.RenderTransform = stageShake;
            stageShake.BeginAnimation(TranslateTransform.XProperty, shake);
        }
        var bump = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromMilliseconds(320) };
        foreach (var (v, t) in new[] { (k, .15), (-k, .35), (k * .6, .55), (-k * .4, .75), (0.0, 1.0) })
            bump.KeyFrames.Add(new LinearDoubleKeyFrame(target.Move.X + v, KeyTime.FromPercent(t)));
        target.Move.BeginAnimation(TranslateTransform.XProperty, bump);
        var p = CenterOf(target);
        Popup((ev.Crit ? "CRIT " : "") + "-" + ev.Amount, new Point(p.X, p.Y - 20), ev.Crit ? Color.FromRgb(0xF5, 0x9E, 0x0B) : Color.FromRgb(0xEF, 0x44, 0x44), ev.Crit ? 28 : 22);
        if (ev.TypePercent > 100) Popup("Super effective!", new Point(p.X, p.Y - 56), color, 13);
    }

    private Task Lunge(StagePet pet, double toX, int ms)
    {
        var tcs = new TaskCompletionSource();
        var a = new DoubleAnimation(toX, TimeSpan.FromMilliseconds(ms)) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut }, FillBehavior = FillBehavior.HoldEnd };
        a.Completed += (_, _) => tcs.TrySetResult();
        pet.Move.BeginAnimation(TranslateTransform.XProperty, a);
        return tcs.Task;
    }

    private async Task Spin(StagePet pet, int ms)
    {
        var start = DateTime.UtcNow;
        while ((DateTime.UtcNow - start).TotalMilliseconds < ms)
        {
            double t = (DateTime.UtcNow - start).TotalMilliseconds / ms;
            pet.Spin = 360 * (1 - Math.Pow(1 - t, 2)) * pet.Facing;
            pet.Lift = Math.Sin(t * Math.PI) * 26;
            await Task.Delay(16);
        }
        pet.Spin = 0; pet.Lift = 0;
    }

    private Task Projectile(Point from, Point to, Color color, double radius, int ms)
    {
        var tcs = new TaskCompletionSource();
        var orb = new Ellipse
        {
            Width = radius * 2, Height = radius * 2,
            Fill = new RadialGradientBrush(Colors.White, color) { GradientOrigin = new Point(.35, .35) },
            Effect = new DropShadowEffect { Color = color, BlurRadius = 18, ShadowDepth = 0, Opacity = .9 }
        };
        FxLayer.Children.Add(orb);
        var tx = new DoubleAnimation(from.X - radius, to.X - radius, TimeSpan.FromMilliseconds(ms)) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn } };
        var ty = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromMilliseconds(ms) };
        ty.KeyFrames.Add(new SplineDoubleKeyFrame(Math.Min(from.Y, to.Y) - 50 - radius, KeyTime.FromPercent(.5), new KeySpline(.3, 0, .7, 1)));
        ty.KeyFrames.Add(new SplineDoubleKeyFrame(to.Y - radius, KeyTime.FromPercent(1), new KeySpline(.3, 0, .7, 1)));
        Canvas.SetTop(orb, from.Y - radius);
        tx.Completed += (_, _) => { FxLayer.Children.Remove(orb); tcs.TrySetResult(); };
        orb.BeginAnimation(Canvas.LeftProperty, tx);
        orb.BeginAnimation(Canvas.TopProperty, ty);
        // Sparkle trail.
        for (int i = 1; i <= 4; i++)
        {
            int delay = i * ms / 5;
            Dispatcher.BeginInvoke(async () =>
            {
                await Task.Delay(delay);
                double t = delay / (double)ms;
                Burst(new Point(from.X + (to.X - from.X) * t, from.Y + (to.Y - from.Y) * t - Math.Sin(t * Math.PI) * 50), color, 3);
            });
        }
        return tcs.Task;
    }

    private async Task Beam(Point from, Point to, Color color)
    {
        double len = Math.Abs(to.X - from.X);
        var beam = new Rectangle
        {
            Width = len, Height = 34, RadiusX = 17, RadiusY = 17,
            Fill = new LinearGradientBrush(new GradientStopCollection { new(Color.FromArgb(0, color.R, color.G, color.B), 0), new(color, .25), new(Colors.White, .5), new(color, .75), new(Color.FromArgb(0, color.R, color.G, color.B), 1) }, 90),
            Effect = new DropShadowEffect { Color = color, BlurRadius = 28, ShadowDepth = 0, Opacity = 1 },
            RenderTransformOrigin = new Point(from.X < to.X ? 0 : 1, .5),
            RenderTransform = new ScaleTransform(0, 1)
        };
        Canvas.SetLeft(beam, Math.Min(from.X, to.X));
        Canvas.SetTop(beam, (from.Y + to.Y) / 2 - 17);
        FxLayer.Children.Add(beam);
        ((ScaleTransform)beam.RenderTransform).BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(220)));
        await Task.Delay(260);
        var fade = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(380));
        fade.Completed += (_, _) => FxLayer.Children.Remove(beam);
        beam.BeginAnimation(OpacityProperty, fade);
        ((ScaleTransform)beam.RenderTransform).BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(1, 1.8, TimeSpan.FromMilliseconds(380)));
    }

    private void Burst(Point at, Color color, int count)
    {
        var rnd = Random.Shared;
        for (int i = 0; i < count; i++)
        {
            double r = 2.5 + rnd.NextDouble() * 4;
            var dot = new Ellipse { Width = r * 2, Height = r * 2, Fill = new SolidColorBrush(i % 3 == 0 ? Colors.White : color) };
            Canvas.SetLeft(dot, at.X - r); Canvas.SetTop(dot, at.Y - r);
            FxLayer.Children.Add(dot);
            double ang = rnd.NextDouble() * Math.PI * 2, dist = 30 + rnd.NextDouble() * 50;
            var dur = TimeSpan.FromMilliseconds(420 + rnd.Next(240));
            var ax = new DoubleAnimation(at.X - r + Math.Cos(ang) * dist, dur) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } };
            var ay = new DoubleAnimation(at.Y - r + Math.Sin(ang) * dist + 14, dur) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } };
            var fade = new DoubleAnimation(1, 0, dur);
            fade.Completed += (_, _) => FxLayer.Children.Remove(dot);
            dot.BeginAnimation(Canvas.LeftProperty, ax);
            dot.BeginAnimation(Canvas.TopProperty, ay);
            dot.BeginAnimation(OpacityProperty, fade);
        }
    }

    private void Ring(Point at, Color color, double size)
    {
        var ring = new Ellipse { Width = size, Height = size, Stroke = new SolidColorBrush(color), StrokeThickness = 4, RenderTransformOrigin = new Point(.5, .5), RenderTransform = new ScaleTransform(.2, .2) };
        Canvas.SetLeft(ring, at.X - size / 2); Canvas.SetTop(ring, at.Y - size / 2);
        FxLayer.Children.Add(ring);
        var grow = new DoubleAnimation(.2, 1.2, TimeSpan.FromMilliseconds(380)) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } };
        ((ScaleTransform)ring.RenderTransform).BeginAnimation(ScaleTransform.ScaleXProperty, grow);
        ((ScaleTransform)ring.RenderTransform).BeginAnimation(ScaleTransform.ScaleYProperty, grow);
        var fade = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(380));
        fade.Completed += (_, _) => FxLayer.Children.Remove(ring);
        ring.BeginAnimation(OpacityProperty, fade);
    }

    private void Shield(StagePet pet, Color color)
    {
        var at = CenterOf(pet);
        var bubble = new Ellipse
        {
            Width = 150, Height = 130, StrokeThickness = 3, Stroke = new SolidColorBrush(color),
            Fill = new RadialGradientBrush(Color.FromArgb(10, color.R, color.G, color.B), Color.FromArgb(90, color.R, color.G, color.B)),
            Effect = new DropShadowEffect { Color = color, BlurRadius = 16, ShadowDepth = 0, Opacity = .8 }
        };
        Canvas.SetLeft(bubble, at.X - 75); Canvas.SetTop(bubble, at.Y - 72);
        FxLayer.Children.Add(bubble);
        var anim = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromMilliseconds(1300) };
        anim.KeyFrames.Add(new LinearDoubleKeyFrame(1, KeyTime.FromPercent(.15)));
        anim.KeyFrames.Add(new LinearDoubleKeyFrame(1, KeyTime.FromPercent(.7)));
        anim.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromPercent(1)));
        anim.Completed += (_, _) => FxLayer.Children.Remove(bubble);
        bubble.Opacity = 0;
        bubble.BeginAnimation(OpacityProperty, anim);
    }

    private void Sparkles(StagePet pet, Color color)
    {
        var at = CenterOf(pet);
        var rnd = Random.Shared;
        for (int i = 0; i < 9; i++)
        {
            var plus = new TextBlock { Text = "+", FontSize = 14 + rnd.Next(10), FontWeight = FontWeights.Black, Foreground = new SolidColorBrush(color) };
            double x = at.X - 50 + rnd.NextDouble() * 100, y = at.Y + 10 - rnd.NextDouble() * 30;
            Canvas.SetLeft(plus, x); Canvas.SetTop(plus, y);
            FxLayer.Children.Add(plus);
            var dur = TimeSpan.FromMilliseconds(700 + rnd.Next(300));
            plus.BeginAnimation(Canvas.TopProperty, new DoubleAnimation(y - 50 - rnd.Next(30), dur));
            var fade = new DoubleAnimation(1, 0, dur) { BeginTime = TimeSpan.FromMilliseconds(rnd.Next(150)) };
            fade.Completed += (_, _) => FxLayer.Children.Remove(plus);
            plus.BeginAnimation(OpacityProperty, fade);
        }
    }

    private void Popup(string text, Point at, Color color, double size)
    {
        var label = new TextBlock
        {
            Text = text, FontSize = size, FontWeight = FontWeights.Black, Foreground = new SolidColorBrush(color),
            Effect = new DropShadowEffect { Color = Colors.White, BlurRadius = 4, ShadowDepth = 0, Opacity = 1 },
            RenderTransformOrigin = new Point(.5, .5), RenderTransform = new ScaleTransform(.4, .4)
        };
        label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        Canvas.SetLeft(label, at.X - label.DesiredSize.Width / 2);
        Canvas.SetTop(label, at.Y - label.DesiredSize.Height / 2);
        FxLayer.Children.Add(label);
        var pop = new DoubleAnimation(.4, 1, TimeSpan.FromMilliseconds(220)) { EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = .8 } };
        ((ScaleTransform)label.RenderTransform).BeginAnimation(ScaleTransform.ScaleXProperty, pop);
        ((ScaleTransform)label.RenderTransform).BeginAnimation(ScaleTransform.ScaleYProperty, pop);
        label.BeginAnimation(Canvas.TopProperty, new DoubleAnimation(at.Y - label.DesiredSize.Height / 2 - 42, TimeSpan.FromMilliseconds(1000)) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } });
        var fade = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(350)) { BeginTime = TimeSpan.FromMilliseconds(700) };
        fade.Completed += (_, _) => FxLayer.Children.Remove(label);
        label.BeginAnimation(OpacityProperty, fade);
    }

    private static void Fade(UIElement element, double to, int ms) =>
        element.BeginAnimation(OpacityProperty, new DoubleAnimation(to, TimeSpan.FromMilliseconds(ms)) { FillBehavior = FillBehavior.HoldEnd });

    private void Confetti()
    {
        var rnd = Random.Shared;
        Color[] colors = [Color.FromRgb(0xF4, 0x72, 0xB6), Color.FromRgb(0xFA, 0xCC, 0x15), Color.FromRgb(0x34, 0xD3, 0x99), Color.FromRgb(0x60, 0xA5, 0xFA), Color.FromRgb(0xA7, 0x8B, 0xFA)];
        for (int i = 0; i < 46; i++)
        {
            var bit = new Rectangle { Width = 6, Height = 10, Fill = new SolidColorBrush(colors[i % colors.Length]), RenderTransformOrigin = new Point(.5, .5), RenderTransform = new RotateTransform(rnd.Next(360)) };
            double x = rnd.NextDouble() * StageW;
            Canvas.SetLeft(bit, x); Canvas.SetTop(bit, -12);
            FxLayer.Children.Add(bit);
            var dur = TimeSpan.FromMilliseconds(1400 + rnd.Next(1200));
            var fall = new DoubleAnimation(-12, StageH + 12, dur) { BeginTime = TimeSpan.FromMilliseconds(rnd.Next(500)) };
            fall.Completed += (_, _) => FxLayer.Children.Remove(bit);
            bit.BeginAnimation(Canvas.TopProperty, fall);
            bit.BeginAnimation(Canvas.LeftProperty, new DoubleAnimation(x, x + rnd.Next(-60, 60), dur));
            ((RotateTransform)bit.RenderTransform).BeginAnimation(RotateTransform.AngleProperty, new DoubleAnimation(0, rnd.Next(360, 1080), dur));
        }
    }

    // ── Animation tick ───────────────────────────────────────────────────

    private void Animate()
    {
        const double dt = .033;
        breath = (breath + dt * 2.3) % (Math.PI * 20);
        blinkClock += dt;
        if (blink == 0 && blinkClock >= nextBlink) { blinkClock = 0; blinkDown = true; blink = .25; nextBlink = 2.4 + Random.Shared.NextDouble() * 2.6; }
        else if (blink > 0)
        {
            blink += (blinkDown ? 1 : -1) * dt * 11;
            if (blink >= 1) { blink = 1; blinkDown = false; } else if (blink <= 0) blink = 0;
        }

        if (LobbyPanel.Visibility == Visibility.Visible)
        {
            if (lobbyHop > 0) { lobbyHop += dt * 2.1; if (lobbyHop >= 1) lobbyHop = 0; }
            if (DateTime.UtcNow > lobbyMoodUntil)
            {
                MyPetVisual.SetMood("");
                MyPetVisual.SetHeldFood("");
            }
            MyPetVisual.SetMotionState(breath, blink, lobbyHop, 0, 1, false, false, false, true, false, "chat");
            return;
        }

        foreach (var pet in new[] { left, right })
        {
            if (pet.Hop > 0) { pet.Hop += dt * 2.2; if (pet.Hop >= 1) pet.Hop = 0; }
            string mood = pet.Fainted ? PetMood.Fainted
                : DateTime.UtcNow < pet.TempUntil ? pet.TempMood
                : pet.MaxHp > 0 && pet.ShownHp * 100 / pet.MaxHp < 30 ? PetMood.Nervous
                : "";
            if (pet.Fainted && DateTime.UtcNow < pet.TempUntil && pet.TempMood == PetMood.Crying) mood = PetMood.Crying;
            mood = StatusMood(pet, mood);
            pet.Visual.SetMood(mood);
            pet.Visual.SetPose(pet.Spin, pet.Lift);
            pet.Visual.SetMotionState(breath, blink, pet.Hop, 0, pet.Facing, false, false, false, true, false, "chat");
        }
        StatusAuras();
    }


}
