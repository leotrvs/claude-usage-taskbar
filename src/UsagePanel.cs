using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Windows.Forms;

enum UpdatePhase { Idle, Checking, UpToDate, Failed, Available, Installing }

class PanelState
{
    public UsageWindow Session = new UsageWindow();
    public UsageWindow Weekly = new UsageWindow();
    public bool Stale = true;
    public bool OutOfDate; // Numbers exist, but the latest reads have failed.
    public string Status = Localization.T("reading");
    public string SessionPace;
    public string WeeklyPace;
    public List<UsageSample> History = new List<UsageSample>();
    public SessionActivity Sessions = new SessionActivity();
    public double BusyMinutes;
    public double? MinutesToLimit;
}

class PanelCallbacks
{
    public Action<IconStyle> ChangeStyle = delegate { };
    public Action<Language> ChangeLanguage = delegate { };
    public Action<ColorTemplate> ChangeColors = delegate { };
    public Action<PanelTheme> ChangePanelTheme = delegate { };
    public Action<MascotStyle> ChangeMascot = delegate { };
    public Action<IconsShown> ChangeIconsShown = delegate { };
    public Action ToggleNotifications = delegate { };
    public Action ToggleStartWithWindows = delegate { };
    public Action ToggleAutoUpdate = delegate { };
    public Action ToggleConnection = delegate { };
    public Action CheckOrInstallUpdate = delegate { };
    public Action CloseWidget = delegate { };
}

class PanelTarget
{
    public Rectangle Bounds;
    public Action Run;
    public string Glyph;
    public string LabelKey;
    public string LiteralLabel;
    public IconStyle? Style;
    public Language? Language;
    public IconsShown? Shown;
    public ChartRange? Range;
    public Func<bool> Toggle;
    public Func<bool> Selected; // For chips that are not tied to one of the fields above.
    public Color? Swatch;
    public Bitmap Picture;
    public bool Tile;
    public float Hover; // How far the hover and on states have animated, from 0 to 1.
    public float On;
    public bool IsOn;

    public string Label
    {
        get { return LiteralLabel ?? Localization.T(LabelKey); }
    }
}

class UsagePanel : Form
{
    const int LogicalWidth = 300;
    const int MainHeight = 346;
    const int SettingsHeight = 394;
    const int AppearanceHeight = 552;
    const int SessionTop = 62;
    const int WeeklyTop = 132;
    const int WeeklyTopWithoutPace = 118; // Moves up into the line the session pace would use.
    const int HistoryTop = 200;
    const int ChartTop = 216;
    const int DividerTop = 284;
    const int ActionsTop = 292;
    const int ActionHeight = 46;
    const int MascotHeight = 42;
    const string PanelTitle = "Claude Usage";
    const string ChefBalloon = "im cooking"; // Slang, kept the same in every language.
    const int SpinTickMs = 40;
    const int SelectedFill = 70; // Strong enough to stay orange over the panel background.
    const int MotionTickMs = 15;
    const float HoverMs = 110;
    const float SwitchMs = 170;
    const float PageMs = 190;
    const int PageSlide = 18;
    const int LongestFrameMs = 50; // A late timer tick continues the motion instead of jumping.
    const string RefreshGlyph = "\uE72C";
    const string DownloadGlyph = "\uE896";
    const int LoveClicks = 5; // This many clicks within the window make the mascot melt.
    const int LoveWindowMs = 4000;
    const double ResetDropPoints = 20; // A fall this big between two readings is a limit starting over.
    const int StepMs = 100; // How long one step of the documentation animations lasts.
    const int RangeChipWidth = 34;
    const int RangeChipHeight = 18;
    const int ReopenGuardMilliseconds = 300;
    const int OpenAnimationMs = 160;
    const int CloseAnimationMs = 110;
    const int AnimationTickMs = 15;
    const int OpenSlide = 10;
    const int CloseSlide = 6;
    const int WS_EX_TOOLWINDOW = 0x80;
    const int CS_DROPSHADOW = 0x20000;
    const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    const int DWMWA_BORDER_COLOR = 34;
    const int DWMWCP_ROUND = 2;

    readonly List<PanelTarget> mainTargets = new List<PanelTarget>();
    readonly List<PanelTarget> settingsTargets = new List<PanelTarget>();
    readonly List<PanelTarget> appearanceTargets = new List<PanelTarget>();
    readonly float scale;
    readonly System.Windows.Forms.Timer mascotTimer = new System.Windows.Forms.Timer();
    readonly System.Windows.Forms.Timer animationTimer = new System.Windows.Forms.Timer();
    readonly System.Windows.Forms.Timer spinTimer = new System.Windows.Forms.Timer();
    readonly System.Windows.Forms.Timer motionTimer = new System.Windows.Forms.Timer();
    IMascot mascot;
    MascotStyle mascotStyle = Settings.MascotStyle;
    readonly Font titleFont, labelFont, smallFont, tinyFont, percentFont, glyphFont;
    List<PanelTarget> targets;
    PanelState state = new PanelState();
    IconStyle iconStyle;
    Language language;
    IconsShown iconsShown;
    int hovered = -1;
    int hiddenAt;
    int chartHoverX = -1;
    ChartRange chartRange = Settings.ChartRange;
    int animationStart;
    int restingTop;
    bool closing;
    int lastMotionTick;
    Bitmap pageFrom;
    Bitmap pageTo;
    float pageProgress;
    int pageDirection;
    int pageHeightFrom;
    int pageHeightTo;
    PanelTarget updateLink;
    PanelTarget updateRow;
    UpdatePhase updatePhase;
    string updateVersion;
    MascotAction? pendingMascotAction;
    bool overMascot;
    readonly Queue<int> mascotClicks = new Queue<int>();
    readonly Random playChance = new Random();
    MascotStyle shownStyle;
    DateTime updateCheckedAt;

    public UsagePanel(PanelCallbacks callbacks)
    {
        using (Graphics g = CreateGraphics()) scale = g.DpiX / 96f;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        DoubleBuffered = true;
        KeyPreview = true;
        BackColor = Theme.Background;
        Text = App.Name;
        ClientSize = new Size(Px(LogicalWidth), Px(MainHeight));

        titleFont = NewUiFont(16, true);
        labelFont = NewUiFont(13, false);
        smallFont = NewUiFont(12, false);
        tinyFont = NewUiFont(11, false);
        percentFont = NewUiFont(15, true);
        glyphFont = new Font("Segoe MDL2 Assets", Px(14), GraphicsUnit.Pixel);

        BuildMainPage(callbacks);
        BuildSettingsPage(callbacks);
        BuildAppearancePage(callbacks);
        targets = mainTargets;
        mascotTimer.Tick += delegate { AdvanceMascot(); };
        animationTimer.Interval = AnimationTickMs;
        animationTimer.Tick += delegate { AdvanceAnimation(); };
        motionTimer.Interval = MotionTickMs;
        motionTimer.Tick += delegate { AdvanceMotion(); };
        spinTimer.Interval = SpinTickMs;
        spinTimer.Tick += delegate { if (targets == settingsTargets) Invalidate(updateRow.Bounds); };
    }

    void BuildMainPage(PanelCallbacks callbacks)
    {
        updateLink = new PanelTarget();
        updateLink.Bounds = new Rectangle(Px(18), Px(33), Px(224), Px(18));
        updateLink.Run = callbacks.CheckOrInstallUpdate;
        mainTargets.Add(NewRow("", "settings", ActionsTop, delegate { ShowPage(settingsTargets); }));
        mainTargets.Add(NewRow("\uE790", "appearance", ActionsTop + 32, delegate { ShowPage(appearanceTargets); }));
        mainTargets.Add(NewRow("", "closeWidget", ActionsTop + 64, callbacks.CloseWidget));
        for (int column = 0; column < 3; column++)
        {
            PanelTarget action = mainTargets[column];
            action.Tile = true;
            action.Bounds = ChipBounds(column, 3, ActionsTop, ActionHeight);
        }
        AddRangeTarget(ChartRange.Hours12, "12h", 0);
        AddRangeTarget(ChartRange.Hours24, "24h", 1);
        AddRangeTarget(ChartRange.Days7, "7d", 2);
    }

    // Small switch on the history row, hidden while the chart shows its hover details.
    void AddRangeTarget(ChartRange value, string label, int index)
    {
        PanelTarget chip = new PanelTarget();
        chip.Range = value;
        chip.LiteralLabel = label;
        int right = ClientSize.Width - Px(18);
        chip.Bounds = new Rectangle(right - (3 - index) * Px(RangeChipWidth), Px(HistoryTop) - Px(2), Px(RangeChipWidth), Px(RangeChipHeight));
        chip.Run = delegate
        {
            chartRange = value;
            Settings.ChartRange = value;
            Invalidate();
        };
        mainTargets.Add(chip);
    }

    double ChartHours
    {
        get { return chartRange == ChartRange.Days7 ? 168 : chartRange == ChartRange.Hours24 ? 24 : 12; }
    }

    string HistoryLabel
    {
        get { return Localization.T(chartRange == ChartRange.Days7 ? "history7" : chartRange == ChartRange.Hours24 ? "history24" : "history"); }
    }

    // The week chart follows the weekly limit; the shorter ones follow the 5-hour session.
    double? ChartValue(UsageSample sample)
    {
        return chartRange == ChartRange.Days7 ? sample.Weekly : (double?)sample.Session;
    }

    void AddStyleChip(IconStyle style, string labelKey, int column, PanelCallbacks callbacks)
    {
        PanelTarget chip = new PanelTarget();
        chip.Style = style;
        chip.LabelKey = labelKey;
        chip.Bounds = ChipBounds(column, 3, 70, 50);
        chip.Run = delegate { callbacks.ChangeStyle(style); };
        appearanceTargets.Add(chip);
    }

    void BuildSettingsPage(PanelCallbacks callbacks)
    {
        settingsTargets.Add(NewRow("\uE72B", "back", 8, delegate { ShowPage(mainTargets); }));
        AddLanguageChip(Language.Auto, null, 0, callbacks);
        AddLanguageChip(Language.English, "English", 1, callbacks);
        AddLanguageChip(Language.Portuguese, "Português", 2, callbacks);
        AddShownChip(IconsShown.Both, "both", 0, callbacks);
        AddShownChip(IconsShown.Session, "sessionShort", 1, callbacks);
        AddShownChip(IconsShown.Weekly, "weeklyShort", 2, callbacks);
        settingsTargets.Add(NewToggle("startWithWindows", 192, delegate { return Settings.StartWithWindows; }, callbacks.ToggleStartWithWindows));
        settingsTargets.Add(NewToggle("autoUpdate", 226, delegate { return Settings.AutoUpdate; }, callbacks.ToggleAutoUpdate));
        settingsTargets.Add(NewToggle("notifications", 260, delegate { return Settings.Notifications; }, callbacks.ToggleNotifications));
        settingsTargets.Add(NewToggle("claudeCodeConnected", 294, delegate { return ClaudeCodeConnection.IsConnected; }, callbacks.ToggleConnection));
        updateRow = NewRow(RefreshGlyph, "checkUpdates", 330, callbacks.CheckOrInstallUpdate);
        settingsTargets.Add(updateRow);
    }

    void BuildAppearancePage(PanelCallbacks callbacks)
    {
        appearanceTargets.Add(NewRow("\uE72B", "back", 8, delegate { ShowPage(mainTargets); }));
        AddStyleChip(IconStyle.Ring, "ring", 0, callbacks);
        AddStyleChip(IconStyle.Gauge, "gauge", 1, callbacks);
        AddStyleChip(IconStyle.Needle, "needle", 2, callbacks);
        AddColorChip(ColorTemplate.Blue, "colorBlue", 0, callbacks);
        AddColorChip(ColorTemplate.Green, "colorGreen", 1, callbacks);
        AddColorChip(ColorTemplate.Purple, "colorPurple", 2, callbacks);
        AddPanelThemeChip(PanelTheme.Auto, "panelAuto", 0, callbacks);
        AddPanelThemeChip(PanelTheme.Graphite, "panelGraphite", 1, callbacks);
        AddPanelThemeChip(PanelTheme.Black, "panelBlack", 2, callbacks);
        AddPanelThemeChip(PanelTheme.Light, "panelLight", 3, callbacks);
        AddMascotChip(MascotStyle.Classic, "mascotClassic", 0, 0, callbacks);
        AddMascotChip(MascotStyle.Pixel, "mascotPixel", 1, 0, callbacks);
        AddMascotChip(MascotStyle.Santa, "mascotSanta", 2, 0, callbacks);
        AddMascotChip(MascotStyle.Headphones, "mascotHeadphones", 0, 1, callbacks);
        AddMascotChip(MascotStyle.Chef, "mascotChef", 1, 1, callbacks);
        AddMascotChip(MascotStyle.Scientist, "mascotScientist", 2, 1, callbacks);
        AddMascotChip(MascotStyle.Detective, "mascotDetective", 0, 2, callbacks);
        AddMascotChip(MascotStyle.Winter, "mascotWinter", 1, 2, callbacks);
        AddMascotChip(MascotStyle.Racer, "mascotRacer", 2, 2, callbacks);
        appearanceTargets.Add(NewToggle("seasonalOutfits", 508, delegate { return Settings.SeasonalOutfits; }, delegate
        {
            Settings.SeasonalOutfits = !Settings.SeasonalOutfits;
            ChangeMascot(mascotStyle);
        }));
    }

    void AddMascotChip(MascotStyle value, string labelKey, int column, int row, PanelCallbacks callbacks)
    {
        PanelTarget chip = new PanelTarget();
        chip.LabelKey = labelKey;
        chip.Picture = MascotPicture(value);
        chip.Bounds = ChipBounds(column, 3, 268 + row * 80, 72);
        chip.Selected = delegate { return mascotStyle == value; };
        chip.Run = delegate { callbacks.ChangeMascot(value); };
        appearanceTargets.Add(chip);
    }

    Bitmap MascotPicture(MascotStyle style)
    {
        if (style != MascotStyle.Classic) return PixelMascot.Render(style, MascotMood.Idle, 1, MascotPixel);
        using (Mascot classic = Mascot.Load(Px(MascotHeight)))
            return classic != null ? new Bitmap(classic.LaptopFrame) : null;
    }

    int MascotPixel
    {
        get { return Math.Max(1, (int)(2 * scale)); } // A whole number keeps the blocks square.
    }

    void AddColorChip(ColorTemplate value, string labelKey, int column, PanelCallbacks callbacks)
    {
        PanelTarget chip = new PanelTarget();
        chip.LabelKey = labelKey;
        chip.Swatch = Theme.NormalColor(value);
        chip.Bounds = ChipBounds(column, 3, 148, 32);
        chip.Selected = delegate { return Theme.Colors == value; };
        chip.Run = delegate { callbacks.ChangeColors(value); };
        appearanceTargets.Add(chip);
    }

    void AddPanelThemeChip(PanelTheme value, string labelKey, int column, PanelCallbacks callbacks)
    {
        PanelTarget chip = new PanelTarget();
        chip.LabelKey = labelKey;
        chip.Bounds = ChipBounds(column, 4, 208, 32);
        chip.Selected = delegate { return Theme.Panel == value; };
        chip.Run = delegate { callbacks.ChangePanelTheme(value); };
        appearanceTargets.Add(chip);
    }

    void AddShownChip(IconsShown value, string labelKey, int column, PanelCallbacks callbacks)
    {
        PanelTarget chip = new PanelTarget();
        chip.Shown = value;
        chip.LabelKey = labelKey;
        chip.Bounds = ChipBounds(column, 3, 142, 32);
        chip.Run = delegate { callbacks.ChangeIconsShown(value); };
        settingsTargets.Add(chip);
    }
    void AddLanguageChip(Language value, string literal, int column, PanelCallbacks callbacks)
    {
        PanelTarget chip = new PanelTarget();
        chip.Language = value;
        chip.LabelKey = "auto";
        chip.LiteralLabel = literal;
        chip.Bounds = ChipBounds(column, 3, 72, 32);
        chip.Run = delegate { callbacks.ChangeLanguage(value); };
        settingsTargets.Add(chip);
    }

    PanelTarget NewRow(string glyph, string labelKey, int top, Action run)
    {
        PanelTarget row = new PanelTarget();
        row.Glyph = glyph;
        row.LabelKey = labelKey;
        row.Run = run;
        row.Bounds = new Rectangle(Px(6), Px(top), ClientSize.Width - Px(12), Px(32));
        return row;
    }

    PanelTarget NewToggle(string labelKey, int top, Func<bool> isOn, Action run)
    {
        PanelTarget toggle = new PanelTarget();
        toggle.LabelKey = labelKey;
        toggle.Toggle = isOn;
        toggle.Run = run;
        toggle.Bounds = new Rectangle(Px(6), Px(top), ClientSize.Width - Px(12), Px(32));
        return toggle;
    }

    Rectangle ChipBounds(int column, int columns, int top, int height)
    {
        int gap = Px(8);
        int width = (ClientSize.Width - Px(36) - gap * (columns - 1)) / columns;
        return new Rectangle(Px(18) + column * (width + gap), Px(top), width, Px(height));
    }

    Font NewUiFont(int logicalSize, bool strong)
    {
        return EmbeddedFonts.New(Px(logicalSize), strong);
    }

    public void SetState(PanelState value, IconStyle style, Language selectedLanguage, IconsShown shown)
    {
        state = value;
        iconStyle = style;
        language = selectedLanguage;
        iconsShown = shown;
        ApplyMascotMood();
        StartMotion();
    }

    void ApplyMascotMood()
    {
        IReactiveMascot reactive = mascot as IReactiveMascot;
        if (reactive != null) reactive.Mood = PixelMascot.MoodFor(state, DateTime.Now);
    }

    public void RefreshMascot()
    {
        ApplyMascotMood();
    }

    // Waits for the panel to open, so a reaction that happened in the background is still seen.
    public void PlayMascot(MascotAction action)
    {
        pendingMascotAction = action;
        if (Visible) PlayPendingMascotAction();
    }

    void PlayPendingMascotAction()
    {
        IReactiveMascot reactive = mascot as IReactiveMascot;
        if (reactive == null || !pendingMascotAction.HasValue) return;
        reactive.Play(pendingMascotAction.Value);
        pendingMascotAction = null;
    }

    Rectangle MascotArea
    {
        get
        {
            if (mascot == null) return Rectangle.Empty;
            Bitmap frame = mascot.CurrentFrame;
            return new Rectangle(ClientSize.Width - Px(18) - frame.Width - WalkShift, Px(14), frame.Width, frame.Height);
        }
    }

    public void ChangeMascot(MascotStyle style)
    {
        mascotStyle = style;
        mascotTimer.Stop();
        if (mascot != null) mascot.Dispose();
        mascot = null;
        LoadMascot();
        if (Visible) StartMascot();
        Invalidate();
    }

    // A known version shows the install link until it succeeds.
    public void SetUpdate(UpdatePhase phase, string version)
    {
        updatePhase = phase;
        updateVersion = version;
        if (phase == UpdatePhase.UpToDate) updateCheckedAt = DateTime.Now;
        updateRow.LabelKey = version != null ? "installUpdate" : "checkUpdates";
        updateRow.Glyph = version != null && phase != UpdatePhase.Installing ? DownloadGlyph : RefreshGlyph;
        mainTargets.Remove(updateLink);
        if (version != null) mainTargets.Add(updateLink);
        hovered = -1;
        RefreshSpin();
        if (Visible) Invalidate();
    }

    bool UpdateIsSpinning
    {
        get { return updatePhase == UpdatePhase.Checking || updatePhase == UpdatePhase.Installing; }
    }

    void RefreshSpin()
    {
        spinTimer.Enabled = Visible && UpdateIsSpinning;
    }

    // Shows the hover details without a mouse, for the documentation images.
    // Picks the chart range without saving it, for the documentation images.
    public void ShowChartRange(ChartRange range)
    {
        chartRange = range;
        Invalidate();
    }

    public void ShowChartDetails()
    {
        chartHoverX = ChartArea.Left + ChartArea.Width * 3 / 4;
        Invalidate();
    }

    public void ShowMainPage()
    {
        ShowPage(mainTargets);
    }

    public void ApplyTheme()
    {
        BackColor = Theme.Background;
        ApplyBorderColor();
        Invalidate();
    }

    void ApplyBorderColor()
    {
        int border = Theme.Border.R | (Theme.Border.G << 8) | (Theme.Border.B << 16);
        Native.DwmSetWindowAttribute(Handle, DWMWA_BORDER_COLOR, ref border, sizeof(int));
    }

    public void ShowAppearancePage()
    {
        ShowPage(appearanceTargets);
    }

    public void ShowSettingsPage()
    {
        ShowPage(settingsTargets);
    }

    void ShowPage(List<PanelTarget> page)
    {
        bool animate = Visible && SystemInformation.UIEffectsEnabled && page != targets;
        int height = Px(page == settingsTargets ? SettingsHeight : page == appearanceTargets ? AppearanceHeight : MainHeight);
        Bitmap from = animate ? CapturePage(ClientSize.Height) : null;
        pageDirection = page == mainTargets ? -1 : 1;
        targets = page;
        hovered = -1;
        chartHoverX = -1;
        SettleMotion();
        if (animate)
        {
            EndPageMotion();
            pageFrom = from;
            pageTo = CapturePage(height);
            pageProgress = 0;
            pageHeightFrom = ClientSize.Height;
            pageHeightTo = height;
            StartMotion();
        }
        else SetPanelHeight(height);
        Invalidate();
    }

    // The panel stays anchored to the taskbar, so it grows upward.
    void SetPanelHeight(int height)
    {
        int bottom = restingTop + Height;
        ClientSize = new Size(ClientSize.Width, height);
        if (!Visible) return;
        restingTop = bottom - Height;
        Top = restingTop;
    }

    Bitmap CapturePage(int height)
    {
        Bitmap page = new Bitmap(ClientSize.Width, height);
        using (Graphics g = Graphics.FromImage(page))
        {
            g.Clear(Theme.Background);
            PaintPage(g);
        }
        return page;
    }

    void EndPageMotion()
    {
        if (pageFrom != null) pageFrom.Dispose();
        if (pageTo != null) pageTo.Dispose();
        pageFrom = pageTo = null;
    }

    IEnumerable<PanelTarget> AllTargets()
    {
        foreach (PanelTarget target in mainTargets) yield return target;
        foreach (PanelTarget target in settingsTargets) yield return target;
        foreach (PanelTarget target in appearanceTargets) yield return target;
    }

    bool ReadIsOn(PanelTarget target)
    {
        if (target.Toggle != null) return target.Toggle();
        if (target.Selected != null) return target.Selected();
        if (target.Style.HasValue) return target.Style.Value == iconStyle;
        if (target.Shown.HasValue) return target.Shown.Value == iconsShown;
        return target.Language.HasValue && target.Language.Value == language;
    }

    // Jumps to the current state, for a page that is not on screen yet.
    void SettleMotion()
    {
        foreach (PanelTarget target in AllTargets())
        {
            target.IsOn = ReadIsOn(target);
            target.On = target.IsOn ? 1 : 0;
            target.Hover = 0;
        }
    }

    void StartMotion()
    {
        if (!Visible) return;
        if (!SystemInformation.UIEffectsEnabled)
        {
            SettleMotion();
            Invalidate();
            return;
        }
        foreach (PanelTarget target in targets) target.IsOn = ReadIsOn(target);
        if (!motionTimer.Enabled) lastMotionTick = Environment.TickCount;
        motionTimer.Start();
    }

    void AdvanceMotion()
    {
        int now = Environment.TickCount;
        float elapsed = Math.Min(LongestFrameMs, now - lastMotionTick);
        lastMotionTick = now;

        bool moving = false;
        for (int i = 0; i < targets.Count; i++)
        {
            PanelTarget target = targets[i];
            float hover = MoveToward(target.Hover, i == hovered ? 1 : 0, elapsed / HoverMs);
            float on = MoveToward(target.On, target.IsOn ? 1 : 0, elapsed / SwitchMs);
            moving |= hover != target.Hover || on != target.On;
            target.Hover = hover;
            target.On = on;
        }
        if (pageFrom != null)
        {
            pageProgress = Math.Min(1, pageProgress + elapsed / PageMs);
            moving = true;
            SetPanelHeight(pageHeightFrom + (int)((pageHeightTo - pageHeightFrom) * Ease(pageProgress)));
            if (pageProgress >= 1) EndPageMotion();
        }
        if (!moving) motionTimer.Stop();
        Invalidate();
    }

    static float MoveToward(float value, float goal, float step)
    {
        return value < goal ? Math.Min(goal, value + step) : Math.Max(goal, value - step);
    }

    static float Ease(float value)
    {
        return value * value * (3 - 2 * value);
    }

    static Color Blend(Color from, Color to, float amount)
    {
        return Color.FromArgb(
            (int)(from.R + (to.R - from.R) * amount),
            (int)(from.G + (to.G - from.G) * amount),
            (int)(from.B + (to.B - from.B) * amount));
    }

    public void Toggle()
    {
        if (Visible && !closing)
        {
            ClosePanel();
            return;
        }
        if (Environment.TickCount - hiddenAt < ReopenGuardMilliseconds) return; // The click that just closed it.
        ShowPanel();
    }

    public void ShowPanel()
    {
        targets = mainTargets;
        chartHoverX = -1;
        EndPageMotion();
        SettleMotion();
        ClientSize = new Size(ClientSize.Width, Px(MainHeight));
        Rectangle area = Screen.FromPoint(Cursor.Position).WorkingArea;
        restingTop = area.Bottom - Height - Px(12);
        closing = false;
        bool animate = SystemInformation.UIEffectsEnabled;
        Location = new Point(area.Right - Width - Px(12), restingTop + (animate ? Px(OpenSlide) : 0));
        Opacity = animate ? 0 : 1;
        Show();
        Native.SetForegroundWindow(Handle);
        Activate();
        if (animate) StartAnimation();
    }

    public void ClosePanel()
    {
        if (!Visible || closing) return;
        hiddenAt = Environment.TickCount;
        if (!SystemInformation.UIEffectsEnabled)
        {
            Hide();
            return;
        }
        closing = true;
        StartAnimation();
    }

    void StartAnimation()
    {
        animationStart = Environment.TickCount;
        animationTimer.Start();
    }

    void AdvanceAnimation()
    {
        int duration = closing ? CloseAnimationMs : OpenAnimationMs;
        float progress = Math.Min(1f, (Environment.TickCount - animationStart) / (float)duration);
        float eased = 1 - (1 - progress) * (1 - progress) * (1 - progress);
        if (closing)
        {
            Opacity = 1 - progress;
            Top = restingTop + (int)(Px(CloseSlide) * progress);
        }
        else
        {
            Opacity = eased;
            Top = restingTop + (int)(Px(OpenSlide) * (1 - eased));
        }
        if (progress < 1) return;

        animationTimer.Stop();
        if (!closing) return;
        Hide();
        closing = false;
        Opacity = 1;
    }

    protected override bool ShowWithoutActivation { get { return false; } }

    protected override CreateParams CreateParams
    {
        get
        {
            CreateParams parameters = base.CreateParams;
            parameters.ExStyle |= WS_EX_TOOLWINDOW;
            parameters.ClassStyle |= CS_DROPSHADOW;
            return parameters;
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        int corners = DWMWCP_ROUND;
        Native.DwmSetWindowAttribute(Handle, DWMWA_WINDOW_CORNER_PREFERENCE, ref corners, sizeof(int));
        ApplyBorderColor();
    }

    protected override void OnVisibleChanged(EventArgs e)
    {
        base.OnVisibleChanged(e);
        RefreshSpin();
        if (!Visible)
        {
            mascotTimer.Stop();
            motionTimer.Stop();
            return;
        }
        if (mascot != null && shownStyle != Seasons.Effective(mascotStyle))
        {
            mascot.Dispose(); // A special date began or ended since the panel was last open.
            mascot = null;
        }
        LoadMascot();
        StartMascot();
    }

    void StartMascot()
    {
        if (mascot == null) return;
        PlayPendingMascotAction();
        mascotTimer.Interval = mascot.CurrentDelayMs;
        mascotTimer.Start();
    }

    public void LoadMascot()
    {
        if (mascot != null) return;
        mascot = NewMascot();
        ApplyMascotMood();
    }

    IMascot NewMascot()
    {
        shownStyle = Seasons.Effective(mascotStyle);
        if (shownStyle != MascotStyle.Classic) return new PixelMascot(shownStyle, MascotPixel);
        PixelMascot other = new PixelMascot(MascotStyle.Pixel, MascotPixel);
        Mascot working = Mascot.Load(Px(MascotHeight));
        return working != null ? (IMascot)new ClassicMascot(working, other) : other;
    }

    // Moves the animation without the timer, for the documentation demo.
    public void StepMascot()
    {
        if (mascot == null) return;
        UpdateWalkRoom();
        mascot.Advance(StepMs);
    }

    // Starts a walk right away, for the documentation images.
    public void StartMascotWalk(bool special)
    {
        IReactiveMascot reactive = mascot as IReactiveMascot;
        if (reactive == null) return;
        UpdateWalkRoom();
        PixelMascot walker = mascot as PixelMascot;
        if (walker == null) walker = ((ClassicMascot)mascot).Pixel;
        walker.AlwaysSpecial = special;
        walker.BeginWalk();
    }

    void AdvanceMascot()
    {
        UpdateWalkRoom();
        mascot.Advance();
        mascotTimer.Interval = mascot.CurrentDelayMs;
        Invalidate(new Rectangle(Math.Max(0, ClientSize.Width - Px(230)), 0, Px(230), Px(80)));
    }

    int WalkShift
    {
        get
        {
            IReactiveMascot reactive = mascot as IReactiveMascot;
            return reactive == null ? 0 : (int)Math.Round(reactive.WalkOffset * MascotPixel);
        }
    }

    // The character may walk over the empty part of the header, never over the title or the status line.
    void UpdateWalkRoom()
    {
        IReactiveMascot reactive = mascot as IReactiveMascot;
        if (reactive == null) return;
        reactive.UsagePercent = Math.Max(state.Session.Percent ?? 0, state.Weekly.Percent ?? 0);
        if (updateVersion != null)
        {
            reactive.WalkRoom = 0;
            return;
        }
        Size title = TextRenderer.MeasureText(PanelTitle, titleFont, Size.Empty, TextFormatFlags.NoPadding);
        Size status = TextRenderer.MeasureText(state.Status ?? "", tinyFont, Size.Empty, TextFormatFlags.NoPadding);
        int free = ClientSize.Width - Px(36) - mascot.CurrentFrame.Width - Math.Max(title.Width, status.Width) - Px(10);
        reactive.WalkRoom = Math.Max(0, free / MascotPixel);
    }

    protected override void OnDeactivate(EventArgs e)
    {
        base.OnDeactivate(e);
        ClosePanel();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.KeyCode == Keys.Escape) ClosePanel();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        bool onMascot = targets == mainTargets && MascotArea.Contains(e.Location);
        if (onMascot && !overMascot) PlayMascot(playChance.Next(3) == 0 ? MascotAction.Hop : MascotAction.Wave);
        overMascot = onMascot;
        RememberCursorInHeader(e);

        int chartX = targets == mainTargets && ChartArea.Contains(e.Location) ? e.X : -1;
        if (chartX != chartHoverX)
        {
            chartHoverX = chartX;
            Invalidate();
        }

        int index = targets.FindIndex(delegate(PanelTarget t) { return t.Bounds.Contains(e.Location); });
        if (index == hovered) return;
        hovered = index;
        Cursor = index >= 0 ? Cursors.Hand : Cursors.Default;
        StartMotion();
    }

    // Where the cursor was in the empty part of the header, so the mascot may walk over to look for it.
    void RememberCursorInHeader(MouseEventArgs e)
    {
        IReactiveMascot walker = mascot as IReactiveMascot;
        if (walker == null || targets != mainTargets || e.Y >= Px(58)) return;
        int restLeft = ClientSize.Width - Px(18) - mascot.CurrentFrame.Width;
        if (e.X < restLeft - Px(6))
            walker.WalkGoal = Math.Max(1, (restLeft + mascot.CurrentFrame.Width / 2 - e.X) / MascotPixel);
    }

    bool PettedLongEnough()
    {
        int now = Environment.TickCount;
        mascotClicks.Enqueue(now);
        while (mascotClicks.Count > 0 && now - mascotClicks.Peek() > LoveWindowMs) mascotClicks.Dequeue();
        if (mascotClicks.Count < LoveClicks) return false;
        mascotClicks.Clear();
        return true;
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        hovered = -1;
        chartHoverX = -1;
        StartMotion();
    }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        base.OnMouseClick(e);
        if (overMascot) PlayMascot(PettedLongEnough() ? MascotAction.Love : MascotAction.Pet);
        if (hovered < 0) return;
        targets[hovered].Run();
        StartMotion();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        if (!Visible) SettleMotion(); // The documentation images have no timer to finish the motion.
        if (pageFrom != null) PaintPageMotion(e.Graphics);
        else PaintPage(e.Graphics);
    }

    void PaintPageMotion(Graphics g)
    {
        // One page fades out before the next fades in, so their text never overlaps.
        float eased = Ease(pageProgress);
        int slide = (int)(Px(PageSlide) * pageDirection * (1 - eased));
        g.Clear(Theme.Background);
        if (eased < 0.5f) DrawFaded(g, pageFrom, slide - Px(PageSlide) * pageDirection, 1 - eased * 2);
        else DrawFaded(g, pageTo, slide, eased * 2 - 1);
    }

    void DrawFaded(Graphics g, Bitmap page, int x, float opacity)
    {
        ColorMatrix matrix = new ColorMatrix();
        matrix.Matrix33 = opacity;
        using (ImageAttributes attributes = new ImageAttributes())
        {
            attributes.SetColorMatrix(matrix);
            g.DrawImage(page, new Rectangle(x, 0, page.Width, page.Height), 0, 0, page.Width, page.Height, GraphicsUnit.Pixel, attributes);
        }
    }

    void PaintPage(Graphics g)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        int left = Px(18);
        int right = ClientSize.Width - Px(18);

        if (targets == mainTargets) PaintMainPage(g, left, right);
        else if (targets == settingsTargets) PaintSettingsPage(g, left, right);
        else PaintAppearancePage(g, left, right);

        for (int i = 0; i < targets.Count; i++)
        {
            PanelTarget target = targets[i];
            bool isHovered = i == hovered;
            if (target == updateLink) DrawUpdateLink(g, isHovered);
            else if (target.Range.HasValue) { if (chartHoverX < 0) DrawRangeChip(g, target, isHovered); }
            else if (target.Selected != null || target.Style.HasValue || target.Language.HasValue || target.Shown.HasValue) DrawChip(g, target, isHovered);
            else if (target.Toggle != null) DrawToggle(g, target, isHovered);
            else DrawRow(g, target, isHovered);
        }
    }

    void PaintMainPage(Graphics g, int left, int right)
    {
        Draw(g, PanelTitle, titleFont, Theme.Text, left, Px(14), right - left, TextFormatFlags.Left);
        int mascotSpace = mascot != null ? mascot.CurrentFrame.Width + Px(8) : 0;
        if (updateVersion == null) Draw(g, state.Status, tinyFont, state.OutOfDate ? Theme.Warning : Theme.Muted, left, Px(35), right - left - mascotSpace, TextFormatFlags.Left);
        if (mascot != null && targets == mainTargets)
        {
            Bitmap frame = mascot.CurrentFrame;
            g.DrawImageUnscaled(frame, right - frame.Width - WalkShift, Px(14));
            if (ChefIsWorking) DrawChefBalloon(g, right - frame.Width);
        }
        DrawLimit(g, Localization.T("session"), state.Session, state.SessionPace, true, Px(SessionTop), left, right);
        DrawLimit(g, Localization.T("weekly"), state.Weekly, state.WeeklyPace, false, Px(state.SessionPace != null ? WeeklyTop : WeeklyTopWithoutPace), left, right);
        Draw(g, HistoryLabel, tinyFont, Theme.Muted, left, Px(HistoryTop), right - left - Px(RangeChipWidth) * 3, TextFormatFlags.Left);
        DrawChart(g, ChartArea);
        string today = TodayUsage();
        if (today != null) Draw(g, today, tinyFont, Theme.Muted, left, Px(ChartTop + 44), right - left, TextFormatFlags.Left);
        DrawDivider(g, Px(DividerTop));
    }

    bool ChefIsWorking
    {
        get
        {
            IReactiveMascot reactive = mascot as IReactiveMascot;
            if (mascotStyle != MascotStyle.Chef || reactive == null) return false;
            return reactive.Mood == MascotMood.Work || reactive.Mood == MascotMood.Tired || reactive.Mood == MascotMood.Coffee;
        }
    }

    void DrawChefBalloon(Graphics g, int mascotLeft)
    {
        Size text = TextRenderer.MeasureText(ChefBalloon, tinyFont, Size.Empty, TextFormatFlags.NoPadding);
        Rectangle bubble = new Rectangle(mascotLeft + Px(8) - text.Width - Px(12), Px(12), text.Width + Px(12), text.Height + Px(6));
        Point[] tail =
        {
            new Point(bubble.Right - Px(10), bubble.Bottom - Px(1)),
            new Point(bubble.Right - Px(3), bubble.Bottom - Px(1)),
            new Point(bubble.Right - Px(1), bubble.Bottom + Px(5))
        };
        using (GraphicsPath path = Theme.Rounded(bubble, Px(6)))
        using (Brush brush = new SolidBrush(Theme.Text))
        {
            g.FillPath(brush, path);
            g.FillPolygon(brush, tail);
        }
        TextRenderer.DrawText(g, ChefBalloon, tinyFont, bubble, Theme.Background,
            TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPadding);
    }

    Rectangle ChartArea
    {
        get { return new Rectangle(Px(18), Px(ChartTop), ClientSize.Width - Px(36), Px(40)); }
    }

    void DrawChart(Graphics g, Rectangle area)
    {
        using (Pen baseline = new Pen(Theme.Border))
            g.DrawLine(baseline, area.Left, area.Bottom, area.Right, area.Bottom);

        DateTime end = DateTime.Now;
        DateTime start = end.AddHours(-ChartHours);
        List<PointF> points = new List<PointF>();
        List<UsageSample> shown = new List<UsageSample>();
        foreach (UsageSample sample in state.History)
        {
            if (sample.Time < start || !ChartValue(sample).HasValue) continue;
            float x = area.Left + (float)((sample.Time - start).TotalHours / ChartHours) * area.Width;
            float y = area.Bottom - (float)(Math.Min(100, ChartValue(sample).Value) / 100) * area.Height;
            points.Add(new PointF(x, y));
            shown.Add(sample);
        }

        if (points.Count < 2)
        {
            Draw(g, Localization.T("historyEmpty"), tinyFont, Theme.Muted, area.Left, area.Top + Px(10), area.Width, TextFormatFlags.Left);
            return;
        }

        using (Pen mark = new Pen(Color.FromArgb(120, Theme.Muted)))
        {
            mark.DashStyle = DashStyle.Dot;
            for (int i = 1; i < shown.Count; i++)
                if (IsReset(shown[i - 1], shown[i])) g.DrawLine(mark, points[i].X, area.Top, points[i].X, area.Bottom);
        }

        using (GraphicsPath fill = new GraphicsPath())
        using (Brush brush = new SolidBrush(Color.FromArgb(50, Theme.Accent)))
        using (Pen line = new Pen(Theme.Accent, Px(2)))
        {
            fill.AddLines(points.ToArray());
            fill.AddLine(points[points.Count - 1].X, area.Bottom, points[0].X, area.Bottom);
            fill.CloseFigure();
            g.FillPath(brush, fill);
            line.LineJoin = LineJoin.Round;
            g.DrawLines(line, points.ToArray());
        }
        if (chartHoverX >= 0) DrawChartHover(g, area, points, shown);
    }

    bool IsReset(UsageSample before, UsageSample after)
    {
        return ChartValue(before).Value - ChartValue(after).Value >= ResetDropPoints;
    }

    // The history holds only time and percentage, which is all the status line reports.
    void DrawChartHover(Graphics g, Rectangle area, List<PointF> points, List<UsageSample> samples)
    {
        int nearest = 0;
        for (int i = 1; i < points.Count; i++)
            if (Math.Abs(points[i].X - chartHoverX) < Math.Abs(points[nearest].X - chartHoverX)) nearest = i;

        PointF point = points[nearest];
        float radius = Px(3);
        using (Pen guide = new Pen(Theme.Muted))
            g.DrawLine(guide, point.X, area.Top, point.X, area.Bottom);
        using (Brush dot = new SolidBrush(Theme.Text))
            g.FillEllipse(dot, point.X - radius, point.Y - radius, radius * 2, radius * 2);

        int percent = (int)Math.Round(ChartValue(samples[nearest]).Value);
        string format = chartRange == ChartRange.Hours12 ? "HH:mm" : "ddd HH:mm";
        string text = samples[nearest].Time.ToString(format, Localization.Culture) + " · " + percent + "%";
        if (nearest > 0)
        {
            int change = percent - (int)Math.Round(ChartValue(samples[nearest - 1]).Value);
            if (change > 0) text += " · +" + change + "%";
            if (IsReset(samples[nearest - 1], samples[nearest])) text += " · " + Localization.T("chartReset");
        }
        Draw(g, text, tinyFont, Theme.Text, area.Left, Px(HistoryTop), area.Width, TextFormatFlags.Right);
    }

    // The highest the 5-hour session got since midnight, from the saved history.
    string TodayUsage()
    {
        DateTime midnight = DateTime.Today;
        double peak = -1;
        foreach (UsageSample sample in state.History)
            if (sample.Time >= midnight && sample.Session > peak) peak = sample.Session;
        if (peak < 0) return null;
        return Localization.T("today", (int)Math.Round(peak));
    }

    void DrawRangeChip(Graphics g, PanelTarget chip, bool isHovered)
    {
        bool selected = chip.Range.Value == chartRange;
        Color color = selected || isHovered ? Theme.Text : Theme.Muted;
        TextRenderer.DrawText(g, chip.Label, tinyFont, chip.Bounds, color,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        if (!selected) return;
        int width = Px(14);
        using (Pen underline = new Pen(Theme.Accent, Px(2)))
            g.DrawLine(underline, chip.Bounds.X + (chip.Bounds.Width - width) / 2, chip.Bounds.Bottom, chip.Bounds.X + (chip.Bounds.Width + width) / 2, chip.Bounds.Bottom);
    }

    void PaintSettingsPage(Graphics g, int left, int right)
    {
        DrawDivider(g, Px(44));
        Draw(g, Localization.T("language"), tinyFont, Theme.Muted, left, Px(52), right - left, TextFormatFlags.Left);
        Draw(g, Localization.T("taskbarIcons"), tinyFont, Theme.Muted, left, Px(122), right - left, TextFormatFlags.Left);
        string status = UpdateStatus();
        string version = Localization.T("version", App.Version) + (status != null ? " · " + status : "");
        Draw(g, version, tinyFont, Theme.Muted, left, Px(370), right - left, TextFormatFlags.Left);
    }

    void PaintAppearancePage(Graphics g, int left, int right)
    {
        DrawDivider(g, Px(44));
        Draw(g, Localization.T("taskbarIcon"), tinyFont, Theme.Muted, left, Px(52), right - left, TextFormatFlags.Left);
        Draw(g, Localization.T("colors"), tinyFont, Theme.Muted, left, Px(130), right - left, TextFormatFlags.Left);
        Draw(g, Localization.T("panelTheme"), tinyFont, Theme.Muted, left, Px(190), right - left, TextFormatFlags.Left);
        Draw(g, Localization.T("mascot"), tinyFont, Theme.Muted, left, Px(250), right - left, TextFormatFlags.Left);
    }

    string UpdateStatus()
    {
        switch (updatePhase)
        {
            case UpdatePhase.Checking: return Localization.T("updateChecking");
            case UpdatePhase.UpToDate: return Localization.T("updateCurrentAt", updateCheckedAt.ToString("HH:mm", Localization.Culture));
            case UpdatePhase.Failed: return Localization.T(updateVersion != null ? "updateInstallFailed" : "updateCheckFailed");
            case UpdatePhase.Available: return Localization.T("updateAvailable", updateVersion);
            case UpdatePhase.Installing: return Localization.T("updateInstalling");
            default: return null;
        }
    }

    void DrawUpdateLink(Graphics g, bool isHovered)
    {
        string text = UpdateStatus();
        if (updatePhase == UpdatePhase.Available || updatePhase == UpdatePhase.Failed) text += " · " + Localization.T("updateInstallAction");
        TextRenderer.DrawText(g, text, tinyFont, updateLink.Bounds, Blend(Theme.Accent, Theme.Text, Ease(updateLink.Hover)),
            TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
    }

    void DrawDivider(Graphics g, int y)
    {
        using (Pen divider = new Pen(Theme.Border))
            g.DrawLine(divider, 0, y, ClientSize.Width, y);
    }

    void DrawLimit(Graphics g, string label, UsageWindow window, string pace, bool withCountdown, int top, int left, int right)
    {
        Color color = Theme.ForPanel(window.Percent, state.Stale);
        string percent = window.Percent.HasValue ? window.Percent.Value + "%" : "--";
        Draw(g, label, labelFont, Theme.Text, left, top, right - left, TextFormatFlags.Left);
        Draw(g, percent, percentFont, color, left, top - Px(1), right - left, TextFormatFlags.Right);

        float barTop = top + Px(26);
        float barHeight = Px(6);
        float width = right - left;
        using (Brush track = new SolidBrush(Theme.Track))
        using (GraphicsPath path = Theme.Rounded(new RectangleF(left, barTop, width, barHeight), barHeight / 2))
            g.FillPath(track, path);
        if (window.Percent.HasValue && window.Percent.Value > 0)
        {
            float fill = Math.Max(barHeight, width * Math.Min(100, window.Percent.Value) / 100f);
            using (Brush brush = new SolidBrush(color))
            using (GraphicsPath path = Theme.Rounded(new RectangleF(left, barTop, fill, barHeight), barHeight / 2))
                g.FillPath(brush, path);
        }

        Draw(g, ResetText(window, withCountdown), tinyFont, Theme.Muted, left, top + Px(36), right - left, TextFormatFlags.Left);
        if (pace != null) Draw(g, pace, tinyFont, Theme.Accent, left, top + Px(50), right - left, TextFormatFlags.Left);
    }

    static string ResetText(UsageWindow window, bool withCountdown)
    {
        if (!window.ResetsAt.HasValue) return Localization.T("noReset");
        DateTime resetsAt = window.ResetsAt.Value;
        TimeSpan remaining = resetsAt - DateTime.Now;
        if (remaining.Ticks <= 0) return Localization.T("resetting");
        if (!withCountdown) return Localization.T("resetsOn", resetsAt.ToString(Localization.T("resetsOnFormat"), Localization.Culture));

        string countdown = remaining.TotalHours >= 1
            ? Localization.T("hoursMinutes", (int)remaining.TotalHours, remaining.Minutes)
            : Localization.T("minutes", Math.Max(1, remaining.Minutes));
        return Localization.T("resetsAtIn", resetsAt.ToString("HH:mm", Localization.Culture), countdown);
    }

    void DrawChip(Graphics g, PanelTarget chip, bool isHovered)
    {
        float on = Ease(chip.On);
        using (GraphicsPath path = Theme.Rounded(chip.Bounds, Px(8)))
        {
            using (Brush brush = new SolidBrush(Color.FromArgb((int)(255 * Ease(chip.Hover) * (1 - on)), Theme.Hover)))
                g.FillPath(brush, path);
            using (Brush brush = new SolidBrush(Color.FromArgb((int)(SelectedFill * on), Theme.Accent)))
                g.FillPath(brush, path);
            using (Pen pen = new Pen(Blend(Theme.Border, Theme.Accent, on)))
                g.DrawPath(pen, path);
        }

        Color textColor = Blend(Theme.Muted, Theme.Text, on);
        if (chip.Swatch.HasValue)
        {
            int dot = Px(9);
            using (Brush brush = new SolidBrush(chip.Swatch.Value))
                g.FillEllipse(brush, chip.Bounds.X + Px(9), chip.Bounds.Y + (chip.Bounds.Height - dot) / 2, dot, dot);
        }
        if (chip.Picture != null)
        {
            int pictureX = chip.Bounds.X + (chip.Bounds.Width - chip.Picture.Width) / 2;
            g.DrawImageUnscaled(chip.Picture, pictureX, chip.Bounds.Y + Px(4));
            Rectangle caption = new Rectangle(chip.Bounds.X, chip.Bounds.Bottom - Px(20), chip.Bounds.Width, Px(18));
            TextRenderer.DrawText(g, chip.Label, tinyFont, caption, textColor,
                TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPadding);
            return;
        }
        if (!chip.Style.HasValue)
        {
            TextRenderer.DrawText(g, chip.Label, tinyFont, chip.Bounds, textColor,
                TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPadding);
            return;
        }

        int iconSize = Px(22);
        Rectangle icon = new Rectangle(chip.Bounds.X + (chip.Bounds.Width - iconSize) / 2, chip.Bounds.Y + Px(6), iconSize, iconSize);
        int? sample = state.Session.Percent ?? 60;
        using (Bitmap preview = TrayIcon.Draw(chip.Style.Value, sample, true, state.Stale, iconSize, Theme.IsLight))
            g.DrawImageUnscaled(preview, icon.Location);

        Rectangle label = new Rectangle(chip.Bounds.X, icon.Bottom + Px(2), chip.Bounds.Width, chip.Bounds.Bottom - icon.Bottom - Px(3));
        TextRenderer.DrawText(g, chip.Label, tinyFont, label, textColor,
            TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPadding);
    }

    void DrawHover(Graphics g, Rectangle bounds, float amount)
    {
        if (amount <= 0) return;
        using (Brush brush = new SolidBrush(Color.FromArgb((int)(255 * Ease(amount)), Theme.Hover)))
        using (GraphicsPath path = Theme.Rounded(bounds, Px(6)))
            g.FillPath(brush, path);
    }

    void DrawTile(Graphics g, PanelTarget tile)
    {
        DrawHover(g, tile.Bounds, tile.Hover);
        Rectangle glyph = new Rectangle(tile.Bounds.X, tile.Bounds.Y + Px(6), tile.Bounds.Width, Px(18));
        Rectangle label = new Rectangle(tile.Bounds.X, tile.Bounds.Y + Px(26), tile.Bounds.Width, Px(16));
        TextFormatFlags flags = TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPadding;
        TextRenderer.DrawText(g, tile.Glyph, glyphFont, glyph, Theme.Accent, flags);
        TextRenderer.DrawText(g, tile.Label, tinyFont, label, Theme.Text, flags);
    }

    void DrawRow(Graphics g, PanelTarget row, bool isHovered)
    {
        if (row.Tile)
        {
            DrawTile(g, row);
            return;
        }
        DrawHover(g, row.Bounds, row.Hover);
        Rectangle glyph = new Rectangle(row.Bounds.X + Px(12), row.Bounds.Y, Px(18), row.Bounds.Height);
        Rectangle label = new Rectangle(glyph.Right + Px(10), row.Bounds.Y, row.Bounds.Right - glyph.Right - Px(10), row.Bounds.Height);
        TextFormatFlags flags = TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding;
        if (row == updateRow && UpdateIsSpinning) DrawSpinningGlyph(g, row.Glyph, glyph);
        else TextRenderer.DrawText(g, row.Glyph, glyphFont, glyph, Theme.Accent, flags | TextFormatFlags.HorizontalCenter);
        TextRenderer.DrawText(g, row.Label, labelFont, label, Theme.Text, flags | TextFormatFlags.Left);
    }

    // TextRenderer ignores rotation, so the glyph is drawn as a shape turning around its own center.
    void DrawSpinningGlyph(Graphics g, string text, Rectangle area)
    {
        using (GraphicsPath path = new GraphicsPath())
        using (Brush brush = new SolidBrush(Theme.Accent))
        {
            path.AddString(text, glyphFont.FontFamily, 0, glyphFont.Size, PointF.Empty, StringFormat.GenericTypographic);
            RectangleF bounds = path.GetBounds();
            GraphicsState saved = g.Save();
            g.TranslateTransform(area.X + area.Width / 2f, area.Y + area.Height / 2f);
            g.RotateTransform(Environment.TickCount % 1000 * 0.36f);
            g.TranslateTransform(-(bounds.X + bounds.Width / 2), -(bounds.Y + bounds.Height / 2));
            g.FillPath(brush, path);
            g.Restore(saved);
        }
    }

    void DrawToggle(Graphics g, PanelTarget toggle, bool isHovered)
    {
        DrawHover(g, toggle.Bounds, toggle.Hover);
        Rectangle label = new Rectangle(toggle.Bounds.X + Px(12), toggle.Bounds.Y, toggle.Bounds.Width - Px(60), toggle.Bounds.Height);
        TextRenderer.DrawText(g, toggle.Label, labelFont, label, Theme.Text,
            TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPadding);

        float on = Ease(toggle.On);
        int width = Px(34);
        int height = Px(18);
        Rectangle pill = new Rectangle(toggle.Bounds.Right - width - Px(12), toggle.Bounds.Y + (toggle.Bounds.Height - height) / 2, width, height);
        using (Brush brush = new SolidBrush(Blend(Theme.Track, Theme.Accent, on)))
        using (GraphicsPath path = Theme.Rounded(pill, height / 2f))
            g.FillPath(brush, path);

        int knob = height - Px(6);
        float knobX = pill.X + Px(3) + (pill.Width - knob - Px(6)) * on;
        using (Brush brush = new SolidBrush(Color.White))
            g.FillEllipse(brush, knobX, pill.Y + Px(3), knob, knob);
    }

    void Draw(Graphics g, string text, Font font, Color color, int x, int y, int width, TextFormatFlags alignment)
    {
        Rectangle area = new Rectangle(x, y, width, Px(24));
        TextRenderer.DrawText(g, text, font, area, color, alignment | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
    }

    int Px(int logical) { return (int)Math.Round(logical * scale); }
}
