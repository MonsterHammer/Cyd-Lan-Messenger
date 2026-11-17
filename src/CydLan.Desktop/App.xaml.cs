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
    internal bool IsQuitting => quitting;
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        bool captureTray = e.Args.Contains("--tray-screenshot");
        bool captureSettings = e.Args.Contains("--settings-screenshot");
        bool capturing = captureTray || captureSettings || e.Args.Contains("--screenshot");
        bool preview = capturing || e.Args.Contains("--preview") || e.Args.Contains("--empty-preview");
        if (!capturing)
        {
            mutex = new Mutex(true, "Local\\CydLan.Desktop", out var first);
            if (!first) { System.Windows.MessageBox.Show("CydLan is already running.", "CydLan"); Shutdown(); return; }
        }
        try
        {
            model = new MainViewModel(preview, seedPreview: !e.Args.Contains("--empty-preview"));
            int themeArg = Array.IndexOf(e.Args, "--theme");
            if (preview && themeArg >= 0 && themeArg + 1 < e.Args.Length) model.Settings.Theme = ThemeManager.Normalize(e.Args[themeArg + 1]); // preview/screenshot override only
            ThemeManager.Apply(model.Settings.Theme);
            var window = new MainWindow(model);
            if (e.Args.Contains("--compact")) { window.Width = window.MinWidth; window.Height = window.MinHeight; }
            window.Title = preview ? "CydLan · Offline preview" : "CydLan · LAN messenger";
            if (e.Args.Contains("--transfers")) model.TransfersOpen = true;
            if (e.Args.Contains("--groups")) { model.GroupsOpen = true; model.SelectedGroup = model.Groups.FirstOrDefault(); }
            MainWindow = window;
            if (!capturing) CreateTray(window);
            if (!captureTray && (capturing || preview || !model.Settings.StartMinimized)) window.Show();
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
                var settingsWindow = new SettingsWindow(model);
                PopupHost.Attach(window, settingsWindow);
                window.SetScrim(true);
                settingsWindow.Show();
                int pageArg = Array.IndexOf(e.Args, "--settings-page");
                if (pageArg >= 0 && pageArg + 1 < e.Args.Length) settingsWindow.ShowPage(e.Args[pageArg + 1] + "Page");
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
                if (captureSettings) bitmap = Composite(window, captureWindow);
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
        tray = new Forms.NotifyIcon { Text = model!.Preview ? "CydLan · Offline preview" : "CydLan · LAN messenger", Icon = MakeIcon(), Visible = true };
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
            if (window.IsVisible && window.IsActive) return;
            if (model.ShouldPlaySound) System.Media.SystemSounds.Asterisk.Play();
            if (!model.ShouldAlert) return;
            tray.BalloonTipTitle = name; tray.BalloonTipText = text.Length > 160 ? text[..160] + "…" : text;
            tray.ShowBalloonTip(4000);
        };
    }
    private static Icon MakeIcon()
    {
        using var stream = GetResourceStream(new Uri("pack://application:,,,/CydLan;component/Assets/cydlan.ico")).Stream;
        using var icon = new Icon(stream, 32, 32);
        return (Icon)icon.Clone();
    }
    // Draws the popup over the main window at its real offset, so the screenshot shows the backdrop too.
    private static RenderTargetBitmap Composite(Window main, Window popup)
    {
        main.UpdateLayout();
        var origin = main.PointToScreen(new System.Windows.Point(0, 0));
        var target = PresentationSource.FromVisual(main)!.CompositionTarget!.TransformFromDevice;
        origin = target.Transform(origin);
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawRectangle(new VisualBrush(main), null, new Rect(0, 0, main.ActualWidth, main.ActualHeight));
            dc.DrawRectangle(new VisualBrush(popup), null, new Rect(popup.Left - origin.X, popup.Top - origin.Y, popup.ActualWidth, popup.ActualHeight));
        }
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(main.ActualWidth), (int)Math.Ceiling(main.ActualHeight), 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        return bitmap;
    }
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
