using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

[assembly: AssemblyTitle(App.Name)]
[assembly: AssemblyProduct(App.Name)]
[assembly: AssemblyVersion(App.Version + ".0")]
[assembly: AssemblyFileVersion(App.Version + ".0")]

static class App
{
    public const string Name = "Claude Usage Taskbar";
    public const string Version = "1.7.3";
    public const string Id = "ClaudeUsageTaskbar";
    public const string ExeName = Id + ".exe";

    public static string InstallDir
    {
        get { return Path.Combine(LocalAppData, "Programs", Id); }
    }

    public static string InstalledExe
    {
        get { return Path.Combine(InstallDir, ExeName); }
    }

    public static string DataDir
    {
        get { return Path.Combine(LocalAppData, Id); }
    }

    public static string ExePath
    {
        get { return Assembly.GetExecutingAssembly().Location; }
    }

    public static bool IsInstalled
    {
        get { return string.Equals(Path.GetFullPath(ExePath), Path.GetFullPath(InstalledExe), StringComparison.OrdinalIgnoreCase); }
    }

    static string LocalAppData
    {
        get { return Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData); }
    }
}

static class Native
{
    [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
    [DllImport("user32.dll")] public static extern bool DestroyIcon(IntPtr handle);
    const int AcceptsNotifications = 5;

    [DllImport("shell32.dll")] static extern int SHQueryUserNotificationState(out int state);

    // False during Do Not Disturb, full-screen apps and presentations, when only Windows should decide what appears.
    public static bool CanShowOwnNotification
    {
        get
        {
            int state;
            return SHQueryUserNotificationState(out state) == 0 && state == AcceptsNotifications;
        }
    }

    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr handle);
    [DllImport("user32.dll")] public static extern bool AllowSetForegroundWindow(int processId);
    [DllImport("gdi32.dll")] public static extern IntPtr AddFontMemResourceEx(IntPtr font, uint size, IntPtr reserved, out uint fontsInstalled);
    [DllImport("dwmapi.dll")] public static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] public static extern bool DeleteFile(string path);
}
