using System.Collections.Specialized;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Forms = System.Windows.Forms;

namespace CydLan.Desktop;

/// <summary>Compact floating quick-chat and online contact switcher opened from the desktop pet.</summary>
public partial class FloatingQuickChatWindow : Window
{
    private readonly MainViewModel model;
    private readonly Action openMainWindow;
    private readonly Action onDeactivated;

    private DateTime lastShownUtc = DateTime.MinValue;

    public FloatingQuickChatWindow(MainViewModel model, Action openMainWindow, Action onDeactivated)
    {
        InitializeComponent();
        this.model = model;
        this.openMainWindow = openMainWindow;
        this.onDeactivated = onDeactivated;
        DataContext = model;

        SourceInitialized += (_, _) =>
        {
            PopupHost.RoundCorners(this);
            PopupHost.MakeToolWindow(this);
        };
        Loaded += (_, _) => QuickScroller.ScrollToEnd();
        Deactivated += (_, _) =>
        {
            if ((DateTime.UtcNow - lastShownUtc).TotalMilliseconds < 120) return;
            model.Replying = false;
            onDeactivated();
            if (IsVisible) Hide();
        };
        PreviewKeyDown += (_, args) =>
        {
            if (args.Key == Key.Escape)
            {
                args.Handled = true;
                Hide();
            }
        };
        ((INotifyCollectionChanged)model.Messages).CollectionChanged += OnMessagesChanged;
        QuickComposer.PreviewMouseDown += (_, _) => model.Replying = true;
        QuickComposer.PreviewTextInput += (_, _) => model.Replying = true;
        Closed += (_, _) => ((INotifyCollectionChanged)model.Messages).CollectionChanged -= OnMessagesChanged;
    }

    private void OnMessagesChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        Dispatcher.BeginInvoke(() => QuickScroller.ScrollToEnd());

    public void ShowNear(Rect petRectDip)
    {
        var dpi = VisualTreeHelper.GetDpi(this);
        double cxPx = (petRectDip.Left + petRectDip.Width * .5) * dpi.DpiScaleX;
        double cyPx = (petRectDip.Top + petRectDip.Height * .5) * dpi.DpiScaleY;
        var area = Forms.Screen.FromPoint(new System.Drawing.Point((int)Math.Round(cxPx), (int)Math.Round(cyPx))).WorkingArea;
        var work = new Rect(area.Left / dpi.DpiScaleX, area.Top / dpi.DpiScaleY, area.Width / dpi.DpiScaleX, area.Height / dpi.DpiScaleY);

        const double gap = 10;
        double left = Math.Clamp(petRectDip.Left + petRectDip.Width * .5 - Width * .5, work.Left + 8, Math.Max(work.Left + 8, work.Right - Width - 8));
        double aboveTop = petRectDip.Top - Height - gap;
        double belowTop = petRectDip.Bottom + gap;
        double top = aboveTop >= work.Top + 8 ? aboveTop : Math.Min(belowTop, Math.Max(work.Top + 8, work.Bottom - Height - 8));

        Left = left;
        Top = top;
        lastShownUtc = DateTime.UtcNow;
        if (model.SelectedPeer is null && model.QuickChatPeers.FirstOrDefault() is { } firstActive)
            model.SelectedPeer = firstActive;
        Show();
        Activate();
        if (QuickPeersList.SelectedItem is { } selectedPeer)
            QuickPeersList.ScrollIntoView(selectedPeer);
        QuickScroller.ScrollToEnd();
        QuickComposer.Focus();
    }

    private void Peer_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (!IsVisible) return;
        if (QuickPeersList.SelectedItem is { } selectedPeer)
            QuickPeersList.ScrollIntoView(selectedPeer);
        QuickScroller.ScrollToEnd();
        QuickComposer.Focus();
        if (QuickComposer.IsKeyboardFocusWithin || QuickComposer.IsFocused)
            model.Replying = true;
    }

    private void QuickPeers_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (FindScrollViewer(QuickPeersList) is not { } scroller) return;
        scroller.ScrollToHorizontalOffset(scroller.HorizontalOffset - e.Delta * .45);
        e.Handled = true;
    }

    private static System.Windows.Controls.ScrollViewer? FindScrollViewer(DependencyObject root)
    {
        if (root is System.Windows.Controls.ScrollViewer sv) return sv;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            if (FindScrollViewer(VisualTreeHelper.GetChild(root, i)) is { } found) return found;
        return null;
    }

    private void QuickComposer_GotFocus(object sender, KeyboardFocusChangedEventArgs e) => model.Replying = true;

    private async void QuickComposer_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        bool ctrl = (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;
        bool shift = (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;
        bool send = model.Settings.SendWithCtrlEnter ? ctrl : !ctrl && !shift;
        if (!send) return;
        e.Handled = true;
        if (model.CanSend) await model.SendAsync();
    }

    private async void Send_Click(object sender, RoutedEventArgs e)
    {
        if (model.CanSend) await model.SendAsync();
        QuickComposer.Focus();
    }

    private async void ApplyLook_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: SharedLookInfo look })
            await model.ApplyLookCodeAsync(look.Code);
        QuickComposer.Focus();
    }

    private void Roam_Click(object sender, RoutedEventArgs e) =>
        model.UpdateFloatingState(roam: !model.FloatingLanRoam);

    private void OpenFull_Click(object sender, RoutedEventArgs e)
    {
        Hide();
        openMainWindow();
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Hide();
}
