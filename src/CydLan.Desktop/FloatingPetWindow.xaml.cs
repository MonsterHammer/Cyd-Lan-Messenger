using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Forms = System.Windows.Forms;
using Point = System.Windows.Point;

namespace CydLan.Desktop;

/// <summary>Always-on-top desktop companion pet toggled from the system tray (hidden from taskbar, nav rail, and Alt+Tab).</summary>
public partial class FloatingPetWindow : Window
{
    private readonly MainViewModel model;
    private readonly Action toggleQuickChat;
    private readonly Action openMainWindow;
    private readonly Func<bool> isQuickChatOpen;
    private readonly DispatcherTimer animTimer = new(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(33) };
    private readonly DispatcherTimer speechTimer = new() { Interval = TimeSpan.FromSeconds(4.5) };
    private string? speechPeerId;

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

    public FloatingPetWindow(
        MainViewModel model,
        Action toggleQuickChat,
        Action openMainWindow,
        Func<bool> isQuickChatOpen)
    {
        InitializeComponent();
        this.model = model;
        this.toggleQuickChat = toggleQuickChat;
        this.openMainWindow = openMainWindow;
        this.isQuickChatOpen = isQuickChatOpen;
        DataContext = model;

        SourceInitialized += (_, _) => PopupHost.MakeToolWindow(this);
        Loaded += (_, _) => RestoreOrPlaceDefault();
        IsVisibleChanged += (_, _) => UpdateTimerState();
        Closed += (_, _) => { animTimer.Stop(); speechTimer.Stop(); };

        animTimer.Tick += (_, _) => StepAnimation();
        speechTimer.Tick += (_, _) => HideSpeech();
        PetContextMenu.Opened += (_, _) => RoamMenuItem.IsChecked = model.FloatingLanRoam;
    }

    public Rect PetScreenRectDip()
    {
        double left = double.IsFinite(Left) ? Left : 0;
        double top = double.IsFinite(Top) ? Top : 0;
        return new Rect(left + (Width - PetHitArea.Width) * .5, top + Height - PetHitArea.Height - 4, PetHitArea.Width, PetHitArea.Height);
    }

    public void ReactToIncoming(string sender, string previewText, string? peerId)
    {
        speechPeerId = peerId;
        SpeechSender.Text = sender;
        SpeechText.Text = string.IsNullOrWhiteSpace(previewText) ? "New message" : previewText.ReplaceLineEndings(" ");
        SpeechBubble.Visibility = Visibility.Visible;
        speechTimer.Stop();
        speechTimer.Start();

        hopProgress = .02;
        excited = true;
        excitedUntilUtc = DateTime.UtcNow.AddSeconds(3.2);
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

    private void RestoreOrPlaceDefault()
    {
        var work = CurrentWorkAreaDip();
        double targetLeft = model.Settings.FloatingLanLeft is { } l && double.IsFinite(l)
            ? l
            : work.Right - Width + 38;
        double targetTop = model.Settings.FloatingLanTop is { } t && double.IsFinite(t)
            ? t
            : work.Bottom - Height - 10;
        MoveClamped(targetLeft, targetTop);
        UpdateVisual();
        UpdateTimerState();
    }

    private void UpdateTimerState()
    {
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

    private void StepAnimation()
    {
        const double dt = .033;
        breathPhase = (breathPhase + dt * 2.3) % (Math.PI * 20);

        if (excited && DateTime.UtcNow >= excitedUntilUtc)
            excited = false;

        if (hopProgress > 0)
        {
            hopProgress += dt * 2.1;
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

        // Casual screen roaming along the current horizontal perch.
        bool walking = model.FloatingLanRoam && !dragging && !hovered && !isQuickChatOpen();
        if (walking)
        {
            if (roamPauseTicks > 0)
            {
                roamPauseTicks--;
                stepPhase = 0;
            }
            else
            {
                var work = WorkAreaDipForCurrentWindow();
                double minLeft = work.Left - (Width - PetHitArea.Width) * .5 + 8;
                double maxLeft = work.Right - Width + (Width - PetHitArea.Width) * .5 - 8;
                double nextLeft = Left + facing * 1.15;
                if (nextLeft <= minLeft) { nextLeft = minLeft; facing = 1; roamPauseTicks = 45; }
                else if (nextLeft >= maxLeft) { nextLeft = maxLeft; facing = -1; roamPauseTicks = 45; }
                else if (Random.Shared.Next(260) == 0)
                {
                    roamPauseTicks = Random.Shared.Next(45, 95);
                    if (Random.Shared.Next(2) == 0) facing = -facing;
                }
                Left = nextLeft;
                stepPhase = (stepPhase + dt * 9.5) % (Math.PI * 2);
            }
        }
        else
        {
            stepPhase = 0;
        }

        UpdateVisual();
    }

    private void UpdateVisual()
    {
        bool online = model.OnlineCount > 0;
        bool sleepy = model.Settings.Status == "dnd" || (!online && !hovered && !excited);
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
            model.FloatingLanRoam && !dragging && !hovered && !isQuickChatOpen() && roamPauseTicks == 0,
            model.Settings.Status);
    }

    private void Pet_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        dragStartCursor = Forms.Cursor.Position;
        dragStartWindow = new Point(Left, Top);
        dragging = PetHitArea.CaptureMouse();
        movedPastThreshold = false;
        e.Handled = true;
    }

    private void Pet_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (!dragging) return;
        var now = Forms.Cursor.Position;
        var dpi = VisualTreeHelper.GetDpi(this);
        double dx = (now.X - dragStartCursor.X) / dpi.DpiScaleX;
        double dy = (now.Y - dragStartCursor.Y) / dpi.DpiScaleY;
        if (!movedPastThreshold && (Math.Abs(dx) > 5 || Math.Abs(dy) > 5))
            movedPastThreshold = true;
        if (!movedPastThreshold) return;

        if (Math.Abs(dx) > 1.5) facing = dx > 0 ? 1 : -1;
        MoveClamped(dragStartWindow.X + dx, dragStartWindow.Y + dy);
    }

    private void Pet_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!dragging) return;
        PetHitArea.ReleaseMouseCapture();
        dragging = false;
        if (movedPastThreshold)
            model.UpdateFloatingState(left: Left, top: Top);
        else
        {
            hopProgress = .02;
            toggleQuickChat();
        }
        e.Handled = true;
    }

    private void Pet_LostMouseCapture(object sender, System.Windows.Input.MouseEventArgs e) => dragging = false;

    private void Pet_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e) { hovered = true; UpdateVisual(); }
    private void Pet_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e) { hovered = false; UpdateVisual(); }

    private void SpeechBubble_Click(object sender, MouseButtonEventArgs e)
    {
        HideSpeech();
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
