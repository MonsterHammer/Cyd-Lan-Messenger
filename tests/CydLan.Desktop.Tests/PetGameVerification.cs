using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using CydLan.Core;
using CydLan.Desktop;

internal static partial class Program
{
    /// <summary>Pet designs, expressions and actions, free roaming, and the arena mini-game (see --pet-game-only).</summary>
    private static async Task RunPetGameVerificationAsync()
    {
        string artifacts = Path.GetFullPath("artifacts");
        Directory.CreateDirectory(artifacts);

        await TestPetPartsAndMoodsAsync(artifacts);
        Console.WriteLine("PASS  New bodies, ears, eyes, mouths, hats, wings, props, patterns and expressions draw");

        await TestPetActionsAndRoamAsync(artifacts);
        Console.WriteLine("PASS  Pet actions play and free roaming moves around the screen");

        await TestArenaPracticeAsync(artifacts);
        Console.WriteLine("PASS  Arena care, shop and a full practice battle");

        await TestArenaLockstepAsync(artifacts);
        Console.WriteLine("PASS  Two arenas battle in lockstep; desync, leaving and disconnects end cleanly");

        await TestCardBattleAsync(artifacts);
        Console.WriteLine("PASS  Starter deck, deck building, stat re-roll, daily shop, right-click details, and a world card battle");

        await TestCardDuelLanAsync(artifacts);
        Console.WriteLine("PASS  Two CydLan users invite, accept and duel in card battle lockstep");

        await TestPetsModuleAsync(artifacts);
        Console.WriteLine("PASS  Pets is a module on the rail with its own sidebar and the arena inside the main window");
    }

    private static FloatingPetVisual PetOf(PetAppearance look, string mood = "", double w = 150, double h = 136) =>
        new() { Width = w, Height = h, PetStyle = look };

    private static async Task TestPetPartsAndMoodsAsync(string artifacts)
    {
        var baseLook = PetAppearance.Default;
        var cases = new List<(string Label, PetAppearance Look, string Mood)>();
        foreach (var v in new[] { "slime", "egg", "mushroom", "octo", "drop", "chonk" }) cases.Add(("body " + v, baseLook with { Body = v }, ""));
        foreach (var v in new[] { "mouse", "panda", "unicorn", "dragon", "sheep", "leaf" }) cases.Add(("ears " + v, baseLook with { Ears = v }, ""));
        foreach (var v in new[] { "anime", "uwu", "robot", "tiny", "glow" }) cases.Add(("eyes " + v, baseLook with { Eyes = v }, ""));
        foreach (var v in new[] { "buck", "tongue", "wide", "vampire" }) cases.Add(("mouth " + v, baseLook with { Mouth = v }, ""));
        foreach (var v in new[] { "pirate", "witch", "party", "headband", "cowboy", "viking" }) cases.Add(("hat " + v, baseLook with { Hat = v }, ""));
        foreach (var v in new[] { "dragon", "butterfly", "backpack" }) cases.Add(("back " + v, baseLook with { Back = v }, ""));
        foreach (var v in new[] { "sword", "shield", "book", "bubbletea" }) cases.Add(("prop " + v, baseLook with { Prop = v }, ""));
        foreach (var v in new[] { "hearts", "cow", "zigzag" }) cases.Add(("pattern " + v, baseLook with { Pattern = v }, ""));
        foreach (var v in new[] { PetMood.Crying, PetMood.Laughing, PetMood.Surprised, PetMood.Eating, PetMood.Yawning, PetMood.Nervous, PetMood.Fired, PetMood.Hurt })
            cases.Add(("mood " + v, baseLook, v));

        // Every new option is offered and normalizes to itself.
        Check(FloatingPetStyle.Bodies.Count >= 16 && FloatingPetStyle.Hats.Count >= 21 && FloatingPetStyle.Eyes.Count >= 17, "More design options are offered.");
        foreach (var (label, look, _) in cases.Where(c => c.Mood == ""))
        {
            var temp = new AppSettings();
            FloatingPetStyle.Apply(look, temp);
            Check(PetAppearance.From(temp) == look && PetAppearance.FromWireCode(look.ToWireCode()) == look, $"{label} survives settings and sharing.");
        }

        var grid = new System.Windows.Controls.Primitives.UniformGrid { Columns = 9, Background = Brushes.White };
        var reference = PetOf(baseLook);
        grid.Children.Add(Labeled(reference, "default"));
        var visuals = new List<(string, FloatingPetVisual, string)>();
        foreach (var (label, look, mood) in cases)
        {
            var v = PetOf(look);
            v.SetMood(mood);
            if (mood == PetMood.Eating) v.SetHeldFood("cake");
            visuals.Add((label, v, mood));
            grid.Children.Add(Labeled(v, label));
        }
        var host = new Window { Width = 9 * 150 + 20, Height = 1000, WindowStyle = WindowStyle.None, ShowActivated = false, Left = -4000, Top = 0, Content = grid, SizeToContent = SizeToContent.Height };
        host.Show();
        await Task.Delay(120);
        foreach (var v in visuals.Select(x => x.Item2).Prepend(reference)) v.SetMotionState(1.2, 0, 0, 0, 1, false, false, false, true, false, "chat");
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        var basePixels = Pixels(reference);
        foreach (var (label, v, _) in visuals)
            Check(Differences(Pixels(v), basePixels) > 120, $"{label} changes the drawing.");
        CaptureElement(grid, Path.Combine(artifacts, "pet-parts-moods.png"));
        host.Close();

        static FrameworkElement Labeled(FrameworkElement v, string text) => new StackPanel
        {
            Margin = new Thickness(0, 0, 0, 6),
            Children = { v, new TextBlock { Text = text, FontSize = 11, HorizontalAlignment = HorizontalAlignment.Center, Foreground = Brushes.Black } }
        };
    }

    private static async Task TestPetActionsAndRoamAsync(string artifacts)
    {
        await using var model = new MainViewModel(preview: true);
        using var controller = new FloatingPetController(model, () => { });
        model.UpdateFloatingState(enabled: true);
        controller.SyncVisibility();
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        var pet = controller.PetWindow!;
        var visual = (FloatingPetVisual)pet.FindName("PetVisual");

        foreach (var (action, mood) in new[] { ("laugh", PetMood.Laughing), ("cry", PetMood.Crying), ("dance", PetMood.Happy), ("spin", PetMood.Happy),
                     ("flip", PetMood.Happy), ("surprise", PetMood.Surprised), ("yawn", PetMood.Yawning), ("fired", PetMood.Fired), ("nervous", PetMood.Nervous) })
        {
            pet.PlayAction(action);
            Check(pet.CurrentAction == action && visual.Mood == mood, $"{action} shows its expression.");
        }
        pet.PlayAction("cry");
        await Task.Delay(400);
        CaptureElement(pet, Path.Combine(artifacts, "pet-crying.png"));
        pet.PlayAction("laugh");
        await Task.Delay(400);
        CaptureElement(pet, Path.Combine(artifacts, "pet-laughing.png"));

        // Feeding from the game's inventory plays the eating action.
        int berries = PetGame.FoodCount(model.Arena.Progress, "berry");
        model.Arena.Progress.Fullness = 40; model.Arena.Progress.FullnessAtUtc = DateTime.UtcNow;
        Check(model.Arena.Feed("berry") == PetGame.FeedResult.Fed && PetGame.FoodCount(model.Arena.Progress, "berry") == berries - 1, "Feeding uses food.");
        Check(pet.CurrentAction == "eat" && visual.Mood == PetMood.Eating, "The floating pet eats.");
        await Task.Delay(3200);
        Check(pet.CurrentAction == "", "Actions end on their own.");

        // Free roaming moves in two dimensions.
        model.UpdateFloatingState(freeRoam: true);
        model.Settings.Status = "chat";
        double x0 = pet.Left, y0 = pet.Top;
        double maxDx = 0, maxDy = 0;
        var until = DateTime.UtcNow.AddSeconds(20);
        while (DateTime.UtcNow < until && maxDx < 25 && maxDy < 25)
        {
            await Task.Delay(100);
            maxDx = Math.Max(maxDx, Math.Abs(pet.Left - x0));
            maxDy = Math.Max(maxDy, Math.Abs(pet.Top - y0));
        }
        Check(maxDx >= 25 || maxDy >= 25, $"Free roam moves the companion (dx {maxDx:0}, dy {maxDy:0}).");
        var work = SystemParameters.WorkArea;
        var rect = pet.PetScreenRectDip();
        Check(rect.Left >= work.Left - 2 && rect.Right <= work.Right + 2 && rect.Bottom <= work.Bottom + 2 && rect.Top >= work.Top - 2, "Roaming stays on screen.");
        model.UpdateFloatingState(freeRoam: false);
    }

    private static async Task TestArenaPracticeAsync(string artifacts)
    {
        await using var model = new MainViewModel(preview: true);
        var arena = model.Arena;
        var p = arena.Progress;
        p.Coins = 500;
        var (host, window) = OpenArenaView(model);
        await Task.Delay(300);
        Check(arena.BattleFriends.Any() && arena.BattleFriends.All(f => f.Version != "LAN Messenger 1.2.35"), "Only CydLan friends with battles are listed.");

        Check(arena.Buy("fish") && PetGame.FoodCount(p, "fish") == 2 && p.Coins == 475, "The shop sells food.");
        PetGame.AddXp(p, PetGame.XpToNext(1) + PetGame.XpToNext(2) + PetGame.XpToNext(3) + PetGame.XpToNext(4) + PetGame.XpToNext(5));
        Check(p.Level == 6 && PetGame.FreePoints(p) == 2, "Milestone levels give bonus points.");
        Check(arena.AddPoint("power") && arena.AddPoint("speed") && p.Power == 1 && p.Speed == 1, "Points go into stats.");
        arena.SetElement("volt");
        Check(p.Element == "volt", "Element can be chosen.");
        await Task.Delay(150);
        CaptureElement(window, Path.Combine(artifacts, "arena-lobby.png"));

        arena.StartPractice();
        var s = arena.Current!;
        Check(s.IsPractice && !s.Finished && ((FrameworkElement)window.FindName("BattlePanel")).Visibility == Visibility.Visible, "Practice opens the battle stage.");
        int turns = 0;
        bool captured = false;
        var deadline = DateTime.UtcNow.AddSeconds(90);
        while (!s.Finished && DateTime.UtcNow < deadline)
        {
            if (s.MyMove is null && ((System.Windows.Controls.Button)window.FindName("StrikeButton")).IsEnabled)
            {
                string move = s.Battle.CanUse(s.MySide, "ultimate") ? "ultimate" : s.Battle.CanUse(s.MySide, "special") ? "special" : turns % 4 == 3 ? "guard" : "strike";
                await arena.SubmitMoveAsync(move);
                turns++;
            }
            await Task.Delay(turns == 2 && !captured ? 700 : 120);
            if (turns == 2 && !captured) { CaptureElement(window, Path.Combine(artifacts, "arena-battle.png")); captured = true; }
        }
        Check(s.Finished && s.Reward is not null, "The practice battle finishes with a reward.");
        Check(p.Wins + p.Losses + p.Draws == 1, "The result is recorded.");
        var result = (FrameworkElement)window.FindName("ResultCard");
        await WaitFor(() => result.Visibility == Visibility.Visible, 8000);
        await Task.Delay(600);
        CaptureElement(window, Path.Combine(artifacts, "arena-result.png"));
        arena.Dismiss();
        Check(((FrameworkElement)window.FindName("LobbyPanel")).Visibility == Visibility.Visible, "Done returns to the lobby.");
        window.Release(); host.Close();
    }

    private static async Task TestArenaLockstepAsync(string artifacts)
    {
        await using var alexModel = new MainViewModel(preview: true);
        await using var jamieModel = new MainViewModel(preview: true);
        var alex = alexModel.Arena; var jamie = jamieModel.Arena;
        // Alex sees Jamie as sample peer "2"; Jamie sees Alex as sample peer "3".
        var jamieInAlex = alexModel.Peers.First(p => p.Id == "2");
        const string alexInJamie = "3";
        bool linkUp = true;
        var log = new List<string>();
        alex.Transport = (peer, op, bid, fields) => Deliver(jamie, alexInJamie, op, bid, fields);
        jamie.Transport = (peer, op, bid, fields) => Deliver(alex, jamieInAlex.Id, op, bid, fields);
        Task<bool> Deliver(PetArena to, string from, string op, string bid, Dictionary<string, string>? fields)
        {
            if (!linkUp) return Task.FromResult(false);
            log.Add(op);
            var all = new Dictionary<string, string>(fields ?? []) { ["op"] = op, ["bid"] = bid };
            // Arrives later on the UI thread, like the real LAN.
            System.Windows.Application.Current.Dispatcher.BeginInvoke(() => to.OnMessage(from, all), DispatcherPriority.Background);
            return Task.FromResult(true);
        }

        alex.Progress.Level = 7; alex.Progress.Element = "blaze";
        jamie.Progress.Level = 7; jamie.Progress.Element = "leaf";
        PetInvite? received = null;
        jamie.InviteReceived += i => received = i;
        await alex.ChallengeAsync(jamieInAlex);
        Check(alex.ChallengePeerId == "2", "Challenge is pending.");
        await WaitFor(() => received is not null);
        Check(received!.Card.Level == 7 && received.Card.Element == "blaze" && jamie.Invites.Count == 1, "Jamie gets Alex's challenge and card.");

        await jamie.AcceptAsync(received);
        await WaitFor(() => alex.Current is not null);
        var a = alex.Current!; var j = jamie.Current!;
        Check(a.MySide == 0 && j.MySide == 1 && a.Battle.Hash() == j.Battle.Hash(), "Both start the same battle.");

        var rnd = new Random(3);
        string[] moves = ["strike", "special", "guard", "heal", "ultimate"];
        while (!a.Finished)
        {
            await alex.SubmitMoveAsync(moves[rnd.Next(moves.Length)]);
            await jamie.SubmitMoveAsync(moves[rnd.Next(moves.Length)]);
            await WaitFor(() => (a.MyMove is null && j.MyMove is null) || a.Finished);
            Check(a.Battle.Hash() == j.Battle.Hash() && a.Battle.Turn == j.Battle.Turn, "Arenas stay in sync every turn.");
        }
        await WaitFor(() => j.Finished);
        Check(a.Outcome == -j.Outcome && a.Reward is not null && j.Reward is not null, "Both arenas agree on who won and both get rewards.");
        alex.Dismiss(); jamie.Dismiss();

        // A desync ends the battle without a winner instead of carrying on wrong.
        received = null;
        await alex.ChallengeAsync(jamieInAlex);
        await WaitFor(() => received is not null);
        await jamie.AcceptAsync(received!);
        await WaitFor(() => alex.Current is { Finished: false });
        a = alex.Current!; j = jamie.Current!;
        int alexRecord = alex.Progress.Wins + alex.Progress.Losses + alex.Progress.Draws;
        alex.OnMessage("2", new Dictionary<string, string> { ["op"] = "move", ["bid"] = a.Id, ["turn"] = "1", ["move"] = "strike", ["hash"] = "0000000000000000" });
        Check(a.Finished && a.EndReason == "Out of sync" && a.Reward is null, "A desync is caught.");
        await WaitFor(() => j.Finished);
        Check(j.EndReason == "Out of sync" && alex.Progress.Wins + alex.Progress.Losses + alex.Progress.Draws == alexRecord, "Both sides stop and nobody's record changes.");
        alex.Dismiss(); jamie.Dismiss();

        // Leaving forfeits: the one who stays wins.
        received = null;
        await alex.ChallengeAsync(jamieInAlex);
        await WaitFor(() => received is not null);
        await jamie.AcceptAsync(received!);
        await WaitFor(() => alex.Current is { Finished: false });
        a = alex.Current!; j = jamie.Current!;
        await jamie.LeaveAsync();
        await WaitFor(() => a.Finished);
        Check(j.Outcome == -1 && a.Outcome == 1, "Leaving loses and the opponent wins.");
        alex.Dismiss(); jamie.Dismiss();

        // A dropped connection ends the battle with no result.
        received = null;
        await alex.ChallengeAsync(jamieInAlex);
        await WaitFor(() => received is not null);
        await jamie.AcceptAsync(received!);
        await WaitFor(() => alex.Current is { Finished: false });
        a = alex.Current!;
        linkUp = false;
        alex.OnPeerOffline("2");
        Check(a.Finished && a.EndReason == "Connection lost" && a.Reward is null, "Disconnects end the battle without a result.");
        await alex.SubmitMoveAsync("strike");
        alex.Dismiss(); jamie.Dismiss();

        // Busy pets decline new challenges automatically; declines reach the challenger.
        linkUp = true;
        jamie.StartPractice();
        await alex.ChallengeAsync(jamieInAlex);
        await WaitFor(() => alex.ChallengePeerId is null);
        Check(alex.Status.Contains("busy"), "A busy friend auto-declines.");
        await jamie.LeaveAsync(); jamie.Dismiss();

        // Invites from the network open the toast while the arena is closed.
        using var controller = new FloatingPetController(jamieModel, () => { });
        received = null;
        await alex.ChallengeAsync(jamieInAlex);
        await WaitFor(() => received is not null);
        var toasts = System.Windows.Application.Current.Windows.OfType<Window>().Where(w => w.Title == "Battle challenge").ToList();
        Check(toasts.Count == 1, "An invite toast appears.");
        CaptureElement(toasts[0], Path.Combine(artifacts, "arena-invite.png"));
        await jamie.DeclineAsync(received!);
        await WaitFor(() => alex.ChallengePeerId is null);
        Check(alex.Status.Contains("declined"), "Declining tells the challenger.");
        PetInviteToast.CloseAll();
    }

    private static async Task TestCardBattleAsync(string artifacts)
    {
        await using var model = new MainViewModel(preview: true);
        var arena = model.Arena;
        var p = arena.Progress;
        p.Level = 5; p.Coins = 1000;
        arena.EnsureStarted();
        Check(p.Seed != 0 && PetCards.DeckReady(p) && p.Cards.Values.Sum() == PetCards.DeckSize, "A new install gets its random starter cards as a ready deck.");
        var (host, window) = OpenArenaView(model);
        await Task.Delay(300);

        // The account strip shows level and coins; the pet card shows element, stats and the matchup.
        Check(model.PetLevelText == "Level 5" && model.PetCoinsText == "1,000 coins" && model.PetDeckText == $"Deck {PetCards.DeckSize}/{PetCards.DeckSize}" && model.PetDeckReady,
            "The header shows level, coins and the deck.");
        Check(((TextBlock)window.FindName("MatchupText")).Text.StartsWith("Strong vs"), "Matchups show.");
        void Tab(string name) { ((System.Windows.Controls.Primitives.ToggleButton)window.FindName(name + "TabButton")).IsChecked = true; window.UpdateLayout(); }
        var deck = (ItemsControl)window.FindName("DeckList");
        var collection = (ItemsControl)window.FindName("CollectionList");
        var shop = (ItemsControl)window.FindName("ShopList");
        var fights = (ItemsControl)window.FindName("StageFights");
        Tab("Deck");
        Check(deck.Items.Count == PetCards.DeckSize && collection.Items.Count == p.Cards.Count && shop.Items.Count == PetCards.ShopSlots && fights.Items.Count == PetWorld.FightsPerStage,
            "Deck, collection, daily shop and the stage's fights show.");
        Check(CardArt.For(PetCards.CardOf("fireball")) is not null, "Card art loads from the icon sheets.");

        // Deck building through the UI: take a card out, the deck is short, put it back.
        var firstTile = FindAll<Button>(deck).First();
        string removed = (string)firstTile.Tag;
        firstTile.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        Check(p.Deck.Count == PetCards.DeckSize - 1 && !((Button)window.FindName("PracticeButton")).IsEnabled, "Removing a card leaves the deck short and fighting off.");
        Check(!model.PetDeckReady && model.PetDeckText == $"Deck {PetCards.DeckSize - 1}/{PetCards.DeckSize}"
            && ((FrameworkElement)window.FindName("DeckNeededButton")).Visibility == Visibility.Visible
            && ((TextBlock)window.FindName("DeckNeededText")).Text == "Deck needs 1 more card to fight", "A short deck is called out and blocks fighting.");
        arena.StartCardBattle();
        Check(arena.CurrentCard is null, "No fight starts without a full deck.");
        Tab("World");
        ((Button)window.FindName("DeckNeededButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Check(((System.Windows.Controls.Primitives.ToggleButton)window.FindName("DeckTabButton")).IsChecked == true, "The notice opens the deck.");
        window.UpdateLayout();
        FindAll<Button>(collection).First(b => Equals(b.Tag, removed)).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        Check(PetCards.DeckReady(p) && ((Button)window.FindName("PracticeButton")).IsEnabled && model.PetDeckReady
            && ((FrameworkElement)window.FindName("DeckNeededButton")).Visibility == Visibility.Collapsed, "Adding it back readies the deck.");

        // Re-roll stats and buy from the daily shop.
        Tab("Pet");
        ((Button)window.FindName("RerollButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Check(p.Coins == 1000 - PetGame.RerollPrice && PetGame.ValidBase(p.BasePower, p.BaseGuard, p.BaseSpeed, p.BaseVitality), "Re-roll spends coins and keeps a valid spread.");
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        Check(((TextBlock)window.FindName("ElementBadgeText")).Text == PetGame.ElementOf(p.Element).Name && window.FindName("ElementBox") is null,
            "The element shows as a badge and can't be picked by hand.");
        Tab("Shop");
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        var offer = arena.ShopOffers.First(o => o.Price <= p.Coins);
        int coins = p.Coins, have = PetCards.Owned(p, offer.Card.Id);
        FindAll<Button>(shop).First(b => Equals(b.Tag, offer.Slot)).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Check(p.Coins == coins - offer.Price && PetCards.Owned(p, offer.Card.Id) == have + 1 && arena.ShopOffers[offer.Slot].Bought, "Buying from the daily shop adds the card.");
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        Tab("World");
        CaptureElement(window, Path.Combine(artifacts, "cards-lobby.png"));

        arena.StartCardBattle();
        var s = arena.CurrentCard!;
        Check(((FrameworkElement)window.FindName("CardBar")).Visibility == Visibility.Visible, "The card hand shows.");
        Check(!arena.RemoveFromDeck(p.Deck[0]) && !arena.Reroll(), "The deck and stats are locked during a battle.");
        await Task.Delay(400);

        // Every element & weapon archetype VFX, icon palette, terrain tier, red enemy HP + white damage trail work cleanly.
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var view = window;
        var pets = typeof(PetArenaView).GetField("left", flags)!.GetValue(view)!;
        var foePet = typeof(PetArenaView).GetField("right", flags)!.GetValue(view)!;
        var attack = typeof(PetArenaView).GetMethod("ElementAttack", flags)!;
        var supportFx = typeof(PetArenaView).GetMethod("SupportCardVfx", flags)!;
        var status = typeof(PetArenaView).GetMethod("StatusHit", flags)!;
        var updateCardBars = typeof(PetArenaView).GetMethod("UpdateCardBars", flags)!;
        var paintTerrain = typeof(PetArenaView).GetMethod("PaintTerrain", flags)!;

        // Verify red enemy HP bar and white minus-HP damage trail animation.
        var rightFill = (Border)window.FindName("RightHpFill");
        var rightTrail = (Border)window.FindName("RightHpTrail");
        var rightColor = ((SolidColorBrush)rightFill.Background).Color;
        Check(rightColor.R > 200 && rightColor.G < 90 && rightColor.B < 90, $"Enemy HP bar is red (got {rightColor}).");
        var shownHpProp = foePet.GetType().GetField("ShownHp")!;
        int fullFoeHp = (int)shownHpProp.GetValue(foePet)!;
        shownHpProp.SetValue(foePet, Math.Max(1, fullFoeHp / 2));
        updateCardBars.Invoke(view, [false]);
        await Task.Delay(80);
        Check(rightTrail.ActualWidth > rightFill.ActualWidth + 4, "Taking damage leaves a white trail bar that drains down.");
        CaptureElement(window, Path.Combine(artifacts, "vfx-hp-drain.png"));
        await Task.Delay(580);
        shownHpProp.SetValue(foePet, fullFoeHp);
        updateCardBars.Invoke(view, [true]);

        // Verify terrain draws distinct landmarks by biome, stage level and boss status.
        var terrainLayer = (Canvas)window.FindName("TerrainLayer");
        paintTerrain.Invoke(view, ["blaze", "Ember Canyon", 1, false]);
        int blazeStage1 = terrainLayer.Children.Count;
        paintTerrain.Invoke(view, ["blaze", "Kiln Peak", 32, true]);
        int blazeBoss32 = terrainLayer.Children.Count;
        Check(blazeStage1 > 4 && blazeBoss32 > blazeStage1, "Terrain scales up with stage level and boss encounters.");
        CaptureElement(window, Path.Combine(artifacts, "terrain-blaze-boss.png"));
        paintTerrain.Invoke(view, ["frost", "Glacier Gate", 18, false]);
        CaptureElement(window, Path.Combine(artifacts, "terrain-frost-18.png"));
        paintTerrain.Invoke(view, [s.Stage!.Element, s.Stage.Name, s.Stage.Number, s.Enemy!.Boss]);

        // Verify bespoke weapon/card VFX and icon palettes.
        foreach (var (label, sample) in new[] { ("sword", "strike"), ("meteor", "meteor"), ("wave", "tsunami"), ("glacier", "glacier"), ("whip", "leaf"), ("railgun", "railgun"), ("scythe", "reaper"), ("fangs", "vampire"), ("starfall", "starfall"), ("swarm", "batswarm") })
        {
            var play = (Task)attack.Invoke(view, [pets, foePet, PetCards.CardOf(sample), false])!;
            await Task.Delay(label is "sword" or "railgun" or "scythe" or "fangs" ? 90 : 160);
            CaptureElement(window, Path.Combine(artifacts, $"vfx-{label}.png"));
            await play;
        }
        foreach (var supportSample in new[] { "phoenix", "lullaby", "firewall", "iceprison", "archangel" })
        {
            supportFx.Invoke(view, [pets, foePet, PetCards.CardOf(supportSample)]);
            await Task.Delay(180);
            if (supportSample is "phoenix" or "lullaby") CaptureElement(window, Path.Combine(artifacts, $"vfx-{supportSample}.png"));
        }
        foreach (var kind in new[] { "poisoned", "sleep", "fear", "slow", "regen", "rage", "weak" })
        {
            status.Invoke(view, [foePet, kind]);
            await Task.Delay(260);
            CaptureElement(window, Path.Combine(artifacts, $"vfx-{kind}.png"));
        }
        s.Them.Poison = 3; s.Them.Sleep = 1; s.Me.Rage = 2; s.Me.Regen = 2; s.Me.NextEnergy = 1;
        updateCardBars.Invoke(view, [true]);
        Check(((TextBlock)window.FindName("LeftStatus")).Inlines.Count >= 3 && ((TextBlock)window.FindName("RightStatus")).Inlines.Count >= 2,
            "Active statuses show as chips on the HP cards.");
        await Task.Delay(1200);
        CaptureElement(window, Path.Combine(artifacts, "vfx-auras.png"));
        s.Them.Poison = 0; s.Them.Sleep = 0; s.Me.Rage = 0; s.Me.Regen = 0; s.Me.NextEnergy = 0;
        updateCardBars.Invoke(view, [true]);
        Console.WriteLine("PASS  Element attacks, bespoke card VFX, terrain tiers, red HP drain and status effects play");

        var hand = (StackPanel)window.FindName("HandPanel");
        var queue = (StackPanel)window.FindName("QueuePanel");
        var queueTray = (Border)window.FindName("QueueTray");
        var fight = (Button)window.FindName("FightButton");
        var battlePanel = (FrameworkElement)window.FindName("BattlePanel");
        var cardBar = (FrameworkElement)window.FindName("CardBar");

        // Right-clicking any card builds and displays its stat-scaled detail popup.
        var detailSample = PetCards.CardOf("fireball");
        var detailPanel = window.BuildCardDetailPanel(detailSample);
        var detailHost = new Window { Width = 310, Height = 300, Left = -3500, Top = 0, ShowActivated = false, WindowStyle = WindowStyle.None, SizeToContent = SizeToContent.Height, Content = detailPanel };
        detailHost.Show();
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        var detailTexts = FindAll<TextBlock>(detailPanel).Select(t => t.Text).ToList();
        Check(detailTexts.Any(t => t.Contains("Fireball")) && detailTexts.Any(t => t.Contains("Power")), "Right-click detail shows card name and stat scaling.");
        CaptureElement(detailPanel, Path.Combine(artifacts, "card-detail-popup.png"));
        detailHost.Close();

        // Dragging cards upward moves them out of the hand into the top execution tray; dragging back down returns them.
        await WaitFor(() => fight.IsEnabled && hand.Children.Count >= 2, 10000);
        int handBefore = hand.Children.Count;
        var firstAffordable = hand.Children.OfType<Border>().First(b => b.Opacity >= 1);
        int draggedIdx = (int)firstAffordable.Tag;
        window.DropCardAt(draggedIdx, fromQueue: false, new Point(battlePanel.ActualWidth / 2, 60));
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        Check(queueTray.Visibility == Visibility.Visible && queue.Children.Count == 1 && hand.Children.Count == handBefore - 1,
            "Dragging a card to the top moves it out of the hand into the execution tray.");
        var secondAffordable = hand.Children.OfType<Border>().FirstOrDefault(b => b.Opacity >= 1);
        if (secondAffordable is not null)
        {
            int secondIdx = (int)secondAffordable.Tag;
            window.DropCardAt(secondIdx, fromQueue: false, new Point(battlePanel.ActualWidth * .75, 60));
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            Check(queue.Children.Count == 2 && Equals(((Border)queue.Children[0]).Tag, draggedIdx) && Equals(((Border)queue.Children[1]).Tag, secondIdx),
                "A second dragged card lines up next in execution order.");
            // Dragging the second queued card to the left reorders it ahead of the first.
            window.DropCardAt(secondIdx, fromQueue: true, new Point(10, 60));
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            Check(Equals(((Border)queue.Children[0]).Tag, secondIdx) && Equals(((Border)queue.Children[1]).Tag, draggedIdx),
                "Dragging a queued card horizontally reorders execution.");
            // Dragging a queued card back down to the hand returns it and refunds energy.
            double handY = cardBar.TranslatePoint(new Point(0, 20), battlePanel).Y;
            window.DropCardAt(secondIdx, fromQueue: true, new Point(battlePanel.ActualWidth / 2, handY));
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            Check(queue.Children.Count == 1 && hand.Children.Count == handBefore - 1,
                "Dragging a queued card back down returns it to the hand.");
        }
        // Clicking a queued card also returns it to the hand.
        ((Border)queue.Children[0]).RaiseEvent(new System.Windows.Input.MouseButtonEventArgs(System.Windows.Input.Mouse.PrimaryDevice, 0, System.Windows.Input.MouseButton.Left)
            { RoutedEvent = UIElement.MouseLeftButtonUpEvent });
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        Check(queue.Children.Count == 0 && queueTray.Visibility == Visibility.Collapsed && hand.Children.Count == handBefore,
            "Returning all queued cards clears the top tray.");

        int rounds = 0;
        bool shot = false;
        var deadline = DateTime.UtcNow.AddSeconds(150);
        while (!s.Finished && DateTime.UtcNow < deadline)
        {
            await WaitFor(() => fight.IsEnabled || s.Finished, 20000);
            if (s.Finished) break;
            // Pick affordable cards from the hand into the top execution tray while energy allows.
            while (hand.Children.OfType<Border>().FirstOrDefault(face => face.Opacity >= 1) is { } face)
            {
                face.RaiseEvent(new System.Windows.Input.MouseButtonEventArgs(System.Windows.Input.Mouse.PrimaryDevice, 0, System.Windows.Input.MouseButton.Left)
                    { RoutedEvent = UIElement.MouseLeftButtonUpEvent });
            }
            if (!shot && rounds == 1) { await Task.Delay(140); CaptureElement(window, Path.Combine(artifacts, "cards-hand.png")); }
            fight.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            rounds++;
            if (!shot && rounds == 2) { await Task.Delay(900); CaptureElement(window, Path.Combine(artifacts, "cards-battle.png")); shot = true; }
        }
        Check(s.Finished && rounds >= 2 && s.Reward is not null, $"The card battle finishes ({rounds} rounds).");
        Check(p.CardWins + p.CardLosses >= (s.Outcome == 0 ? 0 : 1), "The card record updates.");
        Check(s.Reward!.Xp > 0 && s.Reward.Coins > 0, "Win or lose, the fight pays XP and coins.");
        Check(s.Outcome > 0 ? p.StageFight == 1 : p.StageFight == 0, "Winning moves to the next fight of the stage.");
        var result = (FrameworkElement)window.FindName("ResultCard");
        await WaitFor(() => result.Visibility == Visibility.Visible, 15000);
        await Task.Delay(500);
        CaptureElement(window, Path.Combine(artifacts, "cards-result.png"));
        ((Button)window.FindName("RematchButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Check(arena.CurrentCard is { Finished: false } && arena.CurrentCard != s, "Rematch starts the next card battle.");
        arena.LeaveCardBattle();
        Check(arena.CurrentCard!.Finished && arena.CurrentCard.Outcome == -1, "Leaving counts as a loss.");
        await WaitFor(() => result.Visibility == Visibility.Visible, 15000);
        arena.DismissCardBattle();
        Check(((FrameworkElement)window.FindName("LobbyPanel")).Visibility == Visibility.Visible, "Back to the lobby.");
        window.Release(); host.Close();
    }

    private static async Task TestCardDuelLanAsync(string artifacts)
    {
        await using var alexModel = new MainViewModel(preview: true);
        await using var jamieModel = new MainViewModel(preview: true);
        var alex = alexModel.Arena; var jamie = jamieModel.Arena;
        alex.EnsureStarted(); jamie.EnsureStarted();
        var jamieInAlex = alexModel.Peers.First(p => p.Id == "2");
        const string alexInJamie = "3";
        alex.Transport = (peer, op, bid, fields) => Deliver(jamie, alexInJamie, op, bid, fields);
        jamie.Transport = (peer, op, bid, fields) => Deliver(alex, jamieInAlex.Id, op, bid, fields);
        Task<bool> Deliver(PetArena to, string from, string op, string bid, Dictionary<string, string>? fields)
        {
            var all = new Dictionary<string, string>(fields ?? []) { ["op"] = op, ["bid"] = bid };
            System.Windows.Application.Current.Dispatcher.BeginInvoke(() => to.OnMessage(from, all), DispatcherPriority.Background);
            return Task.FromResult(true);
        }

        PetInvite? received = null;
        jamie.InviteReceived += i => received = i;
        await alex.ChallengeCardDuelAsync(jamieInAlex);
        Check(alex.CardChallengePeerId == "2", "Card duel invite is pending.");
        await WaitFor(() => received is not null);
        Check(received!.IsCardDuel && received.CardLoadout is { Deck.Count: PetCards.DeckSize }, "Jamie receives Alex's card duel invite with loadout.");

        await jamie.AcceptAsync(received);
        await WaitFor(() => alex.CurrentCard is { IsLan: true } && jamie.CurrentCard is { IsLan: true });
        var a = alex.CurrentCard!; var j = jamie.CurrentCard!;
        Check(a.MySide == 0 && j.MySide == 1 && a.Battle.Hash() == j.Battle.Hash(), "Both peers start the exact same card duel.");

        var rnd = new Random(19);
        while (!a.Finished)
        {
            await alex.PlayCardsAsync(a.Battle.ChooseAi(a.MySide, rnd));
            await jamie.PlayCardsAsync(j.Battle.ChooseAi(j.MySide, rnd));
            await WaitFor(() => (a.MyPicks is null && j.MyPicks is null) || a.Finished);
            Check(a.Battle.Hash() == j.Battle.Hash() && a.Battle.Round == j.Battle.Round, "Card duel stays in lockstep every round.");
        }
        await WaitFor(() => j.Finished);
        Check(a.Outcome == -j.Outcome && a.Reward is not null && j.Reward is not null, "Both peers agree on the card duel winner.");
        alex.DismissCardBattle(); jamie.DismissCardBattle();
    }

    private static (Window Host, PetArenaView View) OpenArenaView(MainViewModel model)
    {
        var view = new PetArenaView(model);
        var host = new Window { Width = 860, Height = 600, Left = -3000, Top = 0, ShowActivated = false, WindowStyle = WindowStyle.None, Content = view };
        host.SetResourceReference(Window.BackgroundProperty, "Canvas");
        host.Show();
        return (host, view);
    }

    private static async Task TestPetsModuleAsync(string artifacts)
    {
        await using var model = new MainViewModel(preview: true);
        var window = new MainWindow(model) { Left = 40, Top = 40, Width = 1240, Height = 780, ShowActivated = false };
        window.Show();
        await Task.Delay(300);
        var nav = (Button)window.FindName("PetsNav");
        Check(nav is not null, "The rail has a Pets button.");
        nav!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await Task.Delay(400);
        Check(model.PetsOpen && model.IsPetsSection && !model.ChatOpen && model.ChatTitle == "Pets", "Pets opens as its own section.");
        Check(window.PetsView is { IsVisible: true }, "The arena shows inside the main window.");
        Check(model.ChatSubtitle == "" && model.PetLevelText.StartsWith("Level") && model.PetCoinsText.EndsWith("coins") && model.PetDeckText.StartsWith("Deck"), "The header shows level, coins and deck as chips.");
        CaptureElement(window, Path.Combine(artifacts, "pets-module.png"));

        // Other sections close it, and it closes them.
        model.RemindersOpen = true;
        Check(!model.PetsOpen && window.PetsView!.IsVisible == false, "Opening another module leaves Pets.");
        model.PetsOpen = true;
        Check(!model.RemindersOpen && !model.MediaOpen && !model.TransfersOpen, "Pets replaces the other module.");

        // A card battle runs inside the module, started from the sidebar button like a user would.
        ((Button)window.FindName("PetsNav")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        var sidebarBattle = FindAll<Button>(window).First(b => b.Content as string == "Card battle" && b.IsVisible && b.Name == "");
        sidebarBattle.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await Task.Delay(500);
        Check(model.Arena.CurrentCard is { Finished: false }, "The sidebar button starts a card battle.");

        // Waiting in a battle (lobby hidden) must leave the app idle: hidden bars once retried forever and swamped the app.
        double busy = await BusyFractionAsync(TimeSpan.FromSeconds(4));
        Check(busy < .6, $"The battle screen stays light on the CPU ({busy:P0} of one core).");
        model.PetsOpen = false;
        model.Arena.OnChatSent();
        double hiddenBusy = await BusyFractionAsync(TimeSpan.FromSeconds(3));
        Check(hiddenBusy < .4, $"A hidden Pets module stays idle ({hiddenBusy:P0} of one core).");
        model.PetsOpen = true;
        await Task.Delay(300);

        // A view whose bars have never been measured (not on screen yet) must not spin when the pet changes.
        var unmeasured = new PetArenaView(model);
        model.Arena.Save();
        model.Arena.OnChatSent();
        double unmeasuredBusy = await BusyFractionAsync(TimeSpan.FromSeconds(3));
        unmeasured.Release();
        Check(unmeasuredBusy < .4, $"Unmeasured progress bars don't retry in a loop ({unmeasuredBusy:P0} of one core).");
        Check(((FrameworkElement)window.PetsView!.FindName("CardBar")).IsVisible, "Card battles play inside the module.");
        CaptureElement(window, Path.Combine(artifacts, "pets-module-battle.png"));
        model.Arena.LeaveCardBattle();
        model.Arena.DismissCardBattle();
        model.PetsOpen = false;
        Check(model.ChatOpen, "Chats come back.");
        window.Close();
    }

    private static async Task<double> BusyFractionAsync(TimeSpan span)
    {
        var me = System.Diagnostics.Process.GetCurrentProcess();
        var before = me.TotalProcessorTime;
        var clock = System.Diagnostics.Stopwatch.StartNew();
        await Task.Delay(span);
        me.Refresh();
        return (me.TotalProcessorTime - before).TotalSeconds / clock.Elapsed.TotalSeconds;
    }

    private static async Task WaitFor(Func<bool> condition, int ms = 8000)
    {
        var until = DateTime.UtcNow.AddMilliseconds(ms);
        while (!condition())
        {
            if (DateTime.UtcNow > until) throw new TimeoutException("Timed out waiting.");
            await Task.Delay(20);
        }
    }
}
