using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Windows.Threading;
using CydLan.Core;

namespace CydLan.Desktop;

/// <summary>An incoming battle challenge or card trade invite from a LAN friend.</summary>
public sealed class PetInvite(string peerId, string peerName, string battleId, PetGame.FighterCard card, string seed, DateTime expiresUtc, PetCards.Loadout? cardLoadout = null, bool isTrade = false)
{
    public string PeerId { get; } = peerId;
    public string PeerName { get; } = peerName;
    public string BattleId { get; } = battleId;
    public PetGame.FighterCard Card { get; } = card;
    public PetCards.Loadout? CardLoadout { get; } = cardLoadout;
    public bool IsCardDuel => CardLoadout is not null;
    public bool IsTrade { get; } = isTrade;
    public string Seed { get; } = seed;
    public DateTime ExpiresUtc { get; } = expiresUtc;
    public PetAppearance Appearance { get; } = PetAppearance.FromWireCode(cardLoadout?.PetCode ?? card.PetCode) ?? PetAppearance.Default;
    public string Summary => IsTrade ? "Card trade"
        : IsCardDuel ? $"Card duel · Lv {CardLoadout!.Level} · {PetGame.ElementOf(CardLoadout.Element).Name}"
        : $"Lv {Card.Level} · {PetGame.ElementOf(Card.Element).Name}";
    public string AcceptText => IsTrade ? "Trade" : "Duel";
}

/// <summary>One battle, against a LAN friend or a practice opponent. Side 0 is always the challenger.</summary>
public sealed class PetBattleSession
{
    public required string Id { get; init; }
    /// <summary>The friend's peer id, or null for practice.</summary>
    public string? PeerId { get; init; }
    public required string OpponentName { get; init; }
    public required int MySide { get; init; }
    public required PetBattle Battle { get; init; }
    public int TheirSide => 1 - MySide;
    public PetBattle.Fighter Me => Battle.Fighters[MySide];
    public PetBattle.Fighter Them => Battle.Fighters[TheirSide];
    public bool IsPractice => PeerId is null;
    public string? MyMove { get; internal set; }
    internal string? TheirMove { get; set; }
    internal DateTime TurnDeadlineUtc { get; set; }
    internal DateTime? WaitingSinceUtc { get; set; }
    public bool Finished { get; internal set; }
    /// <summary>1 win, 0 draw, -1 loss.</summary>
    public int Outcome { get; internal set; }
    public string EndReason { get; internal set; } = "";
    public PetGame.BattleReward? Reward { get; internal set; }
    public bool WaitingForThem => MyMove is not null && TheirMove is null && !Finished;
    public int SecondsLeft => Math.Max(0, (int)Math.Ceiling((TurnDeadlineUtc - DateTime.UtcNow).TotalSeconds));

    public event Action<IReadOnlyList<BattleEvent>>? TurnResolved;
    public event Action? Ended;
    public event Action? StateChanged;
    internal void RaiseTurn(IReadOnlyList<BattleEvent> events) => TurnResolved?.Invoke(events);
    internal void RaiseEnded() => Ended?.Invoke();
    internal void RaiseState() => StateChanged?.Invoke();
}

/// <summary>
/// The companion mini-game: progress, feeding, practice fights and LAN battles. Battles run in lockstep: both peers pick
/// a move, exchange it with a fingerprint of the state, and resolve the same turn from a shared seed.
/// </summary>
public sealed partial class PetArena : INotifyPropertyChanged
{
    public const int InviteSeconds = 30, TurnSeconds = 30, SilenceSeconds = 75;

    private readonly MainViewModel model;
    private readonly LanService? lan;
    private readonly Random random = new();
    private readonly DispatcherTimer clock = new() { Interval = TimeSpan.FromSeconds(1) };
    private (string PeerId, string BattleId, string Seed, PetGame.FighterCard Card, DateTime ExpiresUtc)? challenge;

    public PetArena(MainViewModel model, LanService? lan)
    {
        this.model = model;
        this.lan = lan;
        PetGame.Normalize(Progress);
        clock.Tick += (_, _) => Tick();
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void Changed([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new(name));

    public PetProgress Progress => model.Settings.PetGame ??= new();
    public ObservableCollection<PetInvite> Invites { get; } = [];
    public PetBattleSession? Current { get; private set; }
    public string? ChallengePeerId => challenge?.PeerId;
    public string Status { get; private set; } = "";

    public event Action<PetInvite>? InviteReceived;
    public event Action<PetBattleSession>? BattleStarted;
    /// <summary>The floating companion should act out something (laugh, cry, eat…), optionally holding a food.</summary>
    public event Action<string, string?>? PetAction;
    public event Action<string>? PetLine;

    private void SetStatus(string text) { Status = text; Changed(nameof(Status)); }

    public void Save()
    {
        PetGame.Normalize(Progress);
        model.SavePetGame();
        Changed(nameof(Progress));
    }

    // ── Care ─────────────────────────────────────────────────────────────

    public int Fullness => PetGame.CurrentFullness(Progress, DateTime.UtcNow);

    public bool ClaimDaily()
    {
        int coins = PetGame.ClaimDaily(Progress, DateTime.Now);
        if (coins == 0) return false;
        Save();
        return true;
    }

    public PetGame.FeedResult Feed(string food)
    {
        var result = PetGame.Feed(Progress, food, DateTime.UtcNow, out int levels);
        if (result == PetGame.FeedResult.Fed)
        {
            Save();
            PetAction?.Invoke("eat", food);
            if (levels > 0) LevelledUp();
        }
        return result;
    }

    public bool Buy(string food)
    {
        if (!PetGame.Buy(Progress, food)) return false;
        Save();
        return true;
    }

    public bool AddPoint(string stat)
    {
        if (PetGame.FreePoints(Progress) <= 0) return false;
        switch (stat)
        {
            case "power": Progress.Power++; break;
            case "guard": Progress.Guard++; break;
            case "speed": Progress.Speed++; break;
            case "vitality": Progress.Vitality++; break;
            default: return false;
        }
        Save();
        return true;
    }

    public void ResetPoints()
    {
        Progress.Power = Progress.Guard = Progress.Speed = Progress.Vitality = 0;
        Save();
    }

    public void SetElement(string element)
    {
        Progress.Element = PetGame.NormalizeElement(element);
        Save();
    }

    public void OnChatSent()
    {
        if (PetGame.AddChatXp(Progress, DateTime.Now) > 0) LevelledUp();
        if (!model.Preview) model.SavePetGame();
    }

    private void LevelledUp()
    {
        PetAction?.Invoke("flip", null);
        PetLine?.Invoke($"Level {Progress.Level}!");
    }

    // ── Friends ──────────────────────────────────────────────────────────

    public IEnumerable<Peer> BattleFriends => model.Peers.Where(p => p.Online && LanService.SupportsPetBattles(p.Version)).OrderBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase);

    private PetGame.FighterCard MyCard() =>
        PetGame.CardFor(Progress, model.Settings.DisplayName, PetAppearance.From(model.Settings).ToWireCode(), DateTime.UtcNow);

    private static string NewId() => Convert.ToHexString(RandomNumberGenerator.GetBytes(8));
    private static string NewSeed() => Convert.ToHexString(RandomNumberGenerator.GetBytes(8));

    private static ulong CombineSeeds(string challengerSeed, string accepterSeed)
    {
        ulong a = ulong.TryParse(challengerSeed, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var x) ? x : 1;
        ulong b = ulong.TryParse(accepterSeed, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var y) ? y : 2;
        return new BattleRng(a ^ ((b << 17) | (b >> 47))).Next();
    }

    /// <summary>Replaces the LAN for in-process tests.</summary>
    internal Func<string, string, string, Dictionary<string, string>?, Task<bool>>? Transport { get; set; }

    private async Task<bool> SendAsync(string peerId, string op, string battleId, Dictionary<string, string>? fields = null)
    {
        if (Transport is { } transport) return await transport(peerId, op, battleId, fields);
        if (lan is null) return false;
        try { await lan.SendGameAsync(peerId, op, battleId, fields); return true; }
        catch (Exception e) when (e is IOException or System.Net.Sockets.SocketException or OperationCanceledException or InvalidOperationException or ObjectDisposedException)
        { return false; }
    }

    public async Task ChallengeAsync(Peer peer)
    {
        if (Current is { Finished: false }) return;
        if (!peer.Online || !LanService.SupportsPetBattles(peer.Version)) { SetStatus("Unavailable"); return; }
        await CancelChallengeAsync();
        var card = MyCard();
        string id = NewId(), seed = NewSeed();
        challenge = (peer.Id, id, seed, card, DateTime.UtcNow.AddSeconds(InviteSeconds));
        Changed(nameof(ChallengePeerId));
        SetStatus($"Waiting for {peer.Name}…");
        clock.Start();
        if (!await SendAsync(peer.Id, "invite", id, new() { ["card"] = PetGame.EncodeCard(card), ["seed"] = seed }))
        {
            challenge = null; Changed(nameof(ChallengePeerId));
            SetStatus("Couldn't reach " + peer.Name);
        }
    }

    public async Task CancelChallengeAsync()
    {
        if (challenge is not { } c) return;
        challenge = null;
        Changed(nameof(ChallengePeerId));
        SetStatus("");
        await SendAsync(c.PeerId, "cancel", c.BattleId);
    }

    public async Task AcceptAsync(PetInvite invite)
    {
        if (invite.IsTrade) { await AcceptTradeAsync(invite); return; }
        if (invite.IsCardDuel) { await AcceptCardDuelAsync(invite); return; }
        Invites.Remove(invite);
        if (Current is { Finished: false } || CurrentCard is { Finished: false } || CurrentTrade is { Finished: false }) { await SendAsync(invite.PeerId, "decline", invite.BattleId, new() { ["reason"] = "busy" }); return; }
        if (DateTime.UtcNow > invite.ExpiresUtc) { SetStatus("Challenge expired"); return; }
        await CancelChallengeAsync();
        await CancelCardChallengeAsync();
        var card = MyCard();
        string seed = NewSeed();
        if (!await SendAsync(invite.PeerId, "accept", invite.BattleId, new() { ["card"] = PetGame.EncodeCard(card), ["seed"] = seed }))
        {
            SetStatus("Couldn't reach " + invite.PeerName);
            return;
        }
        Start(new PetBattleSession
        {
            Id = invite.BattleId, PeerId = invite.PeerId, OpponentName = PeerName(invite.PeerId, invite.Card.Name), MySide = 1,
            Battle = new PetBattle(invite.Card, card, CombineSeeds(invite.Seed, seed))
        });
    }

    public async Task DeclineAsync(PetInvite invite)
    {
        if (invite.IsTrade) { await DeclineTradeAsync(invite); return; }
        if (invite.IsCardDuel) { await DeclineCardDuelAsync(invite); return; }
        Invites.Remove(invite);
        await SendAsync(invite.PeerId, "decline", invite.BattleId, new() { ["reason"] = "no" });
    }

    public void StartPractice()
    {
        if (Current is { Finished: false }) return;
        _ = CancelChallengeAsync();
        var preset = FloatingPetStyle.Presets[random.Next(FloatingPetStyle.Presets.Count)];
        var wild = PetGame.WildCard(Progress.Level, random, preset.Name, preset.Appearance.ToWireCode());
        Start(new PetBattleSession
        {
            Id = NewId(), PeerId = null, OpponentName = "Wild " + preset.Name, MySide = 0,
            Battle = new PetBattle(MyCard(), wild, (ulong)random.NextInt64())
        });
    }

    private string PeerName(string peerId, string fallback) => model.Peers.FirstOrDefault(p => p.Id == peerId)?.Name is { Length: > 0 } n ? n : fallback;

    private void Start(PetBattleSession session)
    {
        // A food boost lasts for one battle.
        if (Progress.Boost.Length > 0) { Progress.Boost = ""; Save(); }
        Current = session;
        session.TurnDeadlineUtc = DateTime.UtcNow.AddSeconds(TurnSeconds + 4);
        SetStatus("");
        Changed(nameof(Current));
        clock.Start();
        BattleStarted?.Invoke(session);
        PetAction?.Invoke("fired", null);
    }

    // ── Turns ────────────────────────────────────────────────────────────

    public async Task SubmitMoveAsync(string move)
    {
        if (Current is not { Finished: false } s || s.MyMove is not null) return;
        string hash = s.Battle.Hash();
        int turn = s.Battle.Turn + 1;
        s.MyMove = s.Battle.Sanitize(s.MySide, move);
        s.WaitingSinceUtc = DateTime.UtcNow;
        if (s.IsPractice) s.TheirMove = s.Battle.ChooseAi(s.TheirSide, random);
        else if (!await SendAsync(s.PeerId!, "move", s.Id, new() { ["turn"] = turn.ToString(CultureInfo.InvariantCulture), ["move"] = s.MyMove, ["hash"] = hash }))
        {
            End(s, "Connection lost", reward: false);
            return;
        }
        s.RaiseState();
        TryResolve(s);
    }

    /// <summary>Called by the battle view once a turn's animation has played, so the move timer starts fresh.</summary>
    public void RestartTurnTimer()
    {
        if (Current is { Finished: false } s) s.TurnDeadlineUtc = DateTime.UtcNow.AddSeconds(TurnSeconds);
    }

    private void TryResolve(PetBattleSession s)
    {
        if (s.Finished || s.MyMove is null || s.TheirMove is null) return;
        string? m0 = s.MySide == 0 ? s.MyMove : s.TheirMove;
        string? m1 = s.MySide == 0 ? s.TheirMove : s.MyMove;
        s.MyMove = null; s.TheirMove = null; s.WaitingSinceUtc = null;
        var events = s.Battle.Resolve(m0, m1);
        s.TurnDeadlineUtc = DateTime.UtcNow.AddSeconds(TurnSeconds + 6);
        s.RaiseTurn(events);
        if (s.Battle.Over)
        {
            int outcome = s.Battle.Winner < 0 ? 0 : s.Battle.Winner == s.MySide ? 1 : -1;
            Finish(s, outcome, "");
        }
        else s.RaiseState();
    }

    public async Task LeaveAsync()
    {
        if (Current is not { Finished: false } s) return;
        if (!s.IsPractice) await SendAsync(s.PeerId!, "leave", s.Id);
        Finish(s, -1, "You left");
    }

    /// <summary>Clears a finished battle so the arena shows the lobby again.</summary>
    public void Dismiss()
    {
        if (Current is { Finished: false }) return;
        Current = null;
        Changed(nameof(Current));
    }

    private void Finish(PetBattleSession s, int outcome, string reason)
    {
        if (s.Finished) return;
        s.Finished = true;
        s.Outcome = outcome;
        s.EndReason = reason;
        s.Reward = PetGame.Reward(Progress, outcome, s.Them.Card.Level, friend: !s.IsPractice, DateTime.UtcNow);
        Save();
        s.RaiseEnded();
        s.RaiseState();
        PetAction?.Invoke(outcome > 0 ? "laugh" : outcome < 0 ? "cry" : "surprise", null);
        if (s.Reward.LevelsGained > 0) PetLine?.Invoke($"Level {Progress.Level}!");
    }

    /// <summary>Ends a battle without a result: nobody wins or loses when the connection drops.</summary>
    private void End(PetBattleSession s, string reason, bool reward)
    {
        if (s.Finished) return;
        if (reward) { Finish(s, 0, reason); return; }
        s.Finished = true;
        s.Outcome = 0;
        s.EndReason = reason;
        s.RaiseEnded();
        s.RaiseState();
        PetAction?.Invoke("nervous", null);
    }

    // ── Network ──────────────────────────────────────────────────────────

    public void OnMessage(string peerId, IReadOnlyDictionary<string, string> f)
    {
        if (HandleTradeMessage(peerId, f)) return;
        if (HandleCardMessage(peerId, f)) return;
        string op = f.GetValueOrDefault("op", ""), bid = f.GetValueOrDefault("bid", "");
        switch (op)
        {
            case "invite":
            {
                if (PetGame.DecodeCard(f.GetValueOrDefault("card")) is not { } card || f.GetValueOrDefault("seed", "") is not { Length: > 0 } seed) return;
                if (Current is { Finished: false } || Invites.Count >= 5)
                {
                    _ = SendAsync(peerId, "decline", bid, new() { ["reason"] = "busy" });
                    return;
                }
                if (Invites.Any(i => i.BattleId == bid)) return;
                foreach (var old in Invites.Where(i => i.PeerId == peerId).ToList()) Invites.Remove(old);
                var invite = new PetInvite(peerId, PeerName(peerId, card.Name), bid, card, seed, DateTime.UtcNow.AddSeconds(InviteSeconds));
                Invites.Add(invite);
                clock.Start();
                InviteReceived?.Invoke(invite);
                break;
            }
            case "cancel":
                foreach (var i in Invites.Where(i => i.BattleId == bid && i.PeerId == peerId).ToList()) Invites.Remove(i);
                break;
            case "decline":
                if (challenge is { } pending && pending.BattleId == bid && pending.PeerId == peerId)
                {
                    challenge = null;
                    Changed(nameof(ChallengePeerId));
                    SetStatus(f.GetValueOrDefault("reason") == "busy" ? PeerName(peerId, "They") + " is busy" : PeerName(peerId, "They") + " declined");
                }
                break;
            case "accept":
            {
                if (challenge is not { } mine || mine.BattleId != bid || mine.PeerId != peerId) return;
                if (PetGame.DecodeCard(f.GetValueOrDefault("card")) is not { } card || f.GetValueOrDefault("seed", "") is not { Length: > 0 } seed) return;
                challenge = null;
                Changed(nameof(ChallengePeerId));
                Start(new PetBattleSession
                {
                    Id = bid, PeerId = peerId, OpponentName = PeerName(peerId, card.Name), MySide = 0,
                    Battle = new PetBattle(mine.Card, card, CombineSeeds(mine.Seed, seed))
                });
                break;
            }
            case "move":
            {
                if (Current is not { Finished: false } s || s.Id != bid || s.PeerId != peerId) return;
                if (!int.TryParse(f.GetValueOrDefault("turn"), NumberStyles.None, CultureInfo.InvariantCulture, out int turn)) return;
                if (turn <= s.Battle.Turn) return; // duplicate of a turn already played
                if (turn != s.Battle.Turn + 1 || f.GetValueOrDefault("hash") != s.Battle.Hash())
                {
                    _ = SendAsync(peerId, "leave", s.Id, new() { ["reason"] = "desync" });
                    End(s, "Out of sync", reward: false);
                    return;
                }
                s.TheirMove = s.Battle.Sanitize(s.TheirSide, f.GetValueOrDefault("move"));
                s.RaiseState();
                TryResolve(s);
                break;
            }
            case "leave":
            {
                if (Current is not { Finished: false } s || s.Id != bid || s.PeerId != peerId) return;
                if (f.GetValueOrDefault("reason") == "desync") End(s, "Out of sync", reward: false);
                else Finish(s, 1, s.OpponentName + " left");
                break;
            }
        }
    }

    public void OnPeerOffline(string peerId)
    {
        foreach (var i in Invites.Where(i => i.PeerId == peerId).ToList()) Invites.Remove(i);
        if (challenge is { } c && c.PeerId == peerId) { challenge = null; Changed(nameof(ChallengePeerId)); SetStatus(""); }
        if (Current is { Finished: false } s && s.PeerId == peerId) End(s, "Connection lost", reward: false);
        OnCardPeerOffline(peerId);
        OnTradePeerOffline(peerId);
    }

    private void Tick()
    {
        var now = DateTime.UtcNow;
        foreach (var i in Invites.Where(i => now > i.ExpiresUtc).ToList()) Invites.Remove(i);
        if (challenge is { } c && now > c.ExpiresUtc)
        {
            challenge = null;
            Changed(nameof(ChallengePeerId));
            SetStatus("No answer");
            _ = SendAsync(c.PeerId, "cancel", c.BattleId);
        }
        if (Current is { Finished: false } s)
        {
            // Out of time: play a basic strike so the battle keeps moving.
            if (s.MyMove is null && now > s.TurnDeadlineUtc) _ = SubmitMoveAsync("strike");
            else if (s.WaitingSinceUtc is { } since && (now - since).TotalSeconds > SilenceSeconds) End(s, "Connection lost", reward: false);
            s.RaiseState();
        }
        TickCardDuel(now);
        TickTrade(now);
        if (Invites.Count == 0 && challenge is null && cardChallenge is null && tradeChallenge is null && Current is not { Finished: false } && CurrentCard is not { Finished: false } && CurrentTrade is not { Finished: false }) clock.Stop();
    }
}
