using System.Globalization;

namespace CydLan.Core;

/// <summary>
/// Local time-based tournament bracket so players can reliably grind coins, XP, and cards without facing
/// overly scaled world bosses. Each local hour hosts a themed 3-round bracket scaled to the player's own strength.
/// </summary>
public static class PetTournament
{
    public const int RoundsPerTournament = 3;

    public sealed record Contender(int Round, string RoundName, string Name, string Element, int Power, int Guard, int Speed, int Vitality, IReadOnlyList<string> Deck, int LookSeed);
    public sealed record Event(string Slot, string Name, string Element, DateTime EndsLocal, IReadOnlyList<Contender> Bracket);

    private static readonly string[] RoundNames = ["Quarterfinal", "Semifinal", "Final"];
    private static readonly string[] Names = ["Ace", "Rookie", "Scout", "Spark", "Pip", "Clover", "Dash", "Pebble", "Mochi", "Breeze", "Comet", "Echo"];

    public static string SlotFor(DateTime localNow) => localNow.ToString("yyyy-MM-dd-HH", CultureInfo.InvariantCulture);

    public static DateTime SlotEndsLocal(DateTime localNow) => localNow.Date.AddHours(localNow.Hour + 1);

    public static TimeSpan TimeRemaining(DateTime localNow)
    {
        var left = SlotEndsLocal(localNow) - localNow;
        return left > TimeSpan.Zero ? left : TimeSpan.Zero;
    }

    private static ulong Hash(long seed, string text)
    {
        ulong h = 1469598103934665603UL ^ (ulong)seed;
        foreach (char ch in text) { h ^= ch; h *= 1099511628211UL; }
        return new BattleRng(h).Next();
    }

    private static string PeriodName(int hour) => hour switch
    {
        >= 5 and <= 11 => "Morning",
        >= 12 and <= 16 => "Midday",
        >= 17 and <= 20 => "Evening",
        _ => "Night"
    };

    public static Event EventFor(PetProgress p, DateTime localNow)
    {
        string slot = SlotFor(localNow);
        var rng = new BattleRng(Hash(p.Seed, "tourney:" + slot));
        string element = PetGame.Wheel[rng.Below(PetGame.Wheel.Count)];
        string cupName = $"{PeriodName(localNow.Hour)} {PetGame.ElementOf(element).Name} Cup";
        int playerBonus = Math.Clamp(PetGame.SpentPoints(p), 0, PetGame.MaxBonusPoints);

        var bracket = new List<Contender>(RoundsPerTournament);
        for (int r = 0; r < RoundsPerTournament; r++)
        {
            string e = r == RoundsPerTournament - 1 || rng.Below(100) < 60
                ? element
                : PetGame.Wheel[rng.Below(PetGame.Wheel.Count)];
            int bonus = r switch
            {
                0 => Math.Max(0, playerBonus - 2),
                1 => playerBonus,
                _ => Math.Min(PetGame.MaxBonusPoints, playerBonus + 1)
            };
            int cap = r switch
            {
                0 => playerBonus >= 6 ? 1 : 0,
                1 => 1,
                _ => p.Level >= 6 ? 2 : 1
            };
            int[] stats = RollStats(rng, bonus);
            string title = Names[rng.Below(Names.Length)];
            bracket.Add(new Contender(r, RoundNames[r], title, e, stats[0], stats[1], stats[2], stats[3], DeckFor(rng, e, cap), (int)(rng.Next() & 0x7FFFFFFF)));
        }
        return new Event(slot, cupName, element, SlotEndsLocal(localNow), bracket);
    }

    private static int[] RollStats(BattleRng rng, int bonus)
    {
        int[] v = [PetGame.BaseStatMin, PetGame.BaseStatMin, PetGame.BaseStatMin, PetGame.BaseStatMin];
        for (int left = PetGame.BaseTotal - 4 * PetGame.BaseStatMin; left > 0;)
        {
            int i = rng.Below(4);
            if (v[i] >= PetGame.BaseStatMax) continue;
            v[i]++; left--;
        }
        for (int i = 0; i < bonus; i++) v[rng.Below(4)]++;
        return v;
    }

    private static List<string> DeckFor(BattleRng rng, string element, int rarityCap)
    {
        var own = PetCards.Cards.Where(c => c.Element == element && PetCards.RarityRank(c.Rarity) <= rarityCap).ToList();
        var other = PetCards.Cards.Where(c => c.Element != element && PetCards.RarityRank(c.Rarity) <= rarityCap).ToList();
        var deck = new List<string> { "strike", "guard" };
        for (int tries = 0; deck.Count < PetCards.DeckSize && tries < 500; tries++)
        {
            var pool = rng.Below(100) < 65 ? own : other;
            var card = pool[rng.Below(pool.Count)];
            if (deck.Count(c => c == card.Id) >= PetCards.CopyLimit(card)) continue;
            deck.Add(card.Id);
        }
        return deck;
    }

    public static PetCards.Loadout LoadoutOf(PetProgress p, Contender contender, string displayName, string petCode) =>
        new(displayName, petCode, Math.Max(1, p.Level), contender.Element, PetCards.MaxHpFor(contender.Vitality),
            contender.Power, contender.Guard, contender.Speed, contender.Deck, contender.Vitality);

    public static void SyncSlot(PetProgress p, string slot)
    {
        if (p.TournamentSlot == slot) return;
        p.TournamentSlot = slot;
        p.TournamentRound = 0;
    }

    /// <summary>Updates bracket progress after a tournament match. Winning Round 3 crowns a champion and resets for the next run.</summary>
    public static void Advance(PetProgress p, string slot, bool won, out bool champion, out bool firstClear)
    {
        SyncSlot(p, slot);
        champion = false;
        firstClear = false;
        if (!won)
        {
            p.TournamentRound = 0;
            return;
        }
        if (p.TournamentRound < RoundsPerTournament - 1)
        {
            p.TournamentRound++;
            return;
        }
        champion = true;
        firstClear = p.TournamentWonSlot != slot;
        p.TournamentWonSlot = slot;
        p.TournamentRound = 0;
        p.TournamentWins++;
    }

    public static PetGame.BattleReward Reward(PetProgress p, int outcome, int round, bool champion, bool firstClear, DateTime nowUtc)
    {
        int effectiveStage = Math.Clamp(round * 3 + 2, 1, PetWorld.MaxStage);
        var baseReward = PetGame.Reward(p, outcome, effectiveStage, friend: true, nowUtc, boss: round == RoundsPerTournament - 1);
        if (!champion) return baseReward;
        int bonusCoins = firstClear ? 70 : 40;
        int bonusXp = firstClear ? 45 : 25;
        p.Coins += bonusCoins;
        int extraLevels = PetGame.AddXp(p, bonusXp);
        return new PetGame.BattleReward(baseReward.Xp + bonusXp, baseReward.Coins + bonusCoins, baseReward.LevelsGained + extraLevels);
    }

    /// <summary>Guaranteed card drop for winning a local tournament championship.</summary>
    public static PetCard? RollChampionCard(PetProgress p, Random random, bool firstClear)
    {
        var pool = PetCards.Obtainable(p).Where(c => PetCards.Owned(p, c.Id) < PetCards.MaxOwned).ToList();
        if (pool.Count == 0) pool = PetCards.Obtainable(p).ToList();
        int roll = random.Next(100);
        string rarity = firstClear
            ? (roll < 8 ? PetCards.Legendary : roll < 38 ? PetCards.Epic : PetCards.Rare)
            : (roll < 4 ? PetCards.Legendary : roll < 22 ? PetCards.Epic : roll < 65 ? PetCards.Rare : PetCards.Common);
        var tier = pool.Where(c => c.Rarity == rarity).ToList();
        if (tier.Count == 0) tier = pool;
        return tier[random.Next(tier.Count)];
    }
}
