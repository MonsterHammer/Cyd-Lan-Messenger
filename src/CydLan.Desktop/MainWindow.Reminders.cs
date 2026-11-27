using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CydLan.Core;
using Button = System.Windows.Controls.Button;
using MenuItem = System.Windows.Controls.MenuItem;

namespace CydLan.Desktop;

public partial class MainWindow
{
    private string? editingReminderId;
    private DateOnly editorDate = DateOnly.FromDateTime(DateTime.Now);
    private int editorMonthDay = DateTime.Now.Day;
    private bool loadingReminderEditor;
    private bool reminderEditorReady;
    private bool reminderNarrow;
    private bool reminderEditorOpen;

    private void RemindersSection_SizeChanged(object sender, SizeChangedEventArgs e) => UpdateReminderLayout();

    /// <summary>Wide: list and editor side by side. Narrow: the editor takes over the list while it is open.</summary>
    private void UpdateReminderLayout()
    {
        bool wasNarrow = reminderNarrow;
        reminderNarrow = RemindersSectionGrid.ActualWidth is > 0 and < 700;
        if (reminderNarrow && !wasNarrow) reminderEditorOpen = false; // narrowing shows the list first
        if (!reminderNarrow)
        {
            ReminderGapColumn.Width = new GridLength(18);
            ReminderEditorColumn.Width = new GridLength(RemindersSectionGrid.ActualWidth > 1000 ? 380 : 330);
            Grid.SetColumn(ReminderEditor, 2); Grid.SetColumnSpan(ReminderEditor, 1);
            RemindersList.Visibility = Visibility.Visible;
            ReminderEditor.Visibility = Visibility.Visible;
            ReminderCloseButton.Visibility = Visibility.Collapsed;
            return;
        }
        ReminderGapColumn.Width = new GridLength(0);
        ReminderEditorColumn.Width = new GridLength(0);
        Grid.SetColumn(ReminderEditor, 0); Grid.SetColumnSpan(ReminderEditor, 3);
        RemindersList.Visibility = reminderEditorOpen ? Visibility.Collapsed : Visibility.Visible;
        ReminderEditor.Visibility = reminderEditorOpen ? Visibility.Visible : Visibility.Collapsed;
        ReminderCloseButton.Visibility = Visibility.Visible;
    }

    private void ShowReminderEditor(bool open)
    {
        reminderEditorOpen = open;
        UpdateReminderLayout();
    }

    private void ReminderEditorClose_Click(object sender, RoutedEventArgs e) => ShowReminderEditor(false);

    internal static readonly IReadOnlyList<StyleOption> ReminderRepeatOptions =
    [
        new("today", "Today"), new("date", "On a date"), new(ReminderRepeat.Daily, "Every day"),
        new(ReminderRepeat.Weekdays, "Weekdays"), new(ReminderRepeat.Weekly, "Days of week"), new(ReminderRepeat.Monthly, "Monthly")
    ];
    internal static readonly IReadOnlyList<StyleOption> ReminderDayOptions =
    [
        new("1", "Mon"), new("2", "Tue"), new("3", "Wed"), new("4", "Thu"), new("5", "Fri"), new("6", "Sat"), new("0", "Sun")
    ];
    internal static readonly IReadOnlyList<StyleOption> ReminderEveryOptions =
    [
        new("0", "Once"), new("5", "5 min"), new("10", "10 min"), new("15", "15 min"), new("30", "30 min"), new("60", "1 hour"), new("120", "2 hours")
    ];
    internal static readonly IReadOnlyList<StyleOption> ReminderPriorityOptions =
    [
        new("gentle", "Gentle"), new("normal", "Normal"), new("urgent", "Urgent")
    ];

    private void InitReminderEditor()
    {
        ReminderIconBox.ItemsSource = ReminderIcons.All;
        ReminderRepeatBox.ItemsSource = ReminderRepeatOptions;
        ReminderDaysBox.ItemsSource = ReminderDayOptions;
        ReminderEveryBox.ItemsSource = ReminderEveryOptions;
        ReminderPriorityBox.ItemsSource = ReminderPriorityOptions;
        reminderEditorReady = true;
        LoadReminderEditor(null);
    }

    private void Reminders_Click(object sender, RoutedEventArgs e) => OpenReminders();

    internal void OpenReminders(string? id = null)
    {
        model.RemindersOpen = true;
        if (id is not null) { LoadReminderEditor(model.FindReminder(id)); ShowReminderEditor(true); }
    }

    private void ReminderFilter_Checked(object sender, RoutedEventArgs e)
    {
        if (model is not null && sender is FrameworkElement { Tag: string tag }) model.ReminderFilter = tag;
    }

    private void NewReminder_Click(object sender, RoutedEventArgs e)
    {
        model.RemindersOpen = true;
        LoadReminderEditor(null);
        ShowReminderEditor(true);
        ReminderTitleBox.Focus();
    }

    private static string? IdOf(object sender) => sender is FrameworkElement { Tag: string id } ? id : null;
    private static int MinutesOf(object sender, int fallback)
    {
        object? p = sender switch { MenuItem m => m.CommandParameter, Button b => b.CommandParameter, _ => null };
        return p is string s && int.TryParse(s, out int v) ? v : fallback;
    }

    private void ReminderCard_Click(object sender, MouseButtonEventArgs e)
    {
        if (IdOf(sender) is { } id) { LoadReminderEditor(model.FindReminder(id)); ShowReminderEditor(true); }
    }
    private void ReminderEdit_Click(object sender, RoutedEventArgs e) { if (IdOf(sender) is { } id) { LoadReminderEditor(model.FindReminder(id)); ShowReminderEditor(true); } }
    private void ReminderDone_Click(object sender, RoutedEventArgs e) { if (IdOf(sender) is { } id) model.CompleteReminder(id); }
    private void ReminderSnooze_Click(object sender, RoutedEventArgs e) { if (IdOf(sender) is { } id) model.SnoozeReminder(id, MinutesOf(sender, 10)); }
    private void ReminderSkip_Click(object sender, RoutedEventArgs e) { if (IdOf(sender) is { } id) model.SkipReminder(id); }
    private void ReminderUndo_Click(object sender, RoutedEventArgs e) { if (IdOf(sender) is { } id) model.UndoReminder(id); }
    private void ReminderToggle_Click(object sender, RoutedEventArgs e)
    {
        if (IdOf(sender) is { } id && model.FindReminder(id) is { } r) model.SetReminderEnabled(id, !r.Enabled);
    }
    private void ReminderDelete_Click(object sender, RoutedEventArgs e)
    {
        if (IdOf(sender) is not { } id) return;
        model.DeleteReminder(id);
        if (editingReminderId == id) LoadReminderEditor(null);
    }
    private void ReminderEditorDelete_Click(object sender, RoutedEventArgs e)
    {
        if (editingReminderId is not { } id) return;
        model.DeleteReminder(id);
        LoadReminderEditor(null);
        ShowReminderEditor(false);
    }

    internal void LoadReminderEditor(Reminder? r)
    {
        loadingReminderEditor = true;
        try
        {
            var now = DateTime.Now;
            editingReminderId = r?.Id;
            ReminderEditorTitle.Text = r is null ? "New reminder" : "Edit reminder";
            ReminderDeleteButton.Visibility = r is null ? Visibility.Collapsed : Visibility.Visible;
            ReminderNewButton.Visibility = r is null ? Visibility.Collapsed : Visibility.Visible;
            ReminderTitleBox.Text = r?.Title ?? "";
            ReminderNoteBox.Text = r?.Note ?? "";
            ReminderIconBox.SelectedValue = ReminderIcons.Normalize(r?.Icon);
            string repeat = r is null ? "today" : ReminderRepeat.Normalize(r.Repeat);
            if (repeat == ReminderRepeat.Once)
            {
                editorDate = ReminderSchedule.ParseDate(r!.Date) ?? DateOnly.FromDateTime(now);
                repeat = editorDate == DateOnly.FromDateTime(now) ? "today" : "date";
            }
            else editorDate = DateOnly.FromDateTime(now).AddDays(1);
            editorMonthDay = r is { Repeat: ReminderRepeat.Monthly } ? Math.Clamp(r.DayOfMonth, 1, 31) : now.Day;
            ReminderRepeatBox.SelectedValue = repeat;
            ReminderDaysBox.SelectedItems.Clear();
            var days = r?.Days.Count > 0 ? r.Days : [(int)now.DayOfWeek];
            foreach (var option in ReminderDayOptions.Where(o => days.Contains(int.Parse(o.Value, CultureInfo.InvariantCulture))))
                ReminderDaysBox.SelectedItems.Add(option);
            var defaultStart = TimeOnly.FromDateTime(now.AddMinutes(15 - now.Minute % 15 + 15));
            ReminderStartBox.Text = r?.Start ?? ReminderSchedule.FormatTime(new TimeOnly(defaultStart.Hour, defaultStart.Minute));
            ReminderEndBox.Text = r?.End ?? "";
            ReminderEveryBox.SelectedValue = (r?.EveryMinutes ?? 0).ToString(CultureInfo.InvariantCulture);
            if (ReminderEveryBox.SelectedValue is null) ReminderEveryBox.SelectedValue = "0";
            ReminderPriorityBox.SelectedValue = r?.Priority ?? "normal";
            ReminderErrorText.Visibility = Visibility.Collapsed;
        }
        finally { loadingReminderEditor = false; }
        SyncReminderEditor();
    }

    private string EditorRepeat => ReminderRepeatBox.SelectedValue as string ?? "today";

    private void ReminderRepeat_Changed(object sender, SelectionChangedEventArgs e) => SyncReminderEditor();
    private void ReminderField_Changed(object sender, RoutedEventArgs e) { if (!loadingReminderEditor) SyncReminderEditor(); }

    private void SyncReminderEditor()
    {
        if (loadingReminderEditor || !reminderEditorReady) return;
        string repeat = EditorRepeat;
        ReminderDatePanel.Visibility = repeat == "date" ? Visibility.Visible : Visibility.Collapsed;
        ReminderDaysBox.Visibility = repeat == ReminderRepeat.Weekly ? Visibility.Visible : Visibility.Collapsed;
        ReminderMonthPanel.Visibility = repeat == ReminderRepeat.Monthly ? Visibility.Visible : Visibility.Collapsed;
        ReminderDateText.Text = editorDate.ToString("ddd, MMM d, yyyy", CultureInfo.InvariantCulture);
        ReminderMonthText.Text = "The " + ReminderSchedule.Ordinal(editorMonthDay);
        var (draft, error) = BuildReminderFromEditor(validate: false);
        if (draft is null) { ReminderSummaryText.Text = ""; return; }
        var now = DateTime.Now;
        string summary = ReminderSchedule.Describe(draft);
        if (ReminderSchedule.NextStart(draft, now) is { } next) summary += " · next " + ReminderSchedule.DescribeWhen(next, now);
        else if (error is null && ReminderSchedule.EndOf(draft) is null && draft.Repeat == ReminderRepeat.Once) summary += " · already passed";
        ReminderSummaryText.Text = summary;
    }

    private (Reminder? Reminder, string? Error) BuildReminderFromEditor(bool validate)
    {
        var existing = editingReminderId is { } id ? model.FindReminder(id) : null;
        var r = existing?.Clone() ?? new Reminder();
        r.Title = ReminderTitleBox.Text.Trim();
        r.Note = ReminderNoteBox.Text.Trim();
        r.Icon = ReminderIcons.Normalize(ReminderIconBox.SelectedValue as string);
        string repeat = EditorRepeat;
        r.Repeat = repeat is "today" or "date" ? ReminderRepeat.Once : ReminderRepeat.Normalize(repeat);
        r.Date = repeat == "today" ? ReminderSchedule.Key(DateOnly.FromDateTime(DateTime.Now)) : repeat == "date" ? ReminderSchedule.Key(editorDate) : "";
        r.Days = ReminderDaysBox.SelectedItems.OfType<StyleOption>().Select(o => int.Parse(o.Value, CultureInfo.InvariantCulture)).Order().ToList();
        r.DayOfMonth = editorMonthDay;
        r.EveryMinutes = int.TryParse(ReminderEveryBox.SelectedValue as string, out int every) ? every : 0;
        r.Priority = ReminderPriorityBox.SelectedValue as string ?? "normal";

        var start = ReminderSchedule.ParseTime(ReminderStartBox.Text);
        if (start is null) return (null, "From time not recognized.");
        r.Start = ReminderSchedule.FormatTime(start.Value);
        string endText = ReminderEndBox.Text.Trim();
        if (endText.Length > 0)
        {
            var end = ReminderSchedule.ParseTime(endText);
            if (end is null) return (r, "Until time not recognized.");
            if (end <= start) return (r, "Until must be later than From.");
            r.End = ReminderSchedule.FormatTime(end.Value);
        }
        else r.End = "";
        if (validate && r.Title.Length == 0) return (r, "Add a title.");
        if (r.Repeat == ReminderRepeat.Weekly && r.Days.Count == 0) return (r, "Pick at least one day.");
        return (r, null);
    }

    private void ReminderSave_Click(object sender, RoutedEventArgs e)
    {
        var (r, error) = BuildReminderFromEditor(validate: true);
        if (r is null || error is not null)
        {
            ReminderErrorText.Text = error ?? "Check the reminder details.";
            ReminderErrorText.Visibility = Visibility.Visible;
            return;
        }
        ReminderErrorText.Visibility = Visibility.Collapsed;
        bool isNew = editingReminderId is null;
        var saved = model.SaveReminder(r);
        LoadReminderEditor(saved);
        if (reminderNarrow) ShowReminderEditor(false);
        model.CheckReminders();
    }

    private void ReminderTest_Click(object sender, RoutedEventArgs e)
    {
        var (r, _) = BuildReminderFromEditor(validate: false);
        r ??= new Reminder();
        if (r.Title.Length == 0) r.Title = "Reminder";
        model.PreviewReminder(r);
    }

    private void ReminderQuickTime_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: string tag }) return;
        var now = DateTime.Now;
        loadingReminderEditor = true;
        if (tag == "tomorrow")
        {
            editorDate = DateOnly.FromDateTime(now).AddDays(1);
            ReminderRepeatBox.SelectedValue = "date";
            ReminderStartBox.Text = "09:00";
        }
        else if (int.TryParse(tag, out int minutes))
        {
            var at = now.AddMinutes(minutes);
            editorDate = DateOnly.FromDateTime(at);
            ReminderRepeatBox.SelectedValue = editorDate == DateOnly.FromDateTime(now) ? "today" : "date";
            ReminderStartBox.Text = ReminderSchedule.FormatTime(new TimeOnly(at.Hour, at.Minute));
        }
        ReminderEndBox.Text = "";
        loadingReminderEditor = false;
        SyncReminderEditor();
    }

    private void ReminderDatePrev_Click(object sender, RoutedEventArgs e) { editorDate = editorDate.AddDays(-1); SyncReminderEditor(); }
    private void ReminderDateNext_Click(object sender, RoutedEventArgs e) { editorDate = editorDate.AddDays(1); SyncReminderEditor(); }
    private void ReminderDateWeek_Click(object sender, RoutedEventArgs e) { editorDate = editorDate.AddDays(7); SyncReminderEditor(); }
    private void ReminderMonthPrev_Click(object sender, RoutedEventArgs e) { editorMonthDay = editorMonthDay <= 1 ? 31 : editorMonthDay - 1; SyncReminderEditor(); }
    private void ReminderMonthNext_Click(object sender, RoutedEventArgs e) { editorMonthDay = editorMonthDay >= 31 ? 1 : editorMonthDay + 1; SyncReminderEditor(); }
}
