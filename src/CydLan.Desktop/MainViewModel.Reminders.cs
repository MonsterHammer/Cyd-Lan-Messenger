using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using System.Windows.Threading;
using CydLan.Core;

namespace CydLan.Desktop;

public sealed record ReminderAlert(Reminder Reminder, int Nag, bool Overdue, bool Test = false);

public enum ReminderOutcome { Done, Snoozed, Skipped, Deleted, Disabled }

/// <summary>List row for one reminder: schedule text, live state, and streak.</summary>
public sealed class ReminderRow : Observable
{
    public Reminder Reminder { get; private set; }
    public ReminderRow(Reminder reminder) { Reminder = reminder; }
    public string Id => Reminder.Id;
    public string Title => Reminder.Title;
    public string Note => Reminder.Note;
    public bool HasNote => Reminder.Note.Length > 0;
    public string Icon => ReminderIcons.Resource(Reminder.Icon);
    public string Schedule => ReminderSchedule.Describe(Reminder);
    public string Priority => Reminder.Priority;
    public bool IsUrgent => Reminder.Priority == "urgent";
    public bool Enabled => Reminder.Enabled;
    public ReminderState State { get; private set; }
    public string StateLabel { get; private set; } = "";
    public bool IsDue => State == ReminderState.Due;
    public bool IsDone => State is ReminderState.Done or ReminderState.Skipped;
    public bool CanAct => State is ReminderState.Due or ReminderState.Snoozed or ReminderState.Missed;
    public bool IsToday { get; private set; }
    public DateTime? NextStart { get; private set; }
    public string StreakLabel => Reminder.Streak >= 2 ? $"{Reminder.Streak} in a row" : "";
    public bool HasStreak => Reminder.Streak >= 2;
    public string TotalLabel => Reminder.TotalDone == 1 ? "Done once" : Reminder.TotalDone > 1 ? $"Done {Reminder.TotalDone} times" : "";

    public void Replace(Reminder reminder) { Reminder = reminder; Refresh(DateTime.Now, all: true); }

    public void Refresh(DateTime now, bool all = false)
    {
        var state = ReminderSchedule.StateAt(Reminder, now);
        var next = ReminderSchedule.NextStart(Reminder, now);
        var today = DateOnly.FromDateTime(now);
        bool isToday = Reminder.Enabled && (ReminderSchedule.AppliesOn(Reminder, today) || state == ReminderState.Missed);
        string label = state switch
        {
            ReminderState.Off => "Off",
            ReminderState.Due => "Due now",
            ReminderState.Snoozed => "Snoozed until " + ReminderSchedule.FormatClock(TimeOnly.FromDateTime(Reminder.SnoozeUntil!.Value)),
            ReminderState.Done => Reminder.DoneAt is { } at ? "Done " + ReminderSchedule.FormatClock(TimeOnly.FromDateTime(at)) : "Done",
            ReminderState.Skipped => "Skipped",
            ReminderState.Missed => "Missed",
            _ => next is { } n ? ReminderSchedule.DescribeWhen(n, now) : "No more dates"
        };
        bool changed = all || state != State || label != StateLabel || isToday != IsToday || next != NextStart;
        State = state; StateLabel = label; IsToday = isToday; NextStart = next;
        if (!changed) return;
        foreach (var name in new[] { nameof(State), nameof(StateLabel), nameof(IsDue), nameof(IsDone), nameof(CanAct), nameof(IsToday), nameof(NextStart),
            nameof(StreakLabel), nameof(HasStreak), nameof(TotalLabel), nameof(Title), nameof(Note), nameof(HasNote), nameof(Icon), nameof(Schedule),
            nameof(Priority), nameof(IsUrgent), nameof(Enabled) })
            Changed(name);
    }
}

public static class ReminderIcons
{
    public static IReadOnlyList<StyleOption> All { get; } =
    [
        new("bell", "Bell"), new("coffee", "Coffee"), new("water", "Water"), new("pill", "Medicine"), new("food", "Food"),
        new("money", "Money"), new("meeting", "Meeting"), new("call", "Call"), new("stretch", "Stretch"), new("book", "Study"),
        new("plant", "Plant"), new("heart", "Heart"), new("star", "Star"), new("moon", "Sleep"), new("trash", "Chores")
    ];
    public static string Normalize(string? icon) => All.Any(o => o.Value == icon) ? icon! : "bell";
    public static string Resource(string? icon) => "Reminder" + char.ToUpperInvariant(Normalize(icon)[0]) + Normalize(icon)[1..];
}

public sealed partial class MainViewModel
{
    private readonly ObservableCollection<ReminderRow> reminderRows = [];
    private readonly List<Reminder> reminders = [];
    private readonly DispatcherTimer reminderTimer = new() { Interval = TimeSpan.FromSeconds(10) };
    private bool remindersOpen;
    private string reminderFilter = "today";
    private bool remindersLoaded;

    /// <summary>Overridable clock for tests.</summary>
    internal Func<DateTime> Clock { get; set; } = () => DateTime.Now;

    public ICollectionView RemindersView { get; private set; } = null!;
    public IReadOnlyList<Reminder> Reminders => reminders;
    public event Action<ReminderAlert>? ReminderDue;
    public event Action<Reminder, ReminderOutcome>? ReminderResolved;
    public event Action<string>? PeerCameOnline;

    public bool RemindersOpen
    {
        get => remindersOpen;
        set
        {
            if (remindersOpen == value) return;
            remindersOpen = value;
            if (value) { transfersOpen = false; mediaOpen = false; broadcasting = false; groupsOpen = false; RefreshReminderRows(); }
            SectionChanged();
        }
    }
    public bool IsRemindersSection => RemindersOpen;

    public string ReminderFilter
    {
        get => reminderFilter;
        set
        {
            string v = value is "today" or "upcoming" or "all" or "done" ? value : "today";
            if (reminderFilter == v) return;
            reminderFilter = v;
            Changed();
            RemindersView.Refresh();
            Changed(nameof(NoRemindersShown));
        }
    }

    public int RemindersTodayCount => reminderRows.Count(r => r.IsToday);
    public int RemindersUpcomingCount => reminderRows.Count(r => r.Enabled && !r.IsToday && r.NextStart is not null);
    public int RemindersAllCount => reminderRows.Count;
    public int RemindersDoneCount => reminderRows.Count(r => r.IsDone);
    public int RemindersDueCount => reminderRows.Count(r => r.State is ReminderState.Due or ReminderState.Missed);
    public bool HasDueReminders => RemindersDueCount > 0;
    public bool NoRemindersShown => RemindersView?.IsEmpty != false;
    public string ReminderSubtitle => RemindersDueCount switch { 0 => RemindersTodayCount == 1 ? "1 today" : $"{RemindersTodayCount} today", 1 => "1 due", int n => $"{n} due" };
    public Reminder? FindReminder(string id) => reminders.FirstOrDefault(r => r.Id == id);
    public ReminderRow? FindReminderRow(string id) => reminderRows.FirstOrDefault(r => r.Id == id);

    private void InitReminders()
    {
        if (remindersLoaded) return;
        remindersLoaded = true;
        if (store is not null) reminders.AddRange(store.LoadReminders());
        else if (Preview) SeedPreviewReminders();
        foreach (var r in reminders) reminderRows.Add(new ReminderRow(r));
        var view = new ListCollectionView(reminderRows);
        view.Filter = o => o is ReminderRow row && reminderFilter switch
        {
            "upcoming" => row.Enabled && !row.IsToday && row.NextStart is not null,
            "done" => row.IsDone,
            "all" => true,
            _ => row.IsToday
        };
        view.CustomSort = new ReminderRowOrder();
        view.IsLiveSorting = false;
        RemindersView = view;
        reminderTimer.Tick += (_, _) => CheckReminders();
        RefreshReminderRows();
        if (!Preview) reminderTimer.Start();
    }

    private sealed class ReminderRowOrder : System.Collections.IComparer
    {
        private static int Rank(ReminderRow r) => r.State switch
        {
            ReminderState.Due => 0, ReminderState.Missed => 1, ReminderState.Snoozed => 2, ReminderState.Upcoming => 3,
            ReminderState.Done => 4, ReminderState.Skipped => 5, _ => 6
        };
        public int Compare(object? x, object? y)
        {
            if (x is not ReminderRow a || y is not ReminderRow b) return 0;
            int c = Rank(a).CompareTo(Rank(b));
            if (c != 0) return c;
            c = Nullable.Compare(a.NextStart, b.NextStart);
            return c != 0 ? c : string.Compare(a.Title, b.Title, StringComparison.CurrentCultureIgnoreCase);
        }
    }

    internal void RefreshReminderRows()
    {
        if (RemindersView is null) return;
        var now = Clock();
        foreach (var row in reminderRows) row.Refresh(now);
        RemindersView.Refresh();
        foreach (var name in new[] { nameof(RemindersTodayCount), nameof(RemindersUpcomingCount), nameof(RemindersAllCount), nameof(RemindersDoneCount),
            nameof(RemindersDueCount), nameof(HasDueReminders), nameof(NoRemindersShown), nameof(ReminderSubtitle) })
            Changed(name);
        if (RemindersOpen) Changed(nameof(ChatSubtitle));
    }

    /// <summary>Delivers every nudge that is due now. Returns what fired (for tests).</summary>
    internal List<ReminderAlert> CheckReminders()
    {
        var now = Clock();
        var fired = new List<ReminderAlert>();
        foreach (var r in reminders)
        {
            if (ReminderSchedule.Evaluate(r, now) is not { } fire) continue;
            int nag = r.NagOccurrence == ReminderSchedule.Key(fire.Occurrence) ? r.NagCount : 0;
            ReminderSchedule.MarkFired(r, fire, now);
            fired.Add(new ReminderAlert(r, nag, fire.Overdue));
        }
        if (fired.Count > 0) SaveReminders();
        RefreshReminderRows();
        foreach (var alert in fired) ReminderDue?.Invoke(alert);
        return fired;
    }

    public Reminder SaveReminder(Reminder edited)
    {
        edited.Title = edited.Title.Trim();
        edited.Note = edited.Note.Trim();
        edited.Icon = ReminderIcons.Normalize(edited.Icon);
        edited.Repeat = ReminderRepeat.Normalize(edited.Repeat);
        edited.Priority = edited.Priority is "gentle" or "urgent" ? edited.Priority : "normal";
        edited.EveryMinutes = Math.Clamp(edited.EveryMinutes, 0, 24 * 60);
        edited.Days = edited.Days.Where(d => d is >= 0 and <= 6).Distinct().Order().ToList();
        edited.DayOfMonth = Math.Clamp(edited.DayOfMonth, 1, 31);
        int index = reminders.FindIndex(r => r.Id == edited.Id);
        if (index >= 0)
        {
            var old = reminders[index];
            bool scheduleChanged = (old.Repeat, old.Date, string.Join(',', old.Days), old.DayOfMonth, old.Start, old.End) !=
                (edited.Repeat, edited.Date, string.Join(',', edited.Days), edited.DayOfMonth, edited.Start, edited.End);
            if (scheduleChanged) { edited.NagOccurrence = ""; edited.NagCount = 0; edited.SnoozeUntil = null; edited.LastNagAt = null; }
            reminders[index] = edited;
            FindReminderRow(edited.Id)?.Replace(edited);
        }
        else
        {
            reminders.Add(edited);
            reminderRows.Add(new ReminderRow(edited));
        }
        SaveReminders();
        RefreshReminderRows();
        return edited;
    }

    public void DeleteReminder(string id)
    {
        if (FindReminder(id) is not { } r) return;
        reminders.Remove(r);
        if (FindReminderRow(id) is { } row) reminderRows.Remove(row);
        SaveReminders();
        RefreshReminderRows();
        ReminderResolved?.Invoke(r, ReminderOutcome.Deleted);
    }

    public bool CompleteReminder(string id)
    {
        if (FindReminder(id) is not { } r) return false;
        bool ok = ReminderSchedule.Complete(r, Clock());
        SaveReminders();
        RefreshReminderRows();
        if (ok) ReminderResolved?.Invoke(r, ReminderOutcome.Done);
        return ok;
    }

    public void SnoozeReminder(string id, int minutes)
    {
        if (FindReminder(id) is not { } r) return;
        ReminderSchedule.Snooze(r, Clock(), minutes);
        SaveReminders();
        RefreshReminderRows();
        ReminderResolved?.Invoke(r, ReminderOutcome.Snoozed);
    }

    public void SkipReminder(string id)
    {
        if (FindReminder(id) is not { } r) return;
        ReminderSchedule.Skip(r, Clock());
        SaveReminders();
        RefreshReminderRows();
        ReminderResolved?.Invoke(r, ReminderOutcome.Skipped);
    }

    public void UndoReminder(string id)
    {
        if (FindReminder(id) is not { } r) return;
        ReminderSchedule.Undo(r);
        SaveReminders();
        RefreshReminderRows();
    }

    public void SetReminderEnabled(string id, bool enabled)
    {
        if (FindReminder(id) is not { } r || r.Enabled == enabled) return;
        r.Enabled = enabled;
        if (!enabled) r.SnoozeUntil = null;
        SaveReminders();
        FindReminderRow(id)?.Refresh(Clock(), all: true);
        RefreshReminderRows();
        if (!enabled) ReminderResolved?.Invoke(r, ReminderOutcome.Disabled);
    }

    private void SaveReminders()
    {
        if (Preview || store is null) return;
        try { store.SaveReminders(reminders); }
        catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException) { Toast = "Reminders couldn't be saved."; }
    }

    /// <summary>Shows a reminder on the companion without saving or scheduling it.</summary>
    public void PreviewReminder(Reminder r) => ReminderDue?.Invoke(new ReminderAlert(r, 0, false, Test: true));

    private void DisposeReminders() => reminderTimer.Stop();

    private void SeedPreviewReminders()
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        reminders.Add(new Reminder { Id = "coffee", Title = "Drink my coffee", Icon = "coffee", Repeat = ReminderRepeat.Weekdays, Start = "08:00", End = "09:00", EveryMinutes = 10 });
        reminders.Add(new Reminder { Id = "payroll", Title = "Payroll done?", Note = "Submit timesheets before 3 PM", Icon = "money", Repeat = ReminderRepeat.Weekly, Days = [2], Start = "10:00", EveryMinutes = 30, Priority = "urgent", Streak = 6, BestStreak = 6, TotalDone = 14 });
        reminders.Add(new Reminder { Id = "water", Title = "Water break", Icon = "water", Repeat = ReminderRepeat.Daily, Start = "10:00", End = "17:00", EveryMinutes = 60, Priority = "gentle", Streak = 3, BestStreak = 9, TotalDone = 31 });
        reminders.Add(new Reminder { Id = "standup", Title = "Team standup", Icon = "meeting", Repeat = ReminderRepeat.Weekly, Days = [1, 3, 5], Start = "09:30" });
        reminders.Add(new Reminder { Id = "dentist", Title = "Call the dentist", Icon = "call", Repeat = ReminderRepeat.Once, Date = ReminderSchedule.Key(today.AddDays(3)), Start = "14:00" });
        reminders.Add(new Reminder { Id = "rent", Title = "Pay rent", Icon = "money", Repeat = ReminderRepeat.Monthly, DayOfMonth = 1, Start = "09:00", EveryMinutes = 120 });
    }
}
