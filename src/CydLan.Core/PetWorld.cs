namespace CydLan.Core;

/// <summary>
/// The local world for grinding: an endless run of randomly generated stages, each with a biome element and three
/// fights, the last one a boss. Stages come from the install's seed, so a stage is the same every time it is retried.
/// Enemies follow the same rules as players: rolled base stats, at most 15 bonus points, a 20-card deck.
/// </summary>
public static class PetWorld
{
    public const int MaxStage = 999, FightsPerStage = 3;

    public sealed record Enemy(int Index, string Name, string Element, bool Boss, int Power, int Guard, int Speed, int Vitality, IReadOnlyList<string> Deck, int LookSeed);
    public sealed record Stage(int Number, string Name, string Element, IReadOnlyList<Enemy> Fights);

    private static readonly Dictionary<string, string[]> Places = new()
    {
        ["tide"] = ["Coral Shallows", "Misty Lagoon", "Tidepool Cove", "Sunken Pier", "Bubble Reef", "Rainy Delta"],
        ["blaze"] = ["Cinder Flats", "Ember Canyon", "Lava Steps", "Smoky Ridge", "Ash Hollow", "Kiln Peak"],
        ["frost"] = ["Snowdrift Pass", "Icicle Caves", "Frozen Lake", "Glacier Gate", "Hailstone Hill", "Polar Camp"],
        ["leaf"] = ["Mossy Grove", "Bramble Path", "Fern Valley", "Old Oak Wood", "Petal Meadow", "Vine Ruins"],
        ["light"] = ["Sunlit Plaza", "Halo Fields", "Crystal Spire", "Dawn Temple", "Golden Steps", "Lantern Road"],
        ["shadow"] = ["Gloom Marsh", "Night Market", "Raven Crypt", "Moonless Alley", "Hollow Manor", "Void Well"],
        ["volt"] = ["Storm Plateau", "Static Fields", "Thunder Tower", "Neon Yard", "Spark Mine", "Cloud Docks"]
    };

    private static readonly string[] Titles = ["Wild", "Feral", "Sneaky", "Grumpy", "Brave", "Sleepy", "Angry", "Shiny", "Tiny", "Mighty"];
    private static readonly string[] BossTitles = ["Chief", "Warden", "Overlord", "Champion", "Guardian", "King", "Queen"];

    private static ulong Hash(long seed, string text)
    {
        ulong h = 1469598103934665603UL ^ (ulong)seed;
        foreach (char ch in text) { h ^= ch; h *= 1099511628211UL; }
        return new BattleRng(h).Next();
    }

    public static Stage StageOf(long seed, int number)
    {
        number = Math.Clamp(number, 1, MaxStage);
        var rng = new BattleRng(Hash(seed, "stage:" + number));
        string element = PetGame.Wheel[rng.Below(PetGame.Wheel.Count)];
        var places = Places[element];
        string name = places[rng.Below(places.Length)];
        var fights = new List<Enemy>();
        for (int i = 0; i < FightsPerStage; i++)
        {
            bool boss = i == FightsPerStage - 1;
            // Most enemies match the biome; some bring another element so the player has to adapt.
            string e = boss || rng.Below(100) < 65 ? element : PetGame.Wheel[rng.Below(PetGame.Wheel.Count)];
            int bonus = Math.Min(PetGame.MaxBonusPoints, (number - 1) / 3 + i + (boss ? 2 : 0));
            int[] stats = RollStats(rng, bonus);
            string title = boss ? BossTitles[rng.Below(BossTitles.Length)] : Titles[rng.Below(Titles.Length)];
            fights.Add(new Enemy(i, title, e, boss, stats[0], stats[1], stats[2], stats[3], DeckFor(rng, e, number, boss), (int)(rng.Next() & 0x7FFFFFFF)));
        }
        return new Stage(number, name, element, fights);
    }

    /// <summary>Base spread of 20 plus the bonus points, both placed at random.</summary>
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

    /// <summary>Highest card rarity enemies use on a stage: commons first, legendaries only deep in the world.</summary>
    public static int RarityCap(int stage, bool boss) => Math.Min(3, (stage >= 25 ? 3 : stage >= 10 ? 2 : stage >= 3 ? 1 : 0) + (boss ? 1 : 0));

    private static List<string> DeckFor(BattleRng rng, string element, int stage, bool boss)
    {
        int cap = RarityCap(stage, boss);
        var own = PetCards.Cards.Where(c => c.Element == element && PetCards.RarityRank(c.Rarity) <= cap).ToList();
        var other = PetCards.Cards.Where(c => c.Element != element && PetCards.RarityRank(c.Rarity) <= cap).ToList();
        var deck = new List<string> { "strike", "guard" };
        for (int tries = 0; deck.Count < PetCards.DeckSize && tries < 500; tries++)
        {
            var pool = rng.Below(100) < 70 ? own : other;
            var card = pool[rng.Below(pool.Count)];
            if (deck.Count(c => c == card.Id) >= PetCards.CopyLimit(card)) continue;
            deck.Add(card.Id);
        }
        return deck;
    }

    public static PetCards.Loadout LoadoutOf(Stage stage, Enemy enemy, string displayName, string petCode) =>
        new(displayName, petCode, stage.Number, enemy.Element, PetCards.MaxHpFor(enemy.Vitality), enemy.Power, enemy.Guard, enemy.Speed, enemy.Deck, enemy.Vitality);

    /// <summary>Moves the player on after a fight: wins advance to the next fight, beating the boss opens the next stage.</summary>
    public static bool Advance(PetProgress p, bool won)
    {
        if (!won) return false;
        if (p.StageFight < FightsPerStage - 1) { p.StageFight++; return false; }
        p.StageFight = 0;
        if (p.Stage < MaxStage) p.Stage++;
        return true;
    }
}
