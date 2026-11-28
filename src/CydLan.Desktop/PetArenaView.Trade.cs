using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using CydLan.Core;
using Brush = System.Windows.Media.Brush;

namespace CydLan.Desktop;

/// <summary>LAN card trade panel inside the Pets arena.</summary>
public partial class PetArenaView
{
    private sealed record TradePickRow(string Id, string Name, string Detail, int Cost, int Owned, ImageSource? Art, Brush KindBrush, Brush RarityBrush, bool Selected)
    {
        public string CountText => $"×{Owned}";
        public string PickName => $"Offer {Name}";
    }

    private CardTradeSession? tradeSession;

    private void ShowTrade(CardTradeSession s)
    {
        DetachTrade();
        DetachCard();
        Detach();
        tradeSession = s;
        s.StateChanged += RefreshTrade;
        LobbyPanel.Visibility = Visibility.Collapsed;
        BattlePanel.Visibility = Visibility.Collapsed;
        TradePanel.Visibility = Visibility.Visible;
        TradeMyName.Text = model.Settings.DisplayName;
        TradeTheirName.Text = s.OpponentName;
        TradeMyPet.PetStyle = s.MyLook;
        TradeTheirPet.PetStyle = s.TheirLook;
        RefreshTrade();
    }

    private void DetachTrade()
    {
        if (tradeSession is null) return;
        tradeSession.StateChanged -= RefreshTrade;
        tradeSession = null;
    }

    private void RefreshTrade()
    {
        if (tradeSession is not { } s || TradePanel.Visibility != Visibility.Visible) return;

        void RenderSlot(string? cardId, Border box, Border bg, System.Windows.Controls.Image art, TextBlock cost, TextBlock name, TextBlock detail, Border? empty)
        {
            if (cardId is { } id && PetCards.IsCard(id))
            {
                var c = PetCards.CardOf(id);
                box.Tag = c.Id;
                box.BorderBrush = RarityBrush(c.Rarity);
                bg.Background = KindBrush(c.Kind);
                art.Source = CardArt.For(c);
                cost.Text = c.Cost.ToString();
                name.Text = c.Name;
                detail.Text = $"{RarityName(c.Rarity)} · {ElementName(c)} · {c.Text}";
                box.Visibility = Visibility.Visible;
                if (empty is not null) empty.Visibility = Visibility.Collapsed;
            }
            else
            {
                box.Visibility = Visibility.Collapsed;
                if (empty is not null) empty.Visibility = Visibility.Visible;
            }
        }

        RenderSlot(s.MyOffer, TradeMyCardBox, TradeMyCardBg, TradeMyCardArt, TradeMyCardCost, TradeMyCardName, TradeMyCardDetail, TradeMyEmpty);
        RenderSlot(s.TheirOffer, TradeTheirCardBox, TradeTheirCardBg, TradeTheirCardArt, TradeTheirCardCost, TradeTheirCardName, TradeTheirCardDetail, TradeTheirEmpty);

        TradeMyState.Text = s.Completed ? "Traded" : s.MyLocked ? "Locked" : s.MyOffer is null ? "Choosing" : "Ready";
        TradeTheirState.Text = s.Completed ? "Traded" : s.TheirLocked ? "Locked" : s.TheirOffer is null ? "Choosing" : "Ready";
        TradeStatusText.Text = s.Status;
        TradeStatusText.Visibility = s.Status.Length > 0 ? Visibility.Visible : Visibility.Collapsed;

        TradeLockButton.Content = s.MyLocked ? "Unlock" : "Lock";
        TradeLockButton.IsEnabled = !s.Finished && s.MyOffer is not null && s.TheirOffer is not null;
        TradeLockButton.Visibility = s.Finished ? Visibility.Collapsed : Visibility.Visible;
        TradeCancelButton.Content = s.Finished ? "Done" : "Cancel";

        var p = arena.Progress;
        TradeCollectionList.ItemsSource = p.Cards
            .Select(kv => PetCards.CardOf(kv.Key))
            .OrderByDescending(c => PetCards.RarityRank(c.Rarity)).ThenBy(c => c.Element).ThenBy(c => c.Cost).ThenBy(c => c.Name)
            .Select(c => new TradePickRow(c.Id, c.Name, c.Text, c.Cost, PetCards.Owned(p, c.Id), CardArt.For(c),
                KindBrush(c.Kind), RarityBrush(c.Rarity), s.MyOffer == c.Id))
            .ToList();
        TradeCollectionList.IsEnabled = !s.Finished;
    }

    private async void TradePick_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: string id } || tradeSession is not { Finished: false } s) return;
        await arena.SetTradeOfferAsync(s.MyOffer == id ? null : id);
    }

    private async void TradeLock_Click(object sender, RoutedEventArgs e) => await arena.ToggleTradeLockAsync();

    private async void TradeCancel_Click(object sender, RoutedEventArgs e)
    {
        if (tradeSession is { Finished: false })
        {
            await arena.CancelTradeAsync();
            return;
        }
        arena.DismissTrade();
        ShowLobby();
    }
}
