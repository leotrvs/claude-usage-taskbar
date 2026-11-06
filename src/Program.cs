using System;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

static class Program
{
    const string MutexName = @"Local\ClaudeUsageTaskbar";
    const string ShowEventName = @"Local\ClaudeUsageTaskbar.Show";
    const int UpdateHandoverMs = 5000;

    [STAThread]
    static void Main(string[] args)
    {
        // Runs on every Claude Code status update, so it stays out of the GUI path.
        if (args.Contains("--statusline"))
        {
            StatusLineBridge.Run();
            return;
        }

        Native.SetProcessDPIAware();
        Localization.Apply(Settings.Language);

        if (args.Contains("--uninstall")) { Installer.RunUninstall(); return; }
        if (args.Contains("--install-silent")) { Installer.Install(); return; }

        int previewIndex = Array.IndexOf(args, "--preview");
        if (previewIndex >= 0 && previewIndex + 1 < args.Length)
        {
            Preview.Render(args[previewIndex + 1]);
            return;
        }

        if (!App.IsInstalled && !args.Contains("--portable") && !args.Contains("--after-update"))
        {
            Installer.RunInteractive();
            return;
        }

        RunWidget(args);
    }

    static void RunWidget(string[] args)
    {
        bool startedByWindows = args.Contains("--startup");
        bool afterUpdate = args.Contains("--after-update");

        bool ownsMutex;
        using (Mutex mutex = new Mutex(true, MutexName, out ownsMutex))
        using (EventWaitHandle showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowEventName))
        {
            if (!ownsMutex && afterUpdate) ownsMutex = WaitForPreviousVersion(mutex);
            if (!ownsMutex)
            {
                // Launching from the Start menu while running just opens the panel.
                Native.AllowSetForegroundWindow(-1);
                showEvent.Set();
                return;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new WidgetContext(showEvent, !startedByWindows && !afterUpdate));
            GC.KeepAlive(mutex);
        }
    }

    static bool WaitForPreviousVersion(Mutex mutex)
    {
        try { return mutex.WaitOne(UpdateHandoverMs); }
        catch (AbandonedMutexException) { return true; }
    }
}
