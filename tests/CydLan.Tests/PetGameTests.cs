using System.Collections.Concurrent;
using System.Net;
using CydLan.Core;

/// <summary>Companion mini-game: rules, deterministic battles, and a full lockstep battle over real sockets.</summary>
internal static class PetGameTests
{
    private static void Check(bool condition, string description) { if (!condition) throw new Exception(description); }
    private static async Task Until(Func<bool> predicate, int ms = 8000) { using var deadline = new CancellationTokenSource(ms); while (!predicate()) await Task.Delay(10, deadline.Token); }

    public static async Task RunAsync()
    {
        Rules();
        Cards();
        Determinism();
        CardGame();
        await LanBattleAsync();
    }

    private static void Rules()
    {
        var now = new DateTime(2026, 11, 27, 12, 0, 0, DateTimeKind.Utc);
        var p = new PetProgress { FullnessAtUtc = now };
        PetGame.Normalize(p);
        Check(p.Level == 1 && p.Coins == PetGame.StartingCoins && PetGame.FoodCount(p, "berry") == 3, "New pets start at level 1 with coins and berries.");

        int need = PetGame.XpToNext(1);
        Check(PetGame.AddXp(p, need + 5) == 1 && p.Level == 2 && p.Xp == 5, "XP past the threshold levels up and carries over.");
        Check(PetGame.FreePoints(p) == 1, "Level 2 pays the first bonus point.");

        // Hunger drains with time and food refills it.
        Check(PetGame.CurrentFullness(p, now.AddHours(2)) == 70, "Fullness drops one point every 12 minutes.");
        Check(PetGame.Feed(p, "berry", now.AddHours(2), out _) == PetGame.FeedResult.Fed && PetGame.CurrentFullness(p, now.AddHours(2)) == 85 && PetGame.FoodCount(p, "berry") == 2,
            "Feeding uses one food and fills the pet.");
        Check(PetGame.Feed(p, "cake", now, out _) == PetGame.FeedResult.NoFood, "No food, no feeding.");
        p.Fullness = 100; p.FullnessAtUtc = now;
        Check(PetGame.Feed(p, "berry", now, out _) == PetGame.FeedResult.TooFull && PetGame.FoodCount(p, "berry") == 2, "A full pet refuses plain food without using it up.");

        // Shop, boosts and the star candy.
        int coins = p.Coins;
        Check(PetGame.Buy(p, "pepper") && p.Coins == coins - 35, "Buying spends coins.");
        Check(PetGame.Feed(p, "pepper", now, out _) == PetGame.FeedResult.Fed && p.Boost == "power", "Boost foods set a one-battle boost even when full.");
        p.Coins = 10;
        Check(!PetGame.Buy(p, "golden"), "Can't buy what you can't afford.");
        p.Food["candy"] = 1;
        int level = p.Level;
        Check(PetGame.Feed(p, "candy", now, out int gained) == PetGame.FeedResult.Fed && gained == 1 && p.Level == level + 1, "Star candy gives exactly one level.");

        // Daily bonus once per day, chat XP capped.
        var local = new DateTime(2026, 11, 27, 9, 0, 0);
        Check(PetGame.ClaimDaily(p, local) == 60 && PetGame.ClaimDaily(p, local.AddHours(5)) == 0 && PetGame.ClaimDaily(p, local.AddDays(1)) == 60, "Daily bonus is once per day.");
        int xpBefore = p.Xp + p.Level * 100000;
        for (int i = 0; i < 100; i++) PetGame.AddChatXp(p, local);
        Check(p.ChatXpToday == PetGame.ChatXpDailyCap, "Chat XP stops at the daily cap.");

        // Rewards: friends pay more than practice; hunger halves XP; record kept.
        var fed = new PetProgress { Fullness = 90, FullnessAtUtc = now };
        var hungry = new PetProgress { Fullness = 5, FullnessAtUtc = now };
        var friendWin = PetGame.Reward(fed, 1, 5, friend: true, now);
        var practiceWin = PetGame.Reward(new PetProgress { Fullness = 90, FullnessAtUtc = now }, 1, 5, friend: false, now);
        var hungryWin = PetGame.Reward(hungry, 1, 5, friend: true, now);
        Check(friendWin.Xp > practiceWin.Xp && friendWin.Coins > practiceWin.Coins, "Friend battles reward more than practice.");
        Check(hungryWin.Xp == friendWin.Xp / 2, "Hungry pets earn half XP.");
        Check(fed.Wins == 1 && PetGame.CurrentFullness(fed, now) == 90 - PetGame.BattleHunger, "Battles are recorded and make the pet hungrier.");

        // Normalize repairs hand-edited saves.
        var bad = new PetProgress { Level = 5000, Xp = -4, BasePower = 30, Element = "lava", Power = 500, Boost = "god", Food = new() { ["berry"] = 2, ["rock"] = 9 } };
        PetGame.Normalize(bad);
        Check(bad.Level == PetGame.MaxLevel && bad.Xp == 0 && bad.Element == "blaze" && bad.Power == 0 && bad.BasePower == PetGame.EvenStat && bad.Boost == "" && !bad.Food.ContainsKey("rock"),
            "Invalid saved values are clamped.");

    }

    private static void Cards()
    {
        var card = new PetGame.FighterCard { Name = new string('x', 200) + "\u0007", PetCode = "not a pet", Level = 3, Element = "VOLT", Power = 50, Boost = "nuke" };
        var clean = PetGame.Sanitize(card);
        Check(clean.Name.Length == 40 && !clean.Name.Any(char.IsControl), "Names are trimmed and stripped of control characters.");
        Check(clean.PetCode == "" && clean.Element == "volt" && clean.Power == 0 && clean.Boost == "", "Unknown codes, boosts and over-budget points are dropped.");
        var round = PetGame.DecodeCard(PetGame.EncodeCard(new PetGame.FighterCard { Name = "Jamie", Level = 7, Element = "frost", Power = 1, Speed = 2 }));
        Check(round is { Name: "Jamie", Level: 7, Element: "frost", Power: 1, Speed: 2 }, "Cards survive the wire round trip.");
        Check(PetGame.DecodeCard("{oops") is null && PetGame.DecodeCard(new string('a', PetGame.MaxCardChars + 1)) is null, "Broken or oversized cards are rejected.");
        var boosted = PetGame.StatsFor(new PetGame.FighterCard { Level = 5, Boost = "power" });
        var plain = PetGame.StatsFor(new PetGame.FighterCard { Level = 5 });
        var starving = PetGame.StatsFor(new PetGame.FighterCard { Level = 5, Hungry = true });
        Check(boosted.Attack > plain.Attack && starving.Attack < plain.Attack, "Boosts help and hunger hurts.");
    }

    private static void Determinism()
    {
        var rnd = new Random(7);
        for (int game = 0; game < 300; game++)
        {
            var a = PetGame.WildCard(rnd.Next(1, 30), rnd, "A", "");
            var b = PetGame.WildCard(rnd.Next(1, 30), rnd, "B", "");
            ulong seed = (ulong)rnd.NextInt64();
            var one = new PetBattle(a, b, seed);
            var two = new PetBattle(PetGame.DecodeCard(PetGame.EncodeCard(a))!, PetGame.DecodeCard(PetGame.EncodeCard(b))!, seed);
            var picks = new Random(game);
            while (!one.Over)
            {
                Check(one.Hash() == two.Hash(), "Both peers hold the same state before every turn.");
                string[] all = ["strike", "special", "guard", "heal", "ultimate", "cheat"];
                string m0 = all[picks.Next(all.Length)], m1 = all[picks.Next(all.Length)];
                var e1 = one.Resolve(m0, m1);
                var e2 = two.Resolve(m0, m1);
                Check(e1.SequenceEqual(e2), "Both peers see the same events.");
                Check(one.Turn <= PetGame.MaxTurns, "Battles always end.");
                foreach (var f in one.Fighters) Check(f.Hp >= 0 && f.Hp <= f.Stats.MaxHp && f.Charge is >= 0 and <= PetGame.MaxCharge, "HP and charge stay in range.");
            }
            Check(two.Over && one.Winner == two.Winner && one.Hash() == two.Hash(), "Both peers agree on the winner.");
            Check(one.Winner == -1 || one.Fighters[1 - one.Winner].Hp == 0 || one.Turn == PetGame.MaxTurns, "The loser is out of HP unless time ran out.");
        }
        // Balance: equal pets trade blows for a while; nobody is one-shot.
        var ai = new Random(11);
        int totalTurns = 0, games = 0, biggestHitPct = 0;
        foreach (int level in new[] { 1, 6, 15, 30 })
            for (int i = 0; i < 60; i++)
            {
                var x = PetGame.WildCard(level, ai, "X", ""); var y = PetGame.WildCard(level, ai, "Y", "");
                x.Level = y.Level = level;
                var fight = new PetBattle(PetGame.Sanitize(x), PetGame.Sanitize(y), (ulong)ai.NextInt64());
                while (!fight.Over)
                    foreach (var ev in fight.Resolve(fight.ChooseAi(0, ai), fight.ChooseAi(1, ai)))
                        if (ev.Kind == "hit") biggestHitPct = Math.Max(biggestHitPct, ev.Amount * 100 / fight.Fighters[1 - ev.Actor].Stats.MaxHp);
                totalTurns += fight.Turn; games++;
            }
        double average = totalTurns / (double)games;
        Console.WriteLine($"      balance: {average:0.0} turns on average, biggest hit {biggestHitPct}% of max HP");
        Check(average is >= 5 and <= 18, $"Equal battles last a handful of turns ({average:0.0}).");
        Check(biggestHitPct < 75, "No single hit takes most of a pet's HP.");

        // Moves that aren't ready fall back to a strike.
        var fresh = new PetBattle(new PetGame.FighterCard { Level = 1 }, new PetGame.FighterCard { Level = 1 }, 1);
        Check(fresh.Sanitize(0, "heal") == "strike" && fresh.Sanitize(0, "ultimate") == "strike" && fresh.Sanitize(0, "special") == "special", "Locked moves are refused.");
    }

    private static void CardGame()
    {
        // The catalog: unique ids, art for every card, 20 per element, all four rarities in each element.
        Check(PetCards.Cards.Select(c => c.Id).Distinct().Count() == PetCards.Cards.Count, "Card ids are unique.");
        Check(PetCards.Cards.All(c => c.ArtCell.Index is >= 0 and < 48 && c.ArtCell.Sheet.Length > 0), "Every card has an icon.");
        Check(PetCards.Cards.Select(c => c.Art).Distinct().Count() == PetCards.Cards.Count, "No two cards share an icon.");
        foreach (var e in PetGame.Wheel)
        {
            var mine = PetCards.Cards.Where(c => c.Element == e).ToList();
            Check(mine.Count == 25 && PetCards.CardRarities.All(r => mine.Any(c => c.Rarity == r)), $"{e} has 25 cards across every rarity.");
            Check(mine.Any(c => c.Parry > 0) && mine.Any(c => c.Interrupt) && mine.Any(c => c.Stun || c.Feint || c.Punish > 0), $"{e} has parry, interrupt and counter-attack cards.");
        }
        Check(PetCards.CardOf("hoodup").Id == "strike" && PetCards.CardOf("thornhide").Text == "Block 10 · Thorns 8", "Unknown ids fall back; text is built from numbers.");

        // The element wheel: every element beats two, loses to two, and is even with two.
        foreach (var e in PetGame.Wheel)
        {
            Check(PetGame.BeatenBy(e).Count() == 2 && PetGame.WeakTo(e).Count() == 2 && !PetGame.Beats(e, e), $"{e} has two counters and two prey.");
            foreach (var prey in PetGame.BeatenBy(e)) Check(!PetGame.Beats(prey, e), "No element beats its own counter.");
            foreach (var counter in PetGame.WeakTo(e)) Check(PetGame.WeakTo(counter).Any(), "Every counter has its own counter.");
        }
        Check(PetGame.TypePercent("tide", "blaze") == PetGame.AdvantagePercent && PetGame.TypePercent("blaze", "tide") == PetGame.DisadvantagePercent
            && PetGame.TypePercent("tide", "light") == 100, "The wheel sets the damage multiplier.");

        // Account level: up to 999, bonus points stop at 15.
        Check(PetGame.PointBudget(1) == 0 && PetGame.PointBudget(2) == 1 && PetGame.PointBudget(PetGame.MaxLevel) == PetGame.MaxBonusPoints
            && PetGame.BonusLevels.Count == PetGame.MaxBonusPoints, "Levels pay at most 15 bonus points, the last at level 999.");
        var lvl = new PetProgress { Level = PetGame.MaxLevel - 1 };
        PetGame.AddXp(lvl, 1_000_000);
        Check(lvl.Level == PetGame.MaxLevel && PetGame.FreePoints(lvl) == PetGame.MaxBonusPoints, "Level 999 is the cap.");

        // Base stats start even; re-rolling costs coins, keeps the total and leaves bonus points alone.
        var rnd = new Random(5);
        var p = new PetProgress { Level = 10, Power = 2, Coins = PetGame.RerollPrice * 40 };
        PetGame.Normalize(p);
        Check(p.BasePower == PetGame.EvenStat && PetGame.ValidBase(p.BasePower, p.BaseGuard, p.BaseSpeed, p.BaseVitality), "Every pet starts with even stats.");
        var spreads = new HashSet<(int, int, int, int)>();
        var elements = new HashSet<string>();
        for (int i = 0; i < 40; i++)
        {
            Check(PetGame.Reroll(p, rnd) && PetGame.ValidBase(p.BasePower, p.BaseGuard, p.BaseSpeed, p.BaseVitality) && p.Power == 2 && PetGame.Wheel.Contains(p.Element),
                "Re-rolls stay valid and keep bonus points.");
            spreads.Add((p.BasePower, p.BaseGuard, p.BaseSpeed, p.BaseVitality));
            elements.Add(p.Element);
        }
        Check(spreads.Count > 20 && elements.Count == PetGame.Wheel.Count && p.Coins == 0 && !PetGame.Reroll(p, rnd),
            "Re-rolls vary the element and stats, cost coins, and stop when broke.");
        var installs = Enumerable.Range(0, 60).Select(i => { var n = new PetProgress(); PetCards.EnsureStarted(n, new Random(i)); return n.Element; }).Distinct().Count();
        Check(installs >= 5, "New installs start with a random element.");
        var looks = new PetProgress { BasePower = 9, BaseGuard = 1, BaseSpeed = 5, BaseVitality = 5, Power = 3 };
        var lo = PetCards.LoadoutFor(looks, "A", "");
        Check(lo.Power == 12 && lo.Guard == 1 && lo.MaxHp == PetCards.MaxHpFor(5), "Stats are base plus bonus; level adds nothing.");
        Check(PetCards.LoadoutFor(new PetProgress { Level = 900 }, "A", "").Power == PetCards.LoadoutFor(new PetProgress { Level = 1 }, "A", "").Power,
            "A high level alone doesn't make a pet stronger.");

        // Starter cards: random per install, fixed per seed, always a legal 20-card deck.
        var s1 = PetCards.Starter(11); var s2 = PetCards.Starter(11); var s3 = PetCards.Starter(12);
        Check(s1.SequenceEqual(s2) && !s1.SequenceEqual(s3), "The starter set is random per install and stable for one.");
        for (long seed = 1; seed < 60; seed++)
        {
            var st = PetCards.Starter(seed);
            Check(st.Count == PetCards.DeckSize && st.GroupBy(c => c).All(g => g.Count() <= PetCards.CopyLimit(PetCards.CardOf(g.Key)))
                && st.Count(c => PetCards.CardOf(c).Damage > 0) >= 9, "Starters are legal decks with enough attacks.");
        }
        var fresh = new PetProgress();
        PetCards.EnsureStarted(fresh, rnd);
        Check(fresh.Seed != 0 && fresh.Deck.Count == PetCards.DeckSize && PetCards.DeckReady(fresh) && fresh.Cards.Values.Sum() == PetCards.DeckSize,
            "A new install owns its starter cards and has them as its deck.");

        // Deck building respects ownership and copy limits.
        string any = fresh.Deck[0];
        Check(!PetCards.AddToDeck(fresh, any), "A full deck takes no more cards.");
        Check(PetCards.RemoveFromDeck(fresh, any) && !PetCards.DeckReady(fresh) && PetCards.AddToDeck(fresh, any) && PetCards.DeckReady(fresh), "Cards come out and go back in.");
        PetCards.RemoveFromDeck(fresh, any);
        Check(!PetCards.AddToDeck(fresh, "hellfire"), "Unowned cards can't go in the deck.");
        PetCards.AddCard(fresh, "hellfire"); PetCards.AddCard(fresh, "hellfire");
        Check(PetCards.AddToDeck(fresh, "hellfire") && PetCards.InDeck(fresh, "hellfire") == 1, "Owned cards go in.");
        PetCards.RemoveFromDeck(fresh, fresh.Deck.First(c => c != "hellfire"));
        Check(!PetCards.AddToDeck(fresh, "hellfire"), "Legendaries are one per deck.");
        fresh.Deck.Add("tsunami"); fresh.Deck.Add("tsunami");
        PetCards.Normalize(fresh);
        Check(!fresh.Deck.Contains("tsunami"), "A hand-edited deck loses cards it doesn't own.");

        // Some cards never drop for an install; the shop never offers them; the shop is fixed for the day.
        var shopper = new PetProgress { Seed = 77, Coins = 100_000, Level = 50 };
        PetCards.EnsureStarted(shopper, rnd);
        int never = PetCards.Cards.Count(c => PetCards.NeverDrops(shopper, c.Id));
        int rarePlus = PetCards.Cards.Count(c => c.Rarity != PetCards.Common);
        Check(never > rarePlus / 10 && never < rarePlus * 3 / 10 && PetCards.Cards.Where(c => c.Rarity == PetCards.Common).All(c => !PetCards.NeverDrops(shopper, c.Id)),
            $"About a fifth of rare-and-up cards are never obtainable for an install ({never} of {rarePlus}); commons always are.");
        Check(PetCards.Cards.Count(c => PetCards.NeverDrops(new PetProgress { Seed = 78 }, c.Id)) > 0
            && PetCards.Cards.Any(c => PetCards.NeverDrops(shopper, c.Id) != PetCards.NeverDrops(new PetProgress { Seed = 78 }, c.Id)), "Installs miss different cards.");
        for (int d = 1; d <= 60; d++)
        {
            string day = $"2026-12-{d % 28 + 1:00}";
            var offers = PetCards.Shop(shopper, day);
            Check(offers.Count == PetCards.ShopSlots && offers.Select(o => o.Card.Id).Distinct().Count() == offers.Count, "Six different offers a day.");
            Check(offers.All(o => !PetCards.NeverDrops(shopper, o.Card.Id)), "The shop never sells a card this install can't get.");
            Check(PetCards.Shop(shopper, day).Select(o => o.Card.Id).SequenceEqual(offers.Select(o => o.Card.Id)), "The shop is the same all day.");
        }
        var today = PetCards.Shop(shopper, "2026-12-01");
        int before = shopper.Coins, owned = PetCards.Owned(shopper, today[0].Card.Id);
        Check(PetCards.BuyFromShop(shopper, "2026-12-01", 0) && shopper.Coins == before - today[0].Price && PetCards.Owned(shopper, today[0].Card.Id) == owned + 1,
            "Buying spends coins and adds the card.");
        Check(!PetCards.BuyFromShop(shopper, "2026-12-01", 0) && PetCards.Shop(shopper, "2026-12-01")[0].Bought, "Each offer sells once a day.");
        Check(!PetCards.BuyFromShop(new PetProgress { Seed = 77, Coins = 0 }, "2026-12-01", 1), "No coins, no card.");
        for (int i = 0; i < 300; i++)
            if (PetCards.RollDrop(shopper, rnd, 30, true, true) is { } drop) Check(!PetCards.NeverDrops(shopper, drop.Id), "Drops never give a card this install can't get.");

        // The world: stages are random per install, fixed per seed, three fights with a boss last, enemies within the same caps.
        var w1 = PetWorld.StageOf(5, 3); var w2 = PetWorld.StageOf(5, 3);
        Check(w1.Name == w2.Name && w1.Fights.Select(f => f.Deck.Count).SequenceEqual(w2.Fights.Select(f => f.Deck.Count)) && w1.Fights[0].Deck.SequenceEqual(w2.Fights[0].Deck),
            "A stage is the same every time it is retried.");
        Check(Enumerable.Range(1, 30).Select(n => PetWorld.StageOf(5, n).Name).Distinct().Count() > 10, "Stages vary.");
        foreach (int n in new[] { 1, 2, 10, 50, 400, PetWorld.MaxStage })
        {
            var st = PetWorld.StageOf(9, n);
            Check(st.Fights.Count == PetWorld.FightsPerStage && st.Fights[^1].Boss && st.Fights.Count(f => f.Boss) == 1, "Three fights, the last one a boss.");
            foreach (var f in st.Fights)
            {
                int total = f.Power + f.Guard + f.Speed + f.Vitality;
                Check(total <= PetGame.BaseTotal + PetGame.MaxBonusPoints && f.Deck.Count == PetCards.DeckSize
                    && f.Deck.All(c => PetCards.RarityRank(PetCards.CardOf(c).Rarity) <= PetWorld.RarityCap(n, f.Boss)), $"Stage {n} enemies follow the player caps.");
            }
        }
        var walker = new PetProgress();
        Check(!PetWorld.Advance(walker, false) && walker.StageFight == 0, "A loss stays on the same fight.");
        Check(!PetWorld.Advance(walker, true) && !PetWorld.Advance(walker, true) && walker.StageFight == 2, "Wins move through the stage.");
        Check(PetWorld.Advance(walker, true) && walker.Stage == 2 && walker.StageFight == 0, "Beating the boss opens the next stage.");
        var loser = new PetProgress { FullnessAtUtc = DateTime.UtcNow, Fullness = 90 };
        var lossReward = PetGame.Reward(loser, -1, 4, friend: true, DateTime.UtcNow);
        Check(lossReward.Xp > 0 && lossReward.Coins > 0, "Losing in the world still pays XP and coins.");
        var bossWin = PetGame.Reward(new PetProgress { Fullness = 90, FullnessAtUtc = DateTime.UtcNow }, 1, 4, friend: true, DateTime.UtcNow, boss: true);
        var plainWin = PetGame.Reward(new PetProgress { Fullness = 90, FullnessAtUtc = DateTime.UtcNow }, 1, 4, friend: true, DateTime.UtcNow);
        Check(bossWin.Coins == plainWin.Coins * 2, "Bosses pay double.");

        // Deterministic rounds; valid picks only; battles end; balance.
        int totalRounds = 0, games = 0;
        for (int game = 0; game < 200; game++)
        {
            int n = 1 + game % 40;
            var stA = PetWorld.StageOf(game, n); var stB = PetWorld.StageOf(game + 1000, n);
            var a = PetWorld.LoadoutOf(stA, stA.Fights[game % 3], "A", ""); var b = PetWorld.LoadoutOf(stB, stB.Fights[(game + 1) % 3], "B", "");
            ulong seed = (ulong)rnd.NextInt64();
            var one = new CardBattle(a, b, seed); var two = new CardBattle(a, b, seed);
            var ai = new Random(game);
            while (!one.Over)
            {
                Check(one.Hash() == two.Hash(), "Card battles stay identical for the same choices.");
                var p0 = one.ChooseAi(0, ai); var p1 = one.ChooseAi(1, ai);
                int cost = p0.Sum(i => PetCards.CardOf(one.Sides[0].Hand[i]).Cost);
                Check(cost <= one.Sides[0].Energy, "The AI never overspends.");
                var e1 = one.PlayRound(p0, p1); var e2 = two.PlayRound(p0, p1);
                Check(e1.SequenceEqual(e2), "Both copies see the same events.");
                foreach (var side in one.Sides) Check(side.Hp >= 0 && side.Hp <= side.Loadout.MaxHp && side.Energy is >= 0 and <= PetCards.MaxEnergy && side.Hand.Count <= PetCards.MaxHand, "Values stay in range.");
                Check(one.Round <= PetCards.MaxRounds, "Card battles end.");
            }
            totalRounds += one.Round; games++;
        }
        double avg = totalRounds / (double)games;
        Console.WriteLine($"      card balance: {avg:0.0} rounds on average");
        Check(avg is >= 4 and <= 14, $"Card battles last a few rounds ({avg:0.0}).");

        // Counters matter more than levels: a countered element loses most fights against equal stats.
        int counterWins = 0;
        for (int game = 0; game < 200; game++)
        {
            var deckOf = (string e) => PetCards.Cards.Where(c => c.Element == e && c.Rarity is PetCards.Common or PetCards.Rare).SelectMany(c => new[] { c.Id, c.Id }).Take(PetCards.DeckSize).ToList();
            var tide = new PetCards.Loadout("T", "", 1, "tide", PetCards.MaxHpFor(5), 5, 5, 5, deckOf("tide"));
            var blaze = new PetCards.Loadout("B", "", 999, "blaze", PetCards.MaxHpFor(5), 5, 5, 5, deckOf("blaze"));
            var fight = new CardBattle(tide, blaze, (ulong)rnd.NextInt64());
            var ai = new Random(game);
            while (!fight.Over) fight.PlayRound(fight.ChooseAi(0, ai), fight.ChooseAi(1, ai));
            if (fight.Winner == 0) counterWins++;
        }
        Console.WriteLine($"      counter wins: {counterWins}/200");
        Check(counterWins > 110, $"The counter element wins most even fights ({counterWins}/200).");

        // Cheating picks are ignored: cards not in hand, duplicates, more than the energy allows.
        PetCards.Loadout Plain(string name, string element) => new(name, "", 1, element, PetCards.MaxHpFor(5), 0, 0, 0, PetCards.Starter(3));
        var t = new CardBattle(Plain("X", "blaze"), Plain("Y", "leaf"), 9);
        var picks = t.Validate(0, [0, 0, 1, 2, 3, 4, 99, -1]);
        Check(picks.Distinct().Count() == picks.Count && picks.Sum(i => PetCards.CardOf(t.Sides[0].Hand[i]).Cost) <= PetCards.StartEnergy, "Invalid picks are dropped.");

        // Thorns hit back, block absorbs, pierce ignores block.
        var thorny = new CardBattle(Plain("T", "leaf"), Plain("S", "shadow"), 3);
        thorny.Sides[0].Hand.Clear(); thorny.Sides[0].Hand.AddRange(["barkskin"]);
        thorny.Sides[1].Hand.Clear(); thorny.Sides[1].Hand.AddRange(["strike", "sneak"]);
        int hp1 = thorny.Sides[1].Hp;
        var ev = thorny.PlayRound([0], [0, 1]);
        Check(ev.Any(e => e.Kind == "blocked" && e.Actor == 0) && ev.Count(e => e.Kind == "thornhit" && e.Actor == 1) == 2 && thorny.Sides[1].Hp == hp1 - 4, "Block absorbs and thorns hit back each time.");
        Check(ev.Any(e => e.Kind == "hit" && e.Card == "sneak" && e.Amount > 0), "Pierce goes through block.");

        // Counters: parry beats an attack, feint beats parry, interrupt beats defence and parry, punish beats block, stun beats slow attacks.
        CardBattle Duel(string[] mine, string[] theirs, int mySpeed = 0, int theirSpeed = 0)
        {
            var b = new CardBattle(Plain("A", "leaf") with { Speed = mySpeed }, Plain("B", "leaf") with { Speed = theirSpeed }, 21);
            b.Sides[0].Hand.Clear(); b.Sides[0].Hand.AddRange(mine);
            b.Sides[1].Hand.Clear(); b.Sides[1].Hand.AddRange(theirs);
            b.Sides[0].Energy = b.Sides[1].Energy = PetCards.MaxEnergy;
            return b;
        }
        var parry = Duel(["parry"], ["strike"]);
        int pa = parry.Sides[0].Hp;
        ev = parry.PlayRound([0], [0]);
        Check(ev.Any(e => e.Kind == "parried" && e.Actor == 1) && parry.Sides[0].Hp == pa && ev.Any(e => e.Kind == "hit" && e.Actor == 1 && e.Card == "parry"),
            "A parry cancels the attack and hits back.");
        var feint = Duel(["parry"], ["feint"]);
        ev = feint.PlayRound([0], [0]);
        Check(!ev.Any(e => e.Kind == "parried") && ev.Any(e => e.Kind == "hit" && e.Actor == 0 && e.Card == "feint"), "A feint can't be parried.");
        var interrupt = Duel(["guard", "parry"], ["disrupt", "strike"]);
        ev = interrupt.PlayRound([0, 1], [0, 1]);
        Check(ev.Count(e => e.Kind == "interrupted" && e.Actor == 0) == 2 && !ev.Any(e => e.Kind == "block") && !ev.Any(e => e.Kind == "parried"),
            "An interrupt cancels the foe's block and parry.");
        var both = Duel(["disrupt"], ["disrupt"]);
        ev = both.PlayRound([0], [0]);
        Check(!ev.Any(e => e.Kind == "interrupted"), "Interrupts never cancel each other.");
        var punish = Duel(["guard"], ["shieldbash"]);
        ev = punish.PlayRound([0], [0]);
        var plain = Duel([], ["shieldbash"]);
        var ev2 = plain.PlayRound([], [0]);
        Check(ev.Any(e => e.Kind == "punish" && e.Actor == 0) && !ev2.Any(e => e.Kind == "punish"), "Punish only adds damage against a guarding foe.");
        var stun = Duel(["shieldbash"], ["strike", "jab"], mySpeed: 9, theirSpeed: 1);
        int sb = stun.Sides[0].Hp;
        ev = stun.PlayRound([0], [0, 1]);
        Check(ev.Any(e => e.Kind == "stun" && e.Actor == 1) && ev.Count(e => e.Kind == "fizzle" && e.Actor == 1) == 2 && stun.Sides[0].Hp == sb,
            "A faster stun stops the foe's attacks this round.");
        var slow = Duel(["shieldbash"], ["strike"], mySpeed: 1, theirSpeed: 9);
        ev = slow.PlayRound([0], [0]);
        Check(!ev.Any(e => e.Kind == "fizzle"), "A slower stun is too late.");

        // Statuses: poison ticks and fades, sleep skips attacks until a hit wakes it, fear misses, slow goes last, regen heals, rage hits harder, cleanse clears.
        var pois = Duel(["toxicspores"], []);
        int ph = pois.Sides[1].Hp;
        ev = pois.PlayRound([0], []);
        Check(ev.Any(e => e.Kind == "poison" && e.Actor == 1 && e.Amount == 4) && pois.Sides[1].Hp == ph - 4 && pois.Sides[1].Poison == 3, "Poison ticks at the end of the round and fades.");
        var nap = Duel(["lullaby"], ["strike"]);
        ev = nap.PlayRound([0], [0]);
        Check(ev.Any(e => e.Kind == "snooze" && e.Actor == 1) && nap.Sides[1].Sleep == 1, "A sleeping pet's attacks do nothing, and the sleep lasts into the next round.");
        nap.Sides[0].Hand.Clear(); nap.Sides[0].Hand.Add("strike");
        nap.Sides[1].Hand.Clear(); nap.Sides[1].Hand.Add("strike");
        ev = nap.PlayRound([0], [0]);
        Check(ev.Any(e => e.Kind == "snooze") || ev.Any(e => e.Kind == "wake"), "Sleep holds the next round unless a hit lands first.");
        var wake = Duel(["sleepdart", "strike"], ["guard"], mySpeed: 9);
        ev = wake.PlayRound([0, 1], [0]);
        Check(ev.Any(e => e.Kind == "sleep" && e.Actor == 1), "A sleep dart puts the foe to sleep.");
        var fear = Duel(["blinding"], ["strike", "jab", "jab"]);
        ev = fear.PlayRound([0], [0, 1, 2]);
        Check(ev.Count(e => e.Kind == "miss" && e.Actor == 1) == 2 && ev.Count(e => e.Kind == "hit" && e.Actor == 0) == 1, "Fear makes the next attacks miss.");
        var slowed = Duel(["frostbind", "strike"], ["strike"], mySpeed: 1, theirSpeed: 9);
        ev = slowed.PlayRound([0, 1], [0]);
        int firstHit = ev.FindIndex(e => e.Kind == "hit");
        Check(firstHit >= 0 && ev[firstHit].Actor == 1, "A slowed pet acts after a slower foe.");
        var regen = Duel(["renewal"], ["strike"]);
        ev = regen.PlayRound([0], [0]);
        Check(ev.Any(e => e.Kind == "heal" && e.Actor == 0 && e.Card == "") && regen.Sides[0].Regen == 3, "Regen heals at the end of the round.");
        int Hit(string[] mine) { var d = Duel(mine, []); d.PlayRound(Enumerable.Range(0, mine.Length), []); return d.Sides[1].Loadout.MaxHp - d.Sides[1].Hp; }
        Check(Hit(["ragepill", "whirlwind"]) > Hit(["whirlwind"]), "Rage hits harder.");
        var clean = Duel(["rinse"], []);
        clean.Sides[0].Poison = 5; clean.Sides[0].Fear = 2; clean.Sides[0].Slow = 2;
        clean.PlayRound([0], []);
        Check(clean.Sides[0].Poison == 0 && clean.Sides[0].Fear == 0 && clean.Sides[0].Slow == 0, "Cleanse clears poison, fear and slow.");

        // Axie-style tricks: next-round energy, on-hit reactive traps, energy steal, combo bonus.
        var battery = Duel(["lantern"], []);
        battery.Sides[0].Energy = 1;
        ev = battery.PlayRound([0], []);
        Check(ev.Any(e => e.Kind == "nextenergy" && e.Actor == 0 && e.Amount == 1) && battery.Sides[0].Energy == PetCards.EnergyPerRound + 1,
            "Next-round energy cards add bonus energy to the next refill.");

        var trap = Duel(["leafshield"], ["strike"]);
        ev = trap.PlayRound([0], [0]);
        Check(ev.Any(e => e.Kind == "trap" && e.Actor == 0) && ev.Any(e => e.Kind == "poisoned" && e.Actor == 1 && e.Amount == 2),
            "An on-hit trap afflicts the attacker when struck.");

        var carrot = Duel(["bubblewrap"], ["strike"]);
        carrot.Sides[0].Energy = 1;
        ev = carrot.PlayRound([0], [0]);
        Check(ev.Any(e => e.Kind == "nextenergy" && e.Actor == 0) && carrot.Sides[0].Energy == PetCards.EnergyPerRound + 1,
            "An on-hit energy trap pays bonus energy next round when hit.");

        var thief = Duel(["sap"], []);
        thief.Sides[0].Energy = 2; thief.Sides[1].Energy = 1;
        ev = thief.PlayRound([0], []);
        Check(ev.Any(e => e.Kind == "stealenergy" && e.Actor == 0) && thief.Sides[0].Energy == PetCards.EnergyPerRound + 1 && thief.Sides[1].Energy == PetCards.EnergyPerRound,
            "Energy steal drains the foe and adds energy next round.");

        var comboDuel = Duel(["focus", "sparkleshot"], []);
        ev = comboDuel.PlayRound([0, 1], []);
        Check(ev.Any(e => e.Kind == "combo" && e.Actor == 0 && e.Amount == 3), "Playing 2+ cards triggers combo bonus.");

        // Every skill scales with its matching stat: Power -> Damage/Burn/Poison, Guard -> Block/Thorns/Traps, Vitality -> Heal/Regen, Speed -> Combo/Punish.
        var powerBattle = new CardBattle(Plain("P", "blaze") with { Power = 15 }, Plain("T", "leaf"), 11);
        powerBattle.Sides[0].Hand.Clear(); powerBattle.Sides[0].Hand.AddRange(["smolder", "toxicspores"]);
        powerBattle.Sides[0].Energy = PetCards.MaxEnergy;
        ev = powerBattle.PlayRound([0, 1], []);
        Check(ev.First(e => e.Kind == "burned").Amount == PetCards.Scale(5, 15) && ev.First(e => e.Kind == "poisoned").Amount == PetCards.Scale(4, 15),
            "Power scales Burn and Poison stacks.");

        var guardBattle = new CardBattle(Plain("G", "leaf") with { Guard = 15 }, Plain("A", "leaf"), 11);
        guardBattle.Sides[0].Hand.Clear(); guardBattle.Sides[0].Hand.AddRange(["bramble"]);
        guardBattle.Sides[1].Hand.Clear(); guardBattle.Sides[1].Hand.AddRange(["strike"]);
        guardBattle.Sides[0].Energy = guardBattle.Sides[1].Energy = PetCards.MaxEnergy;
        ev = guardBattle.PlayRound([0], [0]);
        Check(ev.First(e => e.Kind == "block").Amount == PetCards.Scale(9, 15) && ev.First(e => e.Kind == "thorns").Amount == PetCards.Scale(5, 15)
            && ev.First(e => e.Kind == "poisoned").Amount == PetCards.Scale(3, 15), "Guard scales Block, Thorns and reactive trap stacks.");

        var vitBattle = new CardBattle(Plain("V", "light") with { Vitality = 15, MaxHp = PetCards.MaxHpFor(15) }, Plain("A", "leaf"), 11);
        vitBattle.Sides[0].Hp = 30;
        vitBattle.Sides[0].Hand.Clear(); vitBattle.Sides[0].Hand.AddRange(["prayer", "renewal"]);
        vitBattle.Sides[0].Energy = PetCards.MaxEnergy;
        ev = vitBattle.PlayRound([0, 1], []);
        Check(ev.First(e => e.Kind == "heal" && e.Card == "prayer").Amount == PetCards.Scale(5, 15)
            && ev.First(e => e.Kind == "regen").Amount == PetCards.Scale(4, 15), "Vitality scales Heal and Regen.");

        var speedBattle = new CardBattle(Plain("S", "light") with { Speed = 15 }, Plain("G", "leaf"), 11);
        speedBattle.Sides[0].Hand.Clear(); speedBattle.Sides[0].Hand.AddRange(["focus", "sparkleshot", "shieldbash"]);
        speedBattle.Sides[1].Hand.Clear(); speedBattle.Sides[1].Hand.AddRange(["guard"]);
        speedBattle.Sides[0].Energy = speedBattle.Sides[1].Energy = PetCards.MaxEnergy;
        ev = speedBattle.PlayRound([0, 1, 2], [0]);
        Check(ev.First(e => e.Kind == "combo").Amount == PetCards.Scale(3, 15)
            && ev.First(e => e.Kind == "punish").Amount == PetCards.Scale(4, 15), "Speed scales Combo and Punish bonuses.");

        // Loadout wire encoding round-trips and clamps forged values.
        var encoded = PetCards.EncodeLoadout(PetCards.LoadoutFor(fresh, "Alex", "pet1:cat"));
        var decoded = PetCards.DecodeLoadout(encoded);
        Check(decoded is { Name: "Alex", Deck.Count: PetCards.DeckSize } && decoded.Vitality == PetCards.TotalVitality(fresh), "Card duel loadouts survive wire encoding.");
        var forged = PetCards.SanitizeLoadout(new("Cheater", "bad", 9999, "lava", 9999, 99, 99, 99, ["hellfire", "hellfire", "hellfire"], 99));
        Check(forged.Level == PetGame.MaxLevel && forged.Power == PetGame.EvenStat && forged.Deck.Count == PetCards.DeckSize && forged.Deck.Count(c => c == "hellfire") == 1,
            "Forged duel loadouts are clamped to legal stats and copy limits.");

        // Selling cards: requires strictly more than 20 cards owned, pays half price, keeps deck legal.
        var seller = new PetProgress { Seed = 19, Coins = 0 };
        PetCards.EnsureStarted(seller, rnd);
        Check(PetCards.TotalOwned(seller) == PetCards.DeckSize && !PetCards.CanSell(seller, seller.Deck[0]) && PetCards.SellCard(seller, seller.Deck[0]) == 0,
            "Cannot sell cards when at the 20-card minimum.");
        PetCards.AddCard(seller, "hellfire");
        Check(PetCards.TotalOwned(seller) == PetCards.DeckSize + 1 && PetCards.CanSell(seller, "hellfire"), "Can sell once above 20 cards.");
        int earned = PetCards.SellCard(seller, "hellfire");
        Check(earned == PetCards.SellPrice(PetCards.CardOf("hellfire")) && seller.Coins == earned && PetCards.Owned(seller, "hellfire") == 0 && PetCards.TotalOwned(seller) == PetCards.DeckSize,
            "Selling removes one card, pays half price, and stops at 20 cards.");

        // Trading cards: 1-for-1 swap works even at 20 cards and keeps deck legal.
        string giveFromDeck = seller.Deck[0];
        Check(PetCards.TradeCards(seller, giveFromDeck, "hellfire") && PetCards.Owned(seller, "hellfire") == 1 && PetCards.TotalOwned(seller) == PetCards.DeckSize,
            "Trading swaps one owned card for another while keeping collection size at 20.");

        // Spin raffle: spends SpinPrice coins and awards a card from the catalog.
        var spinner = new PetProgress { Seed = 23, Coins = PetCards.SpinPrice * 5 };
        PetCards.EnsureStarted(spinner, rnd);
        for (int i = 0; i < 5; i++)
        {
            var spin = PetCards.SpinRaffle(spinner, rnd);
            Check(spin is not null && PetCards.IsCard(spin.Card.Id), "Spin raffle awards a valid card.");
        }
        Check(spinner.Coins < PetCards.SpinPrice && PetCards.SpinRaffle(spinner, rnd) is null, "Cannot spin without enough coins.");

        // Balancing: OP cards cost 4 energy and heals are nerfed.
        Check(PetCards.CardOf("solarflare").Cost == 4 && PetCards.CardOf("hellfire").Cost == 4 && PetCards.CardOf("skyfall").Cost == 4
            && PetCards.CardOf("combo").Cost == 2 && PetCards.CardOf("vampire").Cost == 3 && PetCards.CardOf("archangel").Heal == 17,
            "Strong cards cost more energy and heals are toned down.");

        // Local tournament: depends on user's local time slot, scales fairly to player bonus, crowns champion on round 3.
        var tProg = new PetProgress { Seed = 31, Level = 4, Power = 1, Guard = 1 };
        PetCards.EnsureStarted(tProg, rnd);
        var tMorning = new DateTime(2026, 11, 28, 9, 15, 0, DateTimeKind.Local);
        var tEvening = new DateTime(2026, 11, 28, 19, 15, 0, DateTimeKind.Local);
        var evMorning = PetTournament.EventFor(tProg, tMorning);
        var evEvening = PetTournament.EventFor(tProg, tEvening);
        Check(evMorning.Slot != evEvening.Slot && evMorning.Bracket.Count == PetTournament.RoundsPerTournament
            && evMorning.Bracket.All(c => c.Power + c.Guard + c.Speed + c.Vitality <= PetGame.BaseTotal + PetGame.SpentPoints(tProg) + 1),
            "Local tournament depends on local time and keeps enemy stats fair.");
        PetTournament.Advance(tProg, evMorning.Slot, true, out bool c0, out _);
        Check(!c0 && tProg.TournamentRound == 1, "Round 1 win advances to Semifinal.");
        PetTournament.Advance(tProg, evMorning.Slot, true, out bool c1, out _);
        Check(!c1 && tProg.TournamentRound == 2, "Round 2 win advances to Final.");
        PetTournament.Advance(tProg, evMorning.Slot, true, out bool c2, out bool first2);
        Check(c2 && first2 && tProg.TournamentRound == 0 && tProg.TournamentWins == 1 && tProg.TournamentWonSlot == evMorning.Slot,
            "Winning the Final crowns the champion and records the slot.");
        var champReward = PetTournament.Reward(tProg, 1, 2, champion: true, firstClear: true, DateTime.UtcNow);
        Check(champReward.Coins > 70 && champReward.Xp > 45 && PetTournament.RollChampionCard(tProg, rnd, true) is not null,
            "Tournament champion pays bonus coins, XP and a card.");
    }

    private static AppSettings Settings(string name) => new() { DisplayName = name, Port = 0, UserId = AppSettings.NewUserId() };

    private static async Task LanBattleAsync()
    {
        Check(LanService.SupportsPetBattles(LanService.DisplayVersion(LanService.WireVersion)), "This build advertises battles.");
        Check(!LanService.SupportsPetBattles("CydLan 0.1") && !LanService.SupportsPetBattles("LAN Messenger 1.2.35") && LanService.SupportsPetBattles("CydLan 1.0"),
            "Older CydLan builds and LAN Messenger are never challenged.");

        var a = Settings("Alex"); var b = Settings("Jamie");
        await using var one = new LanService(a);
        await using var two = new LanService(b);
        var inboxA = new ConcurrentQueue<IReadOnlyDictionary<string, string>>();
        var inboxB = new ConcurrentQueue<IReadOnlyDictionary<string, string>>();
        int online = 0; string versionSeen = "";
        one.PeerOnline += p => { versionSeen = p.Version; Interlocked.Increment(ref online); };
        two.PeerOnline += _ => Interlocked.Increment(ref online);
        one.GameMessageReceived += (id, f) => { if (id == b.UserId) inboxA.Enqueue(f); };
        two.GameMessageReceived += (id, f) => { if (id == a.UserId) inboxB.Enqueue(f); };
        await one.StartAsync(false, IPAddress.Loopback); await two.StartAsync(false, IPAddress.Loopback);
        await one.ConnectAsync(b.UserId, "127.0.0.1", two.ListeningPort);
        await Until(() => online >= 2);
        Check(LanService.SupportsPetBattles(versionSeen), "Peers see each other's battle support.");

        // Junk never reaches the game.
        await one.SendGameAsync(b.UserId, "invite", "bad id!");
        await one.SendGameAsync(b.UserId, "move", new string('a', 41));
        bool threw = false;
        try { await one.SendGameAsync(b.UserId, "explode", "abc"); } catch (ArgumentException) { threw = true; }
        Check(threw, "Unknown operations are refused before sending.");

        // Invite → accept.
        var cardA = new PetGame.FighterCard { Name = "Alex", Level = 6, Element = "blaze", Power = 6, Speed = 4 };
        var cardB = new PetGame.FighterCard { Name = "Jamie", Level = 7, Element = "tide", Guard = 6, Vitality = 6 };
        await one.SendGameAsync(b.UserId, "invite", "B1", new Dictionary<string, string> { ["card"] = PetGame.EncodeCard(cardA), ["seed"] = "00000000000000AA" });
        await Until(() => !inboxB.IsEmpty);
        inboxB.TryDequeue(out var invite);
        Check(invite!["op"] == "invite" && invite["bid"] == "B1" && PetGame.DecodeCard(invite["card"]) is { Name: "Alex" }, "Only the valid invite arrives.");
        await two.SendGameAsync(a.UserId, "accept", "B1", new Dictionary<string, string> { ["card"] = PetGame.EncodeCard(cardB), ["seed"] = "00000000000000BB" });
        await Until(() => !inboxA.IsEmpty);
        inboxA.TryDequeue(out var accept);
        Check(accept!["op"] == "accept", "Accept arrives.");

        ulong seed = 0xAA ^ (0xBBUL << 17);
        var battleA = new PetBattle(cardA, PetGame.DecodeCard(accept["card"])!, seed);
        var battleB = new PetBattle(PetGame.DecodeCard(invite["card"])!, cardB, seed);

        // Lockstep: each side sends its move with the state hash, resolves when both are in. Moves are sent
        // simultaneously so the test also covers crossing messages.
        var pickA = new Random(1); var pickB = new Random(2);
        string[] moves = ["strike", "special", "guard", "heal", "ultimate"];
        while (!battleA.Over)
        {
            int turn = battleA.Turn + 1;
            string mA = battleA.Sanitize(0, moves[pickA.Next(moves.Length)]), mB = battleB.Sanitize(1, moves[pickB.Next(moves.Length)]);
            await Task.WhenAll(
                one.SendGameAsync(b.UserId, "move", "B1", new Dictionary<string, string> { ["turn"] = turn.ToString(), ["move"] = mA, ["hash"] = battleA.Hash() }),
                two.SendGameAsync(a.UserId, "move", "B1", new Dictionary<string, string> { ["turn"] = turn.ToString(), ["move"] = mB, ["hash"] = battleB.Hash() }));
            await Until(() => !inboxA.IsEmpty && !inboxB.IsEmpty);
            inboxA.TryDequeue(out var fromB); inboxB.TryDequeue(out var fromA);
            Check(fromB!["turn"] == turn.ToString() && fromA!["turn"] == turn.ToString(), "Turns stay in order.");
            Check(fromB["hash"] == battleA.Hash() && fromA!["hash"] == battleB.Hash(), "Peers agree on the state every turn.");
            battleA.Resolve(mA, fromB["move"]);
            battleB.Resolve(fromA!["move"], mB);
        }
        Check(battleB.Over && battleA.Winner == battleB.Winner && battleA.Hash() == battleB.Hash(), "Both peers finish with the same result over the network.");

        await one.SendGameAsync(b.UserId, "leave", "B1");
        await Until(() => !inboxB.IsEmpty);
        inboxB.TryDequeue(out var leave);
        Check(leave!["op"] == "leave", "Leaving is delivered.");

        // Offline peers fail fast instead of hanging.
        await two.DisposeAsync();
        bool failed = false;
        try { await Until(() => false, 600); } catch (OperationCanceledException) { }
        try { await one.SendGameAsync(b.UserId, "move", "B1", new Dictionary<string, string> { ["turn"] = "1", ["move"] = "strike" }); } catch (IOException) { failed = true; }
        Check(failed, "Sending to a peer that left reports an error.");
    }
}
