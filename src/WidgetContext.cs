using System;
using System.Globalization;
using System.Threading;
using System.Windows.Forms;

class WidgetContext : ApplicationContext
{
    const int FileCheckIntervalMs = 5 * 1000;
    const int WarningPercent = 80;
    const int CriticalPercent = 90;
    const int MinuteInTicks = 12; // File checks per minute, to roll countdowns and passed resets forward.
    const int FirstUpdateCheckMs = 60 * 1000;
    const int UpdateCheckIntervalMs = 6 * 60 * 60 * 1000;

    readonly UsagePanel panel;
    readonly TraySlot sessionSlot;
    readonly TraySlot weeklySlot;
    readonly System.Windows.Forms.Timer fileTimer = new System.Windows.Forms.Timer();
    readonly System.Windows.Forms.Timer updateTimer = new System.Windows.Forms.Timer();
    readonly RegisteredWaitHandle showWait;
    PanelState current = new PanelState();
    IconStyle iconStyle = Settings.IconStyle;
    DateTime lastFileWrite = DateTime.MinValue;
    int ticksSinceRender;

    public WidgetContext(WaitHandle showEvent, bool showPanelNow)
    {
        Updater.CleanUp();
        if (App.IsInstalled) Installer.RegisterInAppsList();

        PanelCallbacks callbacks = new PanelCallbacks();
        callbacks.ChangeStyle = ChangeStyle;
        callbacks.ChangeLanguage = ChangeLanguage;
        callbacks.ToggleStartWithWindows = delegate { Settings.StartWithWindows = !Settings.StartWithWindows; };
        callbacks.ToggleAutoUpdate = delegate { Settings.AutoUpdate = !Settings.AutoUpdate; };
        callbacks.ToggleConnection = ToggleConnection;
        callbacks.ChangeIconsShown = ChangeIconsShown;
        callbacks.ToggleNotifications = delegate { Settings.Notifications = !Settings.Notifications; };
        callbacks.CloseWidget = ExitThread;

        panel = new UsagePanel(callbacks);
        IntPtr ensureHandle = panel.Handle; // BeginInvoke needs it before the first Show.
        GC.KeepAlive(ensureHandle);

        sessionSlot = new TraySlot(true, OnTrayMouseUp);
        weeklySlot = new TraySlot(false, OnTrayMouseUp);
        Reload();

        fileTimer.Interval = FileCheckIntervalMs;
        fileTimer.Tick += delegate { CheckFile(); };
        fileTimer.Start();

        updateTimer.Interval = FirstUpdateCheckMs;
        updateTimer.Tick += delegate { CheckForUpdate(); };
        updateTimer.Start();

        showWait = ThreadPool.RegisterWaitForSingleObject(showEvent, delegate
        {
            panel.BeginInvoke(new Action(panel.ShowPanel));
        }, null, Timeout.Infinite, false);

        if (showPanelNow) panel.ShowPanel();
    }

    void OnTrayMouseUp(object sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Right || e.Button == MouseButtons.Left) panel.Toggle();
    }

    void CheckFile()
    {
        DateTime written = UsageStore.LastWriteUtc;
        if (written != lastFileWrite || ++ticksSinceRender >= MinuteInTicks) Reload();
    }

    void Reload()
    {
        lastFileWrite = UsageStore.LastWriteUtc;
        ticksSinceRender = 0;

        UsageSnapshot snapshot = UsageStore.Load();
        PanelState next = new PanelState();
        if (snapshot == null)
        {
            next.Status = Localization.T("waiting");
        }
        else
        {
            next.Session = snapshot.Session;
            next.Weekly = snapshot.Weekly;
            next.Stale = false;
            next.Status = Localization.T("updatedAt", snapshot.UpdatedAt.ToString("HH:mm", Localization.Culture));
            next.History = UsageStore.LoadHistory();
            next.SessionPace = PaceEstimator.DescribeSession(next.Session, next.History, DateTime.Now);
            next.WeeklyPace = PaceEstimator.DescribeWeekly(next.Weekly, DateTime.Now);
        }
        current = next;
        Render();
        CheckAlert("Session", "session", current.Session, sessionSlot);
        CheckAlert("Weekly", "weekly", current.Weekly, weeklySlot);
    }

    // Alerts once per limit period at each level, on an icon that is visible.
    void CheckAlert(string limit, string labelKey, UsageWindow window, TraySlot slot)
    {
        if (!window.Percent.HasValue || !window.ResetsAt.HasValue) return;
        int level = window.Percent.Value >= CriticalPercent ? CriticalPercent
            : window.Percent.Value >= WarningPercent ? WarningPercent : 0;
        if (level == 0) return;

        string period = window.ResetsAt.Value.ToString("yyyyMMddHHmm", CultureInfo.InvariantCulture);
        string stored = Settings.AlertState(limit);
        if (stored == period + ":" + CriticalPercent || stored == period + ":" + level) return;
        Settings.SetAlertState(limit, period + ":" + level);
        if (!Settings.Notifications) return;

        TraySlot target = slot.IsShown ? slot : (sessionSlot.IsShown ? sessionSlot : weeklySlot);
        target.Notify(
            Localization.T("alertTitle", Localization.T(labelKey), window.Percent.Value),
            Localization.T("alertBody", window.ResetsAt.Value.ToString("ddd HH:mm", Localization.Culture)));
    }

    void Render()
    {
        IconsShown shown = Settings.IconsShown;
        sessionSlot.Update(iconStyle, current.Session.Percent, current.Stale, Tooltip("tooltip5h", current.Session, "HH:mm"), shown != IconsShown.Weekly);
        weeklySlot.Update(iconStyle, current.Weekly.Percent, current.Stale, Tooltip("tooltipWeek", current.Weekly, "ddd HH:mm"), shown != IconsShown.Session);
        panel.SetState(current, iconStyle, Settings.Language, shown);
    }

    string Tooltip(string labelKey, UsageWindow window, string dateFormat)
    {
        string text = Localization.T(labelKey) + ": " + (window.Percent.HasValue ? window.Percent.Value + "%" : "--");
        if (current.Stale) return text + " - " + current.Status;
        if (!window.ResetsAt.HasValue) return text;
        return text + " - " + Localization.T("tooltipBack", window.ResetsAt.Value.ToString(dateFormat, Localization.Culture));
    }

    void ChangeStyle(IconStyle style)
    {
        iconStyle = style;
        Settings.IconStyle = style;
        Render();
    }

    void ChangeIconsShown(IconsShown shown)
    {
        Settings.IconsShown = shown;
        Render();
    }

    void ChangeLanguage(Language language)
    {
        Settings.Language = language;
        Localization.Apply(language);
        Reload();
    }

    void ToggleConnection()
    {
        if (ClaudeCodeConnection.IsConnected) ClaudeCodeConnection.Disconnect();
        else ClaudeCodeConnection.Connect(App.ExePath);
    }

    void CheckForUpdate()
    {
        updateTimer.Interval = UpdateCheckIntervalMs;
        ThreadPool.QueueUserWorkItem(delegate
        {
            if (!Updater.DownloadVerified()) return;
            panel.BeginInvoke(new Action(delegate
            {
                if (Updater.ApplyAndRestart()) ExitThread();
            }));
        });
    }

    protected override void ExitThreadCore()
    {
        fileTimer.Dispose();
        updateTimer.Dispose();
        showWait.Unregister(null);
        sessionSlot.Dispose();
        weeklySlot.Dispose();
        panel.Dispose();
        base.ExitThreadCore();
    }
}
