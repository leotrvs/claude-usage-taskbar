using System;
using System.Globalization;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

class WidgetContext : ApplicationContext
{
    const int FileCheckIntervalMs = 5 * 1000;
    const int WarningPercent = 80;
    const int CriticalPercent = 90;
    const int MinuteInTicks = 12; // File checks per minute, to roll countdowns and passed resets forward.
    const int FirstUpdateCheckMs = 5 * 1000;
    const int UpdateCheckIntervalMs = 60 * 60 * 1000;
    const int FirstProbeMs = 3 * 1000;
    const int ProbeIntervalMs = 2 * 60 * 1000;
    const int MinimumManualCheckMs = 900; // Long enough to see the icon turn on a fast connection.

    readonly UsagePanel panel;
    readonly TraySlot sessionSlot;
    readonly TraySlot weeklySlot;
    readonly System.Windows.Forms.Timer fileTimer = new System.Windows.Forms.Timer();
    readonly System.Windows.Forms.Timer updateTimer = new System.Windows.Forms.Timer();
    readonly System.Windows.Forms.Timer probeTimer = new System.Windows.Forms.Timer();
    readonly RegisteredWaitHandle showWait;
    PanelState current = new PanelState();
    IconStyle iconStyle = Settings.IconStyle;
    DateTime lastFileWrite = DateTime.MinValue;
    int ticksSinceRender;
    UpdateInfo availableUpdate;
    bool updateBusy;
    int probing;

    public WidgetContext(WaitHandle showEvent, bool showPanelNow)
    {
        Updater.CleanUp();
        if (App.IsInstalled) Installer.RegisterInAppsList();

        PanelCallbacks callbacks = new PanelCallbacks();
        callbacks.ChangeStyle = ChangeStyle;
        callbacks.ChangeLanguage = ChangeLanguage;
        callbacks.ChangeColors = ChangeColors;
        callbacks.ChangePanelTheme = ChangePanelTheme;
        callbacks.ChangeMascot = ChangeMascot;
        callbacks.ToggleStartWithWindows = delegate { Settings.StartWithWindows = !Settings.StartWithWindows; };
        callbacks.ToggleAutoUpdate = delegate { Settings.AutoUpdate = !Settings.AutoUpdate; };
        callbacks.ToggleConnection = ToggleConnection;
        callbacks.ChangeIconsShown = ChangeIconsShown;
        callbacks.ToggleNotifications = delegate { Settings.Notifications = !Settings.Notifications; };
        callbacks.CheckOrInstallUpdate = CheckOrInstallUpdate;
        callbacks.CloseWidget = ExitThread;

        panel = new UsagePanel(callbacks);
        panel.VisibleChanged += delegate { InstallWaitingUpdate(); };
        IntPtr ensureHandle = panel.Handle; // BeginInvoke needs it before the first Show.
        GC.KeepAlive(ensureHandle);

        SystemEvents.UserPreferenceChanged += OnSystemPreferenceChanged;
        sessionSlot = new TraySlot(true, OnTrayMouseUp);
        weeklySlot = new TraySlot(false, OnTrayMouseUp);
        Reload();
        NotifyIfUpdated();

        fileTimer.Interval = FileCheckIntervalMs;
        fileTimer.Tick += delegate { CheckFile(); };
        fileTimer.Start();

        updateTimer.Interval = FirstUpdateCheckMs;
        updateTimer.Tick += delegate { OnUpdateTimer(); };
        updateTimer.Start();

        probeTimer.Interval = FirstProbeMs;
        probeTimer.Tick += delegate { OnProbeTimer(); };
        probeTimer.Start();

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

    // The result lands in the usage file, which the file timer picks up.
    void OnProbeTimer()
    {
        probeTimer.Interval = ProbeIntervalMs;
        if (!ClaudeCodeConnection.IsConnected) return;
        if (Interlocked.CompareExchange(ref probing, 1, 0) != 0) return;
        ThreadPool.QueueUserWorkItem(delegate
        {
            UsageProbe.Run();
            Interlocked.Exchange(ref probing, 0);
        });
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

    // Windows reports a change of light or dark mode here, which affects the Auto theme and the icons.
    void OnSystemPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category != UserPreferenceCategory.General) return;
        Theme.RefreshSystemTheme();
        panel.ApplyTheme();
        Render();
    }

    void ChangeMascot(MascotStyle style)
    {
        Settings.MascotStyle = style;
        panel.ChangeMascot(style);
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

    // Earlier versions kept no record, so one with usage data on disk counts as an update.
    void NotifyIfUpdated()
    {
        string last = Settings.LastVersion;
        Settings.LastVersion = App.Version;
        bool updated = last != null ? last != App.Version : UsageStore.LastWriteUtc != DateTime.MinValue;
        if (!updated || !Settings.Notifications) return;
        TraySlot slot = sessionSlot.IsShown ? sessionSlot : weeklySlot;
        slot.NotifyInfo(Localization.T("updatedTitle"), Localization.T("updatedBody", App.Name, App.Version));
    }

    // The first check installs at once. Later ones find the panel in use, so it is not closed under the cursor.
    void OnUpdateTimer()
    {
        bool firstCheck = updateTimer.Interval == FirstUpdateCheckMs;
        updateTimer.Interval = UpdateCheckIntervalMs;
        if (Settings.AutoUpdate) CheckForUpdate(false, firstCheck || !panel.Visible);
    }

    // An update found with the panel open is installed once the panel closes.
    void InstallWaitingUpdate()
    {
        if (!panel.Visible && availableUpdate != null && Settings.AutoUpdate) InstallUpdate();
    }

    void CheckOrInstallUpdate()
    {
        if (availableUpdate != null)
        {
            InstallUpdate();
            return;
        }
        CheckForUpdate(true, false);
    }

    void CheckForUpdate(bool manual, bool install)
    {
        if (updateBusy) return;
        updateBusy = true;
        panel.SetUpdate(UpdatePhase.Checking, null);
        ThreadPool.QueueUserWorkItem(delegate
        {
            int started = Environment.TickCount;
            UpdateInfo latest;
            UpdateResult result = Updater.Check(out latest);
            int remaining = MinimumManualCheckMs - (Environment.TickCount - started);
            if (manual && remaining > 0) Thread.Sleep(remaining);
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
                else if (result == UpdateResult.UpToDate)
                {
                    panel.SetUpdate(UpdatePhase.UpToDate, null);
                }
                else
                {
                    panel.SetUpdate(manual ? UpdatePhase.Failed : UpdatePhase.Idle, null);
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
        SystemEvents.UserPreferenceChanged -= OnSystemPreferenceChanged;
        fileTimer.Dispose();
        updateTimer.Dispose();
        probeTimer.Dispose();
        showWait.Unregister(null);
        sessionSlot.Dispose();
        weeklySlot.Dispose();
        panel.Dispose();
        base.ExitThreadCore();
    }
}
