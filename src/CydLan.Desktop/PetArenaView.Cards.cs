using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using CydLan.Core;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using Point = System.Windows.Point;
using Brushes = System.Windows.Media.Brushes;
using Size = System.Windows.Size;

namespace CydLan.Desktop;

/// <summary>Deck, collection, world, daily shop and the card battle view.</summary>
public partial class PetArenaView
{
    private sealed record DeckTile(string Id, string Name, int Cost, ImageSource? Art, Brush KindBrush, Brush RarityBrush, string Tip)
    {
        public string RemoveName => $"Remove {Name} from deck";
    }

    private sealed record CollectionRow(string Id, string Name, string Text, int Cost, int Owned, int Used, int SellPrice, ImageSource? Art, Brush KindBrush, Brush RarityBrush, bool CanAdd, bool CanSell)
    {
        public string CountText => $"{Used}/{Owned}";
        public string AddName => $"Add {Name} to deck";
        public string SellText => $"Sell · {SellPrice}";
        public string SellName => $"Sell {Name} for {SellPrice} coins";
    }

    private sealed record FightRow(string Name, string Detail, string Tag, PetAppearance Look, Brush Edge, double Fade);

    private sealed record ShopRow(int Slot, string CardId, string Name, string Detail, int Price, ImageSource? Art, Brush KindBrush, Brush RarityBrush, bool CanBuy, bool Bought)
    {
        public string PriceText => Bought ? "Bought" : Price.ToString();
        public string BuyName => $"Buy {Name} for {Price} coins";
    }

    private static readonly (string Name, Func<PetCard, bool> Match)[] CollectionFilters =
    [
        ("All", _ => true), ("Neutral", c => c.Element.Length == 0),
        ("Blaze", c => c.Element == "blaze"), ("Tide", c => c.Element == "tide"), ("Leaf", c => c.Element == "leaf"),
        ("Volt", c => c.Element == "volt"), ("Frost", c => c.Element == "frost"), ("Shadow", c => c.Element == "shadow"), ("Light", c => c.Element == "light")
    ];

    private CardBattleSession? cardSession;
    /// <summary>Picked hand indexes in the order they were picked; that is the order they play.</summary>
    private readonly List<int> picked = [];
    private readonly Queue<(IReadOnlyList<CardEvent> Events, IReadOnlyList<string> Mine, IReadOnlyList<string> Theirs)> cardQueue = new();
    private bool cardPlaying;
    /// <summary>Set when a fresh hand should slide in from below.</summary>
    private bool dealPending;
    private int toggledIndex = -1;
    private readonly List<BoardCard> board = [];
    private Border? dragSource;
    private FrameworkElement? dragGhost;
    private int dragIndex = -1;
    private bool dragFromQueue, dragMoved;
    private Point dragStart, dragOffset;

    /// <summary>A played card shown above its pet while the round plays out.</summary>
    private sealed class BoardCard
    {
        public required int Side { get; init; }
        public required string Id { get; init; }
        public required Border Face { get; init; }
        public required ScaleTransform Scale { get; init; }
        public bool Used;
    }

    internal static Brush RarityBrush(string rarity) => rarity switch
    {
        PetCards.Legendary => Frozen("#EAB308"),
        PetCards.Epic => Frozen("#A855F7"),
        PetCards.Rare => Frozen("#3B82F6"),
        _ => Frozen("#94A3B8")
    };

    private static string RarityName(string rarity) => char.ToUpperInvariant(rarity[0]) + rarity[1..];
    private static string ElementName(PetCard c) => c.Element.Length == 0 ? "Neutral" : PetGame.ElementOf(c.Element).Name;

    internal static Brush KindBrush(string kind) => kind switch
    {
        "attack" => Frozen("#EF4444"),
        "defend" => Frozen("#3B82F6"),
        "heal" => Frozen("#22C55E"),
        "counter" => Frozen("#14B8A6"),
        _ => Frozen("#F59E0B")
    };

    private static SolidColorBrush Frozen(string hex)
    {
        var b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        b.Freeze();
        return b;
    }

    // ── Lobby: deck, collection, world, shop ─────────────────────────────

    /// <summary>Sky and ground colours for a stage's biome element.</summary>
    private static readonly Dictionary<string, (string SkyTop, string SkyBottom, string GroundTop, string GroundBottom)> Biomes = new()
    {
        ["tide"] = ("#CDEBFF", "#EEF8FF", "#8FD3E8", "#5FB4D3"),
        ["blaze"] = ("#FFD9B8", "#FFF1E3", "#D9A27A", "#B87A52"),
        ["frost"] = ("#DDEFFF", "#F6FBFF", "#EAF4FB", "#C9DDEB"),
        ["leaf"] = ("#DCEBFF", "#F4F9FF", "#BFE3B4", "#9FD196"),
        ["light"] = ("#FFF3C4", "#FFFBEA", "#F1E2A8", "#DCC77E"),
        ["shadow"] = ("#3B3355", "#6B5F8A", "#4A4062", "#2F2843"),
        ["volt"] = ("#E7E2FF", "#F7F5FF", "#C9C3D8", "#A59DBA")
    };

    private void PaintBiome(string element)
    {
        if (!Biomes.TryGetValue(element, out var b)) b = Biomes["leaf"];
        static Color C(string hex) => (Color)ColorConverter.ConvertFromString(hex);
        SkyTop.Color = C(b.SkyTop); SkyBottom.Color = C(b.SkyBottom); GroundTop.Color = C(b.GroundTop); GroundBottom.Color = C(b.GroundBottom);
    }

    private void RefreshCards()
    {
        var p = arena.Progress;
        bool busy = arena.CurrentCard is { Finished: false };

        DeckTitle.Text = $"Deck · {p.Deck.Count}/{PetCards.DeckSize}";
        DeckTabButton.Content = PetCards.DeckReady(p) ? "Deck" : $"Deck · {p.Deck.Count}/{PetCards.DeckSize}";
        DeckList.ItemsSource = p.Deck.Select(PetCards.CardOf).OrderBy(c => c.Cost).ThenBy(c => c.Name)
            .Select(c => new DeckTile(c.Id, c.Name, c.Cost, CardArt.For(c), KindBrush(c.Kind), RarityBrush(c.Rarity), $"{c.Name} · {c.Text}"))
            .ToList();

        var filter = CollectionFilters[Math.Clamp(CollectionFilter.SelectedIndex, 0, CollectionFilters.Length - 1)].Match;
        var owned = p.Cards.Select(kv => PetCards.CardOf(kv.Key)).Where(filter).ToList();
        int totalOwned = PetCards.TotalOwned(p);
        CollectionTitle.Text = $"Collection · {p.Cards.Count}/{PetCards.Cards.Count} · {totalOwned} cards";
        CollectionList.ItemsSource = owned
            .OrderByDescending(c => PetCards.RarityRank(c.Rarity)).ThenBy(c => c.Element).ThenBy(c => c.Cost).ThenBy(c => c.Name)
            .Select(c => new CollectionRow(c.Id, c.Name, c.Text, c.Cost, PetCards.Owned(p, c.Id), PetCards.InDeck(p, c.Id),
                PetCards.SellPrice(c), CardArt.For(c), KindBrush(c.Kind), RarityBrush(c.Rarity),
                !busy && PetCards.CanAddToDeck(p, c.Id), !busy && PetCards.CanSell(p, c.Id)))
            .ToList();

        var stage = arena.CurrentStage;
        var biome = PetGame.ElementOf(stage.Element);
        StageNumberText.Text = $"Stage {stage.Number}";
        StageNameText.Text = stage.Name;
        StageBadgeText.Text = biome.Name;
        StageBadge.Background = BrushOf(biome.Color);
        var accent = (Brush)FindResource("Accent");
        var line = (Brush)FindResource("Line");
        StageFights.ItemsSource = stage.Fights.Select(f =>
        {
            var look = PetArena.EnemyLook(f, out string kind);
            bool done = f.Index < p.StageFight, next = f.Index == p.StageFight;
            return new FightRow($"{f.Name} {kind}", $"{PetGame.ElementOf(f.Element).Name}{(f.Boss ? " · Boss" : "")}",
                done ? "Cleared" : next ? "Next" : "", look, next ? accent : line, done ? .55 : 1);
        }).ToList();
        bool ready = PetCards.DeckReady(p);
        PracticeButton.IsEnabled = !busy && ready;
        int missing = PetCards.DeckSize - p.Deck.Count;
        string missingText = $"Deck needs {missing} more card{(missing == 1 ? "" : "s")} to fight";
        DeckNeededText.Text = missingText;
        DeckNeededButton.Visibility = ready ? Visibility.Collapsed : Visibility.Visible;

        var tourney = arena.CurrentTournament;
        var tourneyElement = PetGame.ElementOf(tourney.Element);
        int currentRound = p.TournamentSlot == tourney.Slot ? p.TournamentRound : 0;
        bool clearedSlot = p.TournamentWonSlot == tourney.Slot;
        TourneySlotText.Text = clearedSlot
            ? $"Round {currentRound + 1}/{PetTournament.RoundsPerTournament} · Cleared · Wins: {p.TournamentWins}"
            : $"Round {currentRound + 1}/{PetTournament.RoundsPerTournament} · Wins: {p.TournamentWins}";
        TourneyNameText.Text = tourney.Name;
        TourneyBadgeText.Text = tourneyElement.Name;
        TourneyBadge.Background = BrushOf(tourneyElement.Color);
        RefreshTournamentTimer();
        TournamentFights.ItemsSource = tourney.Bracket.Select(c =>
        {
            var look = PetArena.ContenderLook(c, out string kind);
            bool done = c.Round < currentRound, next = c.Round == currentRound;
            return new FightRow($"{c.RoundName} · {c.Name} {kind}", PetGame.ElementOf(c.Element).Name,
                done ? "Cleared" : next ? "Next" : "", look, next ? accent : line, done ? .55 : 1);
        }).ToList();
        TournamentFightButton.IsEnabled = !busy && ready;
        TournamentDeckNeededText.Text = missingText;
        TournamentDeckNeededButton.Visibility = ready ? Visibility.Collapsed : Visibility.Visible;

        EnsureRaffleWheel();
        SpinButton.Content = $"Spin · {PetCards.SpinPrice}";
        SpinButton.IsEnabled = !busy && !raffleSpinning && p.Coins >= PetCards.SpinPrice;

        ShopList.ItemsSource = arena.ShopOffers.Select(o => new ShopRow(o.Slot, o.Card.Id, o.Card.Name,
            $"{RarityName(o.Card.Rarity)} · {ElementName(o.Card)} · {o.Card.Text}", o.Price, CardArt.For(o.Card), KindBrush(o.Card.Kind), RarityBrush(o.Card.Rarity),
            !o.Bought && p.Coins >= o.Price && PetCards.Owned(p, o.Card.Id) < PetCards.MaxOwned, o.Bought)).ToList();

        MyPetVisual.PetStyle = arena.BattleLook;
    }

    private void RefreshTournamentTimer()
    {
        if (TourneyTimerText is null) return;
        var left = PetTournament.TimeRemaining(DateTime.Now);
        int mins = Math.Max(1, (int)Math.Ceiling(left.TotalMinutes));
        TourneyTimerText.Text = $"{mins}m left";
    }

    private void LobbyTab_Checked(object sender, RoutedEventArgs e)
    {
        if (WorldTab is null) return;
        WorldTab.Visibility = WorldTabButton.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        TournamentTab.Visibility = TournamentTabButton.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        PetTab.Visibility = PetTabButton.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        DeckTab.Visibility = DeckTabButton.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        ShopTab.Visibility = ShopTabButton.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
    }

    private void DeckNeeded_Click(object sender, RoutedEventArgs e) => DeckTabButton.IsChecked = true;

    private void DeckCard_Click(object sender, RoutedEventArgs e) { if (sender is FrameworkElement { Tag: string id }) arena.RemoveFromDeck(id); }
    private void AddToDeck_Click(object sender, RoutedEventArgs e) { if (sender is FrameworkElement { Tag: string id }) arena.AddToDeck(id); }
    private void SellCard_Click(object sender, RoutedEventArgs e) { if (sender is FrameworkElement { Tag: string id }) arena.SellCard(id); }
    private void BuyCard_Click(object sender, RoutedEventArgs e) { if (sender is FrameworkElement { Tag: int slot }) arena.BuyCard(slot); }
    private void CollectionFilter_SelectionChanged(object sender, SelectionChangedEventArgs e) { if (IsLoaded) RefreshCards(); }

    private static readonly string[] WheelSlots =
    [
        PetCards.Common, PetCards.Rare, PetCards.Common, PetCards.Epic,
        PetCards.Common, PetCards.Rare, PetCards.Common, PetCards.Legendary,
        PetCards.Common, PetCards.Rare, PetCards.Common, PetCards.Epic
    ];
    private bool raffleBuilt, raffleSpinning;

    private void EnsureRaffleWheel()
    {
        if (raffleBuilt || RaffleWheel is null) return;
        raffleBuilt = true;
        const double cx = 58, cy = 58, r = 54;
        for (int i = 0; i < WheelSlots.Length; i++)
        {
            double a0 = (i * 30 - 90) * Math.PI / 180;
            double a1 = ((i + 1) * 30 - 90) * Math.PI / 180;
            var p0 = new Point(cx + r * Math.Cos(a0), cy + r * Math.Sin(a0));
            var p1 = new Point(cx + r * Math.Cos(a1), cy + r * Math.Sin(a1));
            var seg = new PathFigure(new Point(cx, cy), [new LineSegment(p0, true), new ArcSegment(p1, new Size(r, r), 0, false, SweepDirection.Clockwise, true)], true);
            var wedge = new System.Windows.Shapes.Path
            {
                Data = new PathGeometry([seg]),
                Fill = RarityBrush(WheelSlots[i]),
                Stroke = Brushes.White,
                StrokeThickness = 1.2
            };
            RaffleWheel.Children.Add(wedge);
        }
        var rim = new System.Windows.Shapes.Ellipse { Width = r * 2, Height = r * 2, Stroke = Brushes.White, StrokeThickness = 2 };
        Canvas.SetLeft(rim, cx - r); Canvas.SetTop(rim, cy - r);
        RaffleWheel.Children.Add(rim);
        var hub = new System.Windows.Shapes.Ellipse { Width = 20, Height = 20, Fill = Brushes.White, Stroke = Frozen("#334155"), StrokeThickness = 2 };
        Canvas.SetLeft(hub, cx - 10); Canvas.SetTop(hub, cy - 10);
        RaffleWheel.Children.Add(hub);
    }

    private void SpinRaffle_Click(object sender, RoutedEventArgs e)
    {
        if (raffleSpinning) return;
        if (arena.SpinRaffle() is not { } result) return;
        EnsureRaffleWheel();
        var matching = Enumerable.Range(0, WheelSlots.Length).Where(i => WheelSlots[i] == result.Card.Rarity).ToArray();
        int slot = matching[Random.Shared.Next(matching.Length)];
        double targetMod = (360 - (slot * 30 + 15)) % 360;
        double current = RaffleWheelRotate.Angle;
        double currentMod = ((current % 360) + 360) % 360;
        double delta = (targetMod - currentMod + 360) % 360;
        double endAngle = current + 360 * 3 + delta;

        raffleSpinning = true;
        SpinButton.IsEnabled = false;
        RaffleResultCard.Visibility = Visibility.Collapsed;

        var anim = new DoubleAnimation(current, endAngle, TimeSpan.FromMilliseconds(950))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        anim.Completed += (_, _) =>
        {
            raffleSpinning = false;
            ShowRaffleResult(result);
            RefreshCards();
        };
        RaffleWheelRotate.BeginAnimation(RotateTransform.AngleProperty, anim);
    }

    private void ShowRaffleResult(PetCards.RaffleResult result)
    {
        var c = result.Card;
        RaffleResultCard.Tag = c.Id;
        RaffleResultCard.BorderBrush = RarityBrush(c.Rarity);
        RaffleCardBg.Background = KindBrush(c.Kind);
        RaffleCardArt.Source = CardArt.For(c);
        RaffleCardCost.Text = c.Cost.ToString();
        RaffleCardName.Text = c.Name;
        RaffleCardDetail.Text = $"{RarityName(c.Rarity)} · {ElementName(c)} · {c.Text}";
        if (result.DuplicateCoins > 0)
        {
            RaffleCardExtra.Text = $"+{result.DuplicateCoins} coins";
            RaffleCardExtra.Visibility = Visibility.Visible;
        }
        else RaffleCardExtra.Visibility = Visibility.Collapsed;
        RaffleResultCard.Visibility = Visibility.Visible;
    }

    private void Reroll_Click(object sender, RoutedEventArgs e)
    {
        if (!arena.Reroll()) return;
        lobbyHop = .02;
        MyPetVisual.SetMood(PetMood.Surprised);
        lobbyMoodUntil = DateTime.UtcNow.AddSeconds(1.4);
    }

    private void CardBattle_Click(object sender, RoutedEventArgs e) => arena.StartCardBattle();
    private void TournamentBattle_Click(object sender, RoutedEventArgs e) => arena.StartTournamentBattle();

    // ── Battle view ──────────────────────────────────────────────────────

    private void ShowCardBattle(CardBattleSession s)
    {
        DetachCard();
        Detach();
        DetachTrade();
        cardSession = s;
        s.RoundPlayed += OnCardRound;
        s.Ended += OnCardEnded;
        s.StateChanged += OnCardState;
        picked.Clear();
        board.Clear();
        dealPending = true;
        LobbyPanel.Visibility = Visibility.Collapsed;
        TradePanel.Visibility = Visibility.Collapsed;
        BattlePanel.Visibility = Visibility.Visible;
        CardBar.Visibility = Visibility.Visible;
        MoveRow.Visibility = Visibility.Collapsed;
        LeftChargeTrack.Visibility = RightChargeTrack.Visibility = Visibility.Collapsed;
        ResultCard.Visibility = Visibility.Collapsed;
        FxLayer.Children.Clear();

        LeftPet.PetStyle = s.MyLook;
        RightPet.PetStyle = s.TheirLook;
        foreach (var (pet, side) in new[] { (left, s.Me), (right, s.Them) })
        {
            pet.MaxHp = side.Loadout.MaxHp; pet.ShownHp = side.Hp; pet.Fainted = side.Hp <= 0;
            pet.Hop = 0; pet.Spin = 0; pet.Lift = 0; pet.Move.X = 0; pet.Move.Y = 0;
            pet.Move.BeginAnimation(TranslateTransform.XProperty, null);
            pet.Visual.BeginAnimation(OpacityProperty, null);
            pet.Visual.Opacity = 1;
            pet.TempMood = PetMood.Fired; pet.TempUntil = DateTime.UtcNow.AddSeconds(2);
        }
        LeftName.Text = model.Settings.DisplayName;
        RightName.Text = s.OpponentName;
        LeftLevel.Text = $"Lv {s.Me.Loadout.Level} · {PetGame.ElementOf(s.Me.Loadout.Element).Name}";
        if (s.Stage is { } stage && s.Enemy is { } enemy)
        {
            RightLevel.Text = $"{(enemy.Boss ? "Boss" : $"Stage {stage.Number}")} · {PetGame.ElementOf(s.Them.Loadout.Element).Name}";
            LogText.Text = $"{stage.Name}: {s.OpponentName} appears!";
            PaintTerrain(stage.Element, stage.Name, stage.Number, enemy.Boss);
        }
        else if (s.Tournament is { } tev && s.TournamentContender is { } tc)
        {
            RightLevel.Text = $"{tc.RoundName} · {PetGame.ElementOf(s.Them.Loadout.Element).Name}";
            LogText.Text = $"{tev.Name}: {s.OpponentName} steps up!";
            PaintTerrain(tev.Element, tev.Name, (tc.Round + 1) * 5, tc.Round == PetTournament.RoundsPerTournament - 1);
        }
        else
        {
            RightLevel.Text = $"Lv {s.Them.Loadout.Level} · {PetGame.ElementOf(s.Them.Loadout.Element).Name}";
            LogText.Text = $"{s.OpponentName} is ready!";
            PaintTerrain(s.Them.Loadout.Element, "", s.Them.Loadout.Level, false);
        }
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () => { LayoutStage(); UpdateCardBars(instant: true); });
        RefreshHand();
        if (s.Finished) ShowCardResult();
    }

    private void DetachCard()
    {
        CancelDrag();
        if (cardDetailPopup is not null) cardDetailPopup.IsOpen = false;
        if (cardSession is null) return;
        cardSession.RoundPlayed -= OnCardRound;
        cardSession.Ended -= OnCardEnded;
        cardSession.StateChanged -= OnCardState;
        cardSession = null;
        cardQueue.Clear();
        picked.Clear();
        QueuePanel.Children.Clear();
        QueueTray.Visibility = Visibility.Collapsed;
    }

    private void OnCardState()
    {
        if (cardSession is not { } s || cardPlaying) return;
        if (s.WaitingForThem)
        {
            LogText.Text = $"Waiting for {s.OpponentName}…";
            FightButton.IsEnabled = false;
        }
        else if (!s.Finished && s.IsLan && s.MyPicks is null)
        {
            FightButton.IsEnabled = true;
        }
    }

    private int PickedCost => cardSession is { } s ? picked.Where(i => i < s.Me.Hand.Count).Sum(i => PetCards.CardOf(s.Me.Hand[i]).Cost) : 0;

    private void RefreshHand()
    {
        CancelDrag();
        HandPanel.Children.Clear();
        QueuePanel.Children.Clear();
        // The row keeps its height while the hand is put away, so the stage never jumps.
        HandPanel.MinHeight = CardHeight + CardLift;
        if (cardSession is not { } s) { QueueTray.Visibility = Visibility.Collapsed; return; }
        int left = s.Me.Energy - PickedCost;
        EnergyText.Text = left.ToString();
        bool canPlay = !cardPlaying && !s.Finished && !s.WaitingForThem;
        // While a round plays out or waits on a LAN opponent, the hand and top queue are put away; the next hand is dealt when it ends.
        if (!cardPlaying && !s.WaitingForThem)
        {
            for (int q = 0; q < picked.Count; q++)
            {
                int handIdx = picked[q];
                if (handIdx < 0 || handIdx >= s.Me.Hand.Count) continue;
                QueuePanel.Children.Add(QueueCardFace(PetCards.CardOf(s.Me.Hand[handIdx]), handIdx, q + 1, canPlay));
            }
            SetQueueTrayStyle(dragging: false, hovering: false);
            QueueTray.Visibility = QueuePanel.Children.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

            bool deal = dealPending;
            dealPending = false;
            int slot = 0;
            for (int i = 0; i < s.Me.Hand.Count; i++)
            {
                if (picked.Contains(i)) continue;
                var card = PetCards.CardOf(s.Me.Hand[i]);
                bool affordable = card.Cost <= left;
                HandPanel.Children.Add(CardFace(card, i, canPlay && affordable, deal, slot++));
            }
        }
        else QueueTray.Visibility = Visibility.Collapsed;
        toggledIndex = -1;
        FitHand();
        FitQueue();
        FightButton.IsEnabled = canPlay;
        FightButton.Content = picked.Count == 0 ? "Pass" : "Fight";
        CardLeaveButton.IsEnabled = !s.Finished;
    }

    private static Color ToneOf(PetCard card) =>
        card.Element.Length > 0 ? (Color)ColorConverter.ConvertFromString(PetGame.ElementOf(card.Element).Color) : Color.FromRgb(0x94, 0xA3, 0xB8);

    /// <summary>The element-tinted art window with the pixel icon, shared by hand cards and board cards.</summary>
    private static Border ArtWindow(PetCard card, double height, double icon)
    {
        var tone = ToneOf(card);
        var window = new Border
        {
            Height = height, CornerRadius = new CornerRadius(7),
            Background = new LinearGradientBrush(Color.FromArgb(0x66, tone.R, tone.G, tone.B), Color.FromArgb(0x14, tone.R, tone.G, tone.B), 90),
            BorderBrush = new SolidColorBrush(Color.FromArgb(0x55, tone.R, tone.G, tone.B)), BorderThickness = new Thickness(1)
        };
        if (CardArt.Image(card, icon) is { } art)
        {
            art.HorizontalAlignment = System.Windows.HorizontalAlignment.Center;
            art.VerticalAlignment = VerticalAlignment.Center;
            window.Child = art;
        }
        return window;
    }

    private static Border Gem(string text, double size, Brush? background = null)
    {
        var gem = new Border
        {
            Width = size, Height = size, CornerRadius = new CornerRadius(size / 2), BorderThickness = new Thickness(1.5), BorderBrush = Brushes.White,
            Child = new TextBlock { Text = text, FontWeight = FontWeights.Black, FontSize = size * .52, HorizontalAlignment = System.Windows.HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }
        };
        if (background is null)
        {
            gem.SetResourceReference(Border.BackgroundProperty, "Accent");
            ((TextBlock)gem.Child).SetResourceReference(TextBlock.ForegroundProperty, "AccentInk");
        }
        else
        {
            gem.Background = background;
            ((TextBlock)gem.Child).Foreground = Brushes.White;
        }
        return gem;
    }

    private static readonly IEasingFunction Spring = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = .5 };

    private FrameworkElement CardFace(PetCard card, int index, bool enabled, bool deal, int slot)
    {
        Brush kind = KindBrush(card.Kind);
        var artBox = new Grid { Margin = new Thickness(0, 0, 0, 4) };
        artBox.Children.Add(ArtWindow(card, compactHand ? 44 : 62, compactHand ? 34 : 48));
        var cost = Gem(card.Cost.ToString(), compactHand ? 20 : 22);
        cost.HorizontalAlignment = System.Windows.HorizontalAlignment.Left; cost.VerticalAlignment = VerticalAlignment.Top; cost.Margin = new Thickness(-3, -3, 0, 0);
        artBox.Children.Add(cost);
        if (card.Element.Length > 0)
            artBox.Children.Add(new Border
            {
                Width = 11, Height = 11, CornerRadius = new CornerRadius(5.5), Background = Frozen(PetGame.ElementOf(card.Element).Color),
                BorderBrush = Brushes.White, BorderThickness = new Thickness(1.2),
                HorizontalAlignment = System.Windows.HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 4, 4, 0)
            });
        var name = new TextBlock { Text = card.Name, FontWeight = FontWeights.Bold, FontSize = 11.5, TextTrimming = TextTrimming.CharacterEllipsis };
        var text = new TextBlock { Text = card.Text, FontSize = 10, TextWrapping = TextWrapping.Wrap, TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(0, 1, 0, 0) };
        text.SetResourceReference(TextBlock.ForegroundProperty, "Muted");
        var face = new Border
        {
            Width = CardWidth, Height = CardHeight, Padding = new Thickness(6), Margin = new Thickness(0, CardLift, 8, 0),
            CornerRadius = new CornerRadius(10), BorderThickness = new Thickness(1.6),
            BorderBrush = kind, Cursor = enabled ? System.Windows.Input.Cursors.Hand : null,
            Opacity = enabled ? 1 : .45, Tag = index,
            Child = new StackPanel { Children = { artBox, name, text } },
            Effect = new DropShadowEffect { BlurRadius = 6, ShadowDepth = 1, Opacity = .18 }
        };
        face.SetResourceReference(Border.BackgroundProperty, "Surface");
        System.Windows.Automation.AutomationProperties.SetName(face, $"{card.Name}, {card.Cost} energy, {card.Text}");

        // Transforms: hover zoom, hover nudge and the deal/return slide.
        var zoom = new ScaleTransform(1, 1);
        var lift = new TranslateTransform();
        var nudge = new TranslateTransform();
        var slide = new TranslateTransform();
        face.RenderTransformOrigin = new Point(.5, 1);
        face.RenderTransform = new TransformGroup { Children = { zoom, lift, nudge, slide } };
        if (index == toggledIndex)
            lift.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(-14, 0, TimeSpan.FromMilliseconds(200)) { EasingFunction = Spring });
        if (deal)
        {
            slide.Y = CardHeight + 40;
            slide.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(0, TimeSpan.FromMilliseconds(320))
            { BeginTime = TimeSpan.FromMilliseconds(slot * 70), EasingFunction = Spring });
        }

        if (enabled) AttachCardDrag(face, index, fromQueue: false);
        face.MouseRightButtonUp += (_, e) => { e.Handled = true; ShowCardDetail(card, face); };
        face.MouseEnter += (_, _) =>
        {
            if (dragMoved) return;
            System.Windows.Controls.Panel.SetZIndex(face, 100);
            if (!enabled) return;
            var to = new DoubleAnimation(1.07, TimeSpan.FromMilliseconds(130)) { EasingFunction = Spring };
            zoom.BeginAnimation(ScaleTransform.ScaleXProperty, to);
            zoom.BeginAnimation(ScaleTransform.ScaleYProperty, to);
            nudge.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(-6, TimeSpan.FromMilliseconds(130)));
        };
        face.MouseLeave += (_, _) =>
        {
            System.Windows.Controls.Panel.SetZIndex(face, slot);
            var back = new DoubleAnimation(1, TimeSpan.FromMilliseconds(150));
            zoom.BeginAnimation(ScaleTransform.ScaleXProperty, back);
            zoom.BeginAnimation(ScaleTransform.ScaleYProperty, back);
            nudge.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(0, TimeSpan.FromMilliseconds(150)));
        };
        System.Windows.Controls.Panel.SetZIndex(face, slot);
        return face;
    }

    private FrameworkElement QueueCardFace(PetCard card, int index, int order, bool enabled)
    {
        Brush kind = KindBrush(card.Kind);
        var artBox = new Grid { Margin = new Thickness(0, 0, 0, 3) };
        artBox.Children.Add(ArtWindow(card, compactHand ? 36 : 44, compactHand ? 28 : 34));
        var cost = Gem(card.Cost.ToString(), 17);
        cost.HorizontalAlignment = System.Windows.HorizontalAlignment.Left; cost.VerticalAlignment = VerticalAlignment.Top; cost.Margin = new Thickness(-3, -3, 0, 0);
        artBox.Children.Add(cost);
        if (card.Element.Length > 0)
            artBox.Children.Add(new Border
            {
                Width = 9, Height = 9, CornerRadius = new CornerRadius(4.5), Background = Frozen(PetGame.ElementOf(card.Element).Color),
                BorderBrush = Brushes.White, BorderThickness = new Thickness(1),
                HorizontalAlignment = System.Windows.HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 3, 3, 0)
            });
        var badge = Gem(order.ToString(), 18, kind);
        badge.HorizontalAlignment = System.Windows.HorizontalAlignment.Right; badge.VerticalAlignment = VerticalAlignment.Bottom; badge.Margin = new Thickness(0, 0, -3, -3);
        artBox.Children.Add(badge);

        var name = new TextBlock { Text = card.Name, FontWeight = FontWeights.Bold, FontSize = compactHand ? 9.5 : 10.5, TextTrimming = TextTrimming.CharacterEllipsis, HorizontalAlignment = System.Windows.HorizontalAlignment.Center };
        var text = new TextBlock { Text = card.Text, FontSize = 8.5, TextTrimming = TextTrimming.CharacterEllipsis, HorizontalAlignment = System.Windows.HorizontalAlignment.Center };
        text.SetResourceReference(TextBlock.ForegroundProperty, "Muted");
        var stack = new StackPanel { Children = { artBox, name } };
        if (!compactHand) stack.Children.Add(text);

        var zoom = new ScaleTransform(1, 1);
        var face = new Border
        {
            Width = QueueCardWidth, Height = QueueCardHeight, Padding = new Thickness(5), Margin = new Thickness(0, 0, 6, 0),
            CornerRadius = new CornerRadius(9), BorderThickness = new Thickness(2),
            BorderBrush = kind, Cursor = enabled ? System.Windows.Input.Cursors.Hand : null,
            Tag = index, ToolTip = $"{card.Name} · {card.Text}",
            Child = stack, RenderTransformOrigin = new Point(.5, .5), RenderTransform = zoom,
            Effect = new DropShadowEffect { BlurRadius = 8, ShadowDepth = 2, Opacity = .25 }
        };
        face.SetResourceReference(Border.BackgroundProperty, "Surface");
        System.Windows.Automation.AutomationProperties.SetName(face, $"#{order} {card.Name}, {card.Cost} energy, {card.Text}");

        if (index == toggledIndex)
        {
            var pop = new DoubleAnimation(.82, 1, TimeSpan.FromMilliseconds(200)) { EasingFunction = Spring };
            zoom.BeginAnimation(ScaleTransform.ScaleXProperty, pop);
            zoom.BeginAnimation(ScaleTransform.ScaleYProperty, pop);
        }
        if (enabled) AttachCardDrag(face, index, fromQueue: true);
        face.MouseRightButtonUp += (_, e) => { e.Handled = true; ShowCardDetail(card, face); };
        face.MouseEnter += (_, _) =>
        {
            if (dragMoved || !enabled) return;
            System.Windows.Controls.Panel.SetZIndex(face, 50);
            var up = new DoubleAnimation(1.06, TimeSpan.FromMilliseconds(120)) { EasingFunction = Spring };
            zoom.BeginAnimation(ScaleTransform.ScaleXProperty, up);
            zoom.BeginAnimation(ScaleTransform.ScaleYProperty, up);
        };
        face.MouseLeave += (_, _) =>
        {
            System.Windows.Controls.Panel.SetZIndex(face, order);
            var back = new DoubleAnimation(1, TimeSpan.FromMilliseconds(130));
            zoom.BeginAnimation(ScaleTransform.ScaleXProperty, back);
            zoom.BeginAnimation(ScaleTransform.ScaleYProperty, back);
        };
        System.Windows.Controls.Panel.SetZIndex(face, order);
        return face;
    }

    private void AttachCardDrag(Border face, int index, bool fromQueue)
    {
        face.MouseLeftButtonDown += (_, e) => BeginCardDrag(face, index, fromQueue, e);
        face.MouseMove += (_, e) => MoveCardDrag(face, e);
        face.MouseLeftButtonUp += (_, e) => EndCardDrag(face, index, fromQueue, e);
        face.LostMouseCapture += (_, _) => { if (dragSource == face) CancelDrag(); };
    }

    private void BeginCardDrag(Border face, int index, bool fromQueue, MouseButtonEventArgs e)
    {
        if (cardSession is not { Finished: false } || cardPlaying) return;
        CancelDrag();
        dragSource = face;
        dragIndex = index;
        dragFromQueue = fromQueue;
        dragMoved = false;
        dragStart = e.GetPosition(BattlePanel);
        dragOffset = e.GetPosition(face);
        face.CaptureMouse();
        e.Handled = true;
    }

    private void MoveCardDrag(Border face, System.Windows.Input.MouseEventArgs e)
    {
        if (dragSource != face || e.LeftButton != MouseButtonState.Pressed || cardSession is not { } s) return;
        var pos = e.GetPosition(BattlePanel);
        if (!dragMoved)
        {
            if ((pos - dragStart).Length < 8) return;
            dragMoved = true;
            face.Opacity = .25;
            var card = PetCards.CardOf(s.Me.Hand[dragIndex]);
            dragGhost = DragCardPreview(card, dragFromQueue);
            DragLayer.Children.Add(dragGhost);
            if (!dragFromQueue) QueueTray.Visibility = Visibility.Visible;
        }
        if (dragGhost is not null)
        {
            Canvas.SetLeft(dragGhost, pos.X - dragOffset.X);
            Canvas.SetTop(dragGhost, pos.Y - dragOffset.Y);
        }
        bool overStage = IsOverStage(pos);
        SetQueueTrayStyle(dragging: true, hovering: overStage);
        e.Handled = true;
    }

    private void EndCardDrag(Border face, int index, bool fromQueue, MouseButtonEventArgs e)
    {
        if (dragSource != face)
        {
            // Direct click event without a preceding mouse-down (e.g. automated click).
            ToggleCard(index);
            return;
        }
        bool wasDrag = dragMoved;
        var pos = e.GetPosition(BattlePanel);
        CancelDrag();
        e.Handled = true;
        if (!wasDrag) { ToggleCard(index); return; }
        DropCardAt(index, fromQueue, pos);
    }

    internal void DropCardAt(int index, bool fromQueue, Point posInBattlePanel)
    {
        if (cardSession is not { Finished: false } s || cardPlaying) return;
        if (index < 0 || index >= s.Me.Hand.Count) return;
        bool overStage = IsOverStage(posInBattlePanel);
        if (!fromQueue)
        {
            if (overStage && !picked.Contains(index) && PetCards.CardOf(s.Me.Hand[index]).Cost <= s.Me.Energy - PickedCost)
            {
                int slot = QueueSlotFor(posInBattlePanel);
                picked.Insert(slot, index);
                toggledIndex = index;
            }
        }
        else
        {
            if (!overStage)
            {
                if (picked.Remove(index)) toggledIndex = index;
            }
            else if (picked.Contains(index))
            {
                int slot = QueueSlotFor(posInBattlePanel, excludeIndex: index);
                picked.Remove(index);
                picked.Insert(Math.Clamp(slot, 0, picked.Count), index);
                toggledIndex = index;
            }
        }
        RefreshHand();
    }

    private bool IsOverStage(Point posInBattlePanel)
    {
        if (CardBar.ActualHeight <= 0) return posInBattlePanel.Y < BattlePanel.ActualHeight * .65;
        double handTop = CardBar.TranslatePoint(new Point(0, 0), BattlePanel).Y;
        return posInBattlePanel.Y < handTop;
    }

    private int QueueSlotFor(Point posInBattlePanel, int excludeIndex = -1)
    {
        int slot = 0;
        foreach (FrameworkElement child in QueuePanel.Children)
        {
            if (child.Tag is int idx && idx == excludeIndex) continue;
            double midX = child.TranslatePoint(new Point(child.ActualWidth / 2, 0), BattlePanel).X;
            if (posInBattlePanel.X > midX) slot++;
        }
        return Math.Clamp(slot, 0, picked.Count);
    }

    private void SetQueueTrayStyle(bool dragging, bool hovering)
    {
        bool empty = QueuePanel.Children.Count == 0;
        QueueTray.MinWidth = dragging && empty ? 160 : 0;
        QueueTray.MinHeight = dragging && empty ? QueueCardHeight + 10 : 0;
        if (dragging && hovering)
        {
            QueueTray.SetResourceReference(Border.BorderBrushProperty, "Accent");
            QueueTray.BorderThickness = new Thickness(2);
            QueueTray.Opacity = 1;
        }
        else if (dragging)
        {
            QueueTray.SetResourceReference(Border.BorderBrushProperty, "Accent");
            QueueTray.BorderThickness = new Thickness(1.5);
            QueueTray.Opacity = .82;
        }
        else
        {
            QueueTray.SetResourceReference(Border.BorderBrushProperty, "Line");
            QueueTray.BorderThickness = new Thickness(1.5);
            QueueTray.Opacity = .95;
        }
    }

    private Border DragCardPreview(PetCard card, bool compact)
    {
        Brush kind = KindBrush(card.Kind);
        double w = compact ? QueueCardWidth : CardWidth;
        double h = compact ? QueueCardHeight : CardHeight;
        var artBox = new Grid { Margin = new Thickness(0, 0, 0, 3) };
        artBox.Children.Add(ArtWindow(card, compact ? (compactHand ? 36 : 44) : (compactHand ? 44 : 62), compact ? (compactHand ? 28 : 34) : (compactHand ? 34 : 48)));
        var cost = Gem(card.Cost.ToString(), compact ? 17 : (compactHand ? 20 : 22));
        cost.HorizontalAlignment = System.Windows.HorizontalAlignment.Left; cost.VerticalAlignment = VerticalAlignment.Top; cost.Margin = new Thickness(-3, -3, 0, 0);
        artBox.Children.Add(cost);
        var name = new TextBlock { Text = card.Name, FontWeight = FontWeights.Bold, FontSize = compact ? 10 : 11.5, TextTrimming = TextTrimming.CharacterEllipsis };
        var stack = new StackPanel { Children = { artBox, name } };
        if (!compact)
        {
            var text = new TextBlock { Text = card.Text, FontSize = 10, TextWrapping = TextWrapping.Wrap, TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(0, 1, 0, 0) };
            text.SetResourceReference(TextBlock.ForegroundProperty, "Muted");
            stack.Children.Add(text);
        }
        var ghost = new Border
        {
            Width = w, Height = h, Padding = new Thickness(compact ? 5 : 6), CornerRadius = new CornerRadius(10),
            BorderThickness = new Thickness(2.2), BorderBrush = kind, IsHitTestVisible = false, Opacity = .94,
            Child = stack, RenderTransformOrigin = new Point(.5, .5),
            RenderTransform = new ScaleTransform(1.04, 1.04),
            Effect = new DropShadowEffect { BlurRadius = 18, ShadowDepth = 6, Opacity = .38 }
        };
        ghost.SetResourceReference(Border.BackgroundProperty, "Surface");
        return ghost;
    }

    private void CancelDrag()
    {
        if (dragSource is { } src)
        {
            dragSource = null;
            if (src.IsMouseCaptured) src.ReleaseMouseCapture();
            src.Opacity = 1;
        }
        if (dragGhost is not null) { DragLayer.Children.Remove(dragGhost); dragGhost = null; }
        dragIndex = -1;
        dragMoved = false;
        dragFromQueue = false;
        if (QueueTray is not null)
        {
            SetQueueTrayStyle(dragging: false, hovering: false);
            QueueTray.Visibility = !cardPlaying && QueuePanel.Children.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    // Hand size follows the battle area: shorter cards when height is tight, overlapping cards when the row is narrow.
    private bool compactHand;
    private double CardWidth => compactHand ? 96 : 104;
    private double CardHeight => compactHand ? 114 : 146;
    private double CardLift => compactHand ? 8 : 14;
    private double QueueCardWidth => compactHand ? 66 : 78;
    private double QueueCardHeight => compactHand ? 66 : 86;

    private void BattlePanel_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        bool compact = BattlePanel.ActualHeight < 600;
        if (compact != compactHand)
        {
            compactHand = compact;
            if (cardSession is not null) RefreshHand();
        }
        else FitQueue();
    }

    private void HandPanel_SizeChanged(object sender, SizeChangedEventArgs e) { if (e.WidthChanged) FitHand(); }

    /// <summary>Room for the hand between the energy badge and the Fight/Leave buttons.</summary>
    internal double HandRoom => CardBar.ActualWidth - EnergyBadge.ActualWidth - EnergyBadge.Margin.Right - CardActions.ActualWidth - CardActions.Margin.Left;

    private void FitHand()
    {
        int n = HandPanel.Children.Count;
        double avail = HandRoom;
        if (n == 0 || avail <= 0) return;
        double width = Math.Floor(Math.Clamp((avail + 8) / n - 8, 84, CardWidth));
        double step = n == 1 ? width + 8 : Math.Floor(Math.Min(width + 8, (avail - width) / (n - 1)));
        foreach (FrameworkElement face in HandPanel.Children)
        {
            var m = face.Margin;
            face.Width = width;
            face.Margin = new Thickness(m.Left, m.Top, step - width, m.Bottom);
        }
    }

    private void FitQueue()
    {
        int n = QueuePanel.Children.Count;
        double avail = StageBorder.ActualWidth - 36;
        if (n == 0 || avail <= 0) return;
        double width = Math.Floor(Math.Clamp((avail + 6) / n - 6, 56, QueueCardWidth));
        double step = n == 1 ? width : Math.Floor(Math.Min(width + 6, (avail - width) / (n - 1)));
        int i = 0;
        foreach (FrameworkElement face in QueuePanel.Children)
        {
            face.Width = width;
            double right = i < n - 1 ? step - width : 0;
            face.Margin = new Thickness(0, 0, right, 0);
            i++;
        }
    }

    private void ToggleCard(int index)
    {
        if (cardSession is not { Finished: false } s || cardPlaying) return;
        if (!picked.Remove(index))
        {
            if (index < 0 || index >= s.Me.Hand.Count || PetCards.CardOf(s.Me.Hand[index]).Cost > s.Me.Energy - PickedCost) return;
            picked.Add(index);
        }
        toggledIndex = index;
        RefreshHand();
    }

    private async void Fight_Click(object sender, RoutedEventArgs e)
    {
        if (cardSession is not { Finished: false } s || cardPlaying) return;
        CancelDrag();
        var order = picked.ToList();
        cardPlaying = true;
        FightButton.IsEnabled = false;
        QueueTray.Visibility = Visibility.Collapsed;
        foreach (FrameworkElement face in HandPanel.Children)
        {
            if (face.RenderTransform is not TransformGroup { Children: [_, _, _, TranslateTransform slide] }) continue;
            face.IsHitTestVisible = false;
            slide.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(CardHeight + 40, TimeSpan.FromMilliseconds(200)) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn } });
        }
        await Task.Delay(HandPanel.Children.Count > 0 ? 200 : 40);
        picked.Clear();
        if (cardSession != s || s.Finished) { cardPlaying = false; RefreshHand(); return; }
        if (s.IsLan)
        {
            cardPlaying = false;
            await arena.PlayCardsAsync(order);
            if (cardSession == s && !cardPlaying) RefreshHand();
            return;
        }
        arena.PlayCards(order);
    }

    private void CardLeave_Click(object sender, RoutedEventArgs e) => arena.LeaveCardBattle();

    private void OnCardRound(IReadOnlyList<CardEvent> events)
    {
        if (cardSession is not { } s) return;
        cardQueue.Enqueue((events, s.MyLastCards, s.TheirLastCards));
        if (cardQueue.Count == 1) _ = PlayCardQueueAsync();
    }

    private void OnCardEnded()
    {
        if (cardQueue.Count == 0) ShowCardResult();
    }

    private async Task PlayCardQueueAsync()
    {
        cardPlaying = true;
        RefreshHand();
        while (cardQueue.Count > 0 && cardSession is { } s)
        {
            var (events, mine, theirs) = cardQueue.Peek();
            if (theirs.Count > 0)
                LogText.Text = $"{s.OpponentName}: " + string.Join(", ", theirs.Select(c => PetCards.CardOf(c).Name));
            await RevealBoard(mine, theirs);
            foreach (var ev in events)
            {
                if (cardSession is null) break;
                // A broken animation must never leave the hand locked.
                try { await PlayCardEventAsync(ev); }
                catch (Exception e) when (e is InvalidOperationException or NullReferenceException or ArgumentException or FormatException)
                { System.Diagnostics.Trace.WriteLine("card fx: " + e); Console.Error.WriteLine("card fx: " + e); }
            }
            cardQueue.Dequeue();
            ClearBoard();
            if (cardSession is { } now) { left.ShownHp = now.Me.Hp; right.ShownHp = now.Them.Hp; UpdateCardBars(); }
        }
        cardPlaying = false;
        dealPending = true;
        arena.RestartCardTurnTimer();
        RefreshHand();
        if (cardSession is { Finished: true }) ShowCardResult();
    }

    private StagePet CardPet(int side) => cardSession is { } s && side == s.MySide ? left : right;
    private string CardName(int side) => CardPet(side) == left ? LeftName.Text : RightName.Text;

    private async Task PlayCardEventAsync(CardEvent ev)
    {
        if (cardSession is not { } s) return;
        var pet = ev.Actor is 0 or 1 ? CardPet(ev.Actor) : left;
        var other = pet == left ? right : left;
        var card = ev.Card.Length > 0 ? PetCards.CardOf(ev.Card) : null;
        Color tone = card is not null ? CardArt.Palette(card).Main : Color.FromRgb(0xF4, 0x72, 0xB6);
        var at = CenterOf(pet);
        switch (ev.Kind)
        {
            case "play":
                LogText.Text = $"{CardName(ev.Actor)} plays {card!.Name}.";
                await ActivateBoard(ev.Actor, card.Id);
                if (card.Damage > 0) SetTemp(pet, PetMood.Fired, 600);
                else SupportCardVfx(pet, other, card);
                await Task.Delay(140);
                break;
            case "combo":
                Ring(at, tone, 74);
                Popup($"Combo +{ev.Amount}!", new Point(at.X, at.Y - 66), tone, 15);
                await Task.Delay(160);
                break;
            case "trap":
                Ring(at, tone, 86);
                Popup("Trap set", new Point(at.X, at.Y - 56), tone, 14);
                await Task.Delay(180);
                break;
            case "nextenergy":
                EnergySurge(at, Color.FromRgb(0xF5, 0x9E, 0x0B), Flame);
                Popup($"+{ev.Amount} energy next", new Point(at.X, at.Y - 52), Color.FromRgb(0xD9, 0x77, 0x06), 15);
                await Task.Delay(220);
                break;
            case "stealenergy":
                EnergyOrbFlight(other, pet, Color.FromRgb(0xF5, 0x9E, 0x0B));
                Popup($"Steal +{ev.Amount} energy", new Point(at.X, at.Y - 56), Color.FromRgb(0xD9, 0x77, 0x06), 15);
                await Task.Delay(280);
                break;
            case "hit":
            {
                // Actor is the pet being hit; the attacker is the other one.
                var attacker = other;
                double dir = attacker == left ? 1 : -1;
                bool elemental = card!.Element.Length > 0;
                // Every attack winds up toward the target; elemental ones cast their effect from there, neutral ones slash up close.
                await Lunge(attacker, dir * (elemental ? 24 : 70), elemental ? 90 : 130);
                await ElementAttack(attacker, pet, card, card.Hits > 1);
                SetTemp(pet, PetMood.Hurt, 500);
                Burst(at, tone, card.Pierce ? 10 : 5);
                Recoil(pet, dir);
                if (ev.Amount > 0) Popup("-" + ev.Amount, new Point(at.X, at.Y - 20), Color.FromRgb(0xEF, 0x44, 0x44), ev.Amount * 7 >= pet.MaxHp ? 28 : 22);
                if (ev.Amount * 7 >= pet.MaxHp) ShakeStage(7);
                if (ev.TypePercent > 100) Popup("Super effective!", new Point(at.X, at.Y - 56), tone, 13);
                pet.ShownHp = Math.Max(0, pet.ShownHp - ev.Amount);
                UpdateCardBars();
                _ = Lunge(attacker, 0, 160);
                await Task.Delay(card.Hits > 1 ? 120 : 260);
                break;
            }
            case "parry":
                Shield(pet, Color.FromRgb(0x14, 0xB8, 0xA6));
                Popup("Parry", new Point(at.X, at.Y - 40), Color.FromRgb(0x0D, 0x94, 0x88), 16);
                await Task.Delay(260);
                break;
            case "parried":
            {
                // The attacker charges in and is turned away.
                double dir = pet == left ? 1 : -1;
                LogText.Text = $"{CardName(1 - ev.Actor)} parries {card!.Name}!";
                await ActivateBoard(ev.Actor, card.Id);
                CancelBoard(ev.Actor, card.Id);
                await Lunge(pet, dir * 70, 120);
                var clash = new Point((at.X + CenterOf(other).X) / 2, at.Y - 10);
                Burst(clash, Color.FromRgb(0xE2, 0xE8, 0xF0), 12);
                Ring(clash, Color.FromRgb(0x14, 0xB8, 0xA6), 70);
                Popup("Parried!", new Point(clash.X, clash.Y - 50), Color.FromRgb(0x0D, 0x94, 0x88), 20);
                ShakeStage(4);
                SetTemp(pet, PetMood.Surprised, 600);
                await Lunge(pet, -dir * 20, 140);
                _ = Lunge(pet, 0, 160);
                await Task.Delay(200);
                break;
            }
            case "interrupt":
                Ring(CenterOf(other), Color.FromRgb(0xA8, 0x55, 0xF7), 110);
                Popup("Interrupt", new Point(at.X, at.Y - 40), Color.FromRgb(0x93, 0x33, 0xEA), 16);
                await Task.Delay(300);
                break;
            case "interrupted":
                LogText.Text = $"{card!.Name} was cancelled.";
                CancelBoard(ev.Actor, card.Id);
                SetTemp(pet, PetMood.Nervous, 600);
                Popup("Cancelled", new Point(at.X, at.Y - 60), Color.FromRgb(0x93, 0x33, 0xEA), 14);
                await Task.Delay(220);
                break;
            case "stun":
                LogText.Text = $"{CardName(ev.Actor)} is stunned!";
                SetTemp(pet, PetMood.Dizzy, 900);
                Stars(pet);
                Popup("Stunned!", new Point(at.X, at.Y - 64), Color.FromRgb(0xCA, 0x8A, 0x04), 18);
                _ = Spin(pet, 420);
                await Task.Delay(380);
                break;
            case "fizzle":
                CancelBoard(ev.Actor, card!.Id);
                Popup("Stunned", new Point(at.X, at.Y - 60), Color.FromRgb(0xCA, 0x8A, 0x04), 13);
                await Task.Delay(200);
                break;
            case "poisoned":
                SetTemp(pet, PetMood.Sad, 700);
                StatusHit(pet, ev.Kind);
                Popup($"Poison {ev.Amount}", new Point(at.X, at.Y - 52), Color.FromRgb(0x4D, 0x7C, 0x0F), 15);
                await Task.Delay(300);
                break;
            case "sleep":
                LogText.Text = $"{CardName(ev.Actor)} fell asleep.";
                SetTemp(pet, PetMood.Sleeping, 900);
                StatusHit(pet, ev.Kind);
                Popup("Asleep", new Point(at.X, at.Y - 60), Color.FromRgb(0x63, 0x66, 0xF1), 17);
                await Task.Delay(420);
                break;
            case "wake":
                SetTemp(pet, PetMood.Surprised, 700);
                pet.Hop = .02;
                Popup("Woke up!", new Point(at.X, at.Y - 64), Color.FromRgb(0x63, 0x66, 0xF1), 14);
                await Task.Delay(200);
                break;
            case "snooze":
                CancelBoard(ev.Actor, card!.Id);
                StatusHit(pet, "sleep");
                Popup("Zzz", new Point(at.X, at.Y - 60), Color.FromRgb(0x63, 0x66, 0xF1), 16);
                await Task.Delay(320);
                break;
            case "fear":
                LogText.Text = $"{CardName(ev.Actor)} is afraid.";
                SetTemp(pet, PetMood.Nervous, 900);
                StatusHit(pet, ev.Kind);
                Popup("Feared", new Point(at.X, at.Y - 60), Color.FromRgb(0x93, 0x33, 0xEA), 16);
                await Task.Delay(360);
                break;
            case "miss":
            {
                // The scared pet starts its attack and flinches back.
                double dir = pet == left ? 1 : -1;
                await ActivateBoard(ev.Actor, card!.Id);
                CancelBoard(ev.Actor, card.Id);
                await Lunge(pet, dir * 30, 110);
                Popup("Miss!", new Point(at.X + dir * 50, at.Y - 50), Color.FromRgb(0x93, 0x33, 0xEA), 18);
                SweatDrop(pet);
                await Lunge(pet, -dir * 14, 160);
                _ = Lunge(pet, 0, 140);
                await Task.Delay(160);
                break;
            }
            case "slow":
                SetTemp(pet, PetMood.Drowsy, 700);
                StatusHit(pet, ev.Kind);
                Popup("Slowed", new Point(at.X, at.Y - 60), Color.FromRgb(0x02, 0x84, 0xC7), 16);
                await Task.Delay(340);
                break;
            case "regen":
                SetTemp(pet, PetMood.Happy, 600);
                StatusHit(pet, ev.Kind);
                Popup($"Regen {ev.Amount}", new Point(at.X, at.Y - 52), Color.FromRgb(0x16, 0xA3, 0x4A), 15);
                await Task.Delay(300);
                break;
            case "rage":
                SetTemp(pet, PetMood.Angry, 900);
                StatusHit(pet, ev.Kind);
                _ = Spin(pet, 360);
                Popup("Rage!", new Point(at.X, at.Y - 62), Color.FromRgb(0xDC, 0x26, 0x26), 18);
                await Task.Delay(380);
                break;
            case "poison":
                LogText.Text = $"{CardName(ev.Actor)} is poisoned.";
                StatusHit(pet, "poisoned");
                Popup("-" + ev.Amount, new Point(at.X, at.Y - 20), Color.FromRgb(0x4D, 0x7C, 0x0F), 18);
                SetTemp(pet, PetMood.Hurt, 400);
                pet.ShownHp = Math.Max(0, pet.ShownHp - ev.Amount);
                UpdateCardBars();
                await Task.Delay(380);
                break;
            case "punish":
                Popup($"+{ev.Amount} vs block", new Point(at.X, at.Y - 74), Color.FromRgb(0xEA, 0x58, 0x0C), 14);
                await Task.Delay(160);
                break;
            case "blocked":
                Popup($"Blocked {ev.Amount}", new Point(at.X, at.Y + 8), Color.FromRgb(0x3B, 0x82, 0xF6), 13);
                await Task.Delay(140);
                break;
            case "block":
                Popup($"+{ev.Amount} block", new Point(at.X, at.Y - 40), Color.FromRgb(0x25, 0x63, 0xEB), 16);
                await Task.Delay(280);
                break;
            case "thorns":
                Popup($"Thorns {ev.Amount}", new Point(at.X, at.Y - 16), Color.FromRgb(0x16, 0xA3, 0x4A), 14);
                await Task.Delay(220);
                break;
            case "thornhit":
                SetTemp(pet, PetMood.Hurt, 400);
                Burst(at, Color.FromRgb(0x22, 0xC5, 0x5E), 6);
                Popup("-" + ev.Amount, new Point(at.X, at.Y - 20), Color.FromRgb(0x16, 0xA3, 0x4A), 18);
                pet.ShownHp = Math.Max(0, pet.ShownHp - ev.Amount);
                UpdateCardBars();
                await Task.Delay(220);
                break;
            case "heal":
                SetTemp(pet, PetMood.Happy, 700);
                pet.Hop = .02;
                Sparkles(pet, Color.FromRgb(0x22, 0xC5, 0x5E));
                Popup($"+{ev.Amount}", new Point(at.X, at.Y - 20), Color.FromRgb(0x16, 0xA3, 0x4A), 20);
                pet.ShownHp = Math.Min(pet.MaxHp, pet.ShownHp + ev.Amount);
                UpdateCardBars();
                await Task.Delay(380);
                break;
            case "burned":
                FlameBurst(at, 6);
                Popup($"Burn {ev.Amount}", new Point(at.X, at.Y - 48), Color.FromRgb(0xEA, 0x58, 0x0C), 14);
                await Task.Delay(220);
                break;
            case "weak":
                SetTemp(pet, PetMood.Nervous, 700);
                StatusHit(pet, ev.Kind);
                Popup("Weakened", new Point(at.X, at.Y - 48), Color.FromRgb(0x8B, 0x5C, 0xF6), 14);
                await Task.Delay(220);
                break;
            case "cleanse":
                Sparkles(pet, Color.FromRgb(0x60, 0xA5, 0xFA));
                Popup("Cleansed", new Point(at.X, at.Y - 48), Color.FromRgb(0x3B, 0x82, 0xF6), 14);
                await Task.Delay(200);
                break;
            case "energy":
                Popup($"+{ev.Amount} energy", new Point(at.X, at.Y - 40), Color.FromRgb(0xF5, 0x9E, 0x0B), 14);
                await Task.Delay(180);
                break;
            case "draw":
                if (ev.Actor == 0) Popup($"Draw {ev.Amount}", new Point(at.X, at.Y - 40), Color.FromRgb(0x64, 0x74, 0x8B), 13);
                await Task.Delay(120);
                break;
            case "burn":
                LogText.Text = $"{CardName(ev.Actor)} is burning.";
                FlameBurst(at, 8);
                Popup("-" + ev.Amount, new Point(at.X, at.Y - 20), Color.FromRgb(0xEA, 0x58, 0x0C), 18);
                SetTemp(pet, PetMood.Hurt, 400);
                pet.ShownHp = Math.Max(0, pet.ShownHp - ev.Amount);
                UpdateCardBars();
                await Task.Delay(380);
                break;
            case "faint":
                LogText.Text = $"{CardName(ev.Actor)} fainted!";
                pet.Fainted = true;
                pet.Visual.BeginAnimation(OpacityProperty, new DoubleAnimation(.55, TimeSpan.FromMilliseconds(600)) { FillBehavior = FillBehavior.HoldEnd });
                await Task.Delay(700);
                break;
            case "timeout":
                LogText.Text = "Time's up!";
                await Task.Delay(400);
                break;
        }
    }

    // ── The board: played cards above each pet ──────────────────────────

    private const double BoardW = 52, BoardH = 68;

    private Border BoardFace(PetCard card)
    {
        var name = new TextBlock { Text = card.Name, FontSize = 8.5, FontWeight = FontWeights.Bold, TextTrimming = TextTrimming.CharacterEllipsis, HorizontalAlignment = System.Windows.HorizontalAlignment.Center, Margin = new Thickness(0, 2, 0, 0) };
        name.SetResourceReference(TextBlock.ForegroundProperty, "Ink");
        var box = new Grid();
        box.Children.Add(ArtWindow(card, 40, 30));
        var cost = Gem(card.Cost.ToString(), 15);
        cost.HorizontalAlignment = System.Windows.HorizontalAlignment.Left; cost.VerticalAlignment = VerticalAlignment.Top; cost.Margin = new Thickness(-3, -3, 0, 0);
        box.Children.Add(cost);
        var face = new Border
        {
            Width = BoardW, Height = BoardH, Padding = new Thickness(3), CornerRadius = new CornerRadius(7), BorderThickness = new Thickness(1.8), BorderBrush = KindBrush(card.Kind),
            Child = new StackPanel { Children = { box, name } }, RenderTransformOrigin = new Point(.5, .5),
            Effect = new DropShadowEffect { BlurRadius = 8, ShadowDepth = 2, Opacity = .3 }
        };
        face.SetResourceReference(Border.BackgroundProperty, "Surface");
        face.MouseRightButtonUp += (_, e) => { e.Handled = true; ShowCardDetail(card, face); };
        return face;
    }

    private static Border CardBack()
    {
        var back = new Border
        {
            Width = BoardW, Height = BoardH, CornerRadius = new CornerRadius(7), BorderThickness = new Thickness(1.8), BorderBrush = Brushes.White,
            Background = new LinearGradientBrush(Color.FromRgb(0x63, 0x66, 0xF1), Color.FromRgb(0xA8, 0x55, 0xF7), 45),
            Child = new TextBlock { Text = "?", FontSize = 26, FontWeight = FontWeights.Black, Foreground = Brushes.White, HorizontalAlignment = System.Windows.HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center },
            RenderTransformOrigin = new Point(.5, .5), Effect = new DropShadowEffect { BlurRadius = 8, ShadowDepth = 2, Opacity = .3 }
        };
        return back;
    }

    /// <summary>Lays both sides' played cards above their pets: yours drop in, the foe's flip over from face down.</summary>
    private async Task RevealBoard(IReadOnlyList<string> mine, IReadOnlyList<string> theirs)
    {
        ClearBoard();
        if (mine.Count + theirs.Count == 0) return;
        var flips = new List<Task>();
        foreach (var (side, cards) in new[] { (cardSession?.MySide ?? 0, mine), (1 - (cardSession?.MySide ?? 0), theirs) })
        {
            if (cards.Count == 0) continue;
            var pet = CardPet(side);
            bool isLeft = pet == left;
            double cx = Canvas.GetLeft(pet.Visual) + pet.Visual.Width / 2;
            if (double.IsNaN(cx)) { LayoutStage(); cx = Canvas.GetLeft(pet.Visual) + pet.Visual.Width / 2; }
            double room = Math.Max(BoardW, StageW / 2 - 16);
            double step = Math.Min(BoardW + 6, (room - BoardW) / Math.Max(1, cards.Count - 1));
            double x0 = cx - (step * (cards.Count - 1) + BoardW) / 2;
            x0 = Math.Clamp(x0, isLeft ? 6 : StageW / 2 + 6, Math.Max(6, (isLeft ? StageW / 2 : StageW) - 6 - step * (cards.Count - 1) - BoardW));
            double y = Math.Max(6, Canvas.GetTop(pet.Visual) + 4);
            for (int i = 0; i < cards.Count; i++)
            {
                var card = PetCards.CardOf(cards[i]);
                var face = BoardFace(card);
                var scale = new ScaleTransform(1, 1);
                var drop = new TranslateTransform();
                face.RenderTransform = new TransformGroup { Children = { scale, drop } };
                Canvas.SetLeft(face, x0 + i * step);
                Canvas.SetTop(face, y);
                FxLayer.Children.Add(face);
                board.Add(new BoardCard { Side = side, Id = card.Id, Face = face, Scale = scale });
                var begin = TimeSpan.FromMilliseconds(i * 90);
                if (isLeft)
                {
                    drop.Y = -40; face.Opacity = 0;
                    drop.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(0, TimeSpan.FromMilliseconds(260)) { BeginTime = begin, EasingFunction = Spring });
                    face.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(160)) { BeginTime = begin });
                }
                else flips.Add(Flip(face, x0 + i * step, y, begin));
            }
        }
        await Task.WhenAll(flips.Append(Task.Delay(260 + Math.Max(mine.Count, theirs.Count) * 90)));
    }

    /// <summary>A face-down card turns over to show the face.</summary>
    private Task Flip(Border face, double x, double y, TimeSpan begin)
    {
        var tcs = new TaskCompletionSource();
        var back = CardBack();
        var backScale = new ScaleTransform(1, 1);
        back.RenderTransform = backScale;
        Canvas.SetLeft(back, x); Canvas.SetTop(back, y);
        FxLayer.Children.Add(back);
        var faceScale = (ScaleTransform)((TransformGroup)face.RenderTransform).Children[0];
        faceScale.ScaleX = 0;
        var shut = new DoubleAnimation(0, TimeSpan.FromMilliseconds(130)) { BeginTime = begin + TimeSpan.FromMilliseconds(120), EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn } };
        shut.Completed += (_, _) =>
        {
            FxLayer.Children.Remove(back);
            var open = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(150)) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } };
            open.Completed += (_, _) => { faceScale.BeginAnimation(ScaleTransform.ScaleXProperty, null); faceScale.ScaleX = 1; tcs.TrySetResult(); };
            faceScale.BeginAnimation(ScaleTransform.ScaleXProperty, open);
        };
        backScale.BeginAnimation(ScaleTransform.ScaleXProperty, shut);
        return tcs.Task;
    }

    private BoardCard? BoardFor(int side, string id) => board.FirstOrDefault(b => b.Side == side && b.Id == id && !b.Used);

    /// <summary>The card about to resolve pops up and glows; the one before it settles back.</summary>
    private async Task ActivateBoard(int side, string id)
    {
        foreach (var done in board.Where(b => b.Used && b.Face.Opacity > .5)) Fade(done.Face, .55, 180);
        if (BoardFor(side, id) is not { } b) return;
        b.Used = true;
        System.Windows.Controls.Panel.SetZIndex(b.Face, 10);
        var tone = ((SolidColorBrush)KindBrush(PetCards.CardOf(id).Kind)).Color;
        b.Face.Effect = new DropShadowEffect { Color = tone, BlurRadius = 18, ShadowDepth = 0, Opacity = .9 };
        var pop = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromMilliseconds(260) };
        pop.KeyFrames.Add(new EasingDoubleKeyFrame(1.28, KeyTime.FromPercent(.45), new QuadraticEase { EasingMode = EasingMode.EaseOut }));
        pop.KeyFrames.Add(new EasingDoubleKeyFrame(1.12, KeyTime.FromPercent(1), new QuadraticEase { EasingMode = EasingMode.EaseInOut }));
        b.Scale.BeginAnimation(ScaleTransform.ScaleXProperty, pop);
        b.Scale.BeginAnimation(ScaleTransform.ScaleYProperty, pop);
        await Task.Delay(200);
    }

    /// <summary>A parried, interrupted or stunned card shakes, gets crossed out and greys.</summary>
    private void CancelBoard(int side, string id)
    {
        var live = board.Where(x => x.Side == side && x.Id == id && x.Face.Tag is not TextBlock).ToList();
        var b = live.FirstOrDefault(x => x.Used) ?? live.FirstOrDefault();
        if (b is null) return;
        b.Used = true;
        b.Face.Effect = null;
        var mark = new TextBlock { Text = "✕", FontSize = 40, FontWeight = FontWeights.Black, Foreground = Frozen("#EF4444"), IsHitTestVisible = false };
        mark.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        Canvas.SetLeft(mark, Canvas.GetLeft(b.Face) + (BoardW - mark.DesiredSize.Width) / 2);
        Canvas.SetTop(mark, Canvas.GetTop(b.Face) + (BoardH - mark.DesiredSize.Height) / 2);
        System.Windows.Controls.Panel.SetZIndex(mark, 20);
        FxLayer.Children.Add(mark);
        b.Face.Tag = mark;
        var shake = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromMilliseconds(260) };
        foreach (var (v, t) in new[] { (-6.0, .2), (6.0, .4), (-4.0, .6), (3.0, .8), (0.0, 1.0) })
            shake.KeyFrames.Add(new LinearDoubleKeyFrame(v, KeyTime.FromPercent(t)));
        ((TranslateTransform)((TransformGroup)b.Face.RenderTransform).Children[1]).BeginAnimation(TranslateTransform.XProperty, shake);
        b.Scale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        b.Scale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        Fade(b.Face, .35, 220);
    }

    private void ClearBoard()
    {
        foreach (var b in board)
        {
            var face = b.Face;
            var gone = new DoubleAnimation(0, TimeSpan.FromMilliseconds(220));
            gone.Completed += (_, _) => FxLayer.Children.Remove(face);
            face.BeginAnimation(OpacityProperty, gone);
            if (face.Tag is TextBlock mark) FxLayer.Children.Remove(mark);
        }
        board.Clear();
    }

    /// <summary>The struck pet is knocked back a little and springs back.</summary>
    private static void Recoil(StagePet pet, double dir)
    {
        var knock = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromMilliseconds(260) };
        knock.KeyFrames.Add(new EasingDoubleKeyFrame(dir * 14, KeyTime.FromPercent(.3), new QuadraticEase { EasingMode = EasingMode.EaseOut }));
        knock.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromPercent(1), new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = .6 }));
        pet.Move.BeginAnimation(TranslateTransform.XProperty, knock);
    }

    private void ShakeStage(double k)
    {
        var shake = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromMilliseconds(300) };
        foreach (var (v, t) in new[] { (k, .15), (-k, .35), (k * .6, .55), (-k * .4, .75), (0.0, 1.0) })
            shake.KeyFrames.Add(new LinearDoubleKeyFrame(v, KeyTime.FromPercent(t)));
        var move = new TranslateTransform();
        Stage.RenderTransform = move;
        move.BeginAnimation(TranslateTransform.XProperty, shake);
    }

    /// <summary>Little stars circling a stunned pet's head.</summary>
    private void Stars(StagePet pet)
    {
        var at = CenterOf(pet);
        double top = at.Y - pet.Visual.Height * .55;
        for (int i = 0; i < 3; i++)
        {
            var star = new TextBlock { Text = "★", FontSize = 16, Foreground = Frozen("#FACC15"), Effect = new DropShadowEffect { Color = Colors.White, BlurRadius = 3, ShadowDepth = 0 } };
            Canvas.SetTop(star, top);
            FxLayer.Children.Add(star);
            var orbit = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromMilliseconds(1100) };
            for (int k = 0; k <= 8; k++)
                orbit.KeyFrames.Add(new LinearDoubleKeyFrame(at.X - 8 + Math.Cos((k / 8.0 + i / 3.0) * Math.PI * 2) * 34, KeyTime.FromPercent(k / 8.0)));
            star.BeginAnimation(Canvas.LeftProperty, orbit);
            var fade = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(300)) { BeginTime = TimeSpan.FromMilliseconds(800) };
            fade.Completed += (_, _) => FxLayer.Children.Remove(star);
            star.BeginAnimation(OpacityProperty, fade);
        }
    }

    private void UpdateCardBars(bool instant = false)
    {
        if (cardSession is not { } s) return;
        foreach (var (pet, fill, trail, track, text, status, side, isEnemy) in new[]
        {
            (left, LeftHpFill, LeftHpTrail, LeftHpTrack, LeftHpText, LeftStatus, s.Me, false),
            (right, RightHpFill, RightHpTrail, RightHpTrack, RightHpText, RightStatus, s.Them, true)
        })
        {
            double frac = pet.MaxHp > 0 ? pet.ShownHp / (double)pet.MaxHp : 0;
            SetHpBar(fill, trail, track, frac, isEnemy, instant);
            text.Text = $"{pet.ShownHp} / {pet.MaxHp}";
            StatusChips(status, side);
        }
    }

    private void ShowCardResult()
    {
        if (cardSession is not { Finished: true } s) return;
        RefreshHand();
        left.ShownHp = s.Me.Hp; right.ShownHp = s.Them.Hp;
        UpdateCardBars();
        ResultTitle.Text = s.Reward is null ? "Battle ended" : s.TournamentChampion ? "Champion!" : s.Outcome > 0 ? "You win!" : s.Outcome < 0 ? "You lost" : "Draw";
        ResultReason.Text = s.EndReason;
        ResultReason.Visibility = s.EndReason.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        string reward = s.Reward is { } r ? $"+{r.Xp} XP · +{r.Coins} coins" + (r.LevelsGained > 0 ? $" · Level {arena.Progress.Level}!" : "") : "";
        if (s.Drop is { } drop) reward += $"\nFound {drop.Name} ({RarityName(drop.Rarity)})";
        if (s.StageCleared && s.Stage is { } st) reward += $"\nStage {st.Number} cleared";
        if (s.TournamentChampion && s.Tournament is { } tev) reward += $"\n{tev.Name} won";
        ResultReward.Text = reward;
        ResultReward.TextAlignment = TextAlignment.Center;
        RematchButton.Visibility = !s.IsLan || model.Peers.Any(p => p.Id == s.PeerId && p.Online) ? Visibility.Visible : Visibility.Collapsed;
        ResultCard.Visibility = Visibility.Visible;
        CenterResult();
        bool won = s.Outcome > 0, lost = s.Outcome < 0;
        SetTemp(left, won ? PetMood.Laughing : lost ? PetMood.Crying : PetMood.Surprised, 600_000);
        SetTemp(right, won ? PetMood.Crying : lost ? PetMood.Laughing : PetMood.Surprised, 600_000);
        if (won) Confetti();
    }
}
