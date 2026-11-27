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

namespace CydLan.Desktop;

/// <summary>Gear, bag, deck and the card battle view.</summary>
public partial class PetArenaView
{
    private sealed record SlotRow(string Slot, string SlotName, string Name, string CardName, Brush RarityBrush, bool Worn)
    {
        public Visibility RemoveVisibility => Worn ? Visibility.Visible : Visibility.Hidden;
        public string RemoveName => $"Remove {Name}";
    }

    private sealed record BagRow(string Uid, string Name, string SlotName, string StatText, string CardLine, Brush RarityBrush, bool Worn, int Price, bool Locked)
    {
        public string EquipText => Worn ? "Worn" : "Equip";
        public bool CanEquip => !Worn && !Locked;
        public bool CanSell => !Worn;
        public string SellText => $"Sell {Price}";
        public string EquipName => $"Equip {Name}";
        public string SellName => $"Sell {Name} for {Price} coins";
    }

    private sealed record DeckRow(string Name, string Text, int Cost, int Count, Brush KindBrush) { public string CountText => $"×{Count}"; }

    private CardBattleSession? cardSession;
    private readonly HashSet<int> picked = [];
    private readonly Queue<IReadOnlyList<CardEvent>> cardQueue = new();
    private bool cardPlaying;

    internal static Brush RarityBrush(string rarity) => rarity switch
    {
        "epic" => Frozen("#A855F7"),
        "rare" => Frozen("#3B82F6"),
        _ => Frozen("#94A3B8")
    };

    internal static Brush KindBrush(string kind) => kind switch
    {
        "attack" => Frozen("#EF4444"),
        "defend" => Frozen("#3B82F6"),
        "heal" => Frozen("#22C55E"),
        _ => Frozen("#F59E0B")
    };

    private static SolidColorBrush Frozen(string hex)
    {
        var b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        b.Freeze();
        return b;
    }

    private static string SlotName(string slot) => PetCards.Slots.FirstOrDefault(s => s.Id == slot).Name ?? slot;

    // ── Lobby: gear, bag, deck ───────────────────────────────────────────

    private void RefreshGear()
    {
        var p = arena.Progress;
        bool locked = arena.CurrentCard is { Finished: false };
        GearSlots.ItemsSource = PetCards.Slots.Select(slot =>
        {
            var item = PetCards.Worn(p, slot.Id);
            return item is null
                ? new SlotRow(slot.Id, slot.Name, "Empty", "", Frozen("#CBD5E1"), false)
                : new SlotRow(slot.Id, slot.Name, item.Name, PetCards.CardOf(item.Card).Name, RarityBrush(item.Rarity), true);
        }).ToList();

        BagTitle.Text = $"Bag · {p.Gear.Count}";
        GearPackButton.Content = $"Gear pack · {PetCards.GearPackPrice}";
        GearPackButton.IsEnabled = p.Coins >= PetCards.GearPackPrice && p.Gear.Count < PetCards.BagLimit;
        BagList.ItemsSource = p.Gear
            .Select(g => (Owned: g, Item: PetCards.GearOf(g.Item)!))
            .OrderByDescending(x => PetCards.IsEquipped(p, x.Owned.Uid))
            .ThenBy(x => PetCards.Slots.ToList().FindIndex(s => s.Id == x.Item.Slot))
            .ThenByDescending(x => PetCards.Rarities.ToList().IndexOf(x.Item.Rarity))
            .Select(x =>
            {
                var card = PetCards.CardOf(x.Item.Card);
                return new BagRow(x.Owned.Uid, x.Item.Name, SlotName(x.Item.Slot), x.Item.StatText, $"{card.Name}: {card.Text}",
                    RarityBrush(x.Item.Rarity), PetCards.IsEquipped(p, x.Owned.Uid), PetCards.SellPrice(x.Item), locked);
            }).ToList();

        var loadout = arena.MyLoadout;
        DeckTitle.Text = $"Deck · {loadout.Deck.Count} cards";
        DeckList.ItemsSource = loadout.Deck.GroupBy(id => id)
            .Select(g => { var c = PetCards.CardOf(g.Key); return new DeckRow(c.Name, c.Text, c.Cost, g.Count(), KindBrush(c.Kind)); })
            .OrderBy(r => r.Cost).ThenBy(r => r.Name).ToList();
        CardRecordText.Text = $"{p.CardWins} wins · {p.CardLosses} losses";
        StatsLine.Text = $"HP {loadout.MaxHp} · Power {loadout.Power} · Guard {loadout.Guard} · Speed {loadout.Speed}";
        MyPetVisual.PetStyle = arena.BattleLook;
    }

    private void Equip_Click(object sender, RoutedEventArgs e) { if (sender is FrameworkElement { Tag: string uid }) arena.Equip(uid); }
    private void Unequip_Click(object sender, RoutedEventArgs e) { if (sender is FrameworkElement { Tag: string slot }) arena.Unequip(slot); }
    private void Sell_Click(object sender, RoutedEventArgs e) { if (sender is FrameworkElement { Tag: string uid }) arena.Sell(uid); }

    private void GearPack_Click(object sender, RoutedEventArgs e)
    {
        if (arena.OpenGearPack() is not { } item) return;
        PackResultText.Text = $"Got {item.Name} ({item.Rarity})";
        PackResultText.Foreground = RarityBrush(item.Rarity);
        PackResultText.Visibility = Visibility.Visible;
        lobbyHop = .02;
        MyPetVisual.SetMood(PetMood.Surprised);
        lobbyMoodUntil = DateTime.UtcNow.AddSeconds(1.4);
    }

    private void CardBattle_Click(object sender, RoutedEventArgs e) => arena.StartCardBattle();

    // ── Battle view ──────────────────────────────────────────────────────

    private void ShowCardBattle(CardBattleSession s)
    {
        DetachCard();
        Detach();
        cardSession = s;
        s.RoundPlayed += OnCardRound;
        s.Ended += OnCardEnded;
        picked.Clear();
        LobbyPanel.Visibility = Visibility.Collapsed;
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
        RightLevel.Text = $"Lv {s.Them.Loadout.Level} · {PetGame.ElementOf(s.Them.Loadout.Element).Name}";
        LogText.Text = $"{s.OpponentName} appears!";
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () => { LayoutStage(); UpdateCardBars(); });
        RefreshHand();
        if (s.Finished) ShowCardResult();
    }

    private void DetachCard()
    {
        if (cardSession is null) return;
        cardSession.RoundPlayed -= OnCardRound;
        cardSession.Ended -= OnCardEnded;
        cardSession = null;
        cardQueue.Clear();
        picked.Clear();
    }

    private int PickedCost => cardSession is { } s ? picked.Where(i => i < s.Me.Hand.Count).Sum(i => PetCards.CardOf(s.Me.Hand[i]).Cost) : 0;

    private void RefreshHand()
    {
        HandPanel.Children.Clear();
        if (cardSession is not { } s) return;
        int left = s.Me.Energy - PickedCost;
        EnergyText.Text = left.ToString();
        bool canPlay = !cardPlaying && !s.Finished;
        for (int i = 0; i < s.Me.Hand.Count; i++)
        {
            var card = PetCards.CardOf(s.Me.Hand[i]);
            bool on = picked.Contains(i);
            bool affordable = on || card.Cost <= left;
            HandPanel.Children.Add(CardFace(card, i, on, canPlay && affordable));
        }
        FightButton.IsEnabled = canPlay;
        FightButton.Content = picked.Count == 0 ? "Pass" : "Fight";
        CardLeaveButton.IsEnabled = !s.Finished;
    }

    private FrameworkElement CardFace(PetCard card, int index, bool selected, bool enabled)
    {
        Brush kind = KindBrush(card.Kind);
        var cost = new Border
        {
            Width = 24, Height = 24, CornerRadius = new CornerRadius(12), HorizontalAlignment = System.Windows.HorizontalAlignment.Left,
            Child = new TextBlock { Text = card.Cost.ToString(), FontWeight = FontWeights.Bold, FontSize = 12.5, HorizontalAlignment = System.Windows.HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }
        };
        cost.SetResourceReference(Border.BackgroundProperty, "Accent");
        ((TextBlock)cost.Child).SetResourceReference(TextBlock.ForegroundProperty, "AccentInk");
        var top = new DockPanel { Margin = new Thickness(0, 0, 0, 4) };
        if (card.Element.Length > 0)
        {
            var dot = new Border { Width = 12, Height = 12, CornerRadius = new CornerRadius(6), Background = Frozen(PetGame.ElementOf(card.Element).Color), VerticalAlignment = VerticalAlignment.Center };
            DockPanel.SetDock(dot, Dock.Right);
            top.Children.Add(dot);
        }
        top.Children.Add(cost);
        var stripe = new Border { Height = 4, CornerRadius = new CornerRadius(2), Background = kind, Margin = new Thickness(0, 0, 0, 5) };
        var name = new TextBlock { Text = card.Name, FontWeight = FontWeights.SemiBold, FontSize = 12, TextWrapping = TextWrapping.Wrap };
        var text = new TextBlock { Text = card.Text, FontSize = 10.5, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 3, 0, 0) };
        text.SetResourceReference(TextBlock.ForegroundProperty, "Muted");
        var face = new Border
        {
            Width = 104, Height = 138, Padding = new Thickness(8, 7, 8, 7), Margin = new Thickness(0, selected ? 0 : 14, 8, selected ? 14 : 0),
            CornerRadius = new CornerRadius(10), BorderThickness = new Thickness(selected ? 2.6 : 1.4),
            BorderBrush = selected ? null : kind, Cursor = enabled ? System.Windows.Input.Cursors.Hand : null,
            Opacity = enabled ? 1 : .45, Tag = index,
            Child = new StackPanel { Children = { top, stripe, name, text } },
            Effect = selected ? new DropShadowEffect { BlurRadius = 14, ShadowDepth = 2, Opacity = .3 } : null
        };
        face.SetResourceReference(Border.BackgroundProperty, "Surface");
        if (selected) face.SetResourceReference(Border.BorderBrushProperty, "Accent");
        System.Windows.Automation.AutomationProperties.SetName(face, $"{card.Name}, {card.Cost} energy, {card.Text}");
        if (enabled) face.MouseLeftButtonUp += (_, _) => ToggleCard(index);
        return face;
    }

    private void ToggleCard(int index)
    {
        if (cardSession is not { Finished: false } s || cardPlaying) return;
        if (!picked.Remove(index))
        {
            if (PetCards.CardOf(s.Me.Hand[index]).Cost > s.Me.Energy - PickedCost) return;
            picked.Add(index);
        }
        RefreshHand();
    }

    private void Fight_Click(object sender, RoutedEventArgs e)
    {
        if (cardSession is not { Finished: false } || cardPlaying) return;
        var order = picked.ToList();
        picked.Clear();
        cardPlaying = true;
        RefreshHand();
        arena.PlayCards(order);
    }

    private void CardLeave_Click(object sender, RoutedEventArgs e) => arena.LeaveCardBattle();

    private void OnCardRound(IReadOnlyList<CardEvent> events)
    {
        cardQueue.Enqueue(events);
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
            var events = cardQueue.Peek();
            if (s.TheirLastCards.Count > 0)
                LogText.Text = $"{s.OpponentName}: " + string.Join(", ", s.TheirLastCards.Select(c => PetCards.CardOf(c).Name));
            foreach (var ev in events)
            {
                if (cardSession is null) break;
                // A broken animation must never leave the hand locked.
                try { await PlayCardEventAsync(ev); }
                catch (Exception e) when (e is InvalidOperationException or NullReferenceException or ArgumentException or FormatException)
                { System.Diagnostics.Trace.WriteLine("card fx: " + e); Console.Error.WriteLine("card fx: " + e); }
            }
            cardQueue.Dequeue();
            if (cardSession is { } now) { left.ShownHp = now.Me.Hp; right.ShownHp = now.Them.Hp; UpdateCardBars(); }
        }
        cardPlaying = false;
        RefreshHand();
        if (cardSession is { Finished: true }) ShowCardResult();
    }

    private StagePet CardPet(int side) => side == 0 ? left : right;
    private string CardName(int side) => side == 0 ? LeftName.Text : RightName.Text;

    private async Task PlayCardEventAsync(CardEvent ev)
    {
        if (cardSession is not { } s) return;
        var pet = ev.Actor is 0 or 1 ? CardPet(ev.Actor) : left;
        var other = pet == left ? right : left;
        var card = ev.Card.Length > 0 ? PetCards.CardOf(ev.Card) : null;
        Color tone = card is { Element.Length: > 0 } ? (Color)ColorConverter.ConvertFromString(PetGame.ElementOf(card.Element).Color) : Color.FromRgb(0xF4, 0x72, 0xB6);
        var at = CenterOf(pet);
        switch (ev.Kind)
        {
            case "play":
                LogText.Text = $"{CardName(ev.Actor)} plays {card!.Name}.";
                Popup(card.Name, new Point(at.X, at.Y - 86), ((SolidColorBrush)KindBrush(card.Kind)).Color, 15);
                if (card.Damage > 0) SetTemp(pet, PetMood.Fired, 600);
                await Task.Delay(260);
                break;
            case "hit":
            {
                // Actor is the pet being hit; the attacker is the other one.
                var attacker = other;
                double dir = attacker == left ? 1 : -1;
                if (card!.Element.Length > 0 || card.Hits > 1) await Projectile(CenterOf(attacker), at, tone, card.Hits > 1 ? 9 : 13, 240);
                else { await Lunge(attacker, dir * 60, 130); }
                SetTemp(pet, PetMood.Hurt, 500);
                Burst(at, tone, card.Pierce ? 14 : 8);
                Ring(at, tone, 56);
                if (ev.Amount > 0) Popup("-" + ev.Amount, new Point(at.X, at.Y - 20), Color.FromRgb(0xEF, 0x44, 0x44), 22);
                if (ev.TypePercent > 100) Popup("Super effective!", new Point(at.X, at.Y - 56), tone, 13);
                pet.ShownHp = Math.Max(0, pet.ShownHp - ev.Amount);
                UpdateCardBars();
                if (card.Element.Length == 0 && card.Hits <= 1) await Lunge(attacker, 0, 140);
                await Task.Delay(card.Hits > 1 ? 120 : 280);
                break;
            }
            case "blocked":
                Popup($"Blocked {ev.Amount}", new Point(at.X, at.Y + 8), Color.FromRgb(0x3B, 0x82, 0xF6), 13);
                await Task.Delay(140);
                break;
            case "block":
                Shield(pet, Color.FromRgb(0x3B, 0x82, 0xF6));
                Popup($"+{ev.Amount} block", new Point(at.X, at.Y - 40), Color.FromRgb(0x25, 0x63, 0xEB), 16);
                await Task.Delay(320);
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
                Burst(at, Color.FromRgb(0xF9, 0x73, 0x16), 8);
                Popup($"Burn {ev.Amount}", new Point(at.X, at.Y - 48), Color.FromRgb(0xEA, 0x58, 0x0C), 14);
                await Task.Delay(220);
                break;
            case "weak":
                SetTemp(pet, PetMood.Nervous, 700);
                Popup("Weakened", new Point(at.X, at.Y - 48), Color.FromRgb(0x8B, 0x5C, 0xF6), 14);
                await Task.Delay(220);
                break;
            case "cleanse":
                Sparkles(pet, Color.FromRgb(0x60, 0xA5, 0xFA));
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
                Burst(at, Color.FromRgb(0xF9, 0x73, 0x16), 10);
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

    private void UpdateCardBars()
    {
        if (cardSession is not { } s) return;
        foreach (var (pet, fill, track, text, status, side) in new[]
        {
            (left, LeftHpFill, LeftHpTrack, LeftHpText, LeftStatus, s.Me),
            (right, RightHpFill, RightHpTrack, RightHpText, RightStatus, s.Them)
        })
        {
            double frac = pet.MaxHp > 0 ? pet.ShownHp / (double)pet.MaxHp : 0;
            SetBar(fill, track, frac);
            fill.Background = frac > .5 ? BrushOf("#22C55E") : frac > .25 ? BrushOf("#F59E0B") : BrushOf("#EF4444");
            text.Text = $"{pet.ShownHp} / {pet.MaxHp}";
            var tags = new List<string>();
            if (side.Block > 0) tags.Add($"Block {side.Block}");
            if (side.Thorns > 0) tags.Add($"Thorns {side.Thorns}");
            if (side.Burn > 0) tags.Add($"Burn {side.Burn}");
            if (side.Weak > 0) tags.Add("Weak");
            status.Text = string.Join(" · ", tags);
        }
    }

    private void ShowCardResult()
    {
        if (cardSession is not { Finished: true } s) return;
        RefreshHand();
        left.ShownHp = s.Me.Hp; right.ShownHp = s.Them.Hp;
        UpdateCardBars();
        ResultTitle.Text = s.Outcome > 0 ? "You win!" : s.Outcome < 0 ? "You lost" : "Draw";
        ResultReason.Text = s.EndReason;
        ResultReason.Visibility = s.EndReason.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        string reward = s.Reward is { } r ? $"+{r.Xp} XP · +{r.Coins} coins" + (r.LevelsGained > 0 ? $" · Level {arena.Progress.Level}!" : "") : "";
        if (s.Drop is { } drop) reward += $"\nFound {drop.Name} ({drop.Rarity})";
        ResultReward.Text = reward;
        ResultReward.TextAlignment = TextAlignment.Center;
        RematchButton.Visibility = Visibility.Visible;
        ResultCard.Visibility = Visibility.Visible;
        CenterResult();
        bool won = s.Outcome > 0, lost = s.Outcome < 0;
        SetTemp(left, won ? PetMood.Laughing : lost ? PetMood.Crying : PetMood.Surprised, 600_000);
        SetTemp(right, won ? PetMood.Crying : lost ? PetMood.Laughing : PetMood.Surprised, 600_000);
        if (won) Confetti();
    }
}
