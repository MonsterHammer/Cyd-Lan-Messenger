using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Forms = System.Windows.Forms;

namespace CydLan.Desktop;

public partial class App : System.Windows.Application
{
    private Mutex? mutex;
    private Forms.NotifyIcon? tray;
    private TrayMenuWindow? trayMenu;
    private MainViewModel? model;
    private bool quitting;
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        // Design phase: all desktop entry points stay offline, even if an old
        // shortcut supplies network-related arguments. The transport is retained
        // in Core for a later, explicitly authorized networking phase.
        bool captureTray = e.Args.Contains("--tray-screenshot");
        bool captureSettings = e.Args.Contains("--settings-screenshot");
        bool capturing = captureTray || captureSettings || e.Args.Contains("--screenshot");
        if (!capturing)
        {
            mutex = new Mutex(true, "Local\\CydLan.Desktop", out var first);
            if (!first) { System.Windows.MessageBox.Show("CydLan is already running. Open it from the system tray.", "CydLan"); Shutdown(); return; }
        }
        try
        {
            model = new MainViewModel(preview: true, seedPreview: !e.Args.Contains("--empty-preview"));
            var window = new MainWindow(model);
            if (e.Args.Contains("--compact")) { window.Width = window.MinWidth; window.Height = window.MinHeight; }
            window.Title = "CydLan · Offline design mode";
            if (e.Args.Contains("--transfers")) model.TransfersOpen = true;
            MainWindow = window;
            if (!capturing) CreateTray(window);
            if (!captureTray) window.Show();
            if (e.Args.Contains("--details")) window.ShowConnectionDetails();
            await model.StartAsync();
            Window captureWindow = window;
            if (captureTray)
            {
                trayMenu = new TrayMenuWindow(window, model);
                trayMenu.ShowNear(Forms.Cursor.Position);
                captureWindow = trayMenu;
            }
            if (captureSettings)
            {
                var settingsWindow = new SettingsWindow(model) { Owner = window };
                settingsWindow.Show();
                captureWindow = settingsWindow;
            }
            int capture = Array.IndexOf(e.Args, captureTray ? "--tray-screenshot" : captureSettings ? "--settings-screenshot" : "--screenshot");
            if (capture >= 0 && capture + 1 < e.Args.Length)
            {
                await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
                await Task.Delay(300);
                captureWindow.UpdateLayout();
                var bitmap = new RenderTargetBitmap((int)Math.Ceiling(captureWindow.ActualWidth), (int)Math.Ceiling(captureWindow.ActualHeight), 96, 96, PixelFormats.Pbgra32);
                bitmap.Render(captureWindow);
                var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                var path = Path.GetFullPath(e.Args[capture + 1]); Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                using (var file = File.Create(path)) encoder.Save(file);
                await QuitAsync();
            }
        }
        catch (Exception ex)
        {
            if (capturing) { Console.Error.WriteLine(ex); Environment.ExitCode = 1; }
            else System.Windows.MessageBox.Show(ex.Message, "CydLan could not start");
            await QuitAsync();
        }
    }
    private void CreateTray(MainWindow window)
    {
        tray = new Forms.NotifyIcon { Text = "CydLan · Offline design mode", Icon = MakeIcon(), Visible = true };
        tray.MouseClick += (_, args) => {
            if (args.Button != Forms.MouseButtons.Right) return;
            Dispatcher.Invoke(() => {
                trayMenu?.Close();
                trayMenu = new TrayMenuWindow(window, model!);
                trayMenu.ShowNear(Forms.Cursor.Position);
            });
        };
        tray.DoubleClick += (_, _) => ShowWindow(window);
        tray.BalloonTipClicked += (_, _) => ShowWindow(window);
        model!.Notify += (name, text) => {
            if (!model.Settings.Notifications || model.Settings.Status == "dnd" || (window.IsVisible && window.IsActive)) return;
            tray.BalloonTipTitle = name; tray.BalloonTipText = text.Length > 160 ? text[..160] + "…" : text;
            tray.ShowBalloonTip(4000);
        };
    }
    private static Icon MakeIcon()
    {
        using var bitmap = new Bitmap(32, 32);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        using var background = new SolidBrush(System.Drawing.Color.FromArgb(133, 177, 191));
        graphics.FillEllipse(background, 1, 1, 30, 30);
        using var pen = new System.Drawing.Pen(System.Drawing.Color.FromArgb(19, 38, 46), 3);
        graphics.DrawArc(pen, 8, 8, 16, 16, 45, 270);
        var handle = bitmap.GetHicon();
        try { using var original = Icon.FromHandle(handle); return (Icon)original.Clone(); }
        finally { DestroyIcon(handle); }
    }
    [System.Runtime.InteropServices.DllImport("user32.dll")] [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr handle);
    internal static void ShowWindow(MainWindow window) { window.Show(); window.WindowState = WindowState.Normal; window.Activate(); }
    public async Task QuitAsync()
    {
        if (quitting) return;
        quitting = true;
        if (model is not null) await model.DisposeAsync();
        trayMenu?.Close();
        tray?.Dispose(); mutex?.Dispose(); Shutdown();
    }
}
