using System.Globalization;
using CydLan.Core;

namespace CydLan.Desktop;

/// <summary>A card battle against a world enemy or a LAN friend. Side 0 is always the challenger (or the player in world fights).</summary>
public sealed class CardBattleSession
{
    public string Id { get; init; } = "";
    /// <summary>The friend's peer id, or null for a world fight.</summary>
    public string? PeerId { get; init; }
    public int MySide { get; init; }
    public required CardBattle Battle { get; init; }
    public required string OpponentName { get; init; }
    public required PetAppearance MyLook { get; init; }
    public required PetAppearance TheirLook { get; init; }
    public PetWorld.Stage? Stage { get; init; }
    public PetWorld.Enemy? Enemy { get; init; }
    public PetTournament.Event? Tournament { get; init; }
    public PetTournament.Contender? TournamentContender { get; init; }
    public bool TournamentChampion { get; internal set; }
    public bool IsLan => PeerId is not null;
    public bool IsTournament => Tournament is not null && TournamentContender is not null;
    public CardBattle.Side Me => Battle.Sides[MySide];
    public CardBattle.Side Them => Battle.Sides[1 - MySide];
    public List<int>? MyPicks { get; internal set; }
    internal List<int>? TheirPicks { get; set; }
    internal DateTime TurnDeadlineUtc { get; set; }
    internal DateTime? WaitingSinceUtc { get; set; }
    public bool WaitingForThem => MyPicks is not null && TheirPicks is null && !Finished;
    public int SecondsLeft => Math.Max(0, (int)Math.Ceiling((TurnDeadlineUtc - DateTime.UtcNow).TotalSeconds));
    public bool Finished { get; internal set; }
    /// <summary>1 win, 0 draw, -1 loss.</summary>
    public int Outcome { get; internal set; }
    public string EndReason { get; internal set; } = "";
    public PetGame.BattleReward? Reward { get; internal set; }
    public PetCard? Drop { get; internal set; }
    public bool StageCleared { get; internal set; }
    public IReadOnlyList<string> TheirLastCards { get; internal set; } = [];
    public IReadOnlyList<string> MyLastCards { get; internal set; } = [];

    public event Action<IReadOnlyList<CardEvent>>? RoundPlayed;
    public event Action? Ended;
    public event Action? StateChanged;
    internal void RaiseRound(IReadOnlyList<CardEvent> events) => RoundPlayed?.Invoke(events);
    internal void RaiseEnded() => Ended?.Invoke();
    internal void RaiseState() => StateChanged?.Invoke();
}

public sealed partial class PetArena
{
    private (string PeerId, string BattleId, string Seed, PetCards.Loadout Loadout, DateTime ExpiresUtc)? cardChallenge;

    public string? CardChallengePeerId => cardChallenge?.PeerId;

    public PetCards.Loadout MyLoadout => PetCards.LoadoutFor(Progress, model.Settings.DisplayName, PetAppearance.From(model.Settings).ToWireCode());

    /// <summary>Looks don't change stats; the companion fights as it looks.</summary>
    public PetAppearance BattleLook => model.PetAppearance;

    /// <summary>Gives a new install its seed, starter cards and deck.</summary>
    public void EnsureStarted()
    {
        bool fresh = Progress.Seed == 0 || Progress.Cards.Count == 0 || Progress.Deck.Count == 0;
        PetCards.EnsureStarted(Progress, random);
        if (fresh) Save();
    }

    private bool InBattle => CurrentCard is { Finished: false };

    // ── Stats ────────────────────────────────────────────────────────────

    public bool Reroll()
    {
        if (InBattle || !PetGame.Reroll(Progress, random)) return false;
        Save();
        PetAction?.Invoke("spin", null);
        return true;
    }

    // ── Deck and collection ──────────────────────────────────────────────

    public bool AddToDeck(string id)
    {
        if (InBattle || !PetCards.AddToDeck(Progress, id)) return false;
        Save();
        return true;
    }

    public bool RemoveFromDeck(string id)
    {
        if (InBattle || !PetCards.RemoveFromDeck(Progress, id)) return false;
        Save();
        return true;
    }

    public int SellCard(string id)
    {
        if (InBattle) return 0;
        int coins = PetCards.SellCard(Progress, id);
        if (coins <= 0) return 0;
        Save();
        return coins;
    }

    // ── Daily shop & raffle ──────────────────────────────────────────────

    public static string Today => DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    public IReadOnlyList<PetCards.ShopOffer> ShopOffers => PetCards.Shop(Progress, Today);

    public bool BuyCard(int slot)
    {
        if (!PetCards.BuyFromShop(Progress, Today, slot)) return false;
        Save();
        PetAction?.Invoke("surprise", null);
        return true;
    }

    public PetCards.RaffleResult? SpinRaffle()
    {
        if (InBattle) return null;
        var result = PetCards.SpinRaffle(Progress, random);
        if (result is null) return null;
        Save();
        PetAction?.Invoke("spin", null);
        return result;
    }

    // ── World & local tournament ─────────────────────────────────────────

    public PetWorld.Stage CurrentStage => PetWorld.StageOf(Progress.Seed, Progress.Stage);

    public PetTournament.Event CurrentTournament => PetTournament.EventFor(Progress, DateTime.Now);

    public static PetAppearance EnemyLook(PetWorld.Enemy enemy, out string kind)
    {
        var preset = FloatingPetStyle.Presets[enemy.LookSeed % FloatingPetStyle.Presets.Count];
        kind = preset.Name;
        return preset.Appearance;
    }

    public static PetAppearance ContenderLook(PetTournament.Contender contender, out string kind)
    {
        var preset = FloatingPetStyle.Presets[contender.LookSeed % FloatingPetStyle.Presets.Count];
        kind = preset.Name;
        return preset.Appearance;
    }

    // ── Card battles (World + LAN Duels) ─────────────────────────────────

    public CardBattleSession? CurrentCard { get; private set; }
    public event Action<CardBattleSession>? CardBattleStarted;

    /// <summary>Starts the next fight of the current world stage.</summary>
    public void StartCardBattle()
    {
        if (InBattle || Current is { Finished: false }) return;
        EnsureStarted();
        if (!PetCards.DeckReady(Progress)) { SetStatus($"Deck needs {PetCards.DeckSize} cards"); return; }
        _ = CancelCardChallengeAsync();
        SetStatus("");
        var stage = CurrentStage;
        var enemy = stage.Fights[Progress.StageFight];
        var look = EnemyLook(enemy, out string kind);
        var foe = PetWorld.LoadoutOf(stage, enemy, $"{enemy.Name} {kind}", look.ToWireCode());
        var mine = MyLoadout;
        CurrentCard = new CardBattleSession
        {
            Id = NewId(),
            MySide = 0,
            Battle = new CardBattle(mine, foe, (ulong)random.NextInt64()),
            OpponentName = foe.Name,
            MyLook = BattleLook,
            TheirLook = look,
            Stage = stage,
            Enemy = enemy
        };
        Changed(nameof(CurrentCard));
        CardBattleStarted?.Invoke(CurrentCard);
        PetAction?.Invoke("fired", null);
    }

    /// <summary>Starts the next match of the current local-time tournament bracket.</summary>
    public void StartTournamentBattle()
    {
        if (InBattle || Current is { Finished: false }) return;
        EnsureStarted();
        if (!PetCards.DeckReady(Progress)) { SetStatus($"Deck needs {PetCards.DeckSize} cards"); return; }
        _ = CancelCardChallengeAsync();
        SetStatus("");
        var ev = CurrentTournament;
        PetTournament.SyncSlot(Progress, ev.Slot);
        var contender = ev.Bracket[Progress.TournamentRound];
        var look = ContenderLook(contender, out string kind);
        var foe = PetTournament.LoadoutOf(Progress, contender, $"{contender.Name} {kind}", look.ToWireCode());
        var mine = MyLoadout;
        CurrentCard = new CardBattleSession
        {
            Id = NewId(),
            MySide = 0,
            Battle = new CardBattle(mine, foe, (ulong)random.NextInt64()),
            OpponentName = foe.Name,
            MyLook = BattleLook,
            TheirLook = look,
            Tournament = ev,
            TournamentContender = contender
        };
        Changed(nameof(CurrentCard));
        CardBattleStarted?.Invoke(CurrentCard);
        PetAction?.Invoke("fired", null);
    }

    /// <summary>Invites an online CydLan friend to a lockstep card battle duel.</summary>
    public async Task ChallengeCardDuelAsync(Peer peer)
    {
        if (InBattle || Current is { Finished: false }) return;
        EnsureStarted();
        if (!PetCards.DeckReady(Progress)) { SetStatus($"Deck needs {PetCards.DeckSize} cards"); return; }
        if (!peer.Online || !LanService.SupportsPetBattles(peer.Version)) { SetStatus("Unavailable"); return; }
        await CancelCardChallengeAsync();
        await CancelChallengeAsync();
        var loadout = MyLoadout;
        string id = NewId(), seed = NewSeed();
        cardChallenge = (peer.Id, id, seed, loadout, DateTime.UtcNow.AddSeconds(InviteSeconds));
        Changed(nameof(CardChallengePeerId));
        SetStatus($"Waiting for {peer.Name}…");
        clock.Start();
        if (!await SendAsync(peer.Id, "invite", id, new()
        {
            ["mode"] = "cards",
            ["loadout"] = PetCards.EncodeLoadout(loadout),
            ["seed"] = seed
        }))
        {
            cardChallenge = null;
            Changed(nameof(CardChallengePeerId));
            SetStatus("Couldn't reach " + peer.Name);
        }
    }

    public async Task CancelCardChallengeAsync()
    {
        if (cardChallenge is not { } c) return;
        cardChallenge = null;
        Changed(nameof(CardChallengePeerId));
        SetStatus("");
        await SendAsync(c.PeerId, "cancel", c.BattleId, new() { ["mode"] = "cards" });
    }

    public async Task AcceptCardDuelAsync(PetInvite invite)
    {
        Invites.Remove(invite);
        if (invite.CardLoadout is not { } theirLoadout) return;
        EnsureStarted();
        if (InBattle || Current is { Finished: false })
        {
            await SendAsync(invite.PeerId, "decline", invite.BattleId, new() { ["mode"] = "cards", ["reason"] = "busy" });
            return;
        }
        if (!PetCards.DeckReady(Progress))
        {
            SetStatus($"Deck needs {PetCards.DeckSize} cards");
            await SendAsync(invite.PeerId, "decline", invite.BattleId, new() { ["mode"] = "cards", ["reason"] = "no" });
            return;
        }
        if (DateTime.UtcNow > invite.ExpiresUtc) { SetStatus("Challenge expired"); return; }
        await CancelCardChallengeAsync();
        await CancelChallengeAsync();
        var mine = MyLoadout;
        string seed = NewSeed();
        if (!await SendAsync(invite.PeerId, "accept", invite.BattleId, new()
        {
            ["mode"] = "cards",
            ["loadout"] = PetCards.EncodeLoadout(mine),
            ["seed"] = seed
        }))
        {
            SetStatus("Couldn't reach " + invite.PeerName);
            return;
        }
        StartLanCardDuel(invite.BattleId, invite.PeerId, PeerName(invite.PeerId, theirLoadout.Name), mySide: 1, theirLoadout, mine, CombineSeeds(invite.Seed, seed));
    }

    public async Task DeclineCardDuelAsync(PetInvite invite)
    {
        Invites.Remove(invite);
        await SendAsync(invite.PeerId, "decline", invite.BattleId, new() { ["mode"] = "cards", ["reason"] = "no" });
    }

    private void StartLanCardDuel(string bid, string peerId, string opponentName, int mySide, PetCards.Loadout side0, PetCards.Loadout side1, ulong seed)
    {
        var theirLoadout = mySide == 0 ? side1 : side0;
        var session = new CardBattleSession
        {
            Id = bid,
            PeerId = peerId,
            MySide = mySide,
            Battle = new CardBattle(side0, side1, seed),
            OpponentName = opponentName,
            MyLook = BattleLook,
            TheirLook = PetAppearance.FromWireCode(theirLoadout.PetCode) ?? PetAppearance.Default,
            TurnDeadlineUtc = DateTime.UtcNow.AddSeconds(TurnSeconds + 6)
        };
        CurrentCard = session;
        SetStatus("");
        Changed(nameof(CurrentCard));
        clock.Start();
        CardBattleStarted?.Invoke(session);
        PetAction?.Invoke("fired", null);
    }

    /// <summary>Plays the chosen hand cards against the AI in a world battle.</summary>
    public IReadOnlyList<CardEvent> PlayCards(IEnumerable<int> handIndexes)
    {
        if (CurrentCard is not { Finished: false, IsLan: false } s) return [];
        var mine = s.Battle.Validate(0, handIndexes);
        var theirs = s.Battle.ChooseAi(1, random);
        s.TheirLastCards = theirs.Select(i => s.Them.Hand[i]).ToList();
        s.MyLastCards = mine.Select(i => s.Me.Hand[i]).ToList();
        var events = s.Battle.PlayRound(mine, theirs);
        s.RaiseRound(events);
        if (s.Battle.Over) FinishCard(s, s.Battle.Winner < 0 ? 0 : s.Battle.Winner == 0 ? 1 : -1, "");
        return events;
    }

    /// <summary>Submits the chosen hand cards for either a world fight or a LAN card duel.</summary>
    public async Task PlayCardsAsync(IEnumerable<int> handIndexes)
    {
        if (CurrentCard is not { Finished: false } s) return;
        if (!s.IsLan) { PlayCards(handIndexes); return; }
        if (s.MyPicks is not null) return;
        string hash = s.Battle.Hash();
        int round = s.Battle.Round + 1;
        var mine = s.Battle.Validate(s.MySide, handIndexes);
        s.MyPicks = mine;
        s.WaitingSinceUtc = DateTime.UtcNow;
        string wire = string.Join(',', mine.Select(i => i.ToString(CultureInfo.InvariantCulture)));
        if (!await SendAsync(s.PeerId!, "move", s.Id, new()
        {
            ["mode"] = "cards",
            ["turn"] = round.ToString(CultureInfo.InvariantCulture),
            ["picks"] = wire,
            ["hash"] = hash
        }))
        {
            EndCardNoResult(s, "Connection lost");
            return;
        }
        s.RaiseState();
        TryResolveCardRound(s);
    }

    public void RestartCardTurnTimer()
    {
        if (CurrentCard is { Finished: false, IsLan: true } s) s.TurnDeadlineUtc = DateTime.UtcNow.AddSeconds(TurnSeconds);
    }

    private void TryResolveCardRound(CardBattleSession s)
    {
        if (s.Finished || s.MyPicks is null || s.TheirPicks is null) return;
        var mine = s.MyPicks;
        var theirs = s.TheirPicks;
        s.MyPicks = null; s.TheirPicks = null; s.WaitingSinceUtc = null;
        s.MyLastCards = mine.Select(i => s.Me.Hand[i]).ToList();
        s.TheirLastCards = theirs.Select(i => s.Them.Hand[i]).ToList();
        var p0 = s.MySide == 0 ? mine : theirs;
        var p1 = s.MySide == 0 ? theirs : mine;
        var events = s.Battle.PlayRound(p0, p1);
        s.TurnDeadlineUtc = DateTime.UtcNow.AddSeconds(TurnSeconds + 8);
        s.RaiseRound(events);
        if (s.Battle.Over)
        {
            int outcome = s.Battle.Winner < 0 ? 0 : s.Battle.Winner == s.MySide ? 1 : -1;
            FinishCard(s, outcome, "");
        }
        else s.RaiseState();
    }

    private static List<int> ParsePicks(string? raw)
    {
        var list = new List<int>();
        if (string.IsNullOrWhiteSpace(raw)) return list;
        foreach (var token in raw.Split(',', StringSplitOptions.RemoveEmptyEntries))
            if (int.TryParse(token, NumberStyles.None, CultureInfo.InvariantCulture, out int i)) list.Add(i);
        return list;
    }

    private bool HandleCardMessage(string peerId, IReadOnlyDictionary<string, string> f)
    {
        string op = f.GetValueOrDefault("op", ""), bid = f.GetValueOrDefault("bid", "");
        bool isCard = f.GetValueOrDefault("mode") == "cards"
            || (CurrentCard is { IsLan: true } cur && cur.Id == bid && cur.PeerId == peerId)
            || (cardChallenge is { } ch && ch.BattleId == bid && ch.PeerId == peerId)
            || Invites.Any(i => i.IsCardDuel && i.BattleId == bid && i.PeerId == peerId);
        if (!isCard) return false;

        switch (op)
        {
            case "invite":
            {
                if (PetCards.DecodeLoadout(f.GetValueOrDefault("loadout")) is not { } loadout || f.GetValueOrDefault("seed", "") is not { Length: > 0 } seed) return true;
                if (InBattle || Current is { Finished: false } || Invites.Count >= 5)
                {
                    _ = SendAsync(peerId, "decline", bid, new() { ["mode"] = "cards", ["reason"] = "busy" });
                    return true;
                }
                if (Invites.Any(i => i.BattleId == bid)) return true;
                foreach (var old in Invites.Where(i => i.PeerId == peerId).ToList()) Invites.Remove(old);
                var stub = new PetGame.FighterCard { Name = loadout.Name, PetCode = loadout.PetCode, Level = loadout.Level, Element = loadout.Element };
                var invite = new PetInvite(peerId, PeerName(peerId, loadout.Name), bid, stub, seed, DateTime.UtcNow.AddSeconds(InviteSeconds), loadout);
                Invites.Add(invite);
                clock.Start();
                InviteReceived?.Invoke(invite);
                return true;
            }
            case "cancel":
                foreach (var i in Invites.Where(i => i.BattleId == bid && i.PeerId == peerId).ToList()) Invites.Remove(i);
                return true;
            case "decline":
                if (cardChallenge is { } pending && pending.BattleId == bid && pending.PeerId == peerId)
                {
                    cardChallenge = null;
                    Changed(nameof(CardChallengePeerId));
                    SetStatus(f.GetValueOrDefault("reason") == "busy" ? PeerName(peerId, "They") + " is busy" : PeerName(peerId, "They") + " declined");
                }
                return true;
            case "accept":
            {
                if (cardChallenge is not { } mine || mine.BattleId != bid || mine.PeerId != peerId) return true;
                if (PetCards.DecodeLoadout(f.GetValueOrDefault("loadout")) is not { } theirLoadout || f.GetValueOrDefault("seed", "") is not { Length: > 0 } seed) return true;
                cardChallenge = null;
                Changed(nameof(CardChallengePeerId));
                StartLanCardDuel(bid, peerId, PeerName(peerId, theirLoadout.Name), mySide: 0, mine.Loadout, theirLoadout, CombineSeeds(mine.Seed, seed));
                return true;
            }
            case "move":
            {
                if (CurrentCard is not { Finished: false, IsLan: true } s || s.Id != bid || s.PeerId != peerId) return true;
                if (!int.TryParse(f.GetValueOrDefault("turn"), NumberStyles.None, CultureInfo.InvariantCulture, out int round)) return true;
                if (round <= s.Battle.Round) return true;
                if (round != s.Battle.Round + 1 || f.GetValueOrDefault("hash") != s.Battle.Hash())
                {
                    _ = SendAsync(peerId, "leave", s.Id, new() { ["mode"] = "cards", ["reason"] = "desync" });
                    EndCardNoResult(s, "Out of sync");
                    return true;
                }
                s.TheirPicks = s.Battle.Validate(1 - s.MySide, ParsePicks(f.GetValueOrDefault("picks")));
                s.RaiseState();
                TryResolveCardRound(s);
                return true;
            }
            case "leave":
            {
                if (CurrentCard is not { Finished: false, IsLan: true } s || s.Id != bid || s.PeerId != peerId) return true;
                if (f.GetValueOrDefault("reason") == "desync") EndCardNoResult(s, "Out of sync");
                else FinishCard(s, 1, s.OpponentName + " left");
                return true;
            }
        }
        return true;
    }

    private void OnCardPeerOffline(string peerId)
    {
        if (cardChallenge is { } c && c.PeerId == peerId) { cardChallenge = null; Changed(nameof(CardChallengePeerId)); SetStatus(""); }
        if (CurrentCard is { Finished: false, IsLan: true } s && s.PeerId == peerId) EndCardNoResult(s, "Connection lost");
    }

    private void TickCardDuel(DateTime now)
    {
        if (cardChallenge is { } c && now > c.ExpiresUtc)
        {
            cardChallenge = null;
            Changed(nameof(CardChallengePeerId));
            SetStatus("No answer");
            _ = SendAsync(c.PeerId, "cancel", c.BattleId, new() { ["mode"] = "cards" });
        }
        if (CurrentCard is { Finished: false, IsLan: true } s)
        {
            if (s.MyPicks is null && now > s.TurnDeadlineUtc) _ = PlayCardsAsync([]);
            else if (s.WaitingSinceUtc is { } since && (now - since).TotalSeconds > SilenceSeconds) EndCardNoResult(s, "Connection lost");
            s.RaiseState();
        }
    }

    public void LeaveCardBattle()
    {
        if (CurrentCard is not { Finished: false } s) return;
        if (s.IsLan) _ = SendAsync(s.PeerId!, "leave", s.Id, new() { ["mode"] = "cards" });
        FinishCard(s, -1, "You left");
    }

    public void DismissCardBattle()
    {
        if (InBattle) return;
        CurrentCard = null;
        Changed(nameof(CurrentCard));
    }

    private void EndCardNoResult(CardBattleSession s, string reason)
    {
        if (s.Finished) return;
        s.Finished = true;
        s.Outcome = 0;
        s.EndReason = reason;
        s.RaiseEnded();
        s.RaiseState();
        PetAction?.Invoke("nervous", null);
    }

    private void FinishCard(CardBattleSession s, int outcome, string reason)
    {
        if (s.Finished) return;
        s.Finished = true;
        s.Outcome = outcome;
        s.EndReason = reason;
        // Leaving pays nothing; a fought loss still pays a little.
        int opponentLevel = s.Stage?.Number ?? s.Them.Loadout.Level;
        bool boss = s.Enemy?.Boss == true;
        Progress.Fullness = 100; Progress.FullnessAtUtc = DateTime.UtcNow;
        if (s.IsTournament && reason.Length == 0 && s.Tournament is { } tev && s.TournamentContender is { } tc)
        {
            PetTournament.Advance(Progress, tev.Slot, outcome > 0, out bool champion, out bool firstClear);
            s.TournamentChampion = champion;
            s.Reward = PetTournament.Reward(Progress, outcome, tc.Round, champion, firstClear, DateTime.UtcNow);
            if (champion && PetTournament.RollChampionCard(Progress, random, firstClear) is { } drop && PetCards.AddCard(Progress, drop.Id))
                s.Drop = drop;
        }
        else
        {
            s.Reward = reason == "You left" ? new PetGame.BattleReward(0, 0, 0) : PetGame.Reward(Progress, outcome, opponentLevel, friend: true, DateTime.UtcNow, boss);
            if (!s.IsLan && reason.Length == 0 && s.Stage is { } st && s.Enemy is { } en)
            {
                if (PetCards.RollDrop(Progress, random, st.Number, en.Boss, outcome > 0) is { } drop && PetCards.AddCard(Progress, drop.Id))
                    s.Drop = drop;
                if (st.Number == Progress.Stage && en.Index == Progress.StageFight) s.StageCleared = PetWorld.Advance(Progress, outcome > 0);
            }
        }
        if (outcome > 0) Progress.CardWins++; else if (outcome < 0) Progress.CardLosses++;
        Save();
        s.RaiseEnded();
        s.RaiseState();
        PetAction?.Invoke(outcome > 0 ? "laugh" : outcome < 0 ? "cry" : "surprise", null);
        if (s.Reward.LevelsGained > 0) PetLine?.Invoke($"Level {Progress.Level}!");
    }
}
