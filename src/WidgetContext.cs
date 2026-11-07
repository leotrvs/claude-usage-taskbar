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
    UpdateInfo availableUpdate;
    bool updateBusy;

    public WidgetContext(WaitHandle showEvent, bool showPanelNow)
    {
        Updater.CleanUp();
        if (App.IsInstalled) Installer.RegisterInAppsList();

        PanelCallbacks callbacks = new PanelCallbacks();
        callbacks.ChangeStyle = ChangeStyle;
        callbacks.ChangeLanguage = ChangeLanguage;
        callbacks.ChangeColors = ChangeColors;
        callbacks.ChangePanelTheme = ChangePanelTheme;
        callbacks.ToggleStartWithWindows = delegate { Settings.StartWithWindows = !Settings.StartWithWindows; };
        callbacks.ToggleAutoUpdate = delegate { Settings.AutoUpdate = !Settings.AutoUpdate; };
        callbacks.ToggleConnection = ToggleConnection;
        callbacks.ChangeIconsShown = ChangeIconsShown;
        callbacks.ToggleNotifications = delegate { Settings.Notifications = !Settings.Notifications; };
        callbacks.CheckOrInstallUpdate = CheckOrInstallUpdate;
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
        updateTimer.Tick += delegate { OnUpdateTimer(); };
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

    void ChangeColors(ColorTemplate colors)
    {
        Theme.Colors = colors;
        Settings.Colors = colors;
        Render();
    }

    void ChangePanelTheme(PanelTheme theme)
    {
        Theme.Panel = theme;
        Settings.PanelTheme = theme;
        panel.ApplyTheme();
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

    // With the panel open, the update waits for a click instead of closing it under the cursor.
    void OnUpdateTimer()
    {
        updateTimer.Interval = UpdateCheckIntervalMs;
        if (Settings.AutoUpdate) CheckForUpdate(false, !panel.Visible);
    }

    void CheckOrInstallUpdate()
    {
        if (availableUpdate != null)
        {
            InstallUpdate();
            return;
        }
        if (updateBusy) return;
        panel.SetUpdate(UpdatePhase.Checking, null);
        CheckForUpdate(true, false);
    }

    void CheckForUpdate(bool manual, bool install)
    {
        if (updateBusy) return;
        updateBusy = true;
        ThreadPool.QueueUserWorkItem(delegate
        {
            UpdateInfo latest;
            UpdateResult result = Updater.Check(out latest);
            bool ready = install && result == UpdateResult.Available && Updater.DownloadVerified(latest);
            panel.BeginInvoke(new Action(delegate
            {
                updateBusy = false;
                if (ready && Updater.ApplyAndRestart())
                {
                    ExitThread();
                    return;
                }
                if (result == UpdateResult.Available)
                {
                    availableUpdate = latest;
                    panel.SetUpdate(UpdatePhase.Available, latest.Version);
                }
                else if (manual)
                {
                    panel.SetUpdate(result == UpdateResult.UpToDate ? UpdatePhase.UpToDate : UpdatePhase.Failed, null);
                }
            }));
        });
    }

    void InstallUpdate()
    {
        if (updateBusy) return;
        updateBusy = true;
        UpdateInfo update = availableUpdate;
        panel.SetUpdate(UpdatePhase.Installing, update.Version);
        ThreadPool.QueueUserWorkItem(delegate
        {
            bool ready = Updater.DownloadVerified(update);
            panel.BeginInvoke(new Action(delegate
            {
                updateBusy = false;
                if (ready && Updater.ApplyAndRestart()) ExitThread();
                else panel.SetUpdate(UpdatePhase.Failed, update.Version);
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
