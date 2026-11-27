using System.Globalization;

namespace CydLan.Core;

/// <summary>A local reminder that the desktop companion delivers. Times are local wall-clock times.</summary>
public sealed class Reminder
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..12];
    public string Title { get; set; } = "";
    public string Note { get; set; } = "";
    public string Icon { get; set; } = "bell";
    /// <summary>once, daily, weekdays, weekly, monthly.</summary>
    public string Repeat { get; set; } = ReminderRepeat.Once;
    /// <summary>yyyy-MM-dd; the day of a one-time reminder.</summary>
    public string Date { get; set; } = "";
    /// <summary>Days for weekly reminders (0 = Sunday … 6 = Saturday).</summary>
    public List<int> Days { get; set; } = [];
    public int DayOfMonth { get; set; } = 1;
    /// <summary>HH:mm.</summary>
    public string Start { get; set; } = "09:00";
    /// <summary>HH:mm, or empty for no end time.</summary>
    public string End { get; set; } = "";
    /// <summary>Minutes between repeated nudges until it is marked done; 0 nudges once.</summary>
    public int EveryMinutes { get; set; }
    /// <summary>gentle, normal, urgent.</summary>
    public string Priority { get; set; } = "normal";
    public bool Enabled { get; set; } = true;
    public DateTime Created { get; set; } = DateTime.Now;

    public string DoneOn { get; set; } = "";
    public string SkippedOn { get; set; } = "";
    public DateTime? DoneAt { get; set; }
    public DateTime? SnoozeUntil { get; set; }
    public DateTime? LastNagAt { get; set; }
    public string NagOccurrence { get; set; } = "";
    public int NagCount { get; set; }
    public int Streak { get; set; }
    public int BestStreak { get; set; }
    public int TotalDone { get; set; }
    public string LastCountedOccurrence { get; set; } = "";

    public Reminder Clone() { var r = (Reminder)MemberwiseClone(); r.Days = [.. Days]; return r; }
}

public static class ReminderRepeat
{
    public const string Once = "once", Daily = "daily", Weekdays = "weekdays", Weekly = "weekly", Monthly = "monthly";
    public static string Normalize(string? value) => value is Daily or Weekdays or Weekly or Monthly ? value : Once;
}

public enum ReminderState { Off, Upcoming, Due, Snoozed, Done, Skipped, Missed }

public readonly record struct ReminderFire(Reminder Reminder, DateOnly Occurrence, int Nag, bool Overdue);

/// <summary>Pure schedule math for reminders: which days apply, when a nudge is due, streaks, and labels.</summary>
public static class ReminderSchedule
{
    public const string DateFormat = "yyyy-MM-dd";
    private const int SearchDays = 400;

    public static string Key(DateOnly d) => d.ToString(DateFormat, CultureInfo.InvariantCulture);
    public static DateOnly? ParseDate(string? s) =>
        DateOnly.TryParseExact(s, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : null;

    /// <summary>Parses 8, 8:30, 0830, 8am, 2:30 pm, 14:05.</summary>
    public static TimeOnly? ParseTime(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        string s = text.Trim().ToLowerInvariant().Replace(" ", "").Replace(".", ":");
        bool pm = false, am = false;
        if (s.EndsWith("pm") || s.EndsWith("p")) { pm = true; s = s.TrimEnd('m').TrimEnd('p'); }
        else if (s.EndsWith("am") || s.EndsWith("a")) { am = true; s = s.TrimEnd('m').TrimEnd('a'); }
        int hour, minute = 0;
        if (s.Contains(':'))
        {
            var parts = s.Split(':');
            if (parts.Length != 2 || !int.TryParse(parts[0], out hour) || !int.TryParse(parts[1], out minute)) return null;
        }
        else if (s.Length is 3 or 4 && int.TryParse(s, out int packed)) { hour = packed / 100; minute = packed % 100; }
        else if (!int.TryParse(s, out hour)) return null;
        if (pm || am)
        {
            if (hour is < 1 or > 12) return null;
            if (pm && hour != 12) hour += 12;
            if (am && hour == 12) hour = 0;
        }
        if (hour is < 0 or > 23 || minute is < 0 or > 59) return null;
        return new TimeOnly(hour, minute);
    }

    public static string FormatTime(TimeOnly t) => t.ToString("HH:mm", CultureInfo.InvariantCulture);
    public static TimeOnly StartOf(Reminder r) => ParseTime(r.Start) ?? new TimeOnly(9, 0);
    public static TimeOnly? EndOf(Reminder r) => ParseTime(r.End) is { } e && e > StartOf(r) ? e : null;

    public static bool AppliesOn(Reminder r, DateOnly day)
    {
        switch (ReminderRepeat.Normalize(r.Repeat))
        {
            case ReminderRepeat.Daily: return true;
            case ReminderRepeat.Weekdays: return day.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday);
            case ReminderRepeat.Weekly: return r.Days.Contains((int)day.DayOfWeek);
            case ReminderRepeat.Monthly:
                int dim = DateTime.DaysInMonth(day.Year, day.Month);
                return day.Day == Math.Min(Math.Clamp(r.DayOfMonth, 1, 31), dim);
            default: return ParseDate(r.Date) == day;
        }
    }

    public static DateTime WindowStart(Reminder r, DateOnly day) => day.ToDateTime(StartOf(r));
    /// <summary>Nudging stops at the end time, or at midnight when there is none.</summary>
    public static DateTime WindowEnd(Reminder r, DateOnly day) => EndOf(r) is { } e ? day.ToDateTime(e) : day.AddDays(1).ToDateTime(TimeOnly.MinValue);

    /// <summary>The occurrence that is currently relevant: today's if it applies, or an unfinished past one-time reminder.</summary>
    public static DateOnly? CurrentOccurrence(Reminder r, DateTime now)
    {
        var today = DateOnly.FromDateTime(now);
        if (AppliesOn(r, today)) return today;
        if (ReminderRepeat.Normalize(r.Repeat) == ReminderRepeat.Once && ParseDate(r.Date) is { } d && d < today) return d;
        return null;
    }

    public static ReminderState StateAt(Reminder r, DateTime now)
    {
        if (!r.Enabled) return ReminderState.Off;
        if (CurrentOccurrence(r, now) is not { } occ) return ReminderState.Upcoming;
        string key = Key(occ);
        if (r.DoneOn == key) return ReminderState.Done;
        if (r.SkippedOn == key) return ReminderState.Skipped;
        var today = DateOnly.FromDateTime(now);
        if (occ < today) return ReminderState.Missed;
        if (now < WindowStart(r, occ)) return ReminderState.Upcoming;
        if (now >= WindowEnd(r, occ) && EndOf(r) is not null) return ReminderState.Missed;
        if (r.SnoozeUntil is { } s && s > now) return ReminderState.Snoozed;
        return ReminderState.Due;
    }

    /// <summary>Returns a nudge when one should be delivered now. Does not change the reminder; call <see cref="MarkFired"/>.</summary>
    public static ReminderFire? Evaluate(Reminder r, DateTime now)
    {
        if (!r.Enabled || CurrentOccurrence(r, now) is not { } occ) return null;
        string key = Key(occ);
        if (r.DoneOn == key || r.SkippedOn == key) return null;
        var today = DateOnly.FromDateTime(now);
        bool sameOcc = r.NagOccurrence == key;
        if (occ < today)
            return sameOcc ? null : new ReminderFire(r, occ, 0, true); // missed one-time reminder: say so once
        var start = WindowStart(r, occ);
        if (now < start || now >= WindowEnd(r, occ)) return null;
        if (r.SnoozeUntil is { } snooze)
        {
            if (snooze > now) return null;
            return new ReminderFire(r, occ, sameOcc ? r.NagCount : 0, false);
        }
        if (!sameOcc)
        {
            // Opened late: a reminder whose window already passed far beyond its first nudge is still delivered once.
            return new ReminderFire(r, occ, 0, now - start > TimeSpan.FromMinutes(Math.Max(15, r.EveryMinutes)));
        }
        if (r.EveryMinutes > 0 && r.LastNagAt is { } last && now - last >= TimeSpan.FromMinutes(r.EveryMinutes))
            return new ReminderFire(r, occ, r.NagCount, false);
        return null;
    }

    public static void MarkFired(Reminder r, ReminderFire fire, DateTime now)
    {
        string key = Key(fire.Occurrence);
        if (r.NagOccurrence != key) { r.NagOccurrence = key; r.NagCount = 0; }
        r.NagCount++;
        r.LastNagAt = now;
        r.SnoozeUntil = null;
    }

    public static void Snooze(Reminder r, DateTime now, int minutes)
    {
        r.SnoozeUntil = now.AddMinutes(Math.Clamp(minutes, 1, 24 * 60));
        if (CurrentOccurrence(r, now) is { } occ && r.NagOccurrence != Key(occ)) { r.NagOccurrence = Key(occ); r.NagCount = 0; }
    }

    /// <summary>Marks the current occurrence done and updates the streak. Returns false when nothing was pending.</summary>
    public static bool Complete(Reminder r, DateTime now)
    {
        var occ = CurrentOccurrence(r, now) ?? DateOnly.FromDateTime(now);
        string key = Key(occ);
        if (r.DoneOn == key) return false;
        r.DoneOn = key;
        r.DoneAt = now;
        r.SnoozeUntil = null;
        if (r.LastCountedOccurrence != key)
        {
            var previous = PreviousOccurrence(r, occ);
            string? prevKey = previous is { } p ? Key(p) : null;
            bool chained = prevKey is not null && r.LastCountedOccurrence == prevKey;
            r.Streak = chained ? r.Streak + 1 : 1;
            r.BestStreak = Math.Max(r.BestStreak, r.Streak);
            r.TotalDone++;
            r.LastCountedOccurrence = key;
        }
        return true;
    }

    /// <summary>Skipping keeps the streak alive: the skipped occurrence counts as a link in the chain.</summary>
    public static void Skip(Reminder r, DateTime now)
    {
        var occ = CurrentOccurrence(r, now) ?? DateOnly.FromDateTime(now);
        string key = Key(occ);
        r.SkippedOn = key;
        r.SnoozeUntil = null;
        var previous = PreviousOccurrence(r, occ);
        if (previous is { } p && r.LastCountedOccurrence == Key(p)) r.LastCountedOccurrence = key;
    }

    public static void Undo(Reminder r)
    {
        if (r.DoneOn.Length > 0 && r.LastCountedOccurrence == r.DoneOn)
        {
            r.TotalDone = Math.Max(0, r.TotalDone - 1);
            r.Streak = Math.Max(0, r.Streak - 1);
            r.LastCountedOccurrence = "";
        }
        r.DoneOn = "";
        r.SkippedOn = "";
        r.DoneAt = null;
    }

    public static DateOnly? PreviousOccurrence(Reminder r, DateOnly before)
    {
        if (ReminderRepeat.Normalize(r.Repeat) == ReminderRepeat.Once) return null;
        for (int i = 1; i <= SearchDays; i++)
        {
            var d = before.AddDays(-i);
            if (AppliesOn(r, d)) return d;
        }
        return null;
    }

    /// <summary>The next time a nudge will begin, or null when the reminder never fires again.</summary>
    public static DateTime? NextStart(Reminder r, DateTime now)
    {
        if (!r.Enabled) return null;
        var today = DateOnly.FromDateTime(now);
        for (int i = 0; i <= SearchDays; i++)
        {
            var d = today.AddDays(i);
            if (!AppliesOn(r, d)) continue;
            string key = Key(d);
            if (r.DoneOn == key || r.SkippedOn == key) continue;
            var start = WindowStart(r, d);
            if (start > now) return start;
            if (i == 0 && now < WindowEnd(r, d)) return start;
        }
        return null;
    }

    private static readonly string[] ShortDays = ["Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat"];

    public static string DescribeDays(Reminder r)
    {
        switch (ReminderRepeat.Normalize(r.Repeat))
        {
            case ReminderRepeat.Daily: return "Every day";
            case ReminderRepeat.Weekdays: return "Weekdays";
            case ReminderRepeat.Weekly:
                var days = r.Days.Distinct().Where(d => d is >= 0 and <= 6).OrderBy(d => (d + 6) % 7).ToList();
                if (days.Count == 0) return "No days";
                if (days.Count == 7) return "Every day";
                if (days.Count == 2 && days.Contains(0) && days.Contains(6)) return "Weekends";
                return days.Count == 1 ? "Every " + CultureInfo.InvariantCulture.DateTimeFormat.DayNames[days[0]] : string.Join(", ", days.Select(d => ShortDays[d]));
            case ReminderRepeat.Monthly: return "Monthly on the " + Ordinal(Math.Clamp(r.DayOfMonth, 1, 31));
            default:
                return ParseDate(r.Date) is { } d ? d.ToString("ddd, MMM d", CultureInfo.InvariantCulture) : "Once";
        }
    }

    public static string DescribeTime(Reminder r)
    {
        string time = FormatClock(StartOf(r));
        if (EndOf(r) is { } e) time += "–" + FormatClock(e);
        if (r.EveryMinutes > 0) time += " · every " + DescribeInterval(r.EveryMinutes);
        return time;
    }

    public static string Describe(Reminder r) => DescribeDays(r) + " · " + DescribeTime(r);

    public static string DescribeInterval(int minutes) => minutes switch
    {
        < 60 => minutes + " min",
        60 => "hour",
        _ when minutes % 60 == 0 => minutes / 60 + " hours",
        _ => $"{minutes / 60} h {minutes % 60} min"
    };

    public static string FormatClock(TimeOnly t) => t.ToString(t.Minute == 0 ? "h tt" : "h:mm tt", CultureInfo.InvariantCulture);

    public static string Ordinal(int n) => n + ((n % 100) is 11 or 12 or 13 ? "th" : (n % 10) switch { 1 => "st", 2 => "nd", 3 => "rd", _ => "th" });

    public static string DescribeWhen(DateTime when, DateTime now)
    {
        var day = DateOnly.FromDateTime(when);
        var today = DateOnly.FromDateTime(now);
        string clock = FormatClock(TimeOnly.FromDateTime(when));
        if (day == today) return "Today " + clock;
        if (day == today.AddDays(1)) return "Tomorrow " + clock;
        if (day < today.AddDays(7)) return when.ToString("dddd", CultureInfo.InvariantCulture) + " " + clock;
        return when.ToString("MMM d", CultureInfo.InvariantCulture) + " " + clock;
    }
}
