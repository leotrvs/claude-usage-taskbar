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
    const int ShortestProbeGapMs = 30 * 1000; // Opening the panel repeatedly reads once.
    const int OutOfDateMinutes = 6;
    const int RenewedFromPercent = 90;
    const int RenewedDrop = 50;
    const int BusyBreakMinutes = 10; // A longer pause starts a new stretch of work.
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
    int lastProbeTick = Environment.TickCount - ShortestProbeGapMs;
    int? lastSessionPercent;
    int? lastWeeklyPercent;
    DateTime? busySince;
    DateTime lastBusy;
    readonly TrayMenu trayMenu;
    readonly Toast toast;

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
        callbacks.OpenRepository = OpenRepository;
        callbacks.CloseWidget = ExitThread;

        panel = new UsagePanel(callbacks);
        panel.VisibleChanged += delegate
        {
            InstallWaitingUpdate();
            if (!panel.Visible) return;
            ReadUsage(false);
            RefreshSessions();
        };
        IntPtr ensureHandle = panel.Handle; // BeginInvoke needs it before the first Show.
        GC.KeepAlive(ensureHandle);

        SystemEvents.UserPreferenceChanged += OnSystemPreferenceChanged;
        TrayMenuActions menuActions = new TrayMenuActions();
        menuActions.OpenPanel = delegate { LaterOnPanel(panel.ShowPanel); };
        menuActions.Refresh = delegate { ReadUsage(true); };
        menuActions.OpenSettings = delegate { LaterOnPanel(OpenSettings); };
        menuActions.Quit = ExitThread;
        trayMenu = new TrayMenu(menuActions);
        toast = new Toast(delegate { LaterOnPanel(panel.ShowPanel); });
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
        if (e.Button == MouseButtons.Left) panel.Toggle();
        else if (e.Button == MouseButtons.Right) trayMenu.ShowAt(Cursor.Position);
    }

    // Runs after the menu has closed, so it does not take the focus from the panel.
    void LaterOnPanel(Action action)
    {
        panel.BeginInvoke(action);
    }

    void OpenSettings()
    {
        panel.ShowPanel();
        panel.ShowSettingsPage();
    }

    // Every few seconds while the panel is open, and once a minute with it closed to keep the work time right.
    void RefreshSessions()
    {
        ReadSessions(current);
        panel.RefreshMascot();
    }

    void ReadSessions(PanelState state)
    {
        state.Sessions = ClaudeSessions.Read();
        DateTime now = DateTime.Now;
        if (state.Sessions.Busy > 0)
        {
            lastBusy = now;
            if (!busySince.HasValue) busySince = now;
        }
        else if (busySince.HasValue && (now - lastBusy).TotalMinutes > BusyBreakMinutes)
        {
            busySince = null;
        }
        state.BusyMinutes = busySince.HasValue ? (now - busySince.Value).TotalMinutes : 0;
    }

    // The result lands in the usage file, which the file timer picks up.
    void OnProbeTimer()
    {
        probeTimer.Interval = ProbeIntervalMs;
        ReadUsage(false);
    }

    void ReadUsage(bool now)
    {
        if (!now && Environment.TickCount - lastProbeTick < ShortestProbeGapMs) return;
        if (!ClaudeCodeConnection.IsConnected) return;
        if (Interlocked.CompareExchange(ref probing, 1, 0) != 0) return;
        lastProbeTick = Environment.TickCount;
        ThreadPool.QueueUserWorkItem(delegate
        {
            UsageProbe.Run();
            Interlocked.Exchange(ref probing, 0);
            ShowProbeResult();
        });
    }

    // Also needed after a failed read, which leaves the usage file unchanged.
    void ShowProbeResult()
    {
        try { panel.BeginInvoke(new Action(Reload)); }
        catch (InvalidOperationException) { } // The app is closing.
    }

    void CheckFile()
    {
        DateTime written = UsageStore.LastWriteUtc;
        if (written != lastFileWrite || ++ticksSinceRender >= MinuteInTicks) Reload();
        if (panel.Visible) RefreshSessions();
    }

    void Reload()
    {
        lastFileWrite = UsageStore.LastWriteUtc;
        ticksSinceRender = 0;

        UsageSnapshot snapshot = UsageStore.Load();
        PanelState next = new PanelState();
        if (snapshot == null)
        {
            next.Status = Localization.T(UsageProbe.ClaudeFound ? "waiting" : "claudeMissing");
        }
        else
        {
            next.Session = snapshot.Session;
            next.Weekly = snapshot.Weekly;
            next.Stale = false;
            next.OutOfDate = (DateTime.Now - snapshot.UpdatedAt).TotalMinutes > OutOfDateMinutes;
            next.Status = Localization.T("updatedAt", snapshot.UpdatedAt.ToString("HH:mm", Localization.Culture));
            if (next.OutOfDate) next.Status += " · " + Localization.T("outOfDate");
            next.History = UsageStore.LoadHistory();
            next.SessionPace = PaceEstimator.DescribeSession(next.Session, next.History, DateTime.Now);
            next.MinutesToLimit = PaceEstimator.MinutesToLimit(next.Session, next.History, DateTime.Now);
            next.WeeklyPace = PaceEstimator.DescribeWeekly(next.Weekly, DateTime.Now);
        }
        ReadSessions(next);
        current = next;
        Render();
        CheckRenewed("session", current.Session, ref lastSessionPercent, sessionSlot);
        CheckRenewed("weekly", current.Weekly, ref lastWeeklyPercent, weeklySlot);
        CheckAlert("Session", "session", current.Session, sessionSlot);
        CheckAlert("Weekly", "weekly", current.Weekly, weeklySlot);
    }

    // A big drop from near the limit means its period has ended.
    void CheckRenewed(string labelKey, UsageWindow window, ref int? last, TraySlot slot)
    {
        if (!window.Percent.HasValue) return;
        int? before = last;
        last = window.Percent;
        if (!before.HasValue || before.Value < RenewedFromPercent || window.Percent.Value > before.Value - RenewedDrop) return;
        panel.PlayMascot(MascotAction.Celebrate);
        if (!Settings.Notifications) return;

        TraySlot target = slot.IsShown ? slot : (sessionSlot.IsShown ? sessionSlot : weeklySlot);
        Notify(target, Localization.T("renewedTitle", Localization.T(labelKey)), Localization.T("renewedBody"), false, MascotMood.Idle, MascotAction.Celebrate);
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
        Notify(target,
            Localization.T("alertTitle", Localization.T(labelKey), window.Percent.Value),
            Localization.T("alertBody", window.ResetsAt.Value.ToString("ddd HH:mm", Localization.Culture)),
            true, level >= CriticalPercent ? MascotMood.Panic : MascotMood.Tired, null);
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
        Notify(slot, Localization.T("updatedTitle"), Localization.T("updatedBody", App.Name, App.Version), false, MascotMood.Idle, MascotAction.Rear);
    }

    // Windows' own notification is used when it should stay in charge of what appears, such as during Do Not Disturb.
    void Notify(TraySlot slot, string title, string body, bool warning, MascotMood mood, MascotAction? action)
    {
        if (Native.CanShowOwnNotification) toast.Present(title, body, mood, action);
        else if (warning) slot.Notify(title, body);
        else slot.NotifyInfo(title, body);
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

    void OpenRepository()
    {
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("https://github.com/" + App.Repository) { UseShellExecute = true }).Dispose(); }
        catch { }
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
        trayMenu.Dispose();
        toast.Dispose();
        sessionSlot.Dispose();
        weeklySlot.Dispose();
        panel.Dispose();
        base.ExitThreadCore();
    }
}
