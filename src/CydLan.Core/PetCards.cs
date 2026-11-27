using System.Text;
using System.Text.Json;

namespace CydLan.Core;

/// <summary>A battle card. Every number is a base value; the pet's stats scale damage, block and healing.</summary>
public sealed record PetCard(string Id, string Name, int Cost, string Kind, string Element = "",
    int Damage = 0, int Hits = 1, int Block = 0, int Heal = 0, int Thorns = 0, int Burn = 0, int Weak = 0,
    int Draw = 0, int Energy = 0, bool Pierce = false, int Lifesteal = 0, bool Cleanse = false,
    string Rarity = PetCards.Common, string Art = "",
    int Parry = 0, bool Interrupt = false, int Punish = 0, bool Stun = false, bool Feint = false,
    int Poison = 0, int Sleep = 0, int Fear = 0, int Slow = 0, int Regen = 0, int Rage = 0,
    int NextEnergy = 0, int StealEnergy = 0, string OnHit = "", int Combo = 0)
{
    /// <summary>Short rules text built from the card's numbers.</summary>
    public string Text
    {
        get
        {
            var parts = new List<string>();
            if (Parry > 0) parts.Add($"Parry, hit back {Parry}");
            if (Interrupt) parts.Add("Cancel foe's non-attacks");
            if (Damage > 0) parts.Add(Hits > 1 ? $"Deal {Damage} ×{Hits}" : $"Deal {Damage}");
            if (Punish > 0) parts.Add($"+{Punish} vs block");
            if (Combo > 0) parts.Add($"Combo +{Combo}");
            if (Pierce) parts.Add("ignores block");
            if (Feint) parts.Add("can't be parried");
            if (Stun) parts.Add("Stun");
            if (Lifesteal > 0) parts.Add(Lifesteal >= 100 ? "heal that much" : $"heal {Lifesteal}% of it");
            if (Block > 0) parts.Add($"Block {Block}");
            if (Thorns > 0) parts.Add($"Thorns {Thorns}");
            if (Heal > 0) parts.Add($"Heal {Heal}");
            if (Burn > 0) parts.Add($"Burn {Burn}");
            if (Weak > 0) parts.Add(Weak > 1 ? $"Weaken {Weak} rounds" : "Weaken");
            if (Poison > 0) parts.Add($"Poison {Poison}");
            if (Sleep > 0) parts.Add(Sleep > 1 ? $"Sleep {Sleep} rounds" : "Sleep");
            if (Fear > 0) parts.Add(Fear > 1 ? $"Fear {Fear}" : "Fear");
            if (Slow > 0) parts.Add(Slow > 1 ? $"Slow {Slow} rounds" : "Slow");
            if (Regen > 0) parts.Add($"Regen {Regen}");
            if (Rage > 0) parts.Add(Rage > 1 ? $"Rage {Rage} rounds" : "Rage");
            if (Cleanse) parts.Add("Cleanse");
            if (Draw > 0) parts.Add(Draw > 1 ? $"Draw {Draw}" : "Draw 1");
            if (Energy > 0) parts.Add($"+{Energy} energy");
            if (NextEnergy > 0) parts.Add($"Next +{NextEnergy} energy");
            if (StealEnergy > 0) parts.Add($"Steal {StealEnergy} energy");
            if (OnHit.Length > 0) parts.Add("If hit: " + DescribeOnHit(OnHit));
            return string.Join(" · ", parts);
        }
    }

    public static (string Kind, int Amount) ParseTrap(string spec)
    {
        int colon = spec.IndexOf(':');
        return colon > 0 && int.TryParse(spec.AsSpan(colon + 1), out int n) ? (spec[..colon], n) : (spec, 1);
    }

    private static string DescribeOnHit(string spec)
    {
        var (kind, amount) = ParseTrap(spec);
        return kind switch
        {
            "burn" => $"Burn {amount}",
            "poison" => $"Poison {amount}",
            "weak" => amount > 1 ? $"Weaken {amount}" : "Weaken",
            "slow" => amount > 1 ? $"Slow {amount}" : "Slow",
            "fear" => amount > 1 ? $"Fear {amount}" : "Fear",
            "energy" => $"+{amount} energy next",
            _ => spec
        };
    }

    /// <summary>The icon sheet and cell for <see cref="Art"/> ("fire:12" → ("fire", 12)).</summary>
    public (string Sheet, int Index) ArtCell
    {
        get
        {
            int colon = Art.IndexOf(':');
            return colon > 0 && int.TryParse(Art.AsSpan(colon + 1), out int i) ? (Art[..colon], i) : ("", -1);
        }
    }
}

/// <summary>Cards, collections, decks, the daily shop and drops for the companion card battles.</summary>
public static partial class PetCards
{
    public const int StartEnergy = 3, EnergyPerRound = 3, MaxEnergy = 8, HandSize = 5, MaxHand = 8, MaxRounds = 30;
    public const int DeckSize = 20, MaxCopies = 2, MaxOwned = 9, ShopSlots = 6;
    /// <summary>Share (in percent) of rare-and-up cards that a given install can never get, so trading has a reason to exist.</summary>
    public const int NeverDropPercent = 20;

    private static readonly Dictionary<string, PetCard> CardById = Cards.ToDictionary(c => c.Id);

    public static PetCard CardOf(string id) => CardById.TryGetValue(id, out var c) ? c : CardById["strike"];
    public static bool IsCard(string? id) => id is not null && CardById.ContainsKey(id);
    public static int RarityRank(string rarity) => rarity switch { Legendary => 3, Epic => 2, Rare => 1, _ => 0 };
    public static int CopyLimit(PetCard card) => card.Rarity == Legendary ? 1 : MaxCopies;
    public static int Price(PetCard card) => card.Rarity switch { Legendary => 900, Epic => 320, Rare => 120, _ => 40 };

    private static ulong Mix(long seed, string text)
    {
        ulong h = 1469598103934665603UL ^ (ulong)seed;
        foreach (char ch in text) { h ^= ch; h *= 1099511628211UL; }
        return new BattleRng(h).Next();
    }

    /// <summary>Cards this install will never find in drops or the shop (only by trading). Commons are always obtainable.</summary>
    public static bool NeverDrops(PetProgress p, string id) =>
        CardById.TryGetValue(id, out var c) && c.Rarity != Common && (int)(Mix(p.Seed, "lock:" + id) % 100) < NeverDropPercent;

    public static IEnumerable<PetCard> Obtainable(PetProgress p) => Cards.Where(c => !NeverDrops(p, c.Id));

    public static int Owned(PetProgress p, string id) => p.Cards.TryGetValue(id, out int n) ? n : 0;
    public static int InDeck(PetProgress p, string id) => p.Deck.Count(c => c == id);

    public static bool AddCard(PetProgress p, string id)
    {
        if (!IsCard(id) || Owned(p, id) >= MaxOwned) return false;
        p.Cards[id] = Owned(p, id) + 1;
        return true;
    }

    // ── Starting collection ──────────────────────────────────────────────

    /// <summary>
    /// The random set of <see cref="DeckSize"/> cards every install starts with: a few basics, then commons from any
    /// element and a couple of rares. The same seed always gives the same set.
    /// </summary>
    public static List<string> Starter(long seed)
    {
        var rng = new BattleRng(Mix(seed, "starter"));
        var deck = new List<string> { "strike", "strike", "guard", "guard" };
        var commons = Cards.Where(c => c.Rarity == Common && c.Id is not ("strike" or "guard")).ToList();
        var rares = Cards.Where(c => c.Rarity == Rare).ToList();
        void Take(List<PetCard> pool)
        {
            for (int tries = 0; tries < 200; tries++)
            {
                var card = pool[rng.Below(pool.Count)];
                if (deck.Count(c => c == card.Id) >= CopyLimit(card)) continue;
                deck.Add(card.Id);
                return;
            }
        }
        for (int i = 0; i < 2; i++) Take(rares);
        // Enough attacks that every starter can win a fight.
        var attacks = commons.Where(c => c.Damage > 0).ToList();
        while (deck.Count(c => CardOf(c).Damage > 0) < 9) Take(attacks);
        while (deck.Count < DeckSize) Take(commons);
        return deck;
    }

    /// <summary>Gives a new install its seed, starter cards and deck; repairs a deck that no longer fits the collection.</summary>
    public static void EnsureStarted(PetProgress p, Random random)
    {
        if (p.Seed == 0)
        {
            p.Seed = random.NextInt64(1, long.MaxValue);
            // A new install's element is random too; after that only a paid re-roll changes it.
            if (p.Cards.Count == 0) p.Element = PetGame.RollElement(random);
        }
        if (p.Cards.Count == 0)
            foreach (var id in Starter(p.Seed)) p.Cards[id] = Owned(p, id) + 1;
        if (p.Deck.Count == 0) p.Deck = p.Cards.SelectMany(kv => Enumerable.Repeat(kv.Key, Math.Min(kv.Value, CopyLimit(CardOf(kv.Key))))).Take(DeckSize).ToList();
    }

    public static void Normalize(PetProgress p)
    {
        p.Cards = (p.Cards ?? []).Where(kv => IsCard(kv.Key) && kv.Value > 0).ToDictionary(kv => kv.Key, kv => Math.Min(kv.Value, MaxOwned));
        var deck = new List<string>();
        foreach (var id in p.Deck ?? [])
            if (IsCard(id) && deck.Count < DeckSize && deck.Count(c => c == id) < Math.Min(Owned(p, id), CopyLimit(CardOf(id)))) deck.Add(id);
        p.Deck = deck;
        p.ShopDay ??= "";
        p.ShopBought = (p.ShopBought ?? []).Where(i => i is >= 0 and < ShopSlots).Distinct().ToList();
        p.CardWins = Math.Max(0, p.CardWins); p.CardLosses = Math.Max(0, p.CardLosses);
    }

    // ── Deck building ────────────────────────────────────────────────────

    public static bool DeckReady(PetProgress p) => p.Deck.Count == DeckSize;

    public static bool CanAddToDeck(PetProgress p, string id) =>
        IsCard(id) && p.Deck.Count < DeckSize && InDeck(p, id) < Math.Min(Owned(p, id), CopyLimit(CardOf(id)));

    public static bool AddToDeck(PetProgress p, string id)
    {
        if (!CanAddToDeck(p, id)) return false;
        p.Deck.Add(id);
        return true;
    }

    public static bool RemoveFromDeck(PetProgress p, string id) => p.Deck.Remove(id);

    // ── Daily shop ───────────────────────────────────────────────────────

    public sealed record ShopOffer(int Slot, PetCard Card, int Price, bool Bought);

    private static PetCard RollRarity(BattleRng rng, IReadOnlyList<PetCard> pool, int luck)
    {
        int roll = rng.Below(100);
        int legendary = Math.Clamp(2 + luck / 10, 2, 8), epic = Math.Clamp(10 + luck / 4, 10, 22), rare = 32;
        string rarity = roll < legendary ? Legendary : roll < legendary + epic ? Epic : roll < legendary + epic + rare ? Rare : Common;
        var tier = pool.Where(c => c.Rarity == rarity).ToList();
        if (tier.Count == 0) tier = pool.ToList();
        return tier[rng.Below(tier.Count)];
    }

    /// <summary>Today's six offers. Same for the whole day on this install, never a card this install can't get.</summary>
    public static IReadOnlyList<ShopOffer> Shop(PetProgress p, string day)
    {
        if (p.ShopDay != day) { p.ShopDay = day; p.ShopBought.Clear(); }
        var pool = Obtainable(p).ToList();
        var rng = new BattleRng(Mix(p.Seed, "shop:" + day));
        var offers = new List<ShopOffer>();
        var seen = new HashSet<string>();
        for (int slot = 0; offers.Count < ShopSlots && slot < 60; slot++)
        {
            var card = RollRarity(rng, pool, Math.Min(p.Level, 100));
            if (!seen.Add(card.Id)) continue;
            int i = offers.Count;
            offers.Add(new ShopOffer(i, card, Price(card), p.ShopBought.Contains(i)));
        }
        return offers;
    }

    public static bool BuyFromShop(PetProgress p, string day, int slot)
    {
        var offers = Shop(p, day);
        if (slot < 0 || slot >= offers.Count || offers[slot].Bought || p.Coins < offers[slot].Price || Owned(p, offers[slot].Card.Id) >= MaxOwned) return false;
        p.Coins -= offers[slot].Price;
        AddCard(p, offers[slot].Card.Id);
        p.ShopBought.Add(slot);
        return true;
    }

    // ── Drops ────────────────────────────────────────────────────────────

    /// <summary>A card found after a world fight: likelier on wins and bosses, better on later stages.</summary>
    public static PetCard? RollDrop(PetProgress p, Random random, int stage, bool boss, bool won)
    {
        int chance = won ? (boss ? 80 : 30) : 8;
        if (random.Next(100) >= chance) return null;
        var pool = Obtainable(p).ToList();
        return RollRarity(new BattleRng((ulong)random.NextInt64()), pool, stage + (boss ? 20 : 0));
    }

    // ── Loadouts ─────────────────────────────────────────────────────────

    public const int MaxLoadoutChars = 768;

    public sealed record Loadout(string Name, string PetCode, int Level, string Element, int MaxHp, int Power, int Guard, int Speed, IReadOnlyList<string> Deck, int Vitality = 0);

    public static int TotalPower(PetProgress p) => p.BasePower + p.Power;
    public static int TotalGuard(PetProgress p) => p.BaseGuard + p.Guard;
    public static int TotalSpeed(PetProgress p) => p.BaseSpeed + p.Speed;
    public static int TotalVitality(PetProgress p) => p.BaseVitality + p.Vitality;

    /// <summary>HP from vitality. Level never adds strength: only the rolled stats and the capped bonus points do.</summary>
    public static int MaxHpFor(int vitality) => 50 + Math.Max(0, vitality) * 4;

    public static Loadout LoadoutFor(PetProgress p, string name, string petCode)
    {
        int vit = TotalVitality(p);
        return new(name, petCode, p.Level, PetGame.NormalizeElement(p.Element), MaxHpFor(vit), TotalPower(p), TotalGuard(p), TotalSpeed(p),
            DeckReady(p) ? p.Deck.ToList() : Starter(p.Seed), vit);
    }

    /// <summary>Stat-scaled value: each stat point adds 4%, rounded to the nearest integer (0 stat leaves value exact).</summary>
    public static int Scale(int value, int stat) => value <= 0 || stat <= 0 ? value : value + (value * stat + 12) / 25;

    private sealed class WireLoadout
    {
        public string N { get; set; } = "";
        public string C { get; set; } = "";
        public int L { get; set; } = 1;
        public string E { get; set; } = "blaze";
        public int P { get; set; }
        public int G { get; set; }
        public int S { get; set; }
        public int V { get; set; }
        public string D { get; set; } = "";
    }

    public static Loadout SanitizeLoadout(Loadout l)
    {
        string name = new string((l.Name ?? "").Trim().Where(ch => !char.IsControl(ch)).Take(40).ToArray());
        string code = (l.PetCode ?? "").Trim();
        if (code.Length > 80 || (code.Length > 0 && !code.StartsWith("pet1:", StringComparison.Ordinal))) code = "";
        int maxStat = PetGame.BaseStatMax + PetGame.MaxBonusPoints;
        int p = Math.Clamp(l.Power, PetGame.BaseStatMin, maxStat);
        int g = Math.Clamp(l.Guard, PetGame.BaseStatMin, maxStat);
        int s = Math.Clamp(l.Speed, PetGame.BaseStatMin, maxStat);
        int v = Math.Clamp(l.Vitality > 0 ? l.Vitality : Math.Max(PetGame.BaseStatMin, (l.MaxHp - 50) / 4), PetGame.BaseStatMin, maxStat);
        if (p + g + s + v > PetGame.BaseTotal + PetGame.MaxBonusPoints)
            p = g = s = v = PetGame.EvenStat;
        var deck = new List<string>();
        foreach (var id in l.Deck ?? [])
            if (IsCard(id) && deck.Count < DeckSize && deck.Count(c => c == id) < CopyLimit(CardOf(id))) deck.Add(id);
        if (deck.Count < DeckSize)
        {
            foreach (var fallback in Starter(1))
                if (deck.Count < DeckSize && deck.Count(c => c == fallback) < CopyLimit(CardOf(fallback))) deck.Add(fallback);
            while (deck.Count < DeckSize) deck.Add("strike");
        }
        return new(name, code, Math.Clamp(l.Level, 1, PetGame.MaxLevel), PetGame.NormalizeElement(l.Element), MaxHpFor(v), p, g, s, deck, v);
    }

    public static string EncodeLoadout(Loadout loadout)
    {
        var s = SanitizeLoadout(loadout);
        var wire = new WireLoadout { N = s.Name, C = s.PetCode, L = s.Level, E = s.Element, P = s.Power, G = s.Guard, S = s.Speed, V = s.Vitality, D = string.Join(',', s.Deck) };
        string json = JsonSerializer.Serialize(wire);
        if (json.Length > MaxLoadoutChars)
        {
            wire.C = "";
            json = JsonSerializer.Serialize(wire);
        }
        return json;
    }

    public static Loadout? DecodeLoadout(string? json)
    {
        if (string.IsNullOrWhiteSpace(json) || json.Length > MaxLoadoutChars) return null;
        try
        {
            var w = JsonSerializer.Deserialize<WireLoadout>(json);
            if (w is null) return null;
            var deck = (w.D ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries);
            return SanitizeLoadout(new(w.N, w.C, w.L, w.E, MaxHpFor(w.V), w.P, w.G, w.S, deck, w.V));
        }
        catch (JsonException) { return null; }
    }
}

public sealed record CardEvent(int Actor, string Kind, string Card, int Amount = 0, int TypePercent = 100);

/// <summary>
/// A one-on-one card battle. Each round both sides get energy and a hand, secretly pick cards, then the round plays out:
/// interrupts first, then defence, healing, parries and skills, then attacks in speed order. Deterministic for a given seed and choices.
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
        /// <summary>Damage at the end of each round; drops by one each time.</summary>
        public int Poison { get; set; }
        /// <summary>Rounds asleep: attack cards do nothing. A hit to HP wakes the pet.</summary>
        public int Sleep { get; set; }
        /// <summary>Attack cards that will miss.</summary>
        public int Fear { get; set; }
        /// <summary>Rounds acting after the foe whatever the speed.</summary>
        public int Slow { get; set; }
        /// <summary>Healing at the end of each round; drops by one each time.</summary>
        public int Regen { get; set; }
        /// <summary>Rounds of +30% damage.</summary>
        public int Rage { get; set; }
        /// <summary>Bonus energy added at the start of the next round.</summary>
        public int NextEnergy { get; set; }
        /// <summary>Energy drained from the next round's refill.</summary>
        public int EnergyDrain { get; set; }
        // Sleep and slow put on during a round still last through the next one.
        internal bool SleepFresh, SlowFresh;
        public int Energy { get; set; }
        /// <summary>Parry cards waiting this round; each cancels one incoming attack card.</summary>
        public List<string> Parries { get; } = [];
        /// <summary>Reactive tricks armed this round that trigger when an enemy attack hits.</summary>
        public List<(string Card, string Spec)> Traps { get; } = [];
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
            s.Block = 0; s.Thorns = 0; s.Parries.Clear(); s.Traps.Clear();
        }

        // Interrupts land first and cancel the foe's non-attack cards (interrupts land together, so they never cancel each other).
        for (int side = 0; side < 2; side++)
            foreach (var id in played[side].Where(c => PetCards.CardOf(c).Interrupt))
                Support(side, PetCards.CardOf(id), events);
        var cancelled = new[] { new List<string>(), new List<string>() };
        for (int side = 0; side < 2; side++)
        {
            if (!played[1 - side].Any(c => PetCards.CardOf(c).Interrupt)) continue;
            foreach (var id in played[side].Where(c => PetCards.CardOf(c) is { Damage: 0, Interrupt: false }))
            {
                cancelled[side].Add(id);
                events.Add(new(side, "interrupted", id));
            }
        }
        for (int side = 0; side < 2; side++)
            foreach (var id in cancelled[side]) played[side].Remove(id);

        // Defence, healing, parries and skills, for both sides.
        for (int side = 0; side < 2; side++)
            foreach (var id in played[side].Where(c => PetCards.CardOf(c) is { Damage: 0, Interrupt: false }))
                Support(side, PetCards.CardOf(id), events);

        // Then attacks, the faster pet first. Parries cancel attacks; a stun stops the foe's attacks still to come.
        bool[] guarded = [played[0].Any(c => PetCards.CardOf(c).Block > 0), played[1].Any(c => PetCards.CardOf(c).Block > 0)];
        bool[] comboReady = [played[0].Count >= 2, played[1].Count >= 2];
        bool[] stunned = new bool[2];
        int sp0 = Sides[0].Loadout.Speed, sp1 = Sides[1].Loadout.Speed;
        bool slow0 = Sides[0].Slow > 0, slow1 = Sides[1].Slow > 0;
        int first = slow0 != slow1 ? (slow0 ? 1 : 0) : sp0 > sp1 ? 0 : sp1 > sp0 ? 1 : rng.Below(2);
        foreach (int side in new[] { first, 1 - first })
            foreach (var id in played[side].Where(c => PetCards.CardOf(c).Damage > 0))
            {
                if (Over) break;
                var card = PetCards.CardOf(id);
                var me = Sides[side];
                var foe = Sides[1 - side];
                if (stunned[side]) { events.Add(new(side, "fizzle", id)); continue; }
                if (me.Sleep > 0) { events.Add(new(side, "snooze", id)); continue; }
                if (me.Fear > 0) { me.Fear--; events.Add(new(side, "miss", id)); continue; }
                if (foe.Parries.Count > 0 && !card.Feint)
                {
                    var parry = PetCards.CardOf(foe.Parries[0]);
                    foe.Parries.RemoveAt(0);
                    events.Add(new(side, "parried", id));
                    Strike(1 - side, parry, parry.Parry, 1, false, 0, events);
                    continue;
                }
                events.Add(new(side, "play", card.Id));
                int punish = guarded[1 - side] && card.Punish > 0 ? PetCards.Scale(card.Punish, me.Loadout.Speed) : 0;
                int combo = comboReady[side] && card.Combo > 0 ? PetCards.Scale(card.Combo, me.Loadout.Speed) : 0;
                if (combo > 0) events.Add(new(side, "combo", card.Id, combo));
                if (Attack(side, card, punish, combo, events) && card.Stun && !stunned[1 - side] && !Over)
                {
                    stunned[1 - side] = true;
                    events.Add(new(1 - side, "stun", card.Id));
                }
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
            if (s.Poison > 0 && !Over)
            {
                int dmg = s.Poison;
                s.Hp = Math.Max(0, s.Hp - dmg);
                s.Poison--;
                events.Add(new(side, "poison", "", dmg));
                CheckOver(events);
            }
            if (s.Regen > 0 && !Over)
            {
                HealSide(side, s.Regen, "", events);
                s.Regen--;
            }
        }
        foreach (var s in Sides)
        {
            if (s.Weak > 0) s.Weak--;
            if (s.Rage > 0) s.Rage--;
            if (s.SleepFresh) s.SleepFresh = false; else if (s.Sleep > 0) s.Sleep--;
            if (s.SlowFresh) s.SlowFresh = false; else if (s.Slow > 0) s.Slow--;
            int gain = Math.Max(1, PetCards.EnergyPerRound + s.NextEnergy - s.EnergyDrain);
            s.NextEnergy = 0; s.EnergyDrain = 0;
            s.Energy = Math.Min(PetCards.MaxEnergy, s.Energy + gain);
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
        if (card.Thorns > 0) { int th = PetCards.Scale(card.Thorns, me.Loadout.Guard); me.Thorns += th; events.Add(new(side, "thorns", card.Id, th)); }
        if (card.OnHit.Length > 0) { me.Traps.Add((card.Id, card.OnHit)); events.Add(new(side, "trap", card.Id)); }
        if (card.Parry > 0) { me.Parries.Add(card.Id); events.Add(new(side, "parry", card.Id, card.Parry)); }
        if (card.Interrupt) events.Add(new(side, "interrupt", card.Id));
        if (card.Cleanse && (me.Burn > 0 || me.Weak > 0 || me.Poison > 0 || me.Sleep > 0 || me.Fear > 0 || me.Slow > 0))
        {
            me.Burn = 0; me.Weak = 0; me.Poison = 0; me.Sleep = 0; me.Fear = 0; me.Slow = 0;
            me.SleepFresh = false; me.SlowFresh = false;
            events.Add(new(side, "cleanse", card.Id));
        }
        if (card.Heal > 0) HealSide(side, PetCards.Scale(card.Heal, me.Loadout.Vitality), card.Id, events);
        if (card.Burn > 0) { int bn = PetCards.Scale(card.Burn, me.Loadout.Power); foe.Burn += bn; events.Add(new(1 - side, "burned", card.Id, bn)); }
        if (card.Weak > 0) { foe.Weak = Math.Max(foe.Weak, card.Weak); events.Add(new(1 - side, "weak", card.Id, card.Weak)); }
        Afflict(side, card, events);
        if (card.Energy > 0) { me.Energy = Math.Min(PetCards.MaxEnergy, me.Energy + card.Energy); events.Add(new(side, "energy", card.Id, card.Energy)); }
        if (card.NextEnergy > 0) { me.NextEnergy += card.NextEnergy; events.Add(new(side, "nextenergy", card.Id, card.NextEnergy)); }
        if (card.StealEnergy > 0) Steal(side, card, events);
        if (card.Draw > 0) { Draw(me, card.Draw); events.Add(new(side, "draw", card.Id, card.Draw)); }
    }

    private void Steal(int side, PetCard card, List<CardEvent> events)
    {
        var me = Sides[side];
        var foe = Sides[1 - side];
        int amt = card.StealEnergy;
        int fromNow = Math.Min(foe.Energy, amt);
        foe.Energy -= fromNow;
        foe.EnergyDrain += amt - fromNow;
        me.NextEnergy += amt;
        events.Add(new(side, "stealenergy", card.Id, amt));
    }

    private void TriggerTraps(int defenderSide, int attackerSide, List<CardEvent> events)
    {
        var def = Sides[defenderSide];
        var atk = Sides[attackerSide];
        if (def.Traps.Count == 0 || Over) return;
        var armed = def.Traps.ToList();
        def.Traps.Clear();
        foreach (var (cardId, spec) in armed)
        {
            var (kind, amount) = PetCard.ParseTrap(spec);
            switch (kind)
            {
                case "burn": { int bn = PetCards.Scale(amount, def.Loadout.Guard); atk.Burn += bn; events.Add(new(attackerSide, "burned", cardId, bn)); break; }
                case "poison": { int ps = PetCards.Scale(amount, def.Loadout.Guard); atk.Poison += ps; events.Add(new(attackerSide, "poisoned", cardId, ps)); break; }
                case "weak": atk.Weak = Math.Max(atk.Weak, amount); events.Add(new(attackerSide, "weak", cardId, amount)); break;
                case "slow": atk.Slow = Math.Max(atk.Slow, amount); atk.SlowFresh = true; events.Add(new(attackerSide, "slow", cardId, amount)); break;
                case "fear": atk.Fear += amount; events.Add(new(attackerSide, "fear", cardId, amount)); break;
                case "energy": def.NextEnergy += amount; events.Add(new(defenderSide, "nextenergy", cardId, amount)); break;
            }
        }
    }

    /// <summary>Puts a card's statuses on: ailments on the foe, boons on the player.</summary>
    private void Afflict(int side, PetCard card, List<CardEvent> events)
    {
        var me = Sides[side];
        var foe = Sides[1 - side];
        if (card.Poison > 0) { int ps = PetCards.Scale(card.Poison, me.Loadout.Power); foe.Poison += ps; events.Add(new(1 - side, "poisoned", card.Id, ps)); }
        if (card.Sleep > 0) { foe.Sleep = Math.Max(foe.Sleep, card.Sleep); foe.SleepFresh = true; events.Add(new(1 - side, "sleep", card.Id, card.Sleep)); }
        if (card.Fear > 0) { foe.Fear += card.Fear; events.Add(new(1 - side, "fear", card.Id, card.Fear)); }
        if (card.Slow > 0) { foe.Slow = Math.Max(foe.Slow, card.Slow); foe.SlowFresh = true; events.Add(new(1 - side, "slow", card.Id, card.Slow)); }
        if (card.Regen > 0) { int rg = PetCards.Scale(card.Regen, me.Loadout.Vitality); me.Regen += rg; events.Add(new(side, "regen", card.Id, rg)); }
        if (card.Rage > 0) { me.Rage = Math.Max(me.Rage, card.Rage); events.Add(new(side, "rage", card.Id, card.Rage)); }
    }

    private void HealSide(int side, int amount, string card, List<CardEvent> events)
    {
        var s = Sides[side];
        int heal = Math.Min(amount, s.Loadout.MaxHp - s.Hp);
        if (heal <= 0) return;
        s.Hp += heal;
        events.Add(new(side, "heal", card, heal));
    }

    /// <summary>Plays an attack card. True when it took HP off the foe.</summary>
    private bool Attack(int side, PetCard card, int punish, int combo, List<CardEvent> events)
    {
        var me = Sides[side];
        var foe = Sides[1 - side];
        if (card.Block > 0) { int b = PetCards.Scale(card.Block, me.Loadout.Guard); me.Block += b; events.Add(new(side, "block", card.Id, b)); }
        if (card.Thorns > 0) { int th = PetCards.Scale(card.Thorns, me.Loadout.Guard); me.Thorns += th; events.Add(new(side, "thorns", card.Id, th)); }
        if (card.OnHit.Length > 0) { me.Traps.Add((card.Id, card.OnHit)); events.Add(new(side, "trap", card.Id)); }
        if (punish > 0) events.Add(new(1 - side, "punish", card.Id, punish));
        int dealt = Strike(side, card, card.Damage, card.Hits, card.Pierce, foe.Thorns, events, punish + combo);
        if (Over) return dealt > 0;
        TriggerTraps(1 - side, side, events);
        if (card.Lifesteal > 0 && dealt > 0) HealSide(side, Math.Max(1, dealt * card.Lifesteal / 100), card.Id, events);
        if (card.Heal > 0) HealSide(side, PetCards.Scale(card.Heal, me.Loadout.Vitality), card.Id, events);
        if (card.Burn > 0) { int bn = PetCards.Scale(card.Burn, me.Loadout.Power); foe.Burn += bn; events.Add(new(1 - side, "burned", card.Id, bn)); }
        if (card.Weak > 0) { foe.Weak = Math.Max(foe.Weak, card.Weak); events.Add(new(1 - side, "weak", card.Id, card.Weak)); }
        Afflict(side, card, events);
        if (card.NextEnergy > 0) { me.NextEnergy += card.NextEnergy; events.Add(new(side, "nextenergy", card.Id, card.NextEnergy)); }
        if (card.StealEnergy > 0 && dealt > 0) Steal(side, card, events);
        if (card.Draw > 0) { Draw(me, card.Draw); events.Add(new(side, "draw", card.Id, card.Draw)); }
        return dealt > 0;
    }

    /// <summary>Deals a card's hits (also a parry's hit back). Returns the HP taken off the foe.</summary>
    private int Strike(int side, PetCard card, int damage, int hits, bool pierce, int thorns, List<CardEvent> events, int speedBonus = 0)
    {
        var me = Sides[side];
        var foe = Sides[1 - side];
        int typePct = card.Element.Length == 0 ? 100 : PetGame.TypePercent(card.Element, foe.Loadout.Element);
        if (card.Element.Length > 0 && card.Element == me.Loadout.Element) typePct = typePct * PetGame.SameElementPercent / 100;
        int dealt = 0;
        for (int hit = 0; hit < Math.Max(1, hits) && !Over && damage > 0; hit++)
        {
            int dmg = (PetCards.Scale(damage, me.Loadout.Power) + speedBonus) * typePct / 100;
            dmg = dmg * (90 + rng.Below(21)) / 100;
            if (me.Weak > 0) dmg = dmg * 75 / 100;
            if (me.Rage > 0) dmg = dmg * 130 / 100;
            dmg = Math.Max(1, dmg);
            int absorbed = pierce ? 0 : Math.Min(foe.Block, dmg);
            foe.Block -= absorbed;
            int taken = dmg - absorbed;
            foe.Hp = Math.Max(0, foe.Hp - taken);
            dealt += taken;
            events.Add(new(1 - side, "hit", card.Id, taken, typePct));
            if (absorbed > 0) events.Add(new(1 - side, "blocked", card.Id, absorbed));
            if (taken > 0 && foe.Sleep > 0 && foe.Hp > 0) { foe.Sleep = 0; foe.SleepFresh = false; events.Add(new(1 - side, "wake", card.Id)); }
            if (thorns > 0 && foe.Hp > 0)
            {
                me.Hp = Math.Max(0, me.Hp - thorns);
                events.Add(new(side, "thornhit", card.Id, thorns));
            }
            CheckOver(events);
        }
        return dealt;
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
            sb.Append('|').Append(s.Hp).Append(',').Append(s.Energy).Append(',').Append(s.NextEnergy).Append(',').Append(s.EnergyDrain)
              .Append(',').Append(s.Burn).Append(',').Append(s.Weak).Append(',').Append(s.Poison).Append(',').Append(s.Sleep)
              .Append(',').Append(s.Fear).Append(',').Append(s.Slow).Append(',').Append(s.Regen).Append(',').Append(s.Rage)
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
        bool foeAttacks = foe.Hand.Any(id => PetCards.CardOf(id) is { Damage: > 0 } c && c.Cost <= foe.Energy);
        bool foeSupports = foe.Hand.Any(id => PetCards.CardOf(id) is { Damage: 0, Block: > 0 } or { Damage: 0, Heal: > 0 });
        int Score(string id)
        {
            var c = PetCards.CardOf(id);
            int score = c.Damage * Math.Max(1, c.Hits) * 2 + c.Burn * 2 + c.Weak * 4 + c.Draw * 3 + c.Energy * 4;
            // Counters are a read on what the foe is holding.
            if (c.Parry > 0) score += foeAttacks ? c.Parry * 2 + 6 : 0;
            if (c.Interrupt) score += foeSupports ? 14 : 0;
            if (c.Punish > 0 && foeSupports) score += c.Punish * Math.Max(1, c.Hits);
            if (c.Stun) score += 4;
            score += c.Poison * 3 + c.Sleep * (foe.Sleep > 0 ? 0 : 12) + c.Fear * 6 + c.Slow * 4 + c.Regen * (hurt ? 4 : 2);
            score += c.NextEnergy * 5 + c.StealEnergy * 7 + c.Combo * Math.Max(1, c.Hits);
            if (c.OnHit.Length > 0 && foeAttacks) score += 6;
            if (c.Rage > 0) score += me.Hand.Count(h => PetCards.CardOf(h).Damage > 0) * 3;
            if (c.Feint) score += 2;
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
