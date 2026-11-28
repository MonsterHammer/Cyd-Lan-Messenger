using System.Globalization;
using CydLan.Core;

namespace CydLan.Desktop;

/// <summary>A live one-for-one LAN card trade session between two online CydLan peers.</summary>
public sealed class CardTradeSession
{
    public required string Id { get; init; }
    public required string PeerId { get; init; }
    public required string OpponentName { get; init; }
    public required PetAppearance MyLook { get; init; }
    public required PetAppearance TheirLook { get; init; }
    public string? MyOffer { get; internal set; }
    public string? TheirOffer { get; internal set; }
    public bool MyLocked { get; internal set; }
    public bool TheirLocked { get; internal set; }
    public bool Finished { get; internal set; }
    public bool Completed { get; internal set; }
    public string Status { get; internal set; } = "";

    public event Action? StateChanged;
    internal void RaiseState() => StateChanged?.Invoke();
}

public sealed partial class PetArena
{
    private (string PeerId, string TradeId, DateTime ExpiresUtc)? tradeChallenge;

    public string? TradeChallengePeerId => tradeChallenge?.PeerId;
    public CardTradeSession? CurrentTrade { get; private set; }
    public event Action<CardTradeSession>? TradeStarted;

    private bool InAnySession => Current is { Finished: false } || CurrentCard is { Finished: false } || CurrentTrade is { Finished: false };

    public async Task InviteTradeAsync(Peer peer)
    {
        if (InAnySession) return;
        EnsureStarted();
        if (!peer.Online || !LanService.SupportsPetBattles(peer.Version)) { SetStatus("Unavailable"); return; }
        await CancelTradeInviteAsync();
        await CancelCardChallengeAsync();
        await CancelChallengeAsync();
        string id = NewId();
        var card = MyCard();
        tradeChallenge = (peer.Id, id, DateTime.UtcNow.AddSeconds(InviteSeconds));
        Changed(nameof(TradeChallengePeerId));
        SetStatus($"Trade invite sent to {peer.Name}…");
        clock.Start();
        if (!await SendAsync(peer.Id, "invite", id, new() { ["mode"] = "trade", ["card"] = PetGame.EncodeCard(card) }))
        {
            tradeChallenge = null;
            Changed(nameof(TradeChallengePeerId));
            SetStatus("Couldn't reach " + peer.Name);
        }
    }

    public async Task CancelTradeInviteAsync()
    {
        if (tradeChallenge is not { } t) return;
        tradeChallenge = null;
        Changed(nameof(TradeChallengePeerId));
        SetStatus("");
        await SendAsync(t.PeerId, "cancel", t.TradeId, new() { ["mode"] = "trade" });
    }

    public async Task AcceptTradeAsync(PetInvite invite)
    {
        Invites.Remove(invite);
        EnsureStarted();
        if (InAnySession)
        {
            await SendAsync(invite.PeerId, "decline", invite.BattleId, new() { ["mode"] = "trade", ["reason"] = "busy" });
            return;
        }
        if (DateTime.UtcNow > invite.ExpiresUtc) { SetStatus("Trade invite expired"); return; }
        await CancelTradeInviteAsync();
        await CancelCardChallengeAsync();
        await CancelChallengeAsync();
        var card = MyCard();
        if (!await SendAsync(invite.PeerId, "accept", invite.BattleId, new() { ["mode"] = "trade", ["card"] = PetGame.EncodeCard(card) }))
        {
            SetStatus("Couldn't reach " + invite.PeerName);
            return;
        }
        StartTrade(invite.BattleId, invite.PeerId, PeerName(invite.PeerId, invite.Card.Name), invite.Appearance);
    }

    public async Task DeclineTradeAsync(PetInvite invite)
    {
        Invites.Remove(invite);
        await SendAsync(invite.PeerId, "decline", invite.BattleId, new() { ["mode"] = "trade", ["reason"] = "no" });
    }

    private void StartTrade(string id, string peerId, string opponentName, PetAppearance theirLook)
    {
        CurrentTrade = new CardTradeSession
        {
            Id = id,
            PeerId = peerId,
            OpponentName = opponentName,
            MyLook = BattleLook,
            TheirLook = theirLook
        };
        SetStatus("");
        Changed(nameof(CurrentTrade));
        clock.Start();
        TradeStarted?.Invoke(CurrentTrade);
    }

    public async Task SetTradeOfferAsync(string? cardId)
    {
        if (CurrentTrade is not { Finished: false } s) return;
        if (cardId is not null && (!PetCards.IsCard(cardId) || PetCards.Owned(Progress, cardId) <= 0)) return;
        if (s.MyOffer == cardId) return;
        s.MyOffer = cardId;
        s.MyLocked = false;
        s.TheirLocked = false;
        s.Status = "";
        await SendTradeSyncAsync(s);
        s.RaiseState();
    }

    public async Task ToggleTradeLockAsync()
    {
        if (CurrentTrade is not { Finished: false } s) return;
        if (!s.MyLocked && (s.MyOffer is null || s.TheirOffer is null)) return;
        if (!s.MyLocked && !PetCards.CanTrade(Progress, s.MyOffer!, s.TheirOffer!))
        {
            s.Status = "Cannot hold more than " + PetCards.MaxOwned;
            s.RaiseState();
            return;
        }
        s.MyLocked = !s.MyLocked;
        s.Status = "";
        await SendTradeSyncAsync(s);
        TryCompleteTrade(s);
        s.RaiseState();
    }

    public async Task CancelTradeAsync()
    {
        if (CurrentTrade is not { Finished: false } s) return;
        await SendAsync(s.PeerId, "leave", s.Id, new() { ["mode"] = "trade" });
        s.Finished = true;
        s.Status = "Trade cancelled";
        s.RaiseState();
    }

    public void DismissTrade()
    {
        if (CurrentTrade is { Finished: false }) return;
        CurrentTrade = null;
        Changed(nameof(CurrentTrade));
    }

    private async Task SendTradeSyncAsync(CardTradeSession s)
    {
        if (!await SendAsync(s.PeerId, "move", s.Id, new()
        {
            ["mode"] = "trade",
            ["offer"] = s.MyOffer ?? "",
            ["picks"] = s.TheirOffer ?? "",
            ["hash"] = s.MyLocked ? "1" : "0"
        }))
        {
            s.Finished = true;
            s.Status = "Connection lost";
        }
    }

    private void TryCompleteTrade(CardTradeSession s)
    {
        if (s.Finished || !s.MyLocked || !s.TheirLocked || s.MyOffer is not { } give || s.TheirOffer is not { } take) return;
        if (!PetCards.TradeCards(Progress, give, take))
        {
            s.MyLocked = false;
            s.TheirLocked = false;
            s.Status = "Trade unavailable";
            return;
        }
        Save();
        s.Completed = true;
        s.Finished = true;
        s.Status = $"Traded {PetCards.CardOf(give).Name} for {PetCards.CardOf(take).Name}";
        PetAction?.Invoke("laugh", null);
    }

    private bool HandleTradeMessage(string peerId, IReadOnlyDictionary<string, string> f)
    {
        string op = f.GetValueOrDefault("op", ""), bid = f.GetValueOrDefault("bid", "");
        bool isTrade = f.GetValueOrDefault("mode") == "trade"
            || (CurrentTrade is { } cur && cur.Id == bid && cur.PeerId == peerId)
            || (tradeChallenge is { } ch && ch.TradeId == bid && ch.PeerId == peerId)
            || Invites.Any(i => i.IsTrade && i.BattleId == bid && i.PeerId == peerId);
        if (!isTrade) return false;

        switch (op)
        {
            case "invite":
            {
                if (PetGame.DecodeCard(f.GetValueOrDefault("card")) is not { } card) return true;
                if (InAnySession || Invites.Count >= 5)
                {
                    _ = SendAsync(peerId, "decline", bid, new() { ["mode"] = "trade", ["reason"] = "busy" });
                    return true;
                }
                if (Invites.Any(i => i.BattleId == bid)) return true;
                foreach (var old in Invites.Where(i => i.PeerId == peerId).ToList()) Invites.Remove(old);
                var invite = new PetInvite(peerId, PeerName(peerId, card.Name), bid, card, "", DateTime.UtcNow.AddSeconds(InviteSeconds), isTrade: true);
                Invites.Add(invite);
                clock.Start();
                InviteReceived?.Invoke(invite);
                return true;
            }
            case "cancel":
                foreach (var i in Invites.Where(i => i.BattleId == bid && i.PeerId == peerId).ToList()) Invites.Remove(i);
                return true;
            case "decline":
                if (tradeChallenge is { } pending && pending.TradeId == bid && pending.PeerId == peerId)
                {
                    tradeChallenge = null;
                    Changed(nameof(TradeChallengePeerId));
                    SetStatus(f.GetValueOrDefault("reason") == "busy" ? PeerName(peerId, "They") + " is busy" : PeerName(peerId, "They") + " declined");
                }
                return true;
            case "accept":
            {
                if (tradeChallenge is not { } mine || mine.TradeId != bid || mine.PeerId != peerId) return true;
                if (PetGame.DecodeCard(f.GetValueOrDefault("card")) is not { } card) return true;
                tradeChallenge = null;
                Changed(nameof(TradeChallengePeerId));
                var look = PetAppearance.FromWireCode(card.PetCode) ?? PetAppearance.Default;
                StartTrade(bid, peerId, PeerName(peerId, card.Name), look);
                return true;
            }
            case "move":
            {
                if (CurrentTrade is not { Finished: false } s || s.Id != bid || s.PeerId != peerId) return true;
                string rawOffer = f.GetValueOrDefault("offer", "");
                string? theirOffer = rawOffer.Length > 0 && PetCards.IsCard(rawOffer) ? rawOffer : null;
                bool theirLocked = f.GetValueOrDefault("hash") == "1";
                string theirExpectedMine = f.GetValueOrDefault("picks", "");
                if (theirOffer != s.TheirOffer)
                {
                    s.TheirOffer = theirOffer;
                    s.MyLocked = false;
                    s.TheirLocked = false;
                    s.Status = "";
                }
                else
                {
                    s.TheirLocked = theirLocked && theirOffer is not null && theirExpectedMine == (s.MyOffer ?? "");
                }
                TryCompleteTrade(s);
                s.RaiseState();
                return true;
            }
            case "leave":
            {
                if (CurrentTrade is not { Finished: false } s || s.Id != bid || s.PeerId != peerId) return true;
                s.Finished = true;
                s.Status = s.OpponentName + " cancelled";
                s.RaiseState();
                return true;
            }
        }
        return true;
    }

    private void OnTradePeerOffline(string peerId)
    {
        if (tradeChallenge is { } t && t.PeerId == peerId)
        {
            tradeChallenge = null;
            Changed(nameof(TradeChallengePeerId));
            SetStatus("");
        }
        if (CurrentTrade is { Finished: false } s && s.PeerId == peerId)
        {
            s.Finished = true;
            s.Status = "Connection lost";
            s.RaiseState();
        }
    }

    private void TickTrade(DateTime now)
    {
        if (tradeChallenge is { } t && now > t.ExpiresUtc)
        {
            tradeChallenge = null;
            Changed(nameof(TradeChallengePeerId));
            SetStatus("No answer");
            _ = SendAsync(t.PeerId, "cancel", t.TradeId, new() { ["mode"] = "trade" });
        }
    }
}
