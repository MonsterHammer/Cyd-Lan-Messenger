using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using Point = System.Windows.Point;

namespace CydLan.Desktop;

/// <summary>
/// Shows in-app popups over a dimmed main window, keeps them inside the main window's bounds,
/// and replaces Window.DragMove with a drag that cannot leave those bounds.
/// </summary>
public static class PopupHost
{
    private const double Inset = 12; // gap kept between the popup and the main window edge

    public static bool? ShowModal(MainWindow owner, Window popup)
    {
        Attach(owner, popup);
        owner.SetScrim(true);
        try { return popup.ShowDialog(); }
        finally { owner.SetScrim(false); owner.Activate(); }
    }

    /// <summary>Owns, sizes, centres and confines a popup to <paramref name="owner"/> (without showing it).</summary>
    public static void Attach(MainWindow owner, Window popup)
    {
        popup.Owner = owner;
        popup.WindowStartupLocation = WindowStartupLocation.Manual;
        popup.SourceInitialized += (_, _) => RoundCorners(popup);
        popup.Loaded += (_, _) => FitAndCenter(popup);
        popup.LocationChanged += (_, _) => Clamp(popup); // also covers Win+arrow keyboard moves
    }

    /// <summary>Lets <paramref name="handle"/> drag the popup without it leaving the owner window.</summary>
    public static void EnableDrag(Window popup, UIElement handle)
    {
        Point grab = default; bool dragging = false;
        handle.MouseLeftButtonDown += (_, e) => { grab = e.GetPosition(popup); dragging = handle.CaptureMouse(); e.Handled = dragging; };
        handle.MouseMove += (_, e) =>
        {
            if (!dragging) return;
            var now = e.GetPosition(popup);
            MoveTo(popup, popup.Left + now.X - grab.X, popup.Top + now.Y - grab.Y);
        };
        handle.MouseLeftButtonUp += (_, _) => handle.ReleaseMouseCapture();
        handle.LostMouseCapture += (_, _) => dragging = false;
    }

    private static void FitAndCenter(Window popup)
    {
        if (OwnerBounds(popup) is not { } area) return;
        // A popup taller or wider than the main window could never stay inside it, so shrink it to fit.
        if (popup.ActualHeight > area.Height - 2 * Inset) popup.Height = Math.Max(200, area.Height - 2 * Inset);
        if (popup.ActualWidth > area.Width - 2 * Inset) popup.Width = Math.Max(280, area.Width - 2 * Inset);
        popup.UpdateLayout();
        MoveTo(popup, area.Left + (area.Width - popup.ActualWidth) / 2, area.Top + (area.Height - popup.ActualHeight) / 2);
    }

    private static bool clamping;
    private static void Clamp(Window popup) { if (!clamping) MoveTo(popup, popup.Left, popup.Top); }

    private static void MoveTo(Window popup, double left, double top)
    {
        if (OwnerBounds(popup) is not { } area) { popup.Left = left; popup.Top = top; return; }
        double maxLeft = area.Right - popup.ActualWidth - Inset, maxTop = area.Bottom - popup.ActualHeight - Inset;
        left = Math.Max(area.Left + Inset, Math.Min(maxLeft, left));
        top = Math.Max(area.Top + Inset, Math.Min(maxTop, top));
        clamping = true;
        try { popup.Left = left; popup.Top = top; }
        finally { clamping = false; }
    }

    /// <summary>The owner's on-screen client area in device-independent units (correct when maximized too).</summary>
    private static Rect? OwnerBounds(Window popup)
    {
        if (popup.Owner is not { IsVisible: true } owner || PresentationSource.FromVisual(owner)?.CompositionTarget is not { } target) return null;
        var topLeft = target.TransformFromDevice.Transform(owner.PointToScreen(new Point(0, 0)));
        return new Rect(topLeft.X, topLeft.Y, owner.ActualWidth, owner.ActualHeight);
    }

    [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
    /// <summary>Asks Windows 11 to draw rounded corners on a borderless window (no effect on Windows 10).</summary>
    public static void RoundCorners(Window window)
    {
        int round = 2; // DWMWA_WINDOW_CORNER_PREFERENCE = 33, DWMWCP_ROUND = 2; ignored before Windows 11
        try { DwmSetWindowAttribute(new WindowInteropHelper(window).Handle, 33, ref round, sizeof(int)); } catch (DllNotFoundException) { }
    }
}
