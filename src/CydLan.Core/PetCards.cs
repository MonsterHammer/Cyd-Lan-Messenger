using System.Text;

namespace CydLan.Core;

/// <summary>A battle card. Every number is a base value; the pet's stats scale damage, block and healing.</summary>
public sealed record PetCard(string Id, string Name, int Cost, string Kind, string Element = "",
    int Damage = 0, int Hits = 1, int Block = 0, int Heal = 0, int Thorns = 0, int Burn = 0, int Weak = 0,
    int Draw = 0, int Energy = 0, bool Pierce = false, int Lifesteal = 0, bool Cleanse = false)
{
    /// <summary>Short rules text built from the card's numbers.</summary>
    public string Text
    {
        get
        {
            var parts = new List<string>();
            if (Damage > 0) parts.Add(Hits > 1 ? $"Deal {Damage} ×{Hits}" : $"Deal {Damage}");
            if (Pierce) parts.Add("ignores block");
            if (Lifesteal > 0) parts.Add(Lifesteal >= 100 ? "heal that much" : $"heal {Lifesteal}% of it");
            if (Block > 0) parts.Add($"Block {Block}");
            if (Thorns > 0) parts.Add($"Thorns {Thorns}");
            if (Heal > 0) parts.Add($"Heal {Heal}");
            if (Burn > 0) parts.Add($"Burn {Burn}");
            if (Weak > 0) parts.Add(Weak > 1 ? $"Weaken {Weak} rounds" : "Weaken");
            if (Cleanse) parts.Add("Cleanse");
            if (Draw > 0) parts.Add(Draw > 1 ? $"Draw {Draw}" : "Draw 1");
            if (Energy > 0) parts.Add($"+{Energy} energy");
            return string.Join(" · ", parts);
        }
    }
}

/// <summary>A piece of gear: worn in one slot, adds stats, shows on the pet, and puts its card in the deck.</summary>
public sealed record PetGearItem(string Id, string Name, string Slot, string Rarity, string Card, string Look,
    int Hp = 0, int Power = 0, int Guard = 0, int Speed = 0)
{
    public string StatText
    {
        get
        {
            var parts = new List<string>();
            if (Hp > 0) parts.Add($"+{Hp} HP");
            if (Power > 0) parts.Add($"+{Power} Power");
            if (Guard > 0) parts.Add($"+{Guard} Guard");
            if (Speed > 0) parts.Add($"+{Speed} Speed");
            return string.Join(" · ", parts);
        }
    }
}

/// <summary>Gear, cards, decks and loot for the companion card battles.</summary>
public static class PetCards
{
    public const string Head = "head", Armor = "armor", Back = "back", Weapon = "weapon", Charm = "charm";
    public static IReadOnlyList<(string Id, string Name)> Slots { get; } =
        [(Head, "Head"), (Armor, "Armor"), (Back, "Back"), (Weapon, "Weapon"), (Charm, "Charm")];
    public static IReadOnlyList<string> Rarities { get; } = ["common", "rare", "epic"];

    public const int StartEnergy = 3, EnergyPerRound = 3, MaxEnergy = 8, HandSize = 5, MaxHand = 8, MaxRounds = 30;
    public const int GearPackPrice = 120, BagLimit = 60;

    public static IReadOnlyList<PetCard> Cards { get; } =
    [
        // Basics every pet has.
        new("strike", "Strike", 1, "attack", Damage: 6),
        new("guard", "Guard", 1, "defend", Block: 6),
        // Element moves, one per element.
        new("blaze", "Flame Burst", 2, "attack", "blaze", Damage: 8, Burn: 3),
        new("tide", "Tidal Crash", 2, "attack", "tide", Damage: 10, Weak: 1),
        new("leaf", "Thorn Whip", 2, "attack", "leaf", Damage: 7, Thorns: 3),
        new("volt", "Thunder Jolt", 2, "attack", "volt", Damage: 5, Hits: 2, Draw: 1),
        new("frost", "Frost Bite", 2, "attack", "frost", Damage: 7, Block: 5),
        new("shadow", "Shadow Claw", 2, "attack", "shadow", Damage: 9, Pierce: true),
        new("light", "Radiant Beam", 2, "attack", "light", Damage: 7, Heal: 5),
        // Gear cards.
        new("headbutt", "Headbutt", 1, "attack", Damage: 5, Block: 2),
        new("focus", "Focus", 0, "skill", Energy: 1, Draw: 1),
        new("warmup", "Warm Up", 1, "heal", Heal: 5, Block: 3),
        new("horncharge", "Horn Charge", 2, "attack", Damage: 12),
        new("plunder", "Plunder", 1, "attack", Damage: 5, Lifesteal: 100),
        new("hex", "Hex", 1, "skill", "shadow", Burn: 3, Weak: 2),
        new("decree", "Decree", 1, "skill", Draw: 2, Energy: 1),
        new("blessing", "Blessing", 2, "heal", "light", Heal: 14, Cleanse: true),
        new("bundleup", "Bundle Up", 1, "defend", Block: 8),
        new("hoodup", "Hood Up", 1, "defend", Block: 5, Thorns: 2),
        new("ironwall", "Iron Wall", 2, "defend", Block: 16),
        new("snackbreak", "Snack Break", 1, "heal", Heal: 9),
        new("rally", "Rally", 1, "defend", Block: 4, Draw: 1),
        new("overcharge", "Overcharge", 2, "attack", "volt", Damage: 14, Pierce: true),
        new("swoop", "Swoop", 1, "attack", Damage: 5, Draw: 1),
        new("tailwhip", "Tail Whip", 1, "attack", Damage: 3, Hits: 2),
        new("spikeshell", "Spike Shell", 1, "defend", Block: 6, Thorns: 4),
        new("feathermend", "Feather Mend", 1, "heal", "light", Heal: 6, Block: 4),
        new("nightbite", "Night Bite", 1, "attack", "shadow", Damage: 6, Lifesteal: 50),
        new("winggust", "Wing Gust", 2, "attack", Damage: 5, Hits: 3),
        new("boost", "Boost", 0, "skill", Energy: 2),
        new("sparkleshot", "Sparkle Shot", 1, "attack", "light", Damage: 7),
        new("slash", "Slash", 1, "attack", Damage: 9),
        new("bash", "Shield Bash", 1, "defend", Damage: 4, Block: 6),
        new("fireball", "Fireball", 2, "attack", "blaze", Damage: 8, Burn: 4),
        new("soulflame", "Soul Flame", 2, "skill", "shadow", Burn: 8, Weak: 1),
        new("combo", "Combo", 1, "attack", Damage: 2, Hits: 4),
        new("secondwind", "Second Wind", 1, "heal", Heal: 8),
        new("petalstorm", "Petal Storm", 2, "attack", "leaf", Damage: 3, Hits: 4, Heal: 4),
        new("glitch", "Glitch", 1, "attack", "volt", Damage: 5, Pierce: true, Weak: 1),
        new("wish", "Wish", 1, "skill", "light", Draw: 2, Heal: 4),
        new("bubbleshield", "Bubble Shield", 1, "defend", "tide", Block: 8, Cleanse: true),
        new("icewall", "Ice Wall", 2, "defend", "frost", Block: 12, Thorns: 3),
        new("thunderclap", "Thunderclap", 3, "attack", "volt", Damage: 20, Weak: 1),
        new("phoenix", "Phoenix Rise", 3, "heal", "blaze", Heal: 20, Burn: 5)
    ];

    public static IReadOnlyList<PetGearItem> Gear { get; } =
    [
        new("cap", "Training Cap", Head, "common", "headbutt", "cap", Guard: 1, Hp: 4),
        new("headband", "Fighter Band", Head, "common", "focus", "headband", Power: 2),
        new("beanie", "Cozy Beanie", Head, "common", "warmup", "beanie", Hp: 10),
        new("viking", "Viking Helm", Head, "rare", "horncharge", "viking", Guard: 3, Hp: 8),
        new("pirate", "Pirate Hat", Head, "rare", "plunder", "pirate", Power: 3),
        new("witch", "Witch Hat", Head, "rare", "hex", "witch", Power: 2, Speed: 1),
        new("crown", "Royal Crown", Head, "epic", "decree", "crown", Hp: 14, Power: 2, Guard: 2),
        new("halo", "Halo", Head, "epic", "blessing", "halo", Hp: 12, Guard: 3),

        new("scarf", "Wool Scarf", Armor, "common", "bundleup", "scarf", Hp: 8),
        new("hoodie", "Hoodie", Armor, "common", "hoodup", "hoodie", Guard: 2),
        new("knight", "Knight Armor", Armor, "rare", "ironwall", "armor", Guard: 5),
        new("apron", "Chef Apron", Armor, "rare", "snackbreak", "bib", Hp: 12),
        new("badge", "Hero Badge", Armor, "rare", "rally", "star", Power: 2, Speed: 2),
        new("arccore", "Arc Core", Armor, "epic", "overcharge", "arc", Power: 4, Guard: 2),

        new("cape", "Hero Cape", Back, "common", "swoop", "cape", Speed: 2),
        new("tail", "Fluffy Tail", Back, "common", "tailwhip", "tail", Speed: 1, Hp: 5),
        new("shell", "Spike Shell", Back, "rare", "spikeshell", "shell", Guard: 4),
        new("angel", "Angel Wings", Back, "rare", "feathermend", "angel", Hp: 8, Speed: 2),
        new("bat", "Bat Wings", Back, "rare", "nightbite", "bat", Power: 2, Speed: 3),
        new("dragon", "Dragon Wings", Back, "epic", "winggust", "dragon", Power: 4, Speed: 2),
        new("jetpack", "Jetpack", Back, "epic", "boost", "jetpack", Speed: 5),

        new("wand", "Star Wand", Weapon, "common", "sparkleshot", "wand", Power: 2),
        new("pad", "Lucky Pad", Weapon, "common", "combo", "gamepad", Speed: 2),
        new("sword", "Hero Sword", Weapon, "rare", "slash", "sword", Power: 4),
        new("shield", "Kite Shield", Weapon, "rare", "bash", "shield", Guard: 4),
        new("book", "Spell Book", Weapon, "rare", "fireball", "book", Power: 3),
        new("lantern", "Spirit Lantern", Weapon, "epic", "soulflame", "lantern", Power: 3, Hp: 8),
        new("storm", "Storm Rod", Weapon, "epic", "thunderclap", "wand", Power: 5),

        new("ring", "Halo Ring", Charm, "common", "secondwind", "ring", Hp: 10),
        new("petal", "Petal Charm", Charm, "rare", "petalstorm", "sakura", Hp: 6, Speed: 2),
        new("neon", "Neon Charm", Charm, "rare", "glitch", "neon", Power: 3),
        new("bubble", "Bubble Charm", Charm, "rare", "bubbleshield", "bubbles", Guard: 3),
        new("frostgem", "Frost Gem", Charm, "rare", "icewall", "stars", Guard: 2, Hp: 6),
        new("starcharm", "Star Charm", Charm, "epic", "wish", "stars", Hp: 4, Power: 2, Guard: 2, Speed: 2),
        new("ember", "Phoenix Ember", Charm, "epic", "phoenix", "sunburst", Hp: 10, Power: 2)
    ];

    private static readonly Dictionary<string, PetCard> CardById = Cards.ToDictionary(c => c.Id);
    private static readonly Dictionary<string, PetGearItem> GearById = Gear.ToDictionary(g => g.Id);

    public static PetCard CardOf(string id) => CardById.TryGetValue(id, out var c) ? c : CardById["strike"];
    public static PetGearItem? GearOf(string? id) => id is not null && GearById.TryGetValue(id, out var g) ? g : null;

    // ── Owning and wearing gear ──────────────────────────────────────────

    public static void Normalize(PetProgress p)
    {
        p.Gear = (p.Gear ?? []).Where(g => g is not null && GearOf(g.Item) is not null && g.Uid is { Length: > 0 and <= 40 })
            .GroupBy(g => g.Uid).Select(g => g.First()).Take(BagLimit).ToList();
        var owned = p.Gear.ToDictionary(g => g.Uid);
        p.Equipped = (p.Equipped ?? []).Where(kv => owned.TryGetValue(kv.Value, out var g) && GearOf(g.Item)!.Slot == kv.Key)
            .ToDictionary(kv => kv.Key, kv => kv.Value);
        p.CardWins = Math.Max(0, p.CardWins); p.CardLosses = Math.Max(0, p.CardLosses);
    }

    public static OwnedGear? Give(PetProgress p, string itemId, Random random)
    {
        if (GearOf(itemId) is null || p.Gear.Count >= BagLimit) return null;
        var owned = new OwnedGear { Uid = random.Next().ToString("x8") + random.Next().ToString("x8"), Item = itemId };
        p.Gear.Add(owned);
        return owned;
    }

    public static PetGearItem? Worn(PetProgress p, string slot) =>
        p.Equipped.TryGetValue(slot, out var uid) && p.Gear.FirstOrDefault(g => g.Uid == uid) is { } owned ? GearOf(owned.Item) : null;

    public static bool IsEquipped(PetProgress p, string uid) => p.Equipped.ContainsValue(uid);

    public static bool Equip(PetProgress p, string uid)
    {
        if (p.Gear.FirstOrDefault(g => g.Uid == uid) is not { } owned || GearOf(owned.Item) is not { } item) return false;
        p.Equipped[item.Slot] = uid;
        return true;
    }

    public static bool Unequip(PetProgress p, string slot) => p.Equipped.Remove(slot);

    /// <summary>Sells unequipped gear for coins.</summary>
    public static int Sell(PetProgress p, string uid)
    {
        if (IsEquipped(p, uid) || p.Gear.FirstOrDefault(g => g.Uid == uid) is not { } owned) return 0;
        p.Gear.Remove(owned);
        int coins = SellPrice(GearOf(owned.Item)!);
        p.Coins += coins;
        return coins;
    }

    public static int SellPrice(PetGearItem item) => item.Rarity switch { "epic" => 120, "rare" => 45, _ => 15 };

    /// <summary>The gear pack in the shop: one random piece, a little luckier than battle drops.</summary>
    public static OwnedGear? OpenPack(PetProgress p, Random random)
    {
        if (p.Coins < GearPackPrice || p.Gear.Count >= BagLimit) return null;
        p.Coins -= GearPackPrice;
        return Give(p, RollGear(random, p.Level + 5).Id, random);
    }

    public static PetGearItem RollGear(Random random, int luck)
    {
        int roll = random.Next(100);
        int epicChance = Math.Clamp(5 + luck / 2, 5, 25), rareChance = Math.Clamp(28 + luck, 28, 50);
        string rarity = roll < epicChance ? "epic" : roll < epicChance + rareChance ? "rare" : "common";
        var pool = Gear.Where(g => g.Rarity == rarity).ToList();
        return pool[random.Next(pool.Count)];
    }

    /// <summary>Loot after a win: usually something, better against tougher opponents.</summary>
    public static PetGearItem? RollDrop(Random random, int opponentLevel, bool won) =>
        random.Next(100) < (won ? 55 : 15) ? RollGear(random, opponentLevel) : null;

    // ── Decks and stats ──────────────────────────────────────────────────

    public sealed record Loadout(string Name, string PetCode, int Level, string Element, int MaxHp, int Power, int Guard, int Speed, IReadOnlyList<string> Deck,
        IReadOnlyDictionary<string, string> Looks);

    public static Loadout LoadoutFor(PetProgress p, string name, string petCode)
    {
        var worn = Slots.Select(s => Worn(p, s.Id)).Where(g => g is not null).Cast<PetGearItem>().ToList();
        return Build(name, petCode, p.Level, p.Element, p.Power, p.Guard, p.Speed, p.Vitality, worn);
    }

    public static Loadout Build(string name, string petCode, int level, string element, int power, int guard, int speed, int vitality, IReadOnlyList<PetGearItem> worn)
    {
        level = Math.Clamp(level, 1, PetGame.MaxLevel);
        element = PetGame.NormalizeElement(element);
        var deck = new List<string> { "strike", "strike", "strike", "guard", "guard", element, element };
        foreach (var g in worn) { deck.Add(g.Card); deck.Add(g.Card); }
        return new Loadout(name, petCode, level, element,
            MaxHp: 44 + level * 4 + vitality * 4 + worn.Sum(g => g.Hp),
            Power: level + power * 2 + worn.Sum(g => g.Power),
            Guard: level / 2 + guard * 2 + worn.Sum(g => g.Guard),
            Speed: level + speed * 2 + worn.Sum(g => g.Speed),
            deck,
            worn.ToDictionary(g => g.Slot, g => g.Look));
    }

    /// <summary>A practice opponent: random element, points and gear around the player's level.</summary>
    public static Loadout Wild(int playerLevel, Random random, string name, string petCode)
    {
        int level = Math.Clamp(playerLevel + random.Next(-1, 2), 1, PetGame.MaxLevel);
        int budget = PetGame.PointBudget(level), p = 0, g = 0, s = 0, v = 0;
        for (int i = 0; i < budget; i++) switch (random.Next(4)) { case 0: p++; break; case 1: g++; break; case 2: s++; break; default: v++; break; }
        int pieces = Math.Clamp(1 + level / 4 + random.Next(2), 1, 5);
        var worn = Slots.OrderBy(_ => random.Next()).Take(pieces)
            .Select(slot => { var pool = Gear.Where(x => x.Slot == slot.Id && (level >= 8 || x.Rarity != "epic")).ToList(); return pool[random.Next(pool.Count)]; })
            .ToList();
        return Build(name, petCode, level, PetGame.Elements[random.Next(PetGame.Elements.Count)].Id, p, g, s, v, worn);
    }

    public static int Scale(int value, int stat) => value + value * Math.Max(0, stat) / 20;
}

public sealed record CardEvent(int Actor, string Kind, string Card, int Amount = 0, int TypePercent = 100);

/// <summary>
/// A one-on-one card battle. Each round both sides get energy and a hand, secretly pick cards, then the round plays out:
/// defence, healing and skills first, then attacks in speed order. Deterministic for a given seed and choices.
/// </summary>
public sealed class CardBattle
{
    public sealed class Side
    {
        public required PetCards.Loadout Loadout { get; init; }
        public int Hp { get; set; }
        public int Block { get; set; }
        public int Thorns { get; set; }
        public int Burn { get; set; }
        public int Weak { get; set; }
        public int Energy { get; set; }
        public List<string> DrawPile { get; } = [];
        public List<string> Hand { get; } = [];
        public List<string> Discard { get; } = [];
    }

    private readonly BattleRng rng;
    public Side[] Sides { get; }
    public int Round { get; private set; }
    public bool Over { get; private set; }
    public int Winner { get; private set; } = -1;

    public CardBattle(PetCards.Loadout first, PetCards.Loadout second, ulong seed)
    {
        rng = new BattleRng(seed);
        Sides = [Make(first), Make(second)];
        foreach (var s in Sides) { Shuffle(s.DrawPile); s.Energy = PetCards.StartEnergy; Draw(s, PetCards.HandSize); }
    }

    private static Side Make(PetCards.Loadout l)
    {
        var s = new Side { Loadout = l, Hp = l.MaxHp };
        s.DrawPile.AddRange(l.Deck);
        return s;
    }

    private void Shuffle(List<string> cards)
    {
        for (int i = cards.Count - 1; i > 0; i--) { int j = rng.Below(i + 1); (cards[i], cards[j]) = (cards[j], cards[i]); }
    }

    private void Draw(Side s, int count)
    {
        for (int i = 0; i < count && s.Hand.Count < PetCards.MaxHand; i++)
        {
            if (s.DrawPile.Count == 0)
            {
                if (s.Discard.Count == 0) return;
                s.DrawPile.AddRange(s.Discard); s.Discard.Clear(); Shuffle(s.DrawPile);
            }
            s.Hand.Add(s.DrawPile[^1]);
            s.DrawPile.RemoveAt(s.DrawPile.Count - 1);
        }
    }

    /// <summary>Keeps only cards that are in hand and affordable, in the given order.</summary>
    public List<int> Validate(int side, IEnumerable<int> handIndexes)
    {
        var s = Sides[side];
        var picked = new List<int>();
        int energy = s.Energy;
        foreach (int i in handIndexes.Distinct())
        {
            if (i < 0 || i >= s.Hand.Count) continue;
            int cost = PetCards.CardOf(s.Hand[i]).Cost;
            if (cost > energy) continue;
            energy -= cost;
            picked.Add(i);
        }
        return picked;
    }

    public List<CardEvent> PlayRound(IEnumerable<int> picks0, IEnumerable<int> picks1)
    {
        var events = new List<CardEvent>();
        if (Over) return events;
        Round++;
        var played = new List<string>[2];
        for (int side = 0; side < 2; side++)
        {
            var s = Sides[side];
            var idx = Validate(side, side == 0 ? picks0 : picks1);
            played[side] = idx.Select(i => s.Hand[i]).ToList();
            foreach (int i in idx.OrderByDescending(i => i)) { s.Discard.Add(s.Hand[i]); s.Hand.RemoveAt(i); }
            s.Energy -= played[side].Sum(c => PetCards.CardOf(c).Cost);
            s.Block = 0; s.Thorns = 0;
        }

        // Defence, healing and skills land first, for both sides.
        for (int side = 0; side < 2; side++)
            foreach (var id in played[side].Where(c => PetCards.CardOf(c).Damage == 0))
                Support(side, PetCards.CardOf(id), events);

        // Then attacks, the faster pet first.
        int sp0 = Sides[0].Loadout.Speed, sp1 = Sides[1].Loadout.Speed;
        int first = sp0 > sp1 ? 0 : sp1 > sp0 ? 1 : rng.Below(2);
        foreach (int side in new[] { first, 1 - first })
            foreach (var id in played[side].Where(c => PetCards.CardOf(c).Damage > 0))
            {
                if (Over) break;
                Attack(side, PetCards.CardOf(id), events);
            }

        // End of round: burns, weakness wears off, refill.
        for (int side = 0; side < 2 && !Over; side++)
        {
            var s = Sides[side];
            if (s.Burn > 0)
            {
                int dmg = s.Burn;
                s.Hp = Math.Max(0, s.Hp - dmg);
                s.Burn = Math.Max(0, s.Burn - 2);
                events.Add(new(side, "burn", "", dmg));
                CheckOver(events);
            }
        }
        foreach (var s in Sides)
        {
            if (s.Weak > 0) s.Weak--;
            s.Energy = Math.Min(PetCards.MaxEnergy, s.Energy + PetCards.EnergyPerRound);
            Draw(s, Math.Max(0, PetCards.HandSize - s.Hand.Count));
        }
        if (!Over && Round >= PetCards.MaxRounds)
        {
            Over = true;
            long a = (long)Sides[0].Hp * Sides[1].Loadout.MaxHp, b = (long)Sides[1].Hp * Sides[0].Loadout.MaxHp;
            Winner = a > b ? 0 : b > a ? 1 : -1;
            events.Add(new(Winner, "timeout", ""));
        }
        return events;
    }

    private void Support(int side, PetCard card, List<CardEvent> events)
    {
        var me = Sides[side];
        var foe = Sides[1 - side];
        events.Add(new(side, "play", card.Id));
        if (card.Block > 0) { int b = PetCards.Scale(card.Block, me.Loadout.Guard); me.Block += b; events.Add(new(side, "block", card.Id, b)); }
        if (card.Thorns > 0) { me.Thorns += card.Thorns; events.Add(new(side, "thorns", card.Id, card.Thorns)); }
        if (card.Cleanse && (me.Burn > 0 || me.Weak > 0)) { me.Burn = 0; me.Weak = 0; events.Add(new(side, "cleanse", card.Id)); }
        if (card.Heal > 0) HealSide(side, PetCards.Scale(card.Heal, me.Loadout.Level / 2), card.Id, events);
        if (card.Burn > 0) { foe.Burn += card.Burn; events.Add(new(1 - side, "burned", card.Id, card.Burn)); }
        if (card.Weak > 0) { foe.Weak = Math.Max(foe.Weak, card.Weak); events.Add(new(1 - side, "weak", card.Id, card.Weak)); }
        if (card.Energy > 0) { me.Energy = Math.Min(PetCards.MaxEnergy, me.Energy + card.Energy); events.Add(new(side, "energy", card.Id, card.Energy)); }
        if (card.Draw > 0) { Draw(me, card.Draw); events.Add(new(side, "draw", card.Id, card.Draw)); }
    }

    private void HealSide(int side, int amount, string card, List<CardEvent> events)
    {
        var s = Sides[side];
        int heal = Math.Min(amount, s.Loadout.MaxHp - s.Hp);
        if (heal <= 0) return;
        s.Hp += heal;
        events.Add(new(side, "heal", card, heal));
    }

    private void Attack(int side, PetCard card, List<CardEvent> events)
    {
        var me = Sides[side];
        var foe = Sides[1 - side];
        events.Add(new(side, "play", card.Id));
        int typePct = card.Element.Length == 0 ? 100 : PetGame.TypePercent(card.Element, foe.Loadout.Element) switch { > 100 => 125, < 100 => 85, _ => 100 };
        int dealt = 0;
        if (card.Block > 0) { int b = PetCards.Scale(card.Block, me.Loadout.Guard); me.Block += b; events.Add(new(side, "block", card.Id, b)); }
        if (card.Thorns > 0) { me.Thorns += card.Thorns; events.Add(new(side, "thorns", card.Id, card.Thorns)); }
        for (int hit = 0; hit < Math.Max(1, card.Hits) && !Over && card.Damage > 0; hit++)
        {
            int dmg = PetCards.Scale(card.Damage, me.Loadout.Power) * typePct / 100;
            dmg = dmg * (90 + rng.Below(21)) / 100;
            if (me.Weak > 0) dmg = dmg * 75 / 100;
            dmg = Math.Max(1, dmg);
            int absorbed = card.Pierce ? 0 : Math.Min(foe.Block, dmg);
            foe.Block -= absorbed;
            int taken = dmg - absorbed;
            foe.Hp = Math.Max(0, foe.Hp - taken);
            dealt += taken;
            events.Add(new(1 - side, "hit", card.Id, taken, typePct) { });
            if (absorbed > 0) events.Add(new(1 - side, "blocked", card.Id, absorbed));
            if (foe.Thorns > 0 && foe.Hp > 0)
            {
                me.Hp = Math.Max(0, me.Hp - foe.Thorns);
                events.Add(new(side, "thornhit", card.Id, foe.Thorns));
            }
            CheckOver(events);
        }
        if (Over) return;
        if (card.Lifesteal > 0 && dealt > 0) HealSide(side, Math.Max(1, dealt * card.Lifesteal / 100), card.Id, events);
        if (card.Heal > 0) HealSide(side, PetCards.Scale(card.Heal, me.Loadout.Level / 2), card.Id, events);
        if (card.Burn > 0) { foe.Burn += card.Burn; events.Add(new(1 - side, "burned", card.Id, card.Burn)); }
        if (card.Weak > 0) { foe.Weak = Math.Max(foe.Weak, card.Weak); events.Add(new(1 - side, "weak", card.Id, card.Weak)); }
        if (card.Draw > 0) { Draw(me, card.Draw); events.Add(new(side, "draw", card.Id, card.Draw)); }
    }

    private void CheckOver(List<CardEvent> events)
    {
        if (Over) return;
        bool a = Sides[0].Hp <= 0, b = Sides[1].Hp <= 0;
        if (!a && !b) return;
        Over = true;
        Winner = a && b ? -1 : a ? 1 : 0;
        if (a) events.Add(new(0, "faint", ""));
        if (b) events.Add(new(1, "faint", ""));
    }

    public string Hash()
    {
        var sb = new StringBuilder().Append(Round).Append('|').Append(rng.State);
        foreach (var s in Sides)
            sb.Append('|').Append(s.Hp).Append(',').Append(s.Energy).Append(',').Append(s.Burn).Append(',').Append(s.Weak)
              .Append(',').Append(string.Join('.', s.Hand)).Append(',').Append(s.DrawPile.Count);
        ulong h = 1469598103934665603UL;
        foreach (char ch in sb.ToString()) { h ^= ch; h *= 1099511628211UL; }
        return h.ToString("x16");
    }

    /// <summary>Practice-opponent brain: defends when hurt, finishes when it can, otherwise spends its energy.</summary>
    public List<int> ChooseAi(int side, Random random)
    {
        var me = Sides[side];
        var foe = Sides[1 - side];
        bool hurt = me.Hp * 100 / me.Loadout.MaxHp < 45;
        int Score(string id)
        {
            var c = PetCards.CardOf(id);
            int score = c.Damage * Math.Max(1, c.Hits) * 2 + c.Burn * 2 + c.Weak * 4 + c.Draw * 3 + c.Energy * 4;
            score += (c.Block + c.Thorns * 2) * (hurt ? 3 : 1);
            score += c.Heal * (hurt ? 4 : me.Hp < me.Loadout.MaxHp ? 1 : 0);
            if (c.Damage * Math.Max(1, c.Hits) >= foe.Hp) score += 50;
            return score * 10 / Math.Max(1, c.Cost * 10) + random.Next(4);
        }
        var order = Enumerable.Range(0, me.Hand.Count).OrderByDescending(i => Score(me.Hand[i])).ToList();
        // Free cards (cost 0) first so their energy can be spent.
        order = order.OrderBy(i => PetCards.CardOf(me.Hand[i]).Cost == 0 ? 0 : 1).ToList();
        return Validate(side, order);
    }
}
