using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using CydLan.Desktop;

internal static partial class Program
{
    [DllImport("user32.dll")] private static extern IntPtr GetDC(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern int ReleaseDC(IntPtr hwnd, IntPtr dc);
    [DllImport("gdi32.dll")] private static extern uint GetPixel(IntPtr dc, int x, int y);

    private static (int R, int G, int B) ScreenPixel(int x, int y)
    {
        IntPtr dc = GetDC(IntPtr.Zero);
        try { uint c = GetPixel(dc, x, y); return ((int)(c & 0xFF), (int)((c >> 8) & 0xFF), (int)((c >> 16) & 0xFF)); }
        finally { ReleaseDC(IntPtr.Zero, dc); }
    }

    /// <summary>Puts a red window behind the main window and reads real screen pixels to confirm the window is see-through.</summary>
    private static async Task TestWindowOpacityAsync()
    {
        var red = new Window { WindowStyle = WindowStyle.None, AllowsTransparency = true, ResizeMode = ResizeMode.NoResize, Background = Brushes.Red, Left = 40, Top = 40, Width = 980, Height = 760, ShowInTaskbar = false, Topmost = true };
        red.Show();
        await using var model = new MainViewModel(preview: true);
        model.Settings.WindowOpacity = .8;
        model.ApplySettings();
        var window = new MainWindow(model) { WindowStartupLocation = WindowStartupLocation.Manual, Left = 100, Top = 100, Topmost = true };
        window.Show();
        window.Activate();
        await Task.Delay(700);
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        var dpi = VisualTreeHelper.GetDpi(window);
        int px = (int)((window.Left + 560) * dpi.DpiScaleX), py = (int)((window.Top + 300) * dpi.DpiScaleY);
        var see = ScreenPixel(px, py);
        Check(see.R - see.G > 25, $"80% window opacity lets the window behind show through (pixel {see}).");
        window.Close();
        red.Close();
        model.Settings.WindowOpacity = 1.0;
    }
}
