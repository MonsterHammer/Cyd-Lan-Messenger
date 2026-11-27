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
    /// <summary>Reminders module and companion personality only (see --reminders-only).</summary>
    private static async Task RunReminderVerificationAsync()
    {
        string artifacts = Path.GetFullPath("artifacts");
        Directory.CreateDirectory(artifacts);

        TestPetMind();
        Console.WriteLine("PASS  Companion personality: needy, sleepy, grumpy, dramatic, foodie, petting and reminder moods");

        await TestMoodGalleryAsync(artifacts);
        Console.WriteLine("PASS  Companion mood faces render (" + Path.Combine(artifacts, "pet-moods.png") + ")");

        await TestRemindersSectionAsync(artifacts);
        Console.WriteLine("PASS  Reminders section: filters, editor, save, nudges, done, snooze, delete");

        await TestPetReminderDeliveryAsync(artifacts);
        Console.WriteLine("PASS  Companion delivers reminders with Done/Snooze, escalates, and falls back to a card when hidden");

        await TestPersonalitySettingsAsync(artifacts);
        Console.WriteLine("PASS  Personality picker in Companion > Vibe previews and saves traits");

        // The companion window grew to fit reminders, so re-check the existing floating companion behavior.
        await TestFloatingLanSuiteAsync(artifacts);
        Console.WriteLine("PASS  Existing floating companion suite");
    }

    private static void TestPetMind()
    {
        var day = new DateTime(2026, 11, 27, 11, 0, 0);
        PetMindOutput Run(PetMind m, double seconds, bool hovered = false)
        {
            PetMindOutput o = default;
            for (int i = 0; i < seconds; i++) o = m.Tick(1, day.AddSeconds(i), hovered);
            return o;
        }

        var plain = new PetMind(new Random(1));
        Check(Run(plain, 1200).Mood == PetMood.Neutral && !plain.Asleep, "No traits keeps the old calm behavior.");

        var needy = new PetMind(new Random(2));
        needy.SetTraits([PetTraits.Needy]);
        Check(Run(needy, 200).Mood == PetMood.Sad, "Needy gets lonely when ignored.");
        Check(Run(needy, 200).Mood == PetMood.Angry, "Needy gets angry when ignored longer.");
        needy.Poke(PetPoke.Hover);
        var calmed = needy.Tick(0, day, hovered: true);
        Check(calmed.Mood == PetMood.Love && calmed.Line is { Length: > 0 }, "Touching a needy pet calms it.");

        var sleepy = new PetMind(new Random(3));
        sleepy.SetTraits([PetTraits.Sleepy]);
        Check(Run(sleepy, 200).Mood == PetMood.Drowsy, "Sleepy gets drowsy first.");
        Run(sleepy, 200);
        Check(sleepy.Asleep, "Sleepy falls asleep when left alone.");
        sleepy.Poke(PetPoke.Hover);
        Check(sleepy.Asleep, "A hover isn't enough to wake a sleepy pet.");
        sleepy.Poke(PetPoke.Pet);
        Check(!sleepy.Asleep, "Petting wakes it.");

        var combo = new PetMind(new Random(4));
        combo.SetTraits(["needy", "sleepy", "grumpy", "bogus"]);
        Check(combo.Traits.Count == 3, "Traits stack and unknown ones are dropped.");
        combo.ReminderNag = 0;
        Check(combo.Tick(1, day, false).Mood == PetMood.Alert, "A fresh reminder makes it alert.");
        combo.ReminderNag = 2;
        Check(combo.Tick(1, day, false).Mood == PetMood.Angry, "Ignored reminders make a grumpy/needy pet angry.");
        string loud = combo.ReminderLine("Payroll", 2, false, "normal");
        Check(loud.Contains("PAYROLL"), "Escalated lines shout: " + loud);
        string done = combo.DoneLine(4);
        Check(combo.ReminderNag == -1 && done.Contains("4 in a row") && combo.Tick(0, day, false).Mood == PetMood.Celebrate, "Done celebrates with the streak.");

        var dramatic = new PetMind(new Random(5));
        dramatic.SetTraits([PetTraits.Dramatic]);
        dramatic.ReminderNag = 3;
        Check(dramatic.Tick(1, day, false).Mood == PetMood.Fainted, "Dramatic pets faint over ignored reminders.");

        var foodie = new PetMind(new Random(6));
        foodie.SetTraits([PetTraits.Foodie]);
        foodie.Poke(PetPoke.Pet); // stays awake and content
        for (int i = 0; i < 80; i++) { foodie.Tick(60, day.AddMinutes(i), true); }
        Check(foodie.Tick(1, day, true).Mood == PetMood.Hungry, "Foodie gets hungry.");
        foodie.Poke(PetPoke.Feed);
        Check(foodie.Hunger == 0 && foodie.Tick(0, day, true).Mood == PetMood.Love, "Feeding makes it happy.");

        var grumpy = new PetMind(new Random(7));
        grumpy.SetTraits([PetTraits.Grumpy]);
        for (int i = 0; i < 4; i++) grumpy.Poke(PetPoke.Click);
        Check(grumpy.Tick(0, day, false).Mood == PetMood.Angry, "Poking a grumpy pet makes it angry.");
    }

    private static async Task TestMoodGalleryAsync(string artifacts)
    {
        string[] moods = [PetMood.Neutral, PetMood.Happy, PetMood.Love, PetMood.Angry, PetMood.Sad, PetMood.Bored, PetMood.Alert, PetMood.Shy,
            PetMood.Dizzy, PetMood.Hungry, PetMood.Fainted, PetMood.Celebrate, PetMood.Drowsy, PetMood.Curious, PetMood.Smug];
        var grid = new System.Windows.Controls.Primitives.UniformGrid { Columns = 5, Background = Brushes.White };
        foreach (var m in moods)
        {
            var visual = new FloatingPetVisual { Width = 136, Height = 124, PetStyle = PetAppearance.Default };
            visual.SetMotionState(2.0, 0, 0, 0, 1, false, false, false, true, false, "chat");
            visual.SetMood(m);
            var cell = new StackPanel { Margin = new Thickness(6) };
            cell.Children.Add(visual);
            cell.Children.Add(new TextBlock { Text = m.Length == 0 ? "neutral" : m, HorizontalAlignment = HorizontalAlignment.Center, FontSize = 12 });
            grid.Children.Add(cell);
        }
        var host = new Window { Content = grid, SizeToContent = SizeToContent.WidthAndHeight, ShowActivated = false, WindowStyle = WindowStyle.None, Left = -4000, Top = -4000 };
        host.Show();
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        CaptureElement(grid, Path.Combine(artifacts, "pet-moods.png"));
        host.Close();
    }

    private static async Task TestRemindersSectionAsync(string artifacts)
    {
        await using var model = new MainViewModel(preview: true);
        var now = new DateTime(2026, 11, 24, 8, 0, 0); // a Tuesday at 8:00
        model.Clock = () => now;
        model.RefreshReminderRows();
        var window = new MainWindow(model) { Width = 1240, Height = 780 };
        window.Show();
        window.OpenReminders();
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);

        Check(model.IsRemindersSection && !model.ChatOpen && model.SectionTitle == "Reminders", "Reminders is its own section.");
        Check(((FrameworkElement)window.FindName("RemindersNav")).IsVisible, "Reminders has a rail button.");
        Check(((FrameworkElement)window.FindName("ReminderEditor")).IsVisible && ((FrameworkElement)window.FindName("RemindersList")).IsVisible, "Wide windows show list and editor side by side.");
        Check(model.RemindersAllCount == 6 && model.RemindersTodayCount >= 3, "Preview reminders are listed.");

        // Fire the 8:00 coffee nudge, then the 10-minute repeat.
        var fired = new List<ReminderAlert>();
        model.ReminderDue += fired.Add;
        model.CheckReminders();
        Check(fired.Any(a => a.Reminder.Id == "coffee" && a.Nag == 0), "Coffee fires at 8:00.");
        fired.Clear();
        now = now.AddMinutes(5); model.CheckReminders();
        Check(fired.Count == 0, "No repeat before 10 minutes.");
        now = now.AddMinutes(5); model.CheckReminders();
        Check(fired.Any(a => a.Reminder.Id == "coffee" && a.Nag == 1), "Coffee repeats after 10 minutes.");
        Check(model.FindReminderRow("coffee") is { IsDue: true, StateLabel: "Due now" } && model.HasDueReminders, "Due state shows in the list.");
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        Capture(window, Path.Combine(artifacts, "reminders-section.png"));

        // Done from the card stops the nudges.
        var doneButtons = FindButtons(window, "Mark reminder done");
        Check(doneButtons.Count >= 1, "Due cards have a Done button.");
        model.CompleteReminder("coffee");
        fired.Clear();
        now = now.AddMinutes(20); model.CheckReminders();
        Check(!fired.Any(a => a.Reminder.Id == "coffee") && model.FindReminderRow("coffee")!.IsDone, "Done stops reminding.");

        // Editor: a Tuesday payroll reminder built from the form.
        ClickNamed(window, "New reminder");
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        ((TextBox)window.FindName("ReminderTitleBox")).Text = "Did you do payroll?";
        ((ListBox)window.FindName("ReminderIconBox")).SelectedValue = "money";
        ((ListBox)window.FindName("ReminderRepeatBox")).SelectedValue = ReminderRepeat.Weekly;
        var days = (ListBox)window.FindName("ReminderDaysBox");
        days.SelectedItems.Clear();
        days.SelectedItems.Add(MainWindow.ReminderDayOptions.First(o => o.Value == "2"));
        ((TextBox)window.FindName("ReminderStartBox")).Text = "2pm";
        ((TextBox)window.FindName("ReminderEndBox")).Text = "5 pm";
        ((ListBox)window.FindName("ReminderEveryBox")).SelectedValue = "30";
        ((ListBox)window.FindName("ReminderPriorityBox")).SelectedValue = "urgent";
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        Check(((FrameworkElement)window.FindName("ReminderDaysBox")).IsVisible, "Day picker shows for days of week.");
        string summary = ((TextBlock)window.FindName("ReminderSummaryText")).Text;
        Check(summary.StartsWith("Every Tuesday · 2 PM–5 PM · every 30 min", StringComparison.Ordinal), "Editor summary: " + summary);
        Capture(window, Path.Combine(artifacts, "reminders-editor.png"));
        ClickNamed(window, "Save reminder");
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        var payroll = model.Reminders.Single(r => r.Title == "Did you do payroll?");
        Check(payroll is { Repeat: ReminderRepeat.Weekly, Start: "14:00", End: "17:00", EveryMinutes: 30, Priority: "urgent", Icon: "money" } && payroll.Days.SequenceEqual([2]), "Saved reminder matches the form.");

        // Validation.
        ClickNamed(window, "New reminder");
        ((TextBox)window.FindName("ReminderTitleBox")).Text = "Bad";
        ((TextBox)window.FindName("ReminderStartBox")).Text = "9";
        ((TextBox)window.FindName("ReminderEndBox")).Text = "8";
        ClickNamed(window, "Save reminder");
        Check(((TextBlock)window.FindName("ReminderErrorText")) is { IsVisible: true, Text: "Until must be later than From." } && !model.Reminders.Any(r => r.Title == "Bad"), "Invalid times are reported.");

        // Snooze and delete.
        now = new DateTime(2026, 11, 24, 14, 0, 0);
        fired.Clear(); model.CheckReminders();
        Check(fired.Any(a => a.Reminder.Id == payroll.Id), "Payroll fires on Tuesday at 2 PM.");
        model.SnoozeReminder(payroll.Id, 10);
        fired.Clear(); now = now.AddMinutes(5); model.CheckReminders();
        Check(!fired.Any(a => a.Reminder.Id == payroll.Id) && model.FindReminderRow(payroll.Id)!.StateLabel.StartsWith("Snoozed", StringComparison.Ordinal), "Snoozed stays quiet.");
        model.DeleteReminder(payroll.Id);
        Check(model.FindReminder(payroll.Id) is null, "Delete removes it.");

        // Narrow windows: the editor takes over while open.
        window.Width = 820;
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        window.UpdateLayout();
        Check(!((FrameworkElement)window.FindName("ReminderEditor")).IsVisible, "Narrow layout hides the editor until needed.");
        ClickNamed(window, "New reminder");
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        Check(((FrameworkElement)window.FindName("ReminderEditor")).IsVisible && !((FrameworkElement)window.FindName("RemindersList")).IsVisible, "Narrow editor replaces the list.");
        ClickNamed(window, "Close reminder editor");
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        Check(((FrameworkElement)window.FindName("RemindersList")).IsVisible, "Closing returns to the list.");

        // Leaving the section.
        model.RemindersOpen = false;
        Check(model.IsConversationSection, "Chats come back after Reminders.");
        window.Close();
    }

    private static async Task TestPetReminderDeliveryAsync(string artifacts)
    {
        await using var model = new MainViewModel(preview: true);
        model.Settings.PetTraits = [PetTraits.Grumpy, PetTraits.Needy];
        model.ApplySettings();
        var window = new MainWindow(model);
        string? opened = "none";
        using var controller = new FloatingPetController(model, () => { }, null, id => opened = id);
        model.UpdateFloatingState(enabled: true);
        controller.SyncVisibility();
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        var pet = controller.PetWindow!;
        Check(pet.IsVisible && pet.Mind.Traits.Count == 2, "Pet picks up the personality.");

        var coffee = model.FindReminder("coffee")!;
        var alert = new ReminderAlert(coffee, 0, false);
        pet.ShowReminder(alert);
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        var bubble = (FrameworkElement)pet.FindName("ReminderBubble");
        Check(bubble.IsVisible && pet.CurrentMood == PetMood.Alert, "The companion shows the reminder and looks alert.");
        Check(((TextBlock)pet.FindName("ReminderTitleText")).Text == "Drink my coffee", "Bubble shows the title.");
        Capture(pet, Path.Combine(artifacts, "pet-reminder.png"));

        pet.ShowReminder(alert with { Nag = 2 });
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        Check(pet.CurrentMood == PetMood.Angry && ((TextBlock)pet.FindName("ReminderLineText")).Text.Contains("COFFEE"), "Ignored reminders make a grumpy pet angry and loud.");
        Capture(pet, Path.Combine(artifacts, "pet-reminder-angry.png"));

        // Another reminder queues behind it.
        pet.ShowReminder(new ReminderAlert(model.FindReminder("water")!, 0, false));
        Check(pet.PendingReminderCount == 2 && ((FrameworkElement)pet.FindName("ReminderQueueBadge")).IsVisible, "Several reminders queue with a count.");

        // Done on the bubble completes it in the model and shows the next one.
        ClickNamed(pet, "Mark reminder done");
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        Check(model.FindReminder("water")!.DoneOn.Length > 0 && pet.CurrentReminder?.Reminder.Id == "coffee", "Done completes and moves to the next reminder.");
        ClickNamed(pet, "Snooze reminder 10 minutes");
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        Check(coffee.SnoozeUntil is not null && pet.CurrentReminder is null && !bubble.IsVisible, "Snooze hides the bubble.");

        // Completing in the main window clears it from the companion too.
        pet.ShowReminder(new ReminderAlert(model.FindReminder("standup")!, 0, false));
        model.CompleteReminder("standup");
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        Check(pet.CurrentReminder is null && pet.CurrentMood == PetMood.Celebrate, "Done anywhere celebrates on the companion.");
        Capture(pet, Path.Combine(artifacts, "pet-celebrate.png"));

        // Try it: preview without saving.
        model.PreviewReminder(new Reminder { Title = "Stretch", Icon = "stretch" });
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        Check(pet.CurrentReminder is { Test: true }, "Try it shows on the companion.");
        ClickNamed(pet, "Mark reminder done");
        Check(pet.CurrentReminder is null && model.Reminders.All(r => r.Title != "Stretch"), "A preview is not saved.");

        // Context menu actions.
        Check(FloatingPetWindow.MoodLabel(PetMood.Angry, false) == "Feeling angry" && FloatingPetWindow.MoodLabel("", true) == "Sleeping", "Mood labels.");

        // Hidden companion: reminders fall back to a corner card.
        model.UpdateFloatingState(enabled: false);
        controller.SyncVisibility();
        model.PreviewReminder(new Reminder { Title = "Call mom", Icon = "call" });
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        Check(controller.ReminderToast is { IsVisible: true }, "Without the companion a reminder card appears.");
        Capture(controller.ReminderToast!, Path.Combine(artifacts, "reminder-card.png"));
        controller.ReminderToast!.Close();
        window.Close();
    }

    private static async Task TestPersonalitySettingsAsync(string artifacts)
    {
        await using var model = new MainViewModel(preview: true);
        var window = new MainWindow(model);
        window.Show();
        var prefs = window.CreateSettingsWindow();
        prefs.Show();
        prefs.ShowPage("AppearancePage");
        prefs.ShowAppearanceTab("Companion");
        ((RadioButton)prefs.FindName("PetCatMotion")).IsChecked = true;
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        var box = (ListBox)prefs.FindName("PetTraitsBox");
        Check(box.IsVisible && box.Items.Count == PetTraits.All.Count, "Personality picker is on the Vibe tab.");
        box.SelectedItems.Add(PetTraits.All.First(o => o.Value == PetTraits.Needy));
        box.SelectedItems.Add(PetTraits.All.First(o => o.Value == PetTraits.Sleepy));
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        Check(model.PetTraitList.SequenceEqual([PetTraits.Needy, PetTraits.Sleepy]), "Picking traits previews them live.");
        Check(((TextBlock)prefs.FindName("PetTraitsSummary")).Text == "Needy · Sleepy", "Summary lists the traits.");
        ((ListBox)prefs.FindName("PetTraitPresetBox")).SelectedIndex = 1; // Sleepy grump
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        Check(model.PetTraitList.SequenceEqual([PetTraits.Grumpy, PetTraits.Sleepy]), "Presets replace the traits.");
        var scroller = prefs.FindName("PetTraitsBox") as FrameworkElement;
        scroller!.BringIntoView();
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        Capture(prefs, Path.Combine(artifacts, "personality-settings.png"));
        prefs.Close();
        Check(model.PetTraitList.Count == 0, "Closing without saving reverts the preview.");
        window.Close();
    }
}
