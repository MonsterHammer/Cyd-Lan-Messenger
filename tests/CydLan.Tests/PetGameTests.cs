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
        Check(PetGame.FreePoints(p) == PetGame.PointsPerLevel, "Each level grants stat points.");

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
        var bad = new PetProgress { Level = 999, Xp = -4, Element = "lava", Power = 500, Boost = "god", Food = new() { ["berry"] = 2, ["rock"] = 9 } };
        PetGame.Normalize(bad);
        Check(bad.Level == PetGame.MaxLevel && bad.Xp == 0 && bad.Element == "blaze" && bad.Power == 0 && bad.Boost == "" && !bad.Food.ContainsKey("rock"),
            "Invalid saved values are clamped.");

        Check(PetGame.TypePercent("tide", "blaze") == 150 && PetGame.TypePercent("blaze", "tide") == 70 && PetGame.TypePercent("shadow", "light") == 150
            && PetGame.TypePercent("light", "shadow") == 150 && PetGame.TypePercent("volt", "volt") == 100, "Element chart is consistent.");
    }

    private static void Cards()
    {
        var card = new PetGame.FighterCard { Name = new string('x', 200) + "\u0007", PetCode = "not a pet", Level = 3, Element = "VOLT", Power = 50, Boost = "nuke" };
        var clean = PetGame.Sanitize(card);
        Check(clean.Name.Length == 40 && !clean.Name.Any(char.IsControl), "Names are trimmed and stripped of control characters.");
        Check(clean.PetCode == "" && clean.Element == "volt" && clean.Power == 0 && clean.Boost == "", "Unknown codes, boosts and over-budget points are dropped.");
        var round = PetGame.DecodeCard(PetGame.EncodeCard(new PetGame.FighterCard { Name = "Jamie", Level = 7, Element = "frost", Power = 4, Speed = 8 }));
        Check(round is { Name: "Jamie", Level: 7, Element: "frost", Power: 4, Speed: 8 }, "Cards survive the wire round trip.");
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
        // Every gear card and look exists; every slot has commons to start with.
        foreach (var g in PetCards.Gear) Check(PetCards.CardOf(g.Card).Id == g.Card, $"{g.Name} gives a real card.");
        foreach (var (slot, _) in PetCards.Slots) Check(PetCards.Gear.Any(g => g.Slot == slot && g.Rarity == "common"), $"{slot} has common gear.");
        Check(PetCards.Cards.Select(c => c.Id).Distinct().Count() == PetCards.Cards.Count && PetCards.Gear.Select(g => g.Id).Distinct().Count() == PetCards.Gear.Count, "Ids are unique.");
        Check(PetCards.CardOf("plunder").Text.Contains("Deal 5") && PetCards.CardOf("hoodup").Text == "Block 5 · Thorns 2", "Card text is built from its numbers.");

        // Equip, swap, unequip, sell.
        var rnd = new Random(5);
        var p = new PetProgress { Level = 4, Element = "volt" };
        var sword = PetCards.Give(p, "sword", rnd)!;
        var wand = PetCards.Give(p, "wand", rnd)!;
        var hoodie = PetCards.Give(p, "hoodie", rnd)!;
        var bare = PetCards.LoadoutFor(p, "A", "");
        Check(bare.Deck.Count == 7 && bare.Deck.Count(c => c == "volt") == 2, "A bare pet has the basic deck with its element move.");
        Check(PetCards.Equip(p, sword.Uid) && PetCards.Equip(p, hoodie.Uid), "Gear equips.");
        var geared = PetCards.LoadoutFor(p, "A", "");
        Check(geared.Deck.Count(c => c == "slash") == 2 && geared.Deck.Count(c => c == "hoodup") == 2 && geared.Power == bare.Power + 4 && geared.Guard == bare.Guard + 2,
            "Worn gear adds its cards and stats.");
        Check(geared.Looks[PetCards.Weapon] == "sword" && geared.Looks[PetCards.Armor] == "hoodie", "Worn gear shows on the pet.");
        Check(PetCards.Equip(p, wand.Uid) && PetCards.Worn(p, PetCards.Weapon)!.Id == "wand" && p.Equipped.Count == 2, "Equipping into a full slot swaps.");
        Check(PetCards.Sell(p, wand.Uid) == 0, "Worn gear can't be sold.");
        Check(PetCards.Unequip(p, PetCards.Weapon) && PetCards.Worn(p, PetCards.Weapon) is null, "Gear unequips.");
        int coins = p.Coins;
        Check(PetCards.Sell(p, wand.Uid) == 15 && p.Coins == coins + 15 && p.Gear.Count == 2, "Spare gear sells for coins.");
        p.Equipped["head"] = sword.Uid; p.Equipped["back"] = "missing";
        PetCards.Normalize(p);
        Check(!p.Equipped.ContainsKey("head") && !p.Equipped.ContainsKey("back") && p.Equipped.ContainsKey("armor"), "Broken saves drop gear in the wrong slot.");
        p.Coins = 1000;
        Check(PetCards.OpenPack(p, rnd) is not null && p.Coins == 1000 - PetCards.GearPackPrice, "Gear packs cost coins and give gear.");

        // Deterministic rounds; valid picks only; battles end; balance.
        int totalRounds = 0, games = 0;
        for (int game = 0; game < 200; game++)
        {
            int level = 1 + game % 25;
            var a = PetCards.Wild(level, rnd, "A", ""); var b = PetCards.Wild(level, rnd, "B", "");
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

        // Cheating picks are ignored: cards not in hand, duplicates, more than the energy allows.
        var t = new CardBattle(PetCards.Build("X", "", 1, "blaze", 0, 0, 0, 0, []), PetCards.Build("Y", "", 1, "leaf", 0, 0, 0, 0, []), 9);
        var picks = t.Validate(0, [0, 0, 1, 2, 3, 4, 99, -1]);
        Check(picks.Distinct().Count() == picks.Count && picks.Sum(i => PetCards.CardOf(t.Sides[0].Hand[i]).Cost) <= PetCards.StartEnergy, "Invalid picks are dropped.");

        // Thorns hit back, block absorbs, pierce ignores block.
        var thorny = new CardBattle(PetCards.Build("T", "", 1, "leaf", 0, 0, 0, 0, []), PetCards.Build("S", "", 1, "shadow", 0, 0, 0, 0, []), 3);
        thorny.Sides[0].Hand.Clear(); thorny.Sides[0].Hand.AddRange(["spikeshell"]);
        thorny.Sides[1].Hand.Clear(); thorny.Sides[1].Hand.AddRange(["strike", "shadow"]);
        int hp1 = thorny.Sides[1].Hp;
        var ev = thorny.PlayRound([0], [0, 1]);
        Check(ev.Any(e => e.Kind == "blocked" && e.Actor == 0) && ev.Count(e => e.Kind == "thornhit" && e.Actor == 1) == 2 && thorny.Sides[1].Hp == hp1 - 8, "Block absorbs and thorns hit back each time.");
        Check(ev.Any(e => e.Kind == "hit" && e.Card == "shadow" && e.Amount > 0), "Pierce goes through block.");
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
