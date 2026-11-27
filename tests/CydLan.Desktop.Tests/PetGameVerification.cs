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
        Console.WriteLine("PASS  Gear equips into the deck, gear packs, and a full card battle through the hand and Fight button");

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
        Check(p.Level == 6 && PetGame.FreePoints(p) == 10, "Levels give points.");
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
        arena.EnsureStarterGear();
        Check(p.Gear.Count == 3 && p.Equipped.Count == 3, "New pets get starter gear, worn.");
        var (host, window) = OpenArenaView(model);
        await Task.Delay(300);

        var slots = (ItemsControl)window.FindName("GearSlots");
        var bag = (ItemsControl)window.FindName("BagList");
        Check(slots.Items.Count == 5 && bag.Items.Count == 3, "Gear slots and the bag show.");
        var pack = (Button)window.FindName("GearPackButton");
        pack.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        pack.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        Check(p.Gear.Count == 5 && p.Coins == 1000 - 2 * PetCards.GearPackPrice && bag.Items.Count == 5, "Gear packs add to the bag.");
        // Wear a sword so its card shows in the deck, then check the pet wears it.
        var sword = PetCards.Give(p, "sword", new Random(1))!;
        Check(arena.Equip(sword.Uid) && arena.MyLoadout.Deck.Count(c => c == "slash") == 2 && arena.BattleLook.Prop == "sword", "Equipping adds cards and shows on the pet.");
        Check(arena.Unequip(PetCards.Weapon) && !arena.MyLoadout.Deck.Contains("slash") && arena.BattleLook.Prop != "sword", "Unequipping takes them away.");
        Check(arena.Equip(sword.Uid), "Re-equip.");
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        CaptureElement(window, Path.Combine(artifacts, "cards-lobby.png"));

        arena.StartCardBattle();
        var s = arena.CurrentCard!;
        Check(((FrameworkElement)window.FindName("CardBar")).Visibility == Visibility.Visible, "The card hand shows.");
        Check(!arena.Equip(sword.Uid) && !arena.Unequip(PetCards.Weapon), "Gear is locked during a battle.");
        var hand = (StackPanel)window.FindName("HandPanel");
        var fight = (Button)window.FindName("FightButton");
        int rounds = 0;
        bool shot = false;
        var deadline = DateTime.UtcNow.AddSeconds(150);
        while (!s.Finished && DateTime.UtcNow < deadline)
        {
            await WaitFor(() => fight.IsEnabled || s.Finished, 20000);
            if (s.Finished) break;
            // Pick cards left to right while energy allows, like a player clicking them.
            for (int i = 0; i < hand.Children.Count; i++)
            {
                if (hand.Children[i] is not Border face || face.Opacity < 1) continue;
                face.RaiseEvent(new System.Windows.Input.MouseButtonEventArgs(System.Windows.Input.Mouse.PrimaryDevice, 0, System.Windows.Input.MouseButton.Left)
                    { RoutedEvent = UIElement.MouseLeftButtonUpEvent });
            }
            int energyBefore = s.Me.Energy;
            if (!shot && rounds == 1) { await Task.Delay(100); CaptureElement(window, Path.Combine(artifacts, "cards-hand.png")); }
            fight.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            rounds++;
            if (!shot && rounds == 2) { await Task.Delay(900); CaptureElement(window, Path.Combine(artifacts, "cards-battle.png")); shot = true; }
        }
        Check(s.Finished && rounds >= 2 && s.Reward is not null, $"The card battle finishes ({rounds} rounds).");
        Check(p.CardWins + p.CardLosses >= (s.Outcome == 0 ? 0 : 1), "The card record updates.");
        var result = (FrameworkElement)window.FindName("ResultCard");
        await WaitFor(() => result.Visibility == Visibility.Visible, 15000);
        await Task.Delay(500);
        CaptureElement(window, Path.Combine(artifacts, "cards-result.png"));
        ((Button)window.FindName("RematchButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Check(arena.CurrentCard is { Finished: false } && arena.CurrentCard != s, "Rematch starts a new card battle.");
        arena.LeaveCardBattle();
        Check(arena.CurrentCard!.Finished && arena.CurrentCard.Outcome == -1, "Leaving counts as a loss.");
        await WaitFor(() => result.Visibility == Visibility.Visible, 15000);
        arena.DismissCardBattle();
        Check(((FrameworkElement)window.FindName("LobbyPanel")).Visibility == Visibility.Visible, "Back to the lobby.");
        window.Release(); host.Close();
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
        Check(model.ChatSubtitle.Contains("Level") && model.ChatSubtitle.Contains("coins"), "The header shows level and coins.");
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
