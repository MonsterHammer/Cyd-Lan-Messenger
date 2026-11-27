using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Forms = System.Windows.Forms;
using CydLan.Core;

namespace CydLan.Desktop;

public partial class App : System.Windows.Application
{
    private Mutex? mutex;
    private EventWaitHandle? activateEvent;
    private RegisteredWaitHandle? activateRegistration;
    private Forms.NotifyIcon? tray;
    private TrayMenuWindow? trayMenu;
    private FloatingPetController? floatingPet;
    private MessageNotificationWindow? notification;
    private MainViewModel? model;
    private DiagnosticLog? log;
    private bool quitting;
    internal bool IsQuitting => quitting;
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        bool captureTray = e.Args.Contains("--tray-screenshot");
        bool captureSettings = e.Args.Contains("--settings-screenshot");
        bool capturing = captureTray || captureSettings || e.Args.Contains("--screenshot");
        bool preview = capturing || e.Args.Contains("--preview") || e.Args.Contains("--empty-preview");
        // Lab mode: a separate profile bound to one local address (e.g. the Windows Sandbox adapter), so it never
        // reaches real contacts or the office LAN. Usage: --lab 172.x.x.x
        int labArg = Array.IndexOf(e.Args, "--lab");
        System.Net.IPAddress? labAddress = null;
        if (!preview && labArg >= 0 && (labArg + 1 >= e.Args.Length || !System.Net.IPAddress.TryParse(e.Args[labArg + 1], out labAddress)))
        { System.Windows.MessageBox.Show("--lab needs a local IPv4 address.", "CydLan"); Shutdown(); return; }
        string dataDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), labAddress is null ? "CydLan" : "CydLan-Lab");
        if (!capturing)
        {
            string mutexName = preview ? "Local\\CydLan.Desktop.Preview" : labAddress is null ? "Local\\CydLan.Desktop" : "Local\\CydLan.Desktop.Lab";
            string activateName = mutexName + ".Activate";
            mutex = new Mutex(true, mutexName, out var first);
            if (!first)
            {
                bool signaled = false;
                try
                {
                    using var existing = EventWaitHandle.OpenExisting(activateName);
                    signaled = existing.Set();
                }
                catch (WaitHandleCannotBeOpenedException) { }
                catch (UnauthorizedAccessException) { }
                if (!signaled) System.Windows.MessageBox.Show("CydLan is already running.", "CydLan");
                Shutdown();
                return;
            }
            activateEvent = new EventWaitHandle(false, EventResetMode.AutoReset, activateName);
        }
        try
        {
            if (!preview)
            {
                log = new DiagnosticLog(Path.Combine(dataDirectory, "logs"));
                RegisterCrashLogging();
                log.Write("application.start", null, ("version", typeof(App).Assembly.GetName().Version), ("runtime", Environment.Version), ("os", Environment.OSVersion.VersionString));
            }
            model = new MainViewModel(preview, seedPreview: !e.Args.Contains("--empty-preview"), localStore: preview ? null : new LocalStore(dataDirectory), log: log);
            if (preview && e.Args.Contains("--floating")) model.Settings.FloatingLanEnabled = true;
            int themeArg = Array.IndexOf(e.Args, "--theme");
            if (preview && themeArg >= 0 && themeArg + 1 < e.Args.Length) model.Settings.Theme = ThemeManager.Normalize(e.Args[themeArg + 1]); // preview/screenshot override only
            ThemeManager.Apply(model.Settings.Theme, AppearanceOverrides.From(model.Settings));
            var window = new MainWindow(model);
            if (e.Args.Contains("--compact")) { window.Width = window.MinWidth; window.Height = window.MinHeight; }
            window.Title = preview ? "CydLan · Offline preview" : labAddress is not null ? $"CydLan · Lab {labAddress}" : "CydLan · LAN messenger";
            if (e.Args.Contains("--transfers")) model.TransfersOpen = true;
            if (e.Args.Contains("--groups")) { model.GroupsOpen = true; model.SelectedGroup = model.Groups.FirstOrDefault(); }
            MainWindow = window;
            if (activateEvent is not null)
            {
                activateRegistration = ThreadPool.RegisterWaitForSingleObject(activateEvent, (_, _) =>
                {
                    if (quitting) return;
                    Dispatcher.BeginInvoke(() =>
                    {
                        if (quitting) return;
                        if (tray is not null) tray.Visible = true;
                        ShowWindow(window);
                    });
                }, null, Timeout.Infinite, executeOnlyOnce: false);
            }
            if (!capturing)
            {
                CreateTray(window);
                floatingPet = new FloatingPetController(model, () => ShowWindow(window));
                floatingPet.SyncVisibility();
                if (!preview) ConnectNotifications(window, model);
            }
            if (preview && !capturing)
                window.Closed += async (_, _) => await QuitAsync();
            if (!captureTray && (capturing || preview || !model.Settings.StartMinimized)) window.Show();
            if (e.Args.Contains("--details")) window.ShowConnectionDetails();
            if (!preview) await System.Windows.Threading.Dispatcher.Yield(DispatcherPriority.Background);
            if (quitting) return;
            await model.StartAsync(bindAddress: labAddress);
            if (quitting) return;
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
            log?.Write("application.start_failed", ex);
            if (capturing) { Console.Error.WriteLine(ex); Environment.ExitCode = 1; }
            else System.Windows.MessageBox.Show(ex.Message, "CydLan could not start");
            await QuitAsync();
        }
    }
    private void RegisterCrashLogging()
    {
        // Preserve normal fatal-error behavior: write evidence without declaring the exception handled.
        DispatcherUnhandledException += (_, args) => log?.Write("application.dispatcher_crash", args.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, args) => log?.Write("application.unhandled_exception", args.ExceptionObject as Exception, ("terminating", args.IsTerminating));
        TaskScheduler.UnobservedTaskException += (_, args) => log?.Write("application.unobserved_task", args.Exception);
    }
    protected override void OnExit(ExitEventArgs e)
    {
        log?.Write("application.exit", null, ("exitCode", e.ApplicationExitCode));
        base.OnExit(e);
    }
    private void CreateTray(MainWindow window)
    {
        tray = new Forms.NotifyIcon { Text = model!.Preview ? "CydLan · Offline preview" : "CydLan · LAN messenger", Icon = MakeIcon(), Visible = true };
        tray.MouseClick += (_, args) => {
            if (args.Button != Forms.MouseButtons.Right) return;
            Dispatcher.Invoke(() => {
                trayMenu?.CloseOnce();
                trayMenu = new TrayMenuWindow(window, model!);
                trayMenu.ShowNear(Forms.Cursor.Position);
            });
        };
        tray.DoubleClick += (_, _) => ShowWindow(window);
    }
    internal void ConnectNotifications(MainWindow window, MainViewModel source)
    {
        source.PropertyChanged += (_, args) => {
            if (args.PropertyName == nameof(MainViewModel.ShouldShowNotificationPopup) && !source.ShouldShowNotificationPopup)
                notification?.Close();
        };
        source.Notify += preview => {
            if (quitting) return;
            if (source.ShouldPlaySound) System.Media.SystemSounds.Asterisk.Play();
            if (!source.ShouldAlert) return;
            window.RequestTaskbarAttention();
            if (preview is not { } message) return;
            if (notification is null)
            {
                notification = new MessageNotificationWindow(() => ShowWindow(window));
                notification.Closed += (_, _) => notification = null;
            }
            notification.Present(message.Sender, message.Text);
        };
    }
    internal static Icon MakeIcon(int size = 32)
    {
        using var stream = GetResourceStream(new Uri("pack://application:,,,/CydLan;component/Assets/cydlan.ico")).Stream;
        using var icon = new Icon(stream, size, size);
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
        log?.Write("application.stopping");
        try { if (model is not null) await model.DisposeAsync(); }
        catch (Exception e) { log?.Write("application.shutdown_failed", e); Environment.ExitCode = 1; }
        finally
        {
            activateRegistration?.Unregister(null);
            activateEvent?.Dispose();
            floatingPet?.Dispose();
            trayMenu?.CloseOnce();
            notification?.Close();
            tray?.Dispose(); mutex?.Dispose(); Shutdown(Environment.ExitCode);
        }
    }
}
