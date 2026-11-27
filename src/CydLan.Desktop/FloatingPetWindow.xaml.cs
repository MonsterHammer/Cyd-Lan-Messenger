using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using CydLan.Core;
using Forms = System.Windows.Forms;
using Point = System.Windows.Point;

namespace CydLan.Desktop;

/// <summary>Always-on-top desktop companion pet toggled from the system tray (hidden from taskbar, nav rail, and Alt+Tab).</summary>
public partial class FloatingPetWindow : Window
{
    // Saved positions describe the original 240×198 window, so older settings keep the pet where it was.
    private const double SavedWidth = 240, SavedHeight = 198;

    private readonly MainViewModel model;
    private readonly Action toggleQuickChat;
    private readonly Action openMainWindow;
    private readonly Func<bool> isQuickChatOpen;
    private readonly Action? customizePet;
    private readonly Action<string?>? openReminders;
    private readonly DispatcherTimer animTimer = new(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(33) };
    private readonly DispatcherTimer speechTimer = new() { Interval = TimeSpan.FromSeconds(4.5) };
    private readonly DispatcherTimer mindTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly DispatcherTimer gentleReminderTimer = new() { Interval = TimeSpan.FromSeconds(25) };
    private string? speechPeerId;
    private bool speechIsLine;

    private bool dragging;
    private bool movedPastThreshold;
    private System.Drawing.Point dragStartCursor;
    private Point dragStartWindow;

    private double breathPhase;
    private double stepPhase;
    private double blinkProgress;
    private double blinkElapsed;
    private double nextBlinkDelay = 3.2;
    private bool blinkingDown;
    private double hopProgress;
    private double facing = 1;
    private bool hovered;
    private bool excited;
    private DateTime excitedUntilUtc = DateTime.MinValue;
    private int roamPauseTicks;

    // Personality.
    private readonly PetMind mind = new();
    private string currentMood = PetMood.Neutral;
    private bool mindAsleep;
    private bool followCursor;
    private bool faceCursor;
    private DateTime zoomiesUntilUtc = DateTime.MinValue;
    private DateTime shownAtUtc = DateTime.UtcNow;

    // Petting (rubbing the cursor over the pet) and shaking (dragging back and forth).
    private Point? lastPetPoint;
    private double petDistance;
    private DateTime petWindowStartUtc = DateTime.MinValue;
    private DateTime lastPetPokeUtc = DateTime.MinValue;
    private double lastDragX;
    private double lastDragVx;
    private int dragReversals;
    private DateTime reversalWindowStartUtc = DateTime.MinValue;

    // Reminders.
    private readonly List<ReminderAlert> reminderQueue = [];
    private ReminderAlert? currentReminder;
    private double shakeClock;

    public FloatingPetWindow(
        MainViewModel model,
        Action toggleQuickChat,
        Action openMainWindow,
        Func<bool> isQuickChatOpen,
        Action? customizePet = null,
        Action<string?>? openReminders = null)
    {
        InitializeComponent();
        this.model = model;
        this.toggleQuickChat = toggleQuickChat;
        this.openMainWindow = openMainWindow;
        this.isQuickChatOpen = isQuickChatOpen;
        this.customizePet = customizePet;
        this.openReminders = openReminders;
        DataContext = model;
        mind.SetTraits(model.PetTraitList);
        ApplyPetSize();

        SourceInitialized += (_, _) => PopupHost.MakeToolWindow(this);
        Loaded += (_, _) => RestoreOrPlaceDefault();
        IsVisibleChanged += (_, _) => { if (IsVisible) shownAtUtc = DateTime.UtcNow; UpdateTimerState(); };
        Closed += (_, _) =>
        {
            animTimer.Stop(); speechTimer.Stop(); mindTimer.Stop(); gentleReminderTimer.Stop();
            model.PropertyChanged -= ModelChanged;
        };

        animTimer.Tick += (_, _) => StepAnimation();
        speechTimer.Tick += (_, _) => HideSpeech();
        mindTimer.Tick += (_, _) => ApplyMind(mind.Tick(1.0, DateTime.Now, hovered, model.Settings.Status == "dnd"));
        gentleReminderTimer.Tick += (_, _) => { gentleReminderTimer.Stop(); if (currentReminder?.Reminder.Priority == "gentle") CollapseReminder(); };
        PetContextMenu.Opened += (_, _) => SyncContextMenu();
        model.PropertyChanged += ModelChanged;
    }

    private void ModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.PetTraitList)) { mind.SetTraits(model.PetTraitList); ApplyMindNow(); }
        else if (e.PropertyName == nameof(MainViewModel.PetAppearance)) ApplyPetSize();
    }

    private const double BaseHitWidth = 136, BaseHitHeight = 124, BubbleRoom = 172;
    private double appliedPetSize;

    /// <summary>Grows the window, hit area and bubble anchors with the companion size, keeping its feet in place.</summary>
    private void ApplyPetSize()
    {
        double s = FloatingPetStyle.ClampSize(model.PetAppearance.Size);
        if (Math.Abs(s - appliedPetSize) < .001) return;
        bool reposition = appliedPetSize > 0 && IsLoaded;
        appliedPetSize = s;
        PetHitArea.Width = BaseHitWidth * s;
        PetHitArea.Height = BaseHitHeight * s;
        PetRow.Height = new GridLength(BaseHitHeight * s + 4);
        UnreadBadge.Margin = new Thickness(0, 62 * s, 24 * s, 0);
        UnreadBadge.LayoutTransform = Math.Abs(s - 1) < .001 ? Transform.Identity : new ScaleTransform(s, s);
        SpeechBubble.Margin = new Thickness(6, 0, 6, -36 * s);
        ReminderBubble.Margin = new Thickness(4, 0, 4, -36 * s);
        Width = Math.Max(260, BaseHitWidth * s + 124);
        Height = BubbleRoom + BaseHitHeight * s + 4;
        // Place from the saved spot, not the clamped one, so growing and shrinking back returns home.
        if (reposition) MoveToSavedSpot();
    }

    public double AppliedPetSize => appliedPetSize;

    public PetMind Mind => mind;
    public string CurrentMood => currentMood;
    public ReminderAlert? CurrentReminder => currentReminder;
    public int PendingReminderCount => reminderQueue.Count;

    public Rect PetScreenRectDip()
    {
        double left = double.IsFinite(Left) ? Left : 0;
        double top = double.IsFinite(Top) ? Top : 0;
        return new Rect(left + (Width - PetHitArea.Width) * .5, top + Height - PetHitArea.Height - 4, PetHitArea.Width, PetHitArea.Height);
    }

    public void ReactToIncoming(string sender, string previewText, string? peerId)
    {
        if (currentReminder is null)
        {
            speechPeerId = peerId;
            speechIsLine = false;
            SpeechSender.Text = sender;
            SpeechSender.Visibility = Visibility.Visible;
            SpeechText.Text = string.IsNullOrWhiteSpace(previewText) ? "New message" : previewText.ReplaceLineEndings(" ");
            SpeechBubble.Visibility = Visibility.Visible;
            speechTimer.Stop();
            speechTimer.Interval = TimeSpan.FromSeconds(4.5);
            speechTimer.Start();
        }

        mind.Poke(PetPoke.Wake);
        hopProgress = .02;
        excited = true;
        excitedUntilUtc = DateTime.UtcNow.AddSeconds(3.2);
        if (mind.MessageLine(sender) is { } line) Dispatcher.BeginInvoke(DispatcherPriority.Background, () => mind.Say(line));
        UpdateVisual();
    }

    public void ReactToPeerOnline(string name)
    {
        // Ignore the burst of peers that appear right after startup.
        if ((DateTime.UtcNow - shownAtUtc).TotalSeconds < 20 || string.IsNullOrWhiteSpace(name)) return;
        if (mind.PeerOnlineLine(name) is { } line) { mind.Say(line); ApplyMindNow(); }
    }

    public void TriggerHop()
    {
        hopProgress = .02;
        excited = true;
        excitedUntilUtc = DateTime.UtcNow.AddSeconds(1.8);
        UpdateVisual();
    }

    public void SyncPositionAfterQuickChatMove(double deltaX, double deltaY)
    {
        _ = (deltaX, deltaY);
    }

    private void HideSpeech()
    {
        speechTimer.Stop();
        SpeechBubble.Visibility = Visibility.Collapsed;
    }

    /// <summary>Shows a personality line in the speech bubble (never over a reminder).</summary>
    public void ShowLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line) || currentReminder is not null) return;
        speechPeerId = null;
        speechIsLine = true;
        SpeechSender.Text = "";
        SpeechSender.Visibility = Visibility.Collapsed;
        SpeechText.Text = line;
        SpeechBubble.Visibility = Visibility.Visible;
        speechTimer.Stop();
        speechTimer.Interval = TimeSpan.FromSeconds(Math.Clamp(2.4 + line.Length * .06, 3, 6));
        speechTimer.Start();
    }

    // ── Reminders ─────────────────────────────────────────────────────────

    public void ShowReminder(ReminderAlert alert)
    {
        reminderQueue.RemoveAll(a => a.Reminder.Id == alert.Reminder.Id);
        reminderQueue.Insert(0, alert);
        PresentReminder(alert);
    }

    private void PresentReminder(ReminderAlert alert)
    {
        currentReminder = alert;
        HideSpeech();
        var r = alert.Reminder;
        mind.Poke(PetPoke.Wake);
        mind.ReminderNag = alert.Nag;
        mind.ReminderUrgent = r.Priority == "urgent";
        ReminderTitleText.Text = string.IsNullOrWhiteSpace(r.Title) ? "Reminder" : r.Title;
        ReminderWhenText.Text = alert.Overdue ? "Missed · " + ReminderSchedule.DescribeTime(r) : ReminderSchedule.DescribeTime(r);
        ReminderLineText.Text = mind.ReminderLine(r.Title, alert.Nag, alert.Overdue, r.Priority);
        ReminderNoteText.Text = r.Note;
        ReminderNoteText.Visibility = r.Note.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        ReminderGlyph.Data = TryFindResource(ReminderIcons.Resource(r.Icon)) as Geometry;
        int others = reminderQueue.Count - 1;
        ReminderQueueBadge.Visibility = others > 0 ? Visibility.Visible : Visibility.Collapsed;
        ReminderQueueText.Text = "+" + others;
        if (r.Priority == "urgent" || alert.Nag >= 2) ReminderBubble.SetResourceReference(Border.BorderBrushProperty, "Danger");
        else ReminderBubble.SetResourceReference(Border.BorderBrushProperty, "Accent");
        ReminderBubble.Visibility = Visibility.Visible;
        gentleReminderTimer.Stop();
        if (r.Priority == "gentle") gentleReminderTimer.Start();
        hopProgress = .02;
        excited = true;
        excitedUntilUtc = DateTime.UtcNow.AddSeconds(2.2);
        ApplyMindNow();
    }

    /// <summary>Called when a reminder was handled anywhere (here, in the main window, or deleted).</summary>
    public void ResolveReminder(Reminder r, ReminderOutcome outcome)
    {
        bool wasCurrent = currentReminder?.Reminder.Id == r.Id;
        bool wasQueued = reminderQueue.RemoveAll(a => a.Reminder.Id == r.Id) > 0;
        if (!wasCurrent && !wasQueued && outcome != ReminderOutcome.Done) return;
        if (wasCurrent) CollapseReminder(keepQueue: true);
        string? line = outcome switch
        {
            ReminderOutcome.Done => mind.DoneLine(r.Streak),
            ReminderOutcome.Snoozed when wasCurrent || wasQueued => mind.SnoozeLine(r.SnoozeUntil is { } until ? Math.Max(1, (int)Math.Round((until - DateTime.Now).TotalMinutes)) : 10),
            _ => null
        };
        if (outcome == ReminderOutcome.Done) { hopProgress = .02; excited = true; excitedUntilUtc = DateTime.UtcNow.AddSeconds(3); }
        if (reminderQueue.Count > 0) PresentReminder(reminderQueue[0]);
        else
        {
            mind.ReminderNag = -1;
            if (line is not null) mind.Say(line, force: true);
            ApplyMindNow();
        }
    }

    private void CollapseReminder(bool keepQueue = false)
    {
        gentleReminderTimer.Stop();
        ReminderBubble.Visibility = Visibility.Collapsed;
        ReminderShake.X = 0;
        if (!keepQueue && currentReminder is { } c) reminderQueue.RemoveAll(a => a.Reminder.Id == c.Reminder.Id);
        currentReminder = null;
        if (!keepQueue)
        {
            if (reminderQueue.Count > 0) { PresentReminder(reminderQueue[0]); return; }
            mind.ReminderNag = -1;
            ApplyMindNow();
        }
    }

    private void ReminderDone_Click(object sender, RoutedEventArgs e)
    {
        if (currentReminder is not { } alert) return;
        if (alert.Test || !model.CompleteReminder(alert.Reminder.Id))
            ResolveReminder(alert.Reminder, ReminderOutcome.Done);
    }

    private void ReminderSnooze_Click(object sender, RoutedEventArgs e) => SnoozeCurrent(10);

    private void ReminderSnoozeFor_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: string tag } && int.TryParse(tag, out int minutes)) SnoozeCurrent(minutes);
    }

    private void SnoozeCurrent(int minutes)
    {
        if (currentReminder is not { } alert) return;
        if (alert.Test)
        {
            var copy = alert.Reminder.Clone();
            copy.SnoozeUntil = DateTime.Now.AddMinutes(minutes);
            ResolveReminder(copy, ReminderOutcome.Snoozed);
        }
        else model.SnoozeReminder(alert.Reminder.Id, minutes);
    }

    private void ReminderSkip_Click(object sender, RoutedEventArgs e)
    {
        if (currentReminder is not { } alert) return;
        if (alert.Test) ResolveReminder(alert.Reminder, ReminderOutcome.Skipped);
        else model.SkipReminder(alert.Reminder.Id);
    }

    private void ReminderMore_Click(object sender, RoutedEventArgs e)
    {
        ReminderMoreMenu.PlacementTarget = ReminderMoreButton;
        ReminderMoreMenu.IsOpen = true;
    }

    private void ReminderOpen_Click(object sender, RoutedEventArgs e) => (openReminders ?? (_ => openMainWindow()))(currentReminder?.Reminder.Id);
    private void ReminderDismiss_Click(object sender, RoutedEventArgs e) => CollapseReminder();

    // ── Personality ──────────────────────────────────────────────────────

    private void ApplyMindNow() => ApplyMind(mind.Tick(0, DateTime.Now, hovered, model.Settings.Status == "dnd"));

    private void ApplyMind(PetMindOutput o)
    {
        currentMood = o.Mood;
        mindAsleep = o.Asleep;
        followCursor = o.FollowCursor;
        faceCursor = o.FaceCursor;
        if (o.Zoomies && DateTime.UtcNow > zoomiesUntilUtc) zoomiesUntilUtc = DateTime.UtcNow.AddSeconds(5);
        if (o.AngryHop && hopProgress == 0 && !dragging) hopProgress = .02;
        if (o.Line is { } line) ShowLine(line);
        if (currentReminder is not null && currentMood is PetMood.Angry or PetMood.Fainted)
            ReminderBubble.SetResourceReference(Border.BorderBrushProperty, "Danger");
        UpdateVisual();
    }

    private void SyncContextMenu()
    {
        RoamMenuItem.IsChecked = model.FloatingLanRoam;
        MoodMenuItem.Header = MoodLabel(currentMood, mindAsleep);
        SleepMenuItem.Header = mindAsleep ? "Wake up" : "Tuck in";
    }

    internal static string MoodLabel(string mood, bool asleep) => asleep ? "Sleeping" : mood switch
    {
        PetMood.Happy => "Feeling happy",
        PetMood.Love => "Feeling loved",
        PetMood.Angry => "Feeling angry",
        PetMood.Sad => "Feeling lonely",
        PetMood.Bored => "Bored",
        PetMood.Alert => "On reminder duty",
        PetMood.Shy => "Feeling shy",
        PetMood.Dizzy => "Dizzy",
        PetMood.Hungry => "Hungry",
        PetMood.Fainted => "Fainted",
        PetMood.Celebrate => "Celebrating",
        PetMood.Drowsy => "Drowsy",
        PetMood.Sleeping => "Sleeping",
        PetMood.Curious => "Curious",
        PetMood.Smug => "Feeling smug",
        _ => "Feeling fine"
    };

    private void PetPet_Click(object sender, RoutedEventArgs e) { mind.Poke(PetPoke.Pet); hopProgress = .02; ApplyMindNow(); }
    private void PetFeed_Click(object sender, RoutedEventArgs e) { mind.Poke(PetPoke.Feed); hopProgress = .02; ApplyMindNow(); }
    private void PetPlay_Click(object sender, RoutedEventArgs e) { mind.Poke(PetPoke.Play); zoomiesUntilUtc = DateTime.UtcNow.AddSeconds(5.5); hopProgress = .02; ApplyMindNow(); }
    private void PetSleep_Click(object sender, RoutedEventArgs e)
    {
        mind.Poke(mindAsleep ? PetPoke.Wake : PetPoke.Tuck);
        ApplyMindNow();
    }
    private void Reminders_Click(object sender, RoutedEventArgs e) => (openReminders ?? (_ => openMainWindow()))(null);

    // ── Placement & animation ────────────────────────────────────────────

    private void RestoreOrPlaceDefault()
    {
        MoveToSavedSpot();
        UpdateVisual();
        UpdateTimerState();
    }

    private void MoveToSavedSpot()
    {
        var work = CurrentWorkAreaDip();
        double targetLeft = model.Settings.FloatingLanLeft is { } l && double.IsFinite(l)
            ? l - (Width - SavedWidth) * .5
            : work.Right - Width + 38;
        double targetTop = model.Settings.FloatingLanTop is { } t && double.IsFinite(t)
            ? t - (Height - SavedHeight)
            : work.Bottom - Height - 10;
        MoveClamped(targetLeft, targetTop);
    }

    private void SavePosition() => model.UpdateFloatingState(left: Left + (Width - SavedWidth) * .5, top: Top + (Height - SavedHeight));

    private void UpdateTimerState()
    {
        if (IsVisible)
        {
            if (!mindTimer.IsEnabled) mindTimer.Start();
        }
        else mindTimer.Stop();

        if (IsVisible && SystemParameters.ClientAreaAnimation)
        {
            if (!animTimer.IsEnabled) animTimer.Start();
        }
        else
        {
            animTimer.Stop();
            UpdateVisual();
        }
    }

    private bool Fainted => currentMood == PetMood.Fainted;
    private bool Zoomies => DateTime.UtcNow < zoomiesUntilUtc;

    private void StepAnimation()
    {
        const double dt = .033;
        breathPhase = (breathPhase + dt * 2.3) % (Math.PI * 20);

        if (excited && DateTime.UtcNow >= excitedUntilUtc)
            excited = false;

        if (hopProgress > 0)
        {
            hopProgress += dt * (currentMood == PetMood.Angry ? 2.8 : 2.1);
            if (hopProgress >= 1.0) hopProgress = 0;
        }

        // Natural blinking every ~2.5 - 5 seconds.
        blinkElapsed += dt;
        if (blinkProgress == 0 && blinkElapsed >= nextBlinkDelay)
        {
            blinkElapsed = 0;
            blinkingDown = true;
            blinkProgress = .25;
            nextBlinkDelay = 2.6 + (Random.Shared.NextDouble() * 2.4);
        }
        else if (blinkProgress > 0)
        {
            blinkProgress += (blinkingDown ? 1 : -1) * dt * 11.0;
            if (blinkProgress >= 1.0) { blinkProgress = 1.0; blinkingDown = false; }
            else if (blinkProgress <= 0) blinkProgress = 0;
        }

        // Shake an ignored or urgent reminder bubble in short bursts.
        shakeClock += dt;
        if (currentReminder is { } alert && ReminderBubble.IsVisible && (alert.Nag >= 2 || alert.Reminder.Priority == "urgent" || currentMood == PetMood.Angry))
            ReminderShake.X = (shakeClock % 2.2) < .45 ? Math.Sin(shakeClock * 60) * 2.6 : 0;
        else if (ReminderShake.X != 0) ReminderShake.X = 0;

        string vibe = model.PetAppearance.Vibe;
        bool busy = dragging || hovered || isQuickChatOpen() || Fainted || mindAsleep || IsSleepy();
        bool zoom = Zoomies;
        Point? cursor = followCursor || faceCursor ? CursorDip() : null;
        double centerX = Left + Width * .5;

        bool walking = !busy && (model.FloatingLanRoam || zoom || (followCursor && cursor is { } c0 && Math.Abs(c0.X - centerX) > 150));
        if (walking)
        {
            var work = WorkAreaDipForCurrentWindow();
            double minLeft = work.Left - (Width - PetHitArea.Width) * .5 + 8;
            double maxLeft = work.Right - Width + (Width - PetHitArea.Width) * .5 - 8;
            if (followCursor && !zoom && cursor is { } c && Math.Abs(c.X - centerX) > 150)
            {
                // Clingy: scoot toward the cursor along the current perch.
                facing = c.X > centerX ? 1 : -1;
                roamPauseTicks = 0;
                Left = Math.Clamp(Left + facing * 1.5, minLeft, Math.Max(minLeft, maxLeft));
                stepPhase = (stepPhase + dt * 10) % (Math.PI * 2);
            }
            else if (roamPauseTicks > 0 && !zoom)
            {
                roamPauseTicks--;
                stepPhase = 0;
            }
            else
            {
                double walkSpeed = zoom ? 2.8 : vibe == "zoomies" ? 1.65 : vibe == "calm" ? .90 : vibe == "sleepy" ? .70 : 1.15;
                double stepRate = zoom ? 15.0 : vibe == "zoomies" ? 12.0 : 9.5;
                double nextLeft = Left + facing * walkSpeed;
                if (nextLeft <= minLeft) { nextLeft = minLeft; facing = 1; roamPauseTicks = zoom ? 0 : vibe == "zoomies" ? 20 : 45; }
                else if (nextLeft >= maxLeft) { nextLeft = maxLeft; facing = -1; roamPauseTicks = zoom ? 0 : vibe == "zoomies" ? 20 : 45; }
                else if (!zoom && Random.Shared.Next(vibe == "zoomies" ? 360 : 260) == 0)
                {
                    roamPauseTicks = Random.Shared.Next(35, 90);
                    if (Random.Shared.Next(2) == 0) facing = -facing;
                }
                else if ((zoom || vibe == "zoomies") && hopProgress == 0 && Random.Shared.Next(zoom ? 45 : 150) == 0)
                {
                    hopProgress = .02;
                }
                Left = nextLeft;
                stepPhase = (stepPhase + dt * stepRate) % (Math.PI * 2);
            }
        }
        else
        {
            stepPhase = 0;
            bool idleFree = hopProgress == 0 && !busy;
            if (faceCursor && cursor is { } c && !dragging && !Fainted && Math.Abs(c.X - centerX) > 30)
                facing = c.X > centerX ? 1 : -1;
            else if (idleFree)
            {
                // Idle life while perched: glance around and give the occasional hop.
                if (Random.Shared.Next(vibe == "calm" ? 260 : 170) == 0)
                    facing = -facing;
                else if (Random.Shared.Next(vibe == "zoomies" ? 220 : vibe == "calm" ? 600 : 360) == 0)
                    hopProgress = .02;
            }
        }

        UpdateVisual();
    }

    private bool IsSleepy()
    {
        if (mindAsleep) return true;
        // An active personality mood (angry, alert, love…) keeps the companion awake.
        if (currentMood is not (PetMood.Neutral or PetMood.Sleeping or PetMood.Drowsy or PetMood.Bored or PetMood.Happy or PetMood.Smug)) return false;
        bool online = model.OnlineCount > 0;
        bool sleepyVibe = model.PetAppearance.Vibe == "sleepy" && !hovered && !excited && !model.IsMediaPlaying;
        return sleepyVibe || model.Settings.Status == "dnd" || (!online && !hovered && !excited && !model.IsMediaPlaying);
    }

    private void UpdateVisual()
    {
        bool online = model.OnlineCount > 0;
        bool sleepy = IsSleepy();
        PetVisual.SetMood(sleepy ? PetMood.Neutral : currentMood);
        PetVisual.SetMotionState(
            breathPhase,
            blinkProgress,
            hopProgress,
            stepPhase,
            facing,
            hovered,
            excited,
            sleepy,
            online,
            (model.FloatingLanRoam || Zoomies) && !dragging && !hovered && !isQuickChatOpen() && roamPauseTicks == 0 && !sleepy && !Fainted,
            model.Settings.Status,
            model.IsMediaPlaying);
    }

    // ── Mouse ────────────────────────────────────────────────────────────

    private void Pet_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        dragStartCursor = Forms.Cursor.Position;
        dragStartWindow = new Point(Left, Top);
        dragging = PetHitArea.CaptureMouse();
        movedPastThreshold = false;
        lastDragX = dragStartWindow.X;
        lastDragVx = 0;
        dragReversals = 0;
        e.Handled = true;
    }

    private void Pet_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (!dragging)
        {
            TrackPetting(e.GetPosition(PetHitArea));
            return;
        }
        var now = Forms.Cursor.Position;
        var dpi = VisualTreeHelper.GetDpi(this);
        double dx = (now.X - dragStartCursor.X) / dpi.DpiScaleX;
        double dy = (now.Y - dragStartCursor.Y) / dpi.DpiScaleY;
        if (!movedPastThreshold && (Math.Abs(dx) > 5 || Math.Abs(dy) > 5))
        {
            movedPastThreshold = true;
            mind.Poke(PetPoke.DragStart);
            ApplyMindNow();
        }
        if (!movedPastThreshold) return;

        if (Math.Abs(dx) > 1.5) facing = dx > 0 ? 1 : -1;
        MoveClamped(dragStartWindow.X + dx, dragStartWindow.Y + dy);
        TrackShake(Left);
    }

    private void TrackPetting(Point p)
    {
        var nowUtc = DateTime.UtcNow;
        if ((nowUtc - petWindowStartUtc).TotalSeconds > 1.6) { petWindowStartUtc = nowUtc; petDistance = 0; }
        if (lastPetPoint is { } last) petDistance += (p - last).Length;
        lastPetPoint = p;
        if (petDistance > 240 && (nowUtc - lastPetPokeUtc).TotalSeconds > 2.5)
        {
            lastPetPokeUtc = nowUtc;
            petDistance = 0;
            mind.Poke(PetPoke.Pet);
            ApplyMindNow();
        }
    }

    private void TrackShake(double x)
    {
        var nowUtc = DateTime.UtcNow;
        double vx = x - lastDragX;
        lastDragX = x;
        if (Math.Abs(vx) < 5) return;
        if (lastDragVx != 0 && Math.Sign(vx) != Math.Sign(lastDragVx))
        {
            if ((nowUtc - reversalWindowStartUtc).TotalSeconds > 1.4) { reversalWindowStartUtc = nowUtc; dragReversals = 0; }
            dragReversals++;
            if (dragReversals >= 5)
            {
                dragReversals = 0;
                mind.Poke(PetPoke.Shake);
                ApplyMindNow();
            }
        }
        lastDragVx = vx;
    }

    private void Pet_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!dragging) return;
        PetHitArea.ReleaseMouseCapture();
        dragging = false;
        if (movedPastThreshold)
        {
            SavePosition();
            mind.Poke(PetPoke.Drop);
        }
        else
        {
            hopProgress = .02;
            mind.Poke(PetPoke.Click);
            ApplyMindNow();
            toggleQuickChat();
        }
        e.Handled = true;
    }

    private void Pet_LostMouseCapture(object sender, System.Windows.Input.MouseEventArgs e) => dragging = false;

    private void Pet_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
    {
        hovered = true;
        lastPetPoint = null;
        mind.Poke(PetPoke.Hover);
        ApplyMindNow();
    }

    private void Pet_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e) { hovered = false; lastPetPoint = null; UpdateVisual(); }

    private void SpeechBubble_Click(object sender, MouseButtonEventArgs e)
    {
        HideSpeech();
        if (speechIsLine) { e.Handled = true; return; }
        if (!string.IsNullOrEmpty(speechPeerId) && model.Peers.FirstOrDefault(p => p.Id == speechPeerId) is { } peer)
            model.SelectedPeer = peer;
        if (!isQuickChatOpen()) toggleQuickChat();
        e.Handled = true;
    }

    private void QuickChat_Click(object sender, RoutedEventArgs e)
    {
        // Wait for the context menu popup to finish closing and restoring focus before activating QuickChat.
        Dispatcher.BeginInvoke(DispatcherPriority.Input, () =>
        {
            if (!isQuickChatOpen()) toggleQuickChat();
        });
    }

    private void Roam_Click(object sender, RoutedEventArgs e) =>
        model.UpdateFloatingState(roam: !model.FloatingLanRoam);

    private void Customize_Click(object sender, RoutedEventArgs e) => (customizePet ?? openMainWindow)();
    private void OpenApp_Click(object sender, RoutedEventArgs e) => openMainWindow();
    private void HidePet_Click(object sender, RoutedEventArgs e) => model.UpdateFloatingState(enabled: false);

    private void MoveClamped(double targetLeft, double targetTop)
    {
        var work = WorkAreaDipForPoint(targetLeft + Width * .5, targetTop + Height * .5);
        double petSideMargin = (Width - PetHitArea.Width) * .5;
        double minLeft = work.Left - petSideMargin + 6;
        double maxLeft = work.Right - Width + petSideMargin - 6;
        double minTop = work.Top - (Height - PetHitArea.Height) + 6;
        double maxTop = work.Bottom - Height - 6;
        Left = Math.Clamp(targetLeft, minLeft, Math.Max(minLeft, maxLeft));
        Top = Math.Clamp(targetTop, minTop, Math.Max(minTop, maxTop));
    }

    private Point CursorDip()
    {
        var dpi = VisualTreeHelper.GetDpi(this);
        var p = Forms.Cursor.Position;
        return new Point(p.X / dpi.DpiScaleX, p.Y / dpi.DpiScaleY);
    }

    private Rect WorkAreaDipForCurrentWindow() =>
        WorkAreaDipForPoint((double.IsFinite(Left) ? Left : 0) + Width * .5, (double.IsFinite(Top) ? Top : 0) + Height * .5);

    private Rect WorkAreaDipForPoint(double dipX, double dipY)
    {
        var dpi = VisualTreeHelper.GetDpi(this);
        var px = new System.Drawing.Point((int)Math.Round(dipX * dpi.DpiScaleX), (int)Math.Round(dipY * dpi.DpiScaleY));
        var area = Forms.Screen.FromPoint(px).WorkingArea;
        return new Rect(area.Left / dpi.DpiScaleX, area.Top / dpi.DpiScaleY, area.Width / dpi.DpiScaleX, area.Height / dpi.DpiScaleY);
    }

    private Rect CurrentWorkAreaDip()
    {
        var dpi = VisualTreeHelper.GetDpi(this);
        var area = (Forms.Screen.PrimaryScreen ?? Forms.Screen.FromPoint(Forms.Cursor.Position)).WorkingArea;
        return new Rect(area.Left / dpi.DpiScaleX, area.Top / dpi.DpiScaleY, area.Width / dpi.DpiScaleX, area.Height / dpi.DpiScaleY);
    }
}
