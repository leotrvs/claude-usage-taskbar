using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;
using Microsoft.Win32;

static class Installer
{
    const string UninstallKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\" + App.Id;
    const int ExitWaitMs = 3000;

    static string StartMenuShortcut
    {
        get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), App.Name + ".lnk"); }
    }

    static string DesktopShortcut
    {
        get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), App.Name + ".lnk"); }
    }

    public static void RunInteractive()
    {
        DialogResult answer = MessageBox.Show(Localization.T("installPrompt"), Localization.T("installTitle"),
            MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        if (answer != DialogResult.Yes) return;

        if (!Install())
            MessageBox.Show(Localization.T("connectFailed"), App.Name, MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }

    public static bool Install()
    {
        StopInstalledCopy();
        Directory.CreateDirectory(App.InstallDir);
        File.Copy(App.ExePath, App.InstalledExe, true);
        Native.DeleteFile(App.InstalledExe + ":Zone.Identifier"); // The user already approved the download.

        CreateShortcut(StartMenuShortcut);
        CreateShortcut(DesktopShortcut);
        Settings.StartWithWindows = true;
        RegisterInAppsList();
        bool connected = ClaudeCodeConnection.Connect(App.InstalledExe);

        Process.Start(App.InstalledExe, "--startup").Dispose();
        return connected;
    }

    public static void RunUninstall()
    {
        DialogResult answer = MessageBox.Show(Localization.T("uninstallPrompt"), Localization.T("uninstallTitle"),
            MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        if (answer != DialogResult.Yes) return;

        ClaudeCodeConnection.Disconnect();
        StopInstalledCopy();
        Settings.StartWithWindows = false;
        Registry.CurrentUser.DeleteSubKeyTree(UninstallKeyPath, false);
        Registry.CurrentUser.DeleteSubKeyTree(Settings.KeyPath, false);
        File.Delete(StartMenuShortcut);
        File.Delete(DesktopShortcut);
        if (Directory.Exists(App.DataDir)) Directory.Delete(App.DataDir, true);
        DeleteInstallDirAfterExit();
    }

    // Keeps the version shown in Windows' app list correct after an automatic update.
    public static void RegisterInAppsList()
    {
        using (RegistryKey key = Registry.CurrentUser.CreateSubKey(UninstallKeyPath))
        {
            key.SetValue("DisplayName", App.Name);
            key.SetValue("DisplayVersion", App.Version);
            key.SetValue("DisplayIcon", App.InstalledExe);
            key.SetValue("InstallLocation", App.InstallDir);
            key.SetValue("Publisher", "claude-usage-taskbar contributors");
            key.SetValue("UninstallString", "\"" + App.InstalledExe + "\" --uninstall");
            key.SetValue("NoModify", 1, RegistryValueKind.DWord);
            key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
        }
    }

    static void StopInstalledCopy()
    {
        int self = Process.GetCurrentProcess().Id;
        foreach (Process process in Process.GetProcessesByName(App.Id))
        {
            using (process)
            {
                if (process.Id == self) continue;
                try
                {
                    process.Kill();
                    process.WaitForExit(ExitWaitMs);
                }
                catch (Exception)
                {
                    // Already gone, or a status line call that finished on its own.
                }
            }
        }
    }

    static void CreateShortcut(string path)
    {
        dynamic shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell"));
        dynamic shortcut = shell.CreateShortcut(path);
        shortcut.TargetPath = App.InstalledExe;
        shortcut.WorkingDirectory = App.InstallDir;
        shortcut.IconLocation = App.InstalledExe + ",0";
        shortcut.Description = App.Name;
        shortcut.Save();
    }

    static void DeleteInstallDirAfterExit()
    {
        ProcessStartInfo start = new ProcessStartInfo("cmd.exe",
            "/c ping -n 3 127.0.0.1 >nul & rmdir /s /q \"" + App.InstallDir + "\"");
        start.UseShellExecute = false;
        start.CreateNoWindow = true;
        start.WorkingDirectory = Path.GetTempPath();
        Process.Start(start).Dispose();
    }
}
