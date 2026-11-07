using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

enum UpdatePhase { Idle, Checking, UpToDate, Failed, Available, Installing }

class PanelState
{
    public UsageWindow Session = new UsageWindow();
    public UsageWindow Weekly = new UsageWindow();
    public bool Stale = true;
    public string Status = Localization.T("reading");
    public string SessionPace;
    public string WeeklyPace;
    public List<UsageSample> History = new List<UsageSample>();
}

class PanelCallbacks
{
    public Action<IconStyle> ChangeStyle = delegate { };
    public Action<Language> ChangeLanguage = delegate { };
    public Action<ColorTemplate> ChangeColors = delegate { };
    public Action<PanelTheme> ChangePanelTheme = delegate { };
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
    public Func<bool> Toggle;
    public Func<bool> Selected; // For chips that are not tied to one of the fields above.
    public Color? Swatch;

    public string Label
    {
        get { return LiteralLabel ?? Localization.T(LabelKey); }
    }
}

class UsagePanel : Form
{
    const int LogicalWidth = 300;
    const int LogicalHeight = 434;
    const int ChipTop = 300;
    const int ActionsTop = 364;
    const int MascotHeight = 42;
    const double ChartHours = 12;
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
    Mascot mascot;
    readonly Font titleFont, labelFont, smallFont, tinyFont, percentFont, glyphFont;
    List<PanelTarget> targets;
    PanelState state = new PanelState();
    IconStyle iconStyle;
    Language language;
    IconsShown iconsShown;
    int hovered = -1;
    int hiddenAt;
    int chartHoverX = -1;
    int animationStart;
    int restingTop;
    bool closing;
    PanelTarget updateLink;
    PanelTarget updateRow;
    UpdatePhase updatePhase;
    string updateVersion;

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
        ClientSize = new Size(Px(LogicalWidth), Px(LogicalHeight));

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
    }

    void BuildMainPage(PanelCallbacks callbacks)
    {
        AddStyleChip(IconStyle.Gauge, "gauge", 0, callbacks);
        AddStyleChip(IconStyle.Ring, "ring", 1, callbacks);
        AddStyleChip(IconStyle.Needle, "needle", 2, callbacks);
        updateLink = new PanelTarget();
        updateLink.Bounds = new Rectangle(Px(18), Px(33), Px(224), Px(18));
        updateLink.Run = callbacks.CheckOrInstallUpdate;
        mainTargets.Add(NewRow("", "settings", ActionsTop, delegate { ShowPage(settingsTargets); }));
        mainTargets.Add(NewRow("", "closeWidget", ActionsTop + 32, callbacks.CloseWidget));
    }

    void AddStyleChip(IconStyle style, string labelKey, int column, PanelCallbacks callbacks)
    {
        PanelTarget chip = new PanelTarget();
        chip.Style = style;
        chip.LabelKey = labelKey;
        chip.Bounds = ChipBounds(column, 3, ChipTop, 50);
        chip.Run = delegate { callbacks.ChangeStyle(style); };
        mainTargets.Add(chip);
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
        updateRow = NewRow("\uE72C", "checkUpdates", 330, callbacks.CheckOrInstallUpdate);
        settingsTargets.Add(updateRow);
        settingsTargets.Add(NewRow("\uE790", "appearance", 362, delegate { ShowPage(appearanceTargets); }));
    }

    void BuildAppearancePage(PanelCallbacks callbacks)
    {
        appearanceTargets.Add(NewRow("\uE72B", "back", 8, delegate { ShowPage(settingsTargets); }));
        AddColorChip(ColorTemplate.Green, "colorGreen", 0, callbacks);
        AddColorChip(ColorTemplate.Blue, "colorBlue", 1, callbacks);
        AddColorChip(ColorTemplate.Purple, "colorPurple", 2, callbacks);
        AddPanelThemeChip(PanelTheme.Graphite, "panelGraphite", 0, callbacks);
        AddPanelThemeChip(PanelTheme.Black, "panelBlack", 1, callbacks);
    }

    void AddColorChip(ColorTemplate value, string labelKey, int column, PanelCallbacks callbacks)
    {
        PanelTarget chip = new PanelTarget();
        chip.LabelKey = labelKey;
        chip.Swatch = Theme.NormalColor(value);
        chip.Bounds = ChipBounds(column, 3, 72, 32);
        chip.Selected = delegate { return Theme.Colors == value; };
        chip.Run = delegate { callbacks.ChangeColors(value); };
        appearanceTargets.Add(chip);
    }

    void AddPanelThemeChip(PanelTheme value, string labelKey, int column, PanelCallbacks callbacks)
    {
        PanelTarget chip = new PanelTarget();
        chip.LabelKey = labelKey;
        chip.Bounds = ChipBounds(column, 2, 142, 32);
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
        toggle.Run = delegate { run(); Invalidate(); };
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
        if (Visible) Invalidate();
    }

    // A known version shows the install link until it succeeds.
    public void SetUpdate(UpdatePhase phase, string version)
    {
        updatePhase = phase;
        updateVersion = version;
        updateRow.LabelKey = version != null ? "installUpdate" : "checkUpdates";
        mainTargets.Remove(updateLink);
        if (version != null) mainTargets.Add(updateLink);
        hovered = -1;
        if (Visible) Invalidate();
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
        targets = page;
        hovered = -1;
        chartHoverX = -1;
        Invalidate();
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
        if (!Visible)
        {
            mascotTimer.Stop();
            return;
        }
        LoadMascot();
        if (mascot != null)
        {
            mascotTimer.Interval = mascot.CurrentDelayMs;
            mascotTimer.Start();
        }
    }

    public void LoadMascot()
    {
        if (mascot == null) mascot = Mascot.Load(Px(MascotHeight));
    }

    void AdvanceMascot()
    {
        mascot.Advance();
        mascotTimer.Interval = mascot.CurrentDelayMs;
        Invalidate(new Rectangle(ClientSize.Width - Px(90), 0, Px(90), Px(60)));
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
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        hovered = -1;
        chartHoverX = -1;
        Invalidate();
    }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        base.OnMouseClick(e);
        if (hovered >= 0) targets[hovered].Run();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
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
            else if (target.Selected != null || target.Style.HasValue || target.Language.HasValue || target.Shown.HasValue) DrawChip(g, target, isHovered);
            else if (target.Toggle != null) DrawToggle(g, target, isHovered);
            else DrawRow(g, target, isHovered);
        }
    }

    void PaintMainPage(Graphics g, int left, int right)
    {
        Draw(g, App.Name, titleFont, Theme.Text, left, Px(14), right - left, TextFormatFlags.Left);
        int mascotSpace = mascot != null ? mascot.CurrentFrame.Width + Px(8) : 0;
        if (updateVersion == null) Draw(g, state.Status, tinyFont, Theme.Muted, left, Px(35), right - left - mascotSpace, TextFormatFlags.Left);
        if (mascot != null && targets == mainTargets)
        {
            Bitmap frame = mascot.CurrentFrame;
            g.DrawImageUnscaled(frame, right - frame.Width, Px(14));
        }
        DrawLimit(g, Localization.T("session"), state.Session, state.SessionPace, true, Px(62), left, right);
        DrawLimit(g, Localization.T("weekly"), state.Weekly, state.WeeklyPace, false, Px(138), left, right);
        Draw(g, Localization.T("history"), tinyFont, Theme.Muted, left, Px(214), right - left, TextFormatFlags.Left);
        DrawChart(g, ChartArea);
        Draw(g, Localization.T("taskbarIcon"), tinyFont, Theme.Muted, left, Px(280), right - left, TextFormatFlags.Left);
        DrawDivider(g, Px(358));
    }

    Rectangle ChartArea
    {
        get { return new Rectangle(Px(18), Px(232), ClientSize.Width - Px(36), Px(40)); }
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
            if (sample.Time < start) continue;
            float x = area.Left + (float)((sample.Time - start).TotalHours / ChartHours) * area.Width;
            float y = area.Bottom - (float)(Math.Min(100, sample.Session) / 100) * area.Height;
            points.Add(new PointF(x, y));
            shown.Add(sample);
        }

        if (points.Count < 2)
        {
            Draw(g, Localization.T("historyEmpty"), tinyFont, Theme.Muted, area.Left, area.Top + Px(10), area.Width, TextFormatFlags.Left);
            return;
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

        int percent = (int)Math.Round(samples[nearest].Session);
        string text = samples[nearest].Time.ToString("HH:mm", Localization.Culture) + " · " + percent + "%";
        if (nearest > 0)
        {
            int change = percent - (int)Math.Round(samples[nearest - 1].Session);
            if (change > 0) text += " · +" + change + "%";
        }
        Draw(g, text, tinyFont, Theme.Text, area.Left, Px(214), area.Width, TextFormatFlags.Right);
    }

    void PaintSettingsPage(Graphics g, int left, int right)
    {
        DrawDivider(g, Px(44));
        Draw(g, Localization.T("language"), tinyFont, Theme.Muted, left, Px(52), right - left, TextFormatFlags.Left);
        Draw(g, Localization.T("taskbarIcons"), tinyFont, Theme.Muted, left, Px(122), right - left, TextFormatFlags.Left);
        string status = UpdateStatus();
        string version = Localization.T("version", App.Version) + (status != null ? " · " + status : "");
        Draw(g, version, tinyFont, Theme.Muted, left, Px(402), right - left, TextFormatFlags.Left);
    }

    void PaintAppearancePage(Graphics g, int left, int right)
    {
        DrawDivider(g, Px(44));
        Draw(g, Localization.T("colors"), tinyFont, Theme.Muted, left, Px(52), right - left, TextFormatFlags.Left);
        Draw(g, Localization.T("panelTheme"), tinyFont, Theme.Muted, left, Px(122), right - left, TextFormatFlags.Left);
    }

    string UpdateStatus()
    {
        switch (updatePhase)
        {
            case UpdatePhase.Checking: return Localization.T("updateChecking");
            case UpdatePhase.UpToDate: return Localization.T("updateCurrent");
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
        TextRenderer.DrawText(g, text, tinyFont, updateLink.Bounds, isHovered ? Theme.Text : Theme.Accent,
            TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
    }

    void DrawDivider(Graphics g, int y)
    {
        using (Pen divider = new Pen(Theme.Border))
            g.DrawLine(divider, 0, y, ClientSize.Width, y);
    }

    void DrawLimit(Graphics g, string label, UsageWindow window, string pace, bool withCountdown, int top, int left, int right)
    {
        Color color = Theme.ForPercent(window.Percent, state.Stale);
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

        Draw(g, ResetText(window, withCountdown), tinyFont, Theme.Muted, left, top + Px(38), right - left, TextFormatFlags.Left);
        if (pace != null) Draw(g, pace, tinyFont, Theme.Accent, left, top + Px(54), right - left, TextFormatFlags.Left);
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
        bool selected = chip.Selected != null ? chip.Selected()
            : chip.Style.HasValue ? chip.Style.Value == iconStyle
            : chip.Shown.HasValue ? chip.Shown.Value == iconsShown : chip.Language.Value == language;
        using (GraphicsPath path = Theme.Rounded(chip.Bounds, Px(8)))
        {
            if (selected || isHovered)
                using (Brush brush = new SolidBrush(selected ? Color.FromArgb(44, Theme.Accent) : Theme.Hover))
                    g.FillPath(brush, path);
            using (Pen pen = new Pen(selected ? Theme.Accent : Theme.Border))
                g.DrawPath(pen, path);
        }

        Color textColor = selected ? Theme.Text : Theme.Muted;
        if (chip.Swatch.HasValue)
        {
            int dot = Px(9);
            using (Brush brush = new SolidBrush(chip.Swatch.Value))
                g.FillEllipse(brush, chip.Bounds.X + Px(9), chip.Bounds.Y + (chip.Bounds.Height - dot) / 2, dot, dot);
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
        using (Bitmap preview = TrayIcon.Draw(chip.Style.Value, sample, true, state.Stale, iconSize, false))
            g.DrawImageUnscaled(preview, icon.Location);

        Rectangle label = new Rectangle(chip.Bounds.X, icon.Bottom + Px(2), chip.Bounds.Width, chip.Bounds.Bottom - icon.Bottom - Px(3));
        TextRenderer.DrawText(g, chip.Label, tinyFont, label, textColor,
            TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPadding);
    }

    void DrawHover(Graphics g, Rectangle bounds)
    {
        using (Brush brush = new SolidBrush(Theme.Hover))
        using (GraphicsPath path = Theme.Rounded(bounds, Px(6)))
            g.FillPath(brush, path);
    }

    void DrawRow(Graphics g, PanelTarget row, bool isHovered)
    {
        if (isHovered) DrawHover(g, row.Bounds);
        Rectangle glyph = new Rectangle(row.Bounds.X + Px(12), row.Bounds.Y, Px(18), row.Bounds.Height);
        Rectangle label = new Rectangle(glyph.Right + Px(10), row.Bounds.Y, row.Bounds.Right - glyph.Right - Px(10), row.Bounds.Height);
        TextFormatFlags flags = TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding;
        TextRenderer.DrawText(g, row.Glyph, glyphFont, glyph, Theme.Accent, flags | TextFormatFlags.HorizontalCenter);
        TextRenderer.DrawText(g, row.Label, labelFont, label, Theme.Text, flags | TextFormatFlags.Left);
    }

    void DrawToggle(Graphics g, PanelTarget toggle, bool isHovered)
    {
        if (isHovered) DrawHover(g, toggle.Bounds);
        Rectangle label = new Rectangle(toggle.Bounds.X + Px(12), toggle.Bounds.Y, toggle.Bounds.Width - Px(60), toggle.Bounds.Height);
        TextRenderer.DrawText(g, toggle.Label, labelFont, label, Theme.Text,
            TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPadding);

        bool on = toggle.Toggle();
        int width = Px(34);
        int height = Px(18);
        Rectangle pill = new Rectangle(toggle.Bounds.Right - width - Px(12), toggle.Bounds.Y + (toggle.Bounds.Height - height) / 2, width, height);
        using (Brush brush = new SolidBrush(on ? Theme.Accent : Theme.Track))
        using (GraphicsPath path = Theme.Rounded(pill, height / 2f))
            g.FillPath(brush, path);

        int knob = height - Px(6);
        int knobX = on ? pill.Right - knob - Px(3) : pill.X + Px(3);
        using (Brush brush = new SolidBrush(Theme.Text))
            g.FillEllipse(brush, knobX, pill.Y + Px(3), knob, knob);
    }

    void Draw(Graphics g, string text, Font font, Color color, int x, int y, int width, TextFormatFlags alignment)
    {
        Rectangle area = new Rectangle(x, y, width, Px(24));
        TextRenderer.DrawText(g, text, font, area, color, alignment | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
    }

    int Px(int logical) { return (int)Math.Round(logical * scale); }
}
