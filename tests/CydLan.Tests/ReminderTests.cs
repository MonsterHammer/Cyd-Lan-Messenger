using CydLan.Core;

internal static class ReminderTests
{
    private static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
    private static DateTime At(int day, int hour, int minute = 0) => new(2026, 11, day, hour, minute, 0); // Nov 2026: the 24th is a Tuesday

    public static Task RunAsync()
    {
        // Parsing.
        Check(ReminderSchedule.ParseTime("8") == new TimeOnly(8, 0) && ReminderSchedule.ParseTime("8:30") == new TimeOnly(8, 30) &&
              ReminderSchedule.ParseTime("0830") == new TimeOnly(8, 30) && ReminderSchedule.ParseTime("2:30 pm") == new TimeOnly(14, 30) &&
              ReminderSchedule.ParseTime("12am") == new TimeOnly(0, 0) && ReminderSchedule.ParseTime("12 PM") == new TimeOnly(12, 0) &&
              ReminderSchedule.ParseTime("25:00") is null && ReminderSchedule.ParseTime("x") is null, "Times must parse common formats.");

        // Coffee: weekdays 8–9, every 10 minutes until done.
        var coffee = new Reminder { Title = "Drink my coffee", Repeat = ReminderRepeat.Weekdays, Start = "08:00", End = "09:00", EveryMinutes = 10 };
        Check(ReminderSchedule.AppliesOn(coffee, new DateOnly(2026, 11, 27)) && !ReminderSchedule.AppliesOn(coffee, new DateOnly(2026, 11, 28)), "Weekdays must skip Saturday.");
        Check(ReminderSchedule.Evaluate(coffee, At(27, 7, 59)) is null, "Nothing before the window.");
        var first = ReminderSchedule.Evaluate(coffee, At(27, 8, 0));
        Check(first is { Nag: 0, Overdue: false }, "First nudge at 8:00.");
        ReminderSchedule.MarkFired(coffee, first!.Value, At(27, 8, 0));
        Check(ReminderSchedule.Evaluate(coffee, At(27, 8, 5)) is null, "No nudge before the interval.");
        var second = ReminderSchedule.Evaluate(coffee, At(27, 8, 10));
        Check(second is { Nag: 1 }, "Second nudge after 10 minutes.");
        ReminderSchedule.MarkFired(coffee, second!.Value, At(27, 8, 10));
        Check(ReminderSchedule.StateAt(coffee, At(27, 8, 12)) == ReminderState.Due, "Due inside the window.");
        Check(ReminderSchedule.Complete(coffee, At(27, 8, 12)), "Done marks the day.");
        Check(ReminderSchedule.Evaluate(coffee, At(27, 8, 30)) is null && ReminderSchedule.StateAt(coffee, At(27, 8, 30)) == ReminderState.Done, "Done stops the nudges.");
        Check(ReminderSchedule.Evaluate(coffee, At(30, 8, 0)) is { Nag: 0 }, "Next weekday starts fresh.");
        Check(ReminderSchedule.Evaluate(coffee, At(30, 9, 1)) is null && ReminderSchedule.StateAt(coffee, At(30, 9, 1)) == ReminderState.Missed, "Past the end time is missed, not nagged.");

        // Snooze.
        var standup = new Reminder { Title = "Standup", Repeat = ReminderRepeat.Daily, Start = "09:30" };
        var s1 = ReminderSchedule.Evaluate(standup, At(26, 9, 30))!.Value;
        ReminderSchedule.MarkFired(standup, s1, At(26, 9, 30));
        Check(ReminderSchedule.Evaluate(standup, At(26, 10, 30)) is null, "A one-shot nudge doesn't repeat without an interval.");
        ReminderSchedule.Snooze(standup, At(26, 10, 30), 10);
        Check(ReminderSchedule.StateAt(standup, At(26, 10, 35)) == ReminderState.Snoozed && ReminderSchedule.Evaluate(standup, At(26, 10, 35)) is null, "Snoozed stays quiet.");
        Check(ReminderSchedule.Evaluate(standup, At(26, 10, 40)) is { Nag: 1 }, "Snooze expiry nudges again.");

        // Payroll: every Tuesday, streaks across weeks; skipping keeps the chain.
        var payroll = new Reminder { Title = "Payroll", Repeat = ReminderRepeat.Weekly, Days = [2], Start = "10:00" };
        Check(ReminderSchedule.DescribeDays(payroll) == "Every Tuesday", "Weekly label.");
        Check(ReminderSchedule.NextStart(payroll, At(25, 12)) == At(24, 10).AddDays(7), "Next Tuesday after a Wednesday.");
        ReminderSchedule.Complete(payroll, new DateTime(2026, 11, 10, 10, 5, 0));
        ReminderSchedule.Complete(payroll, new DateTime(2026, 11, 17, 10, 5, 0));
        Check(payroll.Streak == 2 && payroll.TotalDone == 2, "Consecutive Tuesdays build a streak.");
        ReminderSchedule.Skip(payroll, At(24, 10, 1));
        ReminderSchedule.Complete(payroll, new DateTime(2026, 12, 1, 10, 3, 0));
        Check(payroll.Streak == 3, "A skipped week keeps the streak.");
        ReminderSchedule.Complete(payroll, new DateTime(2026, 12, 15, 10, 3, 0));
        Check(payroll.Streak == 1 && payroll.BestStreak == 3, "A missed week resets the streak.");

        // One-time and monthly.
        var dentist = new Reminder { Title = "Dentist", Repeat = ReminderRepeat.Once, Date = "2026-11-26", Start = "14:00" };
        Check(ReminderSchedule.Evaluate(dentist, At(27, 9)) is { Overdue: true }, "A missed one-time reminder is delivered once as overdue.");
        ReminderSchedule.MarkFired(dentist, ReminderSchedule.Evaluate(dentist, At(27, 9))!.Value, At(27, 9));
        Check(ReminderSchedule.Evaluate(dentist, At(27, 10)) is null && ReminderSchedule.StateAt(dentist, At(27, 10)) == ReminderState.Missed, "…and then stays missed.");
        var rent = new Reminder { Repeat = ReminderRepeat.Monthly, DayOfMonth = 31, Start = "09:00" };
        Check(ReminderSchedule.AppliesOn(rent, new DateOnly(2026, 11, 30)) && !ReminderSchedule.AppliesOn(rent, new DateOnly(2026, 11, 29)), "Day 31 falls on the last day of short months.");
        Check(ReminderSchedule.Describe(coffee) == "Weekdays · 8 AM–9 AM · every 10 min", "Readable summary: " + ReminderSchedule.Describe(coffee));

        // Undo.
        ReminderSchedule.Undo(coffee);
        Check(coffee.DoneOn == "" && coffee.TotalDone == 0, "Undo reverts done.");

        // Storage round trip.
        string dir = Path.Combine(Path.GetTempPath(), "cydlan-reminders-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new LocalStore(dir);
            store.SaveReminders([coffee, payroll]);
            var loaded = store.LoadReminders();
            Check(loaded.Count == 2 && loaded[1].Days.SequenceEqual([2]) && loaded[1].BestStreak == 3 && loaded[0].End == "09:00", "Reminders must persist.");
        }
        finally { try { Directory.Delete(dir, true); } catch (IOException) { } }
        return Task.CompletedTask;
    }
}
