using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CydLan.Core;

/// <summary>Saved progress for the companion mini-game: level, coins, food and battle record.</summary>
public sealed class PetProgress
{
    public int Level { get; set; } = 1;
    public int Xp { get; set; }
    public int Coins { get; set; } = PetGame.StartingCoins;
    public int Fullness { get; set; } = 80;
    public DateTime? FullnessAtUtc { get; set; }
    public string Element { get; set; } = "blaze";
    public int Power { get; set; }
    public int Guard { get; set; }
    public int Speed { get; set; }
    public int Vitality { get; set; }
    public int Wins { get; set; }
    public int Losses { get; set; }
    public int Draws { get; set; }
    public string Boost { get; set; } = "";
    public Dictionary<string, int> Food { get; set; } = new() { ["berry"] = 3, ["fish"] = 1 };
    public string LastDailyDay { get; set; } = "";
    public string ChatXpDay { get; set; } = "";
    public int ChatXpToday { get; set; }
    /// <summary>Gear the pet owns. Each entry has its own id so duplicates can be told apart.</summary>
    public List<OwnedGear> Gear { get; set; } = [];
    /// <summary>Slot → owned gear id.</summary>
    public Dictionary<string, string> Equipped { get; set; } = [];
    public int CardWins { get; set; }
    public int CardLosses { get; set; }
}

public sealed class OwnedGear
{
    public string Uid { get; set; } = "";
    public string Item { get; set; } = "";
}

public sealed record PetFood(string Id, string Name, int Price, int Fullness, int Xp, string Boost);
public sealed record PetElement(string Id, string Name, string Special, string Color);
public sealed record PetMoveInfo(string Id, string Name, int Cooldown, int UnlockLevel);

/// <summary>Rules for feeding, levelling and battling companions. Pure integer logic so every peer computes the same results.</summary>
public static class PetGame
{
    public const int MaxLevel = 50;
    public const int StartingCoins = 120;
    public const int MaxCharge = 100;
    public const int MaxTurns = 40;
    public const int PointsPerLevel = 2;
    public const int BattleHunger = 8;
    public const int HungryBelow = 20;

    public static IReadOnlyList<PetElement> Elements { get; } =
    [
        new("blaze", "Blaze", "Flame Burst", "#F97316"),
        new("tide", "Tide", "Tidal Crash", "#3B82F6"),
        new("leaf", "Leaf", "Thorn Whip", "#22C55E"),
        new("volt", "Volt", "Thunder Jolt", "#FACC15"),
        new("frost", "Frost", "Frost Bite", "#67E8F9"),
        new("shadow", "Shadow", "Shadow Claw", "#8B5CF6"),
        new("light", "Light", "Radiant Beam", "#FDE68A")
    ];

    public static IReadOnlyList<PetFood> Foods { get; } =
    [
        new("berry", "Berry", 10, 15, 0, ""),
        new("fish", "Fish", 25, 30, 5, ""),
        new("cake", "Cake", 40, 20, 25, ""),
        new("pepper", "Fire pepper", 35, 10, 0, "power"),
        new("tea", "Calm tea", 35, 10, 0, "guard"),
        new("soda", "Zap soda", 35, 10, 0, "speed"),
        new("golden", "Golden apple", 150, 40, 120, ""),
        new("candy", "Star candy", 260, 10, -1, "")
    ];

    public static IReadOnlyList<PetMoveInfo> Moves { get; } =
    [
        new("strike", "Strike", 0, 1),
        new("special", "Special", 2, 1),
        new("guard", "Guard", 0, 1),
        new("heal", "Heal", 3, 3),
        new("ultimate", "Ultimate", 0, 6)
    ];

    private static readonly Dictionary<string, string[]> Strong = new()
    {
        ["blaze"] = ["leaf", "frost"],
        ["tide"] = ["blaze"],
        ["leaf"] = ["tide", "volt"],
        ["volt"] = ["tide", "frost"],
        ["frost"] = ["leaf"],
        ["shadow"] = ["light"],
        ["light"] = ["shadow"]
    };

    public static PetElement ElementOf(string? id) => Elements.FirstOrDefault(e => e.Id == id) ?? Elements[0];
    public static string NormalizeElement(string? id) => ElementOf((id ?? "").Trim().ToLowerInvariant()).Id;
    public static PetFood? FoodOf(string? id) => Foods.FirstOrDefault(f => f.Id == id);

    /// <summary>Damage multiplier in percent for an attack of <paramref name="attacker"/> into <paramref name="defender"/>.</summary>
    public static int TypePercent(string attacker, string defender) =>
        Strong.TryGetValue(attacker, out var a) && a.Contains(defender) ? 150
        : Strong.TryGetValue(defender, out var d) && d.Contains(attacker) && !(Strong.TryGetValue(attacker, out var b) && b.Contains(defender)) ? 70
        : 100;

    public static int XpToNext(int level) => 40 + Math.Clamp(level, 1, MaxLevel) * 25;
    public static int PointBudget(int level) => (Math.Clamp(level, 1, MaxLevel) - 1) * PointsPerLevel;
    public static int SpentPoints(PetProgress p) => p.Power + p.Guard + p.Speed + p.Vitality;
    public static int FreePoints(PetProgress p) => Math.Max(0, PointBudget(p.Level) - SpentPoints(p));

    /// <summary>Clamps saved values so hand-edited or old files stay valid.</summary>
    public static void Normalize(PetProgress p)
    {
        p.Level = Math.Clamp(p.Level, 1, MaxLevel);
        p.Xp = Math.Clamp(p.Xp, 0, XpToNext(p.Level));
        p.Coins = Math.Clamp(p.Coins, 0, 9_999_999);
        p.Fullness = Math.Clamp(p.Fullness, 0, 100);
        p.Element = NormalizeElement(p.Element);
        p.Power = Math.Max(0, p.Power); p.Guard = Math.Max(0, p.Guard); p.Speed = Math.Max(0, p.Speed); p.Vitality = Math.Max(0, p.Vitality);
        if (SpentPoints(p) > PointBudget(p.Level)) p.Power = p.Guard = p.Speed = p.Vitality = 0;
        p.Wins = Math.Max(0, p.Wins); p.Losses = Math.Max(0, p.Losses); p.Draws = Math.Max(0, p.Draws);
        if (p.Boost is not ("" or "power" or "guard" or "speed")) p.Boost = "";
        p.Food = (p.Food ?? []).Where(kv => FoodOf(kv.Key) is not null && kv.Value > 0)
            .ToDictionary(kv => kv.Key, kv => Math.Min(kv.Value, 999));
        p.LastDailyDay ??= ""; p.ChatXpDay ??= "";
        PetCards.Normalize(p);
    }

    // ── Hunger ───────────────────────────────────────────────────────────

    private const double MinutesPerFullnessPoint = 12;

    public static int CurrentFullness(PetProgress p, DateTime nowUtc)
    {
        if (p.FullnessAtUtc is not { } at || nowUtc <= at) return Math.Clamp(p.Fullness, 0, 100);
        int lost = (int)((nowUtc - at).TotalMinutes / MinutesPerFullnessPoint);
        return Math.Clamp(p.Fullness - lost, 0, 100);
    }

    private static void SetFullness(PetProgress p, int value, DateTime nowUtc)
    {
        p.Fullness = Math.Clamp(value, 0, 100);
        p.FullnessAtUtc = nowUtc;
    }

    public static bool IsHungry(PetProgress p, DateTime nowUtc) => CurrentFullness(p, nowUtc) < HungryBelow;

    // ── Food, coins, XP ──────────────────────────────────────────────────

    public static int FoodCount(PetProgress p, string id) => p.Food.TryGetValue(id, out int n) ? n : 0;

    public static bool Buy(PetProgress p, string id)
    {
        if (FoodOf(id) is not { } food || p.Coins < food.Price) return false;
        p.Coins -= food.Price;
        p.Food[id] = FoodCount(p, id) + 1;
        return true;
    }

    public enum FeedResult { Fed, NoFood, TooFull }

    public static FeedResult Feed(PetProgress p, string id, DateTime nowUtc, out int levelsGained)
    {
        levelsGained = 0;
        if (FoodOf(id) is not { } food || FoodCount(p, id) <= 0) return FeedResult.NoFood;
        int full = CurrentFullness(p, nowUtc);
        if (full >= 100 && food.Xp == 0 && food.Boost.Length == 0) return FeedResult.TooFull;
        if (FoodCount(p, id) == 1) p.Food.Remove(id); else p.Food[id]--;
        SetFullness(p, full + food.Fullness, nowUtc);
        if (food.Boost.Length > 0) p.Boost = food.Boost;
        int xp = food.Xp < 0 ? XpToNext(p.Level) - p.Xp : food.Xp;
        if (xp > 0) levelsGained = AddXp(p, xp);
        return FeedResult.Fed;
    }

    public static int AddXp(PetProgress p, int amount)
    {
        int gained = 0;
        if (p.Level >= MaxLevel) { p.Xp = Math.Min(p.Xp + Math.Max(0, amount), XpToNext(MaxLevel)); return 0; }
        p.Xp += Math.Max(0, amount);
        while (p.Level < MaxLevel && p.Xp >= XpToNext(p.Level))
        {
            p.Xp -= XpToNext(p.Level);
            p.Level++;
            gained++;
        }
        if (p.Level >= MaxLevel) p.Xp = Math.Min(p.Xp, XpToNext(MaxLevel));
        return gained;
    }

    /// <summary>Gives the once-a-day coins and berry. Returns the coins added, or 0 when already claimed today.</summary>
    public static int ClaimDaily(PetProgress p, DateTime localNow)
    {
        string day = localNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        if (p.LastDailyDay == day) return 0;
        p.LastDailyDay = day;
        p.Coins += 60;
        p.Food["berry"] = FoodCount(p, "berry") + 1;
        return 60;
    }

    public const int ChatXpPerMessage = 2, ChatXpDailyCap = 40;

    /// <summary>Chatting with friends trains the companion a little each day.</summary>
    public static int AddChatXp(PetProgress p, DateTime localNow)
    {
        string day = localNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        if (p.ChatXpDay != day) { p.ChatXpDay = day; p.ChatXpToday = 0; }
        if (p.ChatXpToday >= ChatXpDailyCap) return 0;
        p.ChatXpToday += ChatXpPerMessage;
        return AddXp(p, ChatXpPerMessage);
    }

    public sealed record BattleReward(int Xp, int Coins, int LevelsGained);

    /// <summary>Applies a finished battle to the saved progress. <paramref name="outcome"/> is 1 win, 0 draw, -1 loss.</summary>
    public static BattleReward Reward(PetProgress p, int outcome, int opponentLevel, bool friend, DateTime nowUtc)
    {
        opponentLevel = Math.Clamp(opponentLevel, 1, MaxLevel);
        int xp = outcome > 0 ? 30 + opponentLevel * 6 : outcome == 0 ? 20 + opponentLevel * 3 : 12 + opponentLevel * 2;
        int coins = outcome > 0 ? 25 + opponentLevel * 3 : outcome == 0 ? 14 : 8;
        if (!friend) { xp = xp * 6 / 10; coins = coins * 6 / 10; }
        if (IsHungry(p, nowUtc)) xp /= 2;
        if (outcome > 0) p.Wins++; else if (outcome < 0) p.Losses++; else p.Draws++;
        p.Coins += coins;
        SetFullness(p, CurrentFullness(p, nowUtc) - BattleHunger, nowUtc);
        return new(xp, coins, AddXp(p, xp));
    }

    // ── Fighters ─────────────────────────────────────────────────────────

    /// <summary>What a peer shares to start a battle. Stats are recomputed from it on both sides.</summary>
    public sealed class FighterCard
    {
        [JsonPropertyName("n")] public string Name { get; set; } = "";
        [JsonPropertyName("c")] public string PetCode { get; set; } = "";
        [JsonPropertyName("l")] public int Level { get; set; } = 1;
        [JsonPropertyName("e")] public string Element { get; set; } = "blaze";
        [JsonPropertyName("p")] public int Power { get; set; }
        [JsonPropertyName("g")] public int Guard { get; set; }
        [JsonPropertyName("s")] public int Speed { get; set; }
        [JsonPropertyName("v")] public int Vitality { get; set; }
        [JsonPropertyName("b")] public string Boost { get; set; } = "";
        [JsonPropertyName("h")] public bool Hungry { get; set; }
    }

    public const int MaxCardChars = 1600;
    private static readonly JsonSerializerOptions CardJson = new() { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault };

    public static FighterCard CardFor(PetProgress p, string name, string petCode, DateTime nowUtc) => Sanitize(new FighterCard
    {
        Name = name, PetCode = petCode, Level = p.Level, Element = p.Element,
        Power = p.Power, Guard = p.Guard, Speed = p.Speed, Vitality = p.Vitality,
        Boost = p.Boost, Hungry = IsHungry(p, nowUtc)
    });

    public static string EncodeCard(FighterCard card) => JsonSerializer.Serialize(Sanitize(card), CardJson);

    public static FighterCard? DecodeCard(string? text)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Length > MaxCardChars) return null;
        try { return JsonSerializer.Deserialize<FighterCard>(text, CardJson) is { } c ? Sanitize(c) : null; }
        catch (JsonException) { return null; }
    }

    /// <summary>Clamps a card from the network: level range, point budget and known values only.</summary>
    public static FighterCard Sanitize(FighterCard c)
    {
        static string Clean(string? s, int max)
        {
            var t = new string((s ?? "").Where(ch => !char.IsControl(ch)).ToArray()).Trim();
            return t.Length > max ? t[..max] : t;
        }
        int level = Math.Clamp(c.Level, 1, MaxLevel);
        int p = Math.Max(0, c.Power), g = Math.Max(0, c.Guard), s = Math.Max(0, c.Speed), v = Math.Max(0, c.Vitality);
        if (p + g + s + v > PointBudget(level)) p = g = s = v = 0;
        string code = Clean(c.PetCode, CompanionProfileCodec.MaxLength);
        return new FighterCard
        {
            Name = Clean(c.Name, 40), PetCode = code.StartsWith(CompanionProfileCodec.Prefix, StringComparison.Ordinal) ? code : "",
            Level = level, Element = NormalizeElement(c.Element),
            Power = p, Guard = g, Speed = s, Vitality = v,
            Boost = c.Boost is "power" or "guard" or "speed" ? c.Boost : "", Hungry = c.Hungry
        };
    }

    public readonly record struct FighterStats(int MaxHp, int Attack, int Defense, int Speed);

    public static FighterStats StatsFor(FighterCard c)
    {
        int l = c.Level;
        int hp = 100 + l * 12 + c.Vitality * 8;
        int atk = 14 + l * 3 + c.Power * 3;
        int def = 10 + l * 2 + c.Guard * 3;
        int spd = 10 + l * 2 + c.Speed * 3;
        switch (c.Element)
        {
            case "blaze": atk += 2 + l / 4; break;
            case "tide": hp += 12 + l * 2; break;
            case "leaf": def += 2 + l / 4; break;
            case "volt": spd += 3 + l / 3; break;
            case "frost": def += 1 + l / 6; hp += 6 + l; break;
            case "shadow": atk += 1 + l / 6; spd += 1 + l / 6; break;
            case "light": hp += 6 + l; def += 1 + l / 6; break;
        }
        if (c.Boost == "power") atk += atk / 5;
        else if (c.Boost == "guard") def += def / 5;
        else if (c.Boost == "speed") spd += spd / 5;
        if (c.Hungry) { atk -= atk * 15 / 100; spd -= spd * 15 / 100; }
        return new(hp, atk, def, spd);
    }

    public static string MoveName(string move, string element) => move switch
    {
        "special" => ElementOf(element).Special,
        _ => Moves.FirstOrDefault(m => m.Id == move)?.Name ?? "Strike"
    };

    /// <summary>A practice opponent near the player's level.</summary>
    public static FighterCard WildCard(int playerLevel, Random random, string name, string petCode)
    {
        int level = Math.Clamp(playerLevel + random.Next(-1, 2), 1, MaxLevel);
        int budget = PointBudget(level);
        var card = new FighterCard { Name = name, PetCode = petCode, Level = level, Element = Elements[random.Next(Elements.Count)].Id };
        for (int i = 0; i < budget; i++)
            switch (random.Next(4)) { case 0: card.Power++; break; case 1: card.Guard++; break; case 2: card.Speed++; break; default: card.Vitality++; break; }
        return card;
    }
}

/// <summary>Deterministic SplitMix64 generator so both sides of a LAN battle roll the same numbers.</summary>
public sealed class BattleRng(ulong seed)
{
    public ulong State { get; private set; } = seed;
    public ulong Next()
    {
        ulong z = State += 0x9E3779B97F4A7C15UL;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }
    /// <summary>Uniform integer in [0, max).</summary>
    public int Below(int max) => max <= 1 ? 0 : (int)(Next() % (ulong)max);
}

public sealed record BattleEvent(int Actor, string Kind, string Move, int Amount, bool Crit, int TypePercent, string Status = "");

/// <summary>Turn-based battle between two companions. Both sides pick a move; <see cref="Resolve"/> plays the turn.</summary>
public sealed class PetBattle
{
    public sealed class Fighter
    {
        public required PetGame.FighterCard Card { get; init; }
        public required PetGame.FighterStats Stats { get; init; }
        public int Hp { get; set; }
        public int Charge { get; set; }
        public int SpecialCooldown { get; set; }
        public int HealCooldown { get; set; }
        public int Burn { get; set; }
        public int Chill { get; set; }
        public int Soak { get; set; }
        public bool Guarding { get; set; }
        public int Speed => Chill > 0 ? Stats.Speed * 6 / 10 : Stats.Speed;
        public int Defense => Soak > 0 ? Stats.Defense * 8 / 10 : Stats.Defense;
    }

    private readonly BattleRng rng;
    public Fighter[] Fighters { get; }
    public int Turn { get; private set; }
    public bool Over { get; private set; }
    /// <summary>0 or 1 for the winning side, -1 for a draw. Only meaningful once <see cref="Over"/>.</summary>
    public int Winner { get; private set; } = -1;

    public PetBattle(PetGame.FighterCard first, PetGame.FighterCard second, ulong seed)
    {
        rng = new BattleRng(seed);
        Fighters = [Make(first), Make(second)];
    }

    private static Fighter Make(PetGame.FighterCard card)
    {
        card = PetGame.Sanitize(card);
        var stats = PetGame.StatsFor(card);
        return new Fighter { Card = card, Stats = stats, Hp = stats.MaxHp };
    }

    public bool CanUse(int side, string move)
    {
        var f = Fighters[side];
        return move switch
        {
            "strike" or "guard" => true,
            "special" => f.SpecialCooldown == 0,
            "heal" => f.Card.Level >= 3 && f.HealCooldown == 0,
            "ultimate" => f.Card.Level >= 6 && f.Charge >= PetGame.MaxCharge,
            _ => false
        };
    }

    public string Sanitize(int side, string? move) => move is not null && CanUse(side, move) ? move : "strike";

    /// <summary>Stable fingerprint of the state, compared between peers to catch a desync.</summary>
    public string Hash()
    {
        var sb = new StringBuilder();
        sb.Append(Turn).Append('|').Append(rng.State);
        foreach (var f in Fighters)
            sb.Append('|').Append(f.Hp).Append(',').Append(f.Charge).Append(',').Append(f.SpecialCooldown).Append(',').Append(f.HealCooldown)
              .Append(',').Append(f.Burn).Append(',').Append(f.Chill).Append(',').Append(f.Soak);
        ulong h = 1469598103934665603UL;
        foreach (char ch in sb.ToString()) { h ^= ch; h *= 1099511628211UL; }
        return h.ToString("x16", CultureInfo.InvariantCulture);
    }

    public List<BattleEvent> Resolve(string? move0, string? move1)
    {
        var events = new List<BattleEvent>();
        if (Over) return events;
        string[] moves = [Sanitize(0, move0), Sanitize(1, move1)];
        Turn++;

        foreach (var f in Fighters) f.Guarding = false;
        for (int i = 0; i < 2; i++)
            if (moves[i] == "guard")
            {
                Fighters[i].Guarding = true;
                Fighters[i].Charge = Math.Min(PetGame.MaxCharge, Fighters[i].Charge + 35);
                int heal = Math.Min(Fighters[i].Stats.MaxHp / 20, Fighters[i].Stats.MaxHp - Fighters[i].Hp);
                Fighters[i].Hp += heal;
                events.Add(new(i, "guard", "guard", heal, false, 100));
            }

        int s0 = Fighters[0].Speed, s1 = Fighters[1].Speed;
        int first = s0 > s1 ? 0 : s1 > s0 ? 1 : rng.Below(2);
        foreach (int side in new[] { first, 1 - first })
        {
            if (Over) break;
            if (moves[side] == "guard") continue;
            Act(side, moves[side], events);
            CheckFaint(events);
        }

        // End of turn: burns tick, statuses and cooldowns wind down.
        for (int i = 0; i < 2 && !Over; i++)
        {
            var f = Fighters[i];
            if (f.Burn > 0)
            {
                int dmg = Math.Max(1, f.Stats.MaxHp / 16);
                f.Hp = Math.Max(0, f.Hp - dmg);
                f.Burn--;
                events.Add(new(i, "burn", "", dmg, false, 100));
                CheckFaint(events);
            }
        }
        foreach (var f in Fighters)
        {
            if (f.Chill > 0) f.Chill--;
            if (f.Soak > 0) f.Soak--;
            if (f.SpecialCooldown > 0) f.SpecialCooldown--;
            if (f.HealCooldown > 0) f.HealCooldown--;
        }
        for (int i = 0; i < 2; i++)
        {
            if (moves[i] == "special") Fighters[i].SpecialCooldown = 2;
            if (moves[i] == "heal") Fighters[i].HealCooldown = 3;
        }

        if (!Over && Turn >= PetGame.MaxTurns)
        {
            Over = true;
            long a = (long)Fighters[0].Hp * Fighters[1].Stats.MaxHp, b = (long)Fighters[1].Hp * Fighters[0].Stats.MaxHp;
            Winner = a > b ? 0 : b > a ? 1 : -1;
            events.Add(new(Winner, "timeout", "", 0, false, 100));
        }
        return events;
    }

    private void Act(int side, string move, List<BattleEvent> events)
    {
        var me = Fighters[side];
        var foe = Fighters[1 - side];
        if (move == "heal")
        {
            int heal = Math.Min(me.Stats.MaxHp * 28 / 100, me.Stats.MaxHp - me.Hp);
            me.Hp += heal;
            me.Burn = 0;
            events.Add(new(side, "heal", move, heal, false, 100));
            return;
        }

        int power = move switch { "special" => 160, "ultimate" => 220, _ => 100 };
        if (move == "ultimate") me.Charge = 0;
        bool typed = move is "special" or "ultimate";
        int typePct = typed ? PetGame.TypePercent(me.Card.Element, foe.Card.Element) : 100;

        int critChance = 8 + (me.Card.Element is "shadow" or "volt" ? 6 : 0) + Math.Clamp((me.Speed - foe.Speed) / 6, 0, 8);
        bool crit = rng.Below(100) < critChance;
        int variance = 90 + rng.Below(21);

        long dmg = (long)me.Stats.Attack * power / 100;
        dmg = dmg * 50 / (50 + foe.Defense);
        dmg = dmg * variance / 100 * typePct / 100;
        if (crit) dmg = dmg * 3 / 2;
        if (foe.Guarding) dmg = dmg * (move == "ultimate" ? 70 : 40) / 100;
        // No one-shots: a single hit takes at most a bit over half of the target's max HP.
        int damage = (int)Math.Max(1, Math.Min(dmg, foe.Stats.MaxHp * 55L / 100));
        foe.Hp = Math.Max(0, foe.Hp - damage);
        me.Charge = Math.Min(PetGame.MaxCharge, me.Charge + 20);
        foe.Charge = Math.Min(PetGame.MaxCharge, foe.Charge + 15);

        string status = "";
        if (move == "special")
        {
            switch (me.Card.Element)
            {
                case "blaze" when rng.Below(100) < 35: foe.Burn = 2; status = "burn"; break;
                case "frost" when rng.Below(100) < 45: foe.Chill = 2; status = "chill"; break;
                case "tide" when rng.Below(100) < 45: foe.Soak = 2; status = "soak"; break;
                case "leaf": { int drain = Math.Min(damage * 30 / 100, me.Stats.MaxHp - me.Hp); me.Hp += drain; if (drain > 0) status = "drain"; break; }
                case "light": { int glow = Math.Min(me.Stats.MaxHp / 10, me.Stats.MaxHp - me.Hp); me.Hp += glow; if (glow > 0) status = "glow"; break; }
            }
        }
        events.Add(new(side, "hit", move, damage, crit, typePct, status));
    }

    private void CheckFaint(List<BattleEvent> events)
    {
        if (Over) return;
        bool a = Fighters[0].Hp <= 0, b = Fighters[1].Hp <= 0;
        if (!a && !b) return;
        Over = true;
        Winner = a && b ? -1 : a ? 1 : 0;
        if (a) events.Add(new(0, "faint", "", 0, false, 100));
        if (b) events.Add(new(1, "faint", "", 0, false, 100));
    }

    /// <summary>Simple practice-opponent brain. Uses its own randomness, never the shared battle generator.</summary>
    public string ChooseAi(int side, Random random)
    {
        var me = Fighters[side];
        var foe = Fighters[1 - side];
        if (CanUse(side, "ultimate")) return "ultimate";
        if (me.Hp * 100 / me.Stats.MaxHp < 35 && CanUse(side, "heal") && random.Next(100) < 70) return "heal";
        if (CanUse(side, "special") && (PetGame.TypePercent(me.Card.Element, foe.Card.Element) >= 100 || random.Next(100) < 40)) return "special";
        if (random.Next(100) < 18) return "guard";
        return "strike";
    }
}
