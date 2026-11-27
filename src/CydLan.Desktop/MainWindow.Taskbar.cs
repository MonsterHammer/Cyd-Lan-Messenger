using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace CydLan.Desktop;

public partial class MainWindow
{
    private const uint FlashTaskbar = 0x00000002;
    private const uint FlashUntilForeground = 0x0000000C;
    private const int ForceIconicRepresentation = 7;
    private const int HasIconicBitmap = 10;
    private const int DisallowPeek = 11;
    private const int SendIconicThumbnail = 0x0323;
    private const int ExtendedStyle = -20;
    private const int AppWindowStyle = 0x00040000;
    private const int ToolWindowStyle = 0x00000080;
    private const int ShowMinimizedWithoutActivation = 7;
    private const uint UpdateFrameWithoutMoving = 0x0037; // FRAMECHANGED | NOACTIVATE | NOZORDER | NOMOVE | NOSIZE
    private static readonly int TaskbarButtonCreated = RegisterWindowMessage("TaskbarButtonCreated");
    private bool taskbarAttentionRequested;

    internal void RequestTaskbarAttention()
    {
        if (IsVisible && WindowState != WindowState.Minimized && IsActive) return;
        taskbarAttentionRequested = true;
        if (!IsVisible)
        {
            // Add a taskbar button for the hidden window without showing the conversation.
            bool activateOnShow = ShowActivated;
            ShowActivated = false;
            try
            {
                ShowInTaskbar = true;
                WindowState = WindowState.Minimized;
                Show();
            }
            finally { ShowActivated = activateOnShow; }
            var handle = new WindowInteropHelper(this).Handle;
            int styles = GetWindowLong(handle, ExtendedStyle);
            int taskbarStyles = (styles | AppWindowStyle) & ~ToolWindowStyle;
            if (styles != taskbarStyles)
            {
                SetWindowLong(handle, ExtendedStyle, taskbarStyles);
                SetWindowPos(handle, IntPtr.Zero, 0, 0, 0, 0, UpdateFrameWithoutMoving);
            }
            ShowWindow(handle, ShowMinimizedWithoutActivation);
            Dispatcher.BeginInvoke(() =>
            {
                if (!taskbarAttentionRequested || !IsVisible || IsActive) return;
                var hwnd = new WindowInteropHelper(this).Handle;
                RegisterTaskbarButton(hwnd);
                FlashTaskbarButton(hwnd);
            }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        }
        FlashTaskbarButton(new WindowInteropHelper(this).Handle);
    }

    private static void RegisterTaskbarButton(IntPtr handle)
    {
        // Explorer can miss the button when WPF brings a tray-only window back already minimized.
        object? shell = null;
        try
        {
            var type = Type.GetTypeFromCLSID(new Guid("56FDF344-FD6D-11D0-958A-006097C9A090"));
            shell = type is null ? null : Activator.CreateInstance(type);
            if (shell is ITaskbarList taskbar && taskbar.HrInit() >= 0) taskbar.AddTab(handle);
        }
        catch (COMException) { } // The native window remains eligible if Explorer is restarting.
        finally { if (shell is not null) Marshal.ReleaseComObject(shell); }
    }

    private void ConfigureTaskbar(HwndSource source)
    {
        source.AddHook(HandleTaskbarMessage);
        // Windows normally uses a live image of the chat for its taskbar preview.
        int enabled = 1;
        DwmSetWindowAttribute(source.Handle, DisallowPeek, ref enabled, sizeof(int));
        DwmSetWindowAttribute(source.Handle, ForceIconicRepresentation, ref enabled, sizeof(int));
        DwmSetWindowAttribute(source.Handle, HasIconicBitmap, ref enabled, sizeof(int));
        Activated += (_, _) => taskbarAttentionRequested = false;
    }

    private IntPtr HandleTaskbarMessage(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message == TaskbarButtonCreated && taskbarAttentionRequested && IsVisible &&
            (WindowState == WindowState.Minimized || !IsActive))
            FlashTaskbarButton(hwnd);
        if (message == SendIconicThumbnail)
        {
            int requestedSize = lParam.ToInt32(); // Width is the high word, height is the low word.
            int size = Math.Min(128, Math.Min((requestedSize >> 16) & 0xffff, requestedSize & 0xffff));
            if (size > 0)
            {
                using var icon = App.MakeIcon(size);
                using var original = icon.ToBitmap();
                using var bitmap = new Bitmap(original, size, size);
                var handle = bitmap.GetHbitmap(Color.Transparent);
                try { DwmSetIconicThumbnail(hwnd, handle, 0); }
                finally { DeleteObject(handle); }
            }
            handled = true;
        }
        return IntPtr.Zero;
    }

    /// <summary>Asks Windows for a fresh taskbar thumbnail after the logo changes.</summary>
    internal void RefreshTaskbarIcon()
    {
        if (PresentationSource.FromVisual(this) is HwndSource source) DwmInvalidateIconicBitmaps(source.Handle);
    }

    private static void FlashTaskbarButton(IntPtr hwnd)
    {
        var info = new FlashInfo
        {
            Size = (uint)Marshal.SizeOf<FlashInfo>(),
            Window = hwnd,
            Flags = FlashTaskbar | FlashUntilForeground
        };
        FlashWindowEx(ref info);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct FlashInfo
    {
        public uint Size;
        public IntPtr Window;
        public uint Flags, Count, Timeout;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int RegisterWindowMessage(string name);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")] private static extern int GetWindowLong(IntPtr window, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongW")] private static extern int SetWindowLong(IntPtr window, int index, int value);
    [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr window, int command);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr window, IntPtr afterWindow, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool FlashWindowEx(ref FlashInfo info);
    [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);
    [DllImport("dwmapi.dll")] private static extern int DwmInvalidateIconicBitmaps(IntPtr window);
    [DllImport("dwmapi.dll")] private static extern int DwmSetIconicThumbnail(IntPtr window, IntPtr bitmap, uint flags);
    [DllImport("gdi32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool DeleteObject(IntPtr handle);

    [ComImport, Guid("56FDF342-FD6D-11D0-958A-006097C9A090"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface ITaskbarList
    {
        [PreserveSig] int HrInit();
        [PreserveSig] int AddTab(IntPtr window);
    }
}
