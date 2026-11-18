using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Forms = System.Windows.Forms;

namespace CydLan.Desktop;

public partial class TrayMenuWindow : Window
{
    private readonly MainWindow mainWindow;
    private readonly MainViewModel model;
    public TrayMenuWindow(MainWindow mainWindow, MainViewModel model)
    {
        InitializeComponent(); this.mainWindow = mainWindow; this.model = model;
        DataContext = model;
        Closing += (_, _) => closing = true;
        Deactivated += (_, _) => CloseOnce();
        PreviewKeyDown += (_, args) => { if (args.Key == Key.Escape) { args.Handled = true; CloseOnce(); } };
    }
    // Closing deactivates the window, which would call Close() again mid-close and crash the app.
    private bool closing;
    public void CloseOnce() { if (!closing) { closing = true; Close(); } }
    public void ShowNear(System.Drawing.Point anchor)
    {
        // Hide the initial layout pass; position only after Windows has sized the
        // popup at the destination monitor's DPI. All coordinates below are pixels.
        Opacity = 0;
        var handle = new WindowInteropHelper(this).EnsureHandle();
        SetWindowPos(handle, IntPtr.Zero, anchor.X, anchor.Y, 0, 0, 0x0015);
        Show(); UpdateLayout();
        var workArea = Forms.Screen.FromPoint(anchor).WorkingArea;
        GetWindowRect(handle, out var bounds);
        int margin = (int)Math.Ceiling(10 * VisualTreeHelper.GetDpi(this).DpiScaleX);
        int width = bounds.Right - bounds.Left, height = bounds.Bottom - bounds.Top;
        int x = Math.Clamp(anchor.X - width + margin, workArea.Left + margin, Math.Max(workArea.Left + margin, workArea.Right - width - margin));
        int y = Math.Clamp(anchor.Y - height - margin, workArea.Top + margin, Math.Max(workArea.Top + margin, workArea.Bottom - height - margin));
        SetWindowPos(handle, new IntPtr(-1), x, y, 0, 0, 0x0001);
        Opacity = 1; Activate(); OpenAction.Focus();
    }
    private void Open_Click(object sender, RoutedEventArgs e) { CloseOnce(); App.ShowWindow(mainWindow); }
    private void Transfers_Click(object sender, RoutedEventArgs e) { model.TransfersOpen = true; CloseOnce(); App.ShowWindow(mainWindow); }
    private void Settings_Click(object sender, RoutedEventArgs e) { CloseOnce(); App.ShowWindow(mainWindow); PopupHost.ShowModal(mainWindow, new SettingsWindow(model)); }
    private async void Quit_Click(object sender, RoutedEventArgs e) { CloseOnce(); await ((App)System.Windows.Application.Current).QuitAsync(); }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr handle, out NativeRect rect);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(IntPtr handle, IntPtr insertAfter, int x, int y, int width, int height, uint flags);
}
