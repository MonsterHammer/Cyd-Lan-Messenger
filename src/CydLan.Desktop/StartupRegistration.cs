using Microsoft.Win32;

namespace CydLan.Desktop;

/// <summary>"Start CydLan when Windows starts": a per-user Run entry (no admin rights needed).</summary>
public static class StartupRegistration
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run", ValueName = "CydLan";

    public static void Apply(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKey);
            if (enabled && Environment.ProcessPath is { } exe && exe.EndsWith("CydLan.exe", StringComparison.OrdinalIgnoreCase)) key.SetValue(ValueName, $"\"{exe}\"");
            else if (!enabled && key.GetValue(ValueName) is not null) key.DeleteValue(ValueName, false);
        }
        catch (Exception e) when (e is UnauthorizedAccessException or System.Security.SecurityException or System.IO.IOException) { }
    }

    public static bool IsEnabled()
    {
        try { using var key = Registry.CurrentUser.OpenSubKey(RunKey); return key?.GetValue(ValueName) is not null; }
        catch (Exception e) when (e is UnauthorizedAccessException or System.Security.SecurityException) { return false; }
    }
}
