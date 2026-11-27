using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Effects;
using CydLan.Core;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;

namespace CydLan.Desktop;

/// <summary>Right-click detail inspector showing a card's icon, stats, live pet scaling, element matchup, and tricks.</summary>
public partial class PetArenaView
{
    private Popup? cardDetailPopup;

    private void ShowCardDetail(PetCard card, UIElement? target = null)
    {
        if (cardDetailPopup is not null) cardDetailPopup.IsOpen = false;
        var panel = BuildCardDetailPanel(card);
        cardDetailPopup = new Popup
        {
            Child = panel,
            StaysOpen = false,
            AllowsTransparency = true,
            PopupAnimation = PopupAnimation.Fade,
            PlacementTarget = target ?? this,
            Placement = target is not null ? PlacementMode.MousePoint : PlacementMode.Center,
            HorizontalOffset = 8,
            VerticalOffset = 8
        };
        cardDetailPopup.IsOpen = true;
    }

    internal Border BuildCardDetailPanel(PetCard card)
    {
        var loadout = cardSession?.Me.Loadout ?? arena.MyLoadout;
        var foe = cardSession?.Them.Loadout;
        Brush kindBrush = KindBrush(card.Kind);
        Brush rarityBrush = RarityBrush(card.Rarity);

        // Header: art + name + badges
        var artWrap = new Grid { Width = 54, Height = 54, Margin = new Thickness(0, 0, 10, 0) };
        artWrap.Children.Add(ArtWindow(card, 54, 44));
        var costGem = Gem(card.Cost.ToString(), 20);
        costGem.HorizontalAlignment = System.Windows.HorizontalAlignment.Left;
        costGem.VerticalAlignment = VerticalAlignment.Top;
        costGem.Margin = new Thickness(-4, -4, 0, 0);
        artWrap.Children.Add(costGem);

        var title = new TextBlock { Text = card.Name, FontSize = 14, FontWeight = FontWeights.Bold, TextTrimming = TextTrimming.CharacterEllipsis };
        title.SetResourceReference(TextBlock.ForegroundProperty, "Ink");

        var badges = new WrapPanel { Margin = new Thickness(0, 4, 0, 0) };
        badges.Children.Add(DetailChip(RarityName(card.Rarity), rarityBrush));
        badges.Children.Add(DetailChip(ElementName(card), card.Element.Length > 0 ? Frozen(PetGame.ElementOf(card.Element).Color) : Frozen("#64748B")));
        badges.Children.Add(DetailChip(char.ToUpperInvariant(card.Kind[0]) + card.Kind[1..], kindBrush));

        var headText = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Children = { title, badges } };
        var header = new DockPanel { Margin = new Thickness(0, 0, 0, 8) };
        DockPanel.SetDock(artWrap, Dock.Left);
        header.Children.Add(artWrap);
        header.Children.Add(headText);

        var rows = new StackPanel();
        void AddRow(string label, string value, string note = "", Brush? accent = null)
        {
            var dp = new DockPanel { Margin = new Thickness(0, 2, 0, 2) };
            var lbl = new TextBlock { Text = label, FontSize = 11.5, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 10, 0) };
            lbl.SetResourceReference(TextBlock.ForegroundProperty, "Muted");
            DockPanel.SetDock(lbl, Dock.Left);
            var val = new TextBlock { Text = note.Length > 0 ? $"{value}  ({note})" : value, FontSize = 11.5, FontWeight = FontWeights.SemiBold, TextAlignment = TextAlignment.Right, TextWrapping = TextWrapping.Wrap };
            if (accent is not null) val.Foreground = accent;
            else val.SetResourceReference(TextBlock.ForegroundProperty, "Ink");
            dp.Children.Add(lbl);
            dp.Children.Add(val);
            rows.Children.Add(dp);
        }

        static string Scaled(int baseVal, int scaledVal, string statName, int stat) =>
            scaledVal != baseVal ? $"{baseVal} → {scaledVal} ({statName} {stat}, +{stat * 4}%)" : $"{baseVal} ({statName} {stat})";

        if (card.Damage > 0)
        {
            int scaled = PetCards.Scale(card.Damage, loadout.Power);
            string hits = card.Hits > 1 ? $" ×{card.Hits}" : "";
            AddRow("Damage", $"{card.Damage}{hits} → {scaled}{hits}", $"Power {loadout.Power}, +{loadout.Power * 4}%", Frozen("#EF4444"));
        }
        if (card.Parry > 0)
        {
            int scaled = PetCards.Scale(card.Parry, loadout.Power);
            AddRow("Parry", $"Counter {card.Parry} → {scaled}", $"Power {loadout.Power}, +{loadout.Power * 4}%", Frozen("#14B8A6"));
        }
        if (card.Combo > 0)
        {
            int scaled = PetCards.Scale(card.Combo, loadout.Speed);
            AddRow("Combo", $"+{card.Combo} → +{scaled}", $"Speed {loadout.Speed}, 2+ cards", Frozen("#F59E0B"));
        }
        if (card.Punish > 0)
        {
            int scaled = PetCards.Scale(card.Punish, loadout.Speed);
            AddRow("Punish", $"+{card.Punish} → +{scaled} vs block", $"Speed {loadout.Speed}", Frozen("#EA580C"));
        }
        if (card.Element.Length > 0 && (card.Damage > 0 || card.Parry > 0))
        {
            var bonusParts = new List<string>();
            if (card.Element == loadout.Element) bonusParts.Add($"Same element +{PetGame.SameElementPercent - 100}%");
            if (foe is not null)
            {
                int pct = PetGame.TypePercent(card.Element, foe.Element);
                if (pct != 100) bonusParts.Add($"vs {PetGame.ElementOf(foe.Element).Name} {pct}%");
            }
            if (bonusParts.Count > 0) AddRow("Element", string.Join(" · ", bonusParts));
        }
        if (card.Block > 0) AddRow("Block", Scaled(card.Block, PetCards.Scale(card.Block, loadout.Guard), "Guard", loadout.Guard), "", Frozen("#3B82F6"));
        if (card.Thorns > 0) AddRow("Thorns", Scaled(card.Thorns, PetCards.Scale(card.Thorns, loadout.Guard), "Guard", loadout.Guard), "", Frozen("#16A34A"));
        if (card.Heal > 0) AddRow("Heal", Scaled(card.Heal, PetCards.Scale(card.Heal, loadout.Vitality), "Vitality", loadout.Vitality), "", Frozen("#16A34A"));
        if (card.Regen > 0) AddRow("Regen", Scaled(card.Regen, PetCards.Scale(card.Regen, loadout.Vitality), "Vitality", loadout.Vitality), "/ round", Frozen("#16A34A"));
        if (card.Burn > 0) AddRow("Burn", Scaled(card.Burn, PetCards.Scale(card.Burn, loadout.Power), "Power", loadout.Power), "/ round", Frozen("#EA580C"));
        if (card.Poison > 0) AddRow("Poison", Scaled(card.Poison, PetCards.Scale(card.Poison, loadout.Power), "Power", loadout.Power), "/ round", Frozen("#4D7C0F"));
        if (card.Weak > 0) AddRow("Weaken", $"{card.Weak} round{(card.Weak == 1 ? "" : "s")}", "-25% damage", Frozen("#7C3AED"));
        if (card.Sleep > 0) AddRow("Sleep", $"{card.Sleep} round{(card.Sleep == 1 ? "" : "s")}", "wakes on HP hit", Frozen("#6366F1"));
        if (card.Fear > 0) AddRow("Fear", $"{card.Fear} attack{(card.Fear == 1 ? "" : "s")}", "misses", Frozen("#9333EA"));
        if (card.Slow > 0) AddRow("Slow", $"{card.Slow} round{(card.Slow == 1 ? "" : "s")}", "acts last", Frozen("#0284C7"));
        if (card.Rage > 0) AddRow("Rage", $"{card.Rage} round{(card.Rage == 1 ? "" : "s")}", "+30% damage", Frozen("#EF4444"));
        if (card.Lifesteal > 0) AddRow("Lifesteal", $"{card.Lifesteal}% of damage");
        if (card.Pierce) AddRow("Pierce", "Ignores block");
        if (card.Feint) AddRow("Feint", "Cannot be parried");
        if (card.Stun) AddRow("Stun", "Cancels slower attacks");
        if (card.Interrupt) AddRow("Interrupt", "Cancels non-attacks");
        if (card.Cleanse) AddRow("Cleanse", "Clears ailments");
        if (card.Draw > 0) AddRow("Draw", $"+{card.Draw} card{(card.Draw == 1 ? "" : "s")}");
        if (card.Energy > 0) AddRow("Energy", $"+{card.Energy} now", "", Frozen("#D97706"));
        if (card.NextEnergy > 0) AddRow("Next round", $"+{card.NextEnergy} energy", "", Frozen("#D97706"));
        if (card.StealEnergy > 0) AddRow("Steal", $"{card.StealEnergy} energy", "", Frozen("#D97706"));
        if (card.OnHit.Length > 0)
        {
            var (tk, ta) = PetCard.ParseTrap(card.OnHit);
            string trapDesc = tk switch
            {
                "burn" => $"Burn {Scaled(ta, PetCards.Scale(ta, loadout.Guard), "Guard", loadout.Guard)}",
                "poison" => $"Poison {Scaled(ta, PetCards.Scale(ta, loadout.Guard), "Guard", loadout.Guard)}",
                "weak" => "Weaken",
                "slow" => "Slow",
                "fear" => "Fear",
                "energy" => $"+{ta} energy next",
                _ => card.OnHit
            };
            AddRow("If hit", trapDesc, "", Frozen("#9333EA"));
        }

        var divider = new Border { Height = 1, Margin = new Thickness(0, 0, 0, 6) };
        divider.SetResourceReference(Border.BackgroundProperty, "Line");

        var content = new StackPanel { Children = { header, divider, rows } };
        var box = new Border
        {
            Width = 286,
            Padding = new Thickness(12, 10, 12, 10),
            CornerRadius = new CornerRadius(12),
            BorderThickness = new Thickness(1.6),
            BorderBrush = rarityBrush,
            Child = content,
            Effect = new DropShadowEffect { BlurRadius = 18, ShadowDepth = 4, Opacity = .32 }
        };
        box.SetResourceReference(Border.BackgroundProperty, "Surface");
        System.Windows.Automation.AutomationProperties.SetName(box, $"{card.Name} details");
        return box;
    }

    private static Border DetailChip(string text, Brush background) => new()
    {
        Background = background,
        CornerRadius = new CornerRadius(7),
        Padding = new Thickness(6, 1, 6, 1),
        Margin = new Thickness(0, 0, 4, 2),
        Child = new TextBlock { Text = text, FontSize = 10, FontWeight = FontWeights.SemiBold, Foreground = Brushes.White }
    };

    private void CardTile_RightClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement { Tag: string id } fe && PetCards.IsCard(id))
        {
            e.Handled = true;
            ShowCardDetail(PetCards.CardOf(id), fe);
        }
    }
}
