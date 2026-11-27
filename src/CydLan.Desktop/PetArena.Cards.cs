using CydLan.Core;

namespace CydLan.Desktop;

/// <summary>A card battle against a practice opponent. The player is always side 0.</summary>
public sealed class CardBattleSession
{
    public required CardBattle Battle { get; init; }
    public required string OpponentName { get; init; }
    public required PetAppearance MyLook { get; init; }
    public required PetAppearance TheirLook { get; init; }
    public CardBattle.Side Me => Battle.Sides[0];
    public CardBattle.Side Them => Battle.Sides[1];
    public bool Finished { get; internal set; }
    /// <summary>1 win, 0 draw, -1 loss.</summary>
    public int Outcome { get; internal set; }
    public string EndReason { get; internal set; } = "";
    public PetGame.BattleReward? Reward { get; internal set; }
    public PetGearItem? Drop { get; internal set; }
    public IReadOnlyList<string> TheirLastCards { get; internal set; } = [];

    public event Action<IReadOnlyList<CardEvent>>? RoundPlayed;
    public event Action? Ended;
    internal void RaiseRound(IReadOnlyList<CardEvent> events) => RoundPlayed?.Invoke(events);
    internal void RaiseEnded() => Ended?.Invoke();
}

public sealed partial class PetArena
{
    // ── Gear ─────────────────────────────────────────────────────────────

    /// <summary>Dresses a look in worn gear: head → hat, armor → outfit, back, weapon → prop, charm → aura.</summary>
    public static PetAppearance Dress(PetAppearance look, IReadOnlyDictionary<string, string> worn)
    {
        foreach (var (slot, part) in worn)
            look = slot switch
            {
                PetCards.Head => look with { Hat = part },
                PetCards.Armor => look with { Outfit = part },
                PetCards.Back => look with { Back = part },
                PetCards.Weapon => look with { Prop = part },
                PetCards.Charm => look with { Aura = part },
                _ => look
            };
        return look;
    }

    public PetCards.Loadout MyLoadout => PetCards.LoadoutFor(Progress, model.Settings.DisplayName, PetAppearance.From(model.Settings).ToWireCode());

    /// <summary>The companion wearing its gear, as shown in the arena and in card battles.</summary>
    public PetAppearance BattleLook => Dress(model.PetAppearance, MyLoadout.Looks);

    public bool Equip(string uid)
    {
        if (CurrentCard is { Finished: false } || !PetCards.Equip(Progress, uid)) return false;
        Save();
        PetAction?.Invoke("spin", null);
        return true;
    }

    public bool Unequip(string slot)
    {
        if (CurrentCard is { Finished: false } || !PetCards.Unequip(Progress, slot)) return false;
        Save();
        return true;
    }

    public int Sell(string uid)
    {
        int coins = PetCards.Sell(Progress, uid);
        if (coins > 0) Save();
        return coins;
    }

    public PetGearItem? OpenGearPack()
    {
        if (PetCards.OpenPack(Progress, random) is not { } owned) return null;
        Save();
        PetAction?.Invoke("surprise", null);
        return PetCards.GearOf(owned.Item);
    }

    /// <summary>New pets get a starter set so the deck has gear cards from the first battle.</summary>
    public void EnsureStarterGear()
    {
        if (Progress.Gear.Count > 0) return;
        foreach (var id in new[] { "cap", "hoodie", "wand" })
            if (PetCards.Give(Progress, id, random) is { } g) PetCards.Equip(Progress, g.Uid);
        Save();
    }

    // ── Card battles ─────────────────────────────────────────────────────

    public CardBattleSession? CurrentCard { get; private set; }
    public event Action<CardBattleSession>? CardBattleStarted;

    public void StartCardBattle()
    {
        if (CurrentCard is { Finished: false } || Current is { Finished: false }) return;
        EnsureStarterGear();
        var preset = FloatingPetStyle.Presets[random.Next(FloatingPetStyle.Presets.Count)];
        var wild = PetCards.Wild(Progress.Level, random, "Wild " + preset.Name, preset.Appearance.ToWireCode());
        // Hunger dulls a pet: a hungry one starts the fight with less HP.
        var mine = MyLoadout;
        if (PetGame.IsHungry(Progress, DateTime.UtcNow)) mine = mine with { MaxHp = mine.MaxHp * 85 / 100 };
        CurrentCard = new CardBattleSession
        {
            Battle = new CardBattle(mine, wild, (ulong)random.NextInt64()),
            OpponentName = wild.Name,
            MyLook = Dress(model.PetAppearance, mine.Looks),
            TheirLook = Dress(preset.Appearance, wild.Looks)
        };
        Changed(nameof(CurrentCard));
        CardBattleStarted?.Invoke(CurrentCard);
        PetAction?.Invoke("fired", null);
    }

    /// <summary>Plays the chosen hand cards (indexes into the hand) against the opponent's picks.</summary>
    public IReadOnlyList<CardEvent> PlayCards(IEnumerable<int> handIndexes)
    {
        if (CurrentCard is not { Finished: false } s) return [];
        var mine = s.Battle.Validate(0, handIndexes);
        var theirs = s.Battle.ChooseAi(1, random);
        s.TheirLastCards = theirs.Select(i => s.Them.Hand[i]).ToList();
        var events = s.Battle.PlayRound(mine, theirs);
        s.RaiseRound(events);
        if (s.Battle.Over) FinishCard(s, s.Battle.Winner < 0 ? 0 : s.Battle.Winner == 0 ? 1 : -1, "");
        return events;
    }

    public void LeaveCardBattle()
    {
        if (CurrentCard is { Finished: false } s) FinishCard(s, -1, "You left");
    }

    public void DismissCardBattle()
    {
        if (CurrentCard is { Finished: false }) return;
        CurrentCard = null;
        Changed(nameof(CurrentCard));
    }

    private void FinishCard(CardBattleSession s, int outcome, string reason)
    {
        if (s.Finished) return;
        s.Finished = true;
        s.Outcome = outcome;
        s.EndReason = reason;
        s.Reward = PetGame.Reward(Progress, outcome, s.Them.Loadout.Level, friend: false, DateTime.UtcNow);
        if (outcome > 0) Progress.CardWins++; else if (outcome < 0) Progress.CardLosses++;
        if (reason.Length == 0 && PetCards.RollDrop(random, s.Them.Loadout.Level, outcome > 0) is { } drop && PetCards.Give(Progress, drop.Id, random) is not null)
            s.Drop = drop;
        Save();
        s.RaiseEnded();
        PetAction?.Invoke(outcome > 0 ? "laugh" : outcome < 0 ? "cry" : "surprise", null);
        if (s.Reward.LevelsGained > 0) PetLine?.Invoke($"Level {Progress.Level}!");
    }
}
