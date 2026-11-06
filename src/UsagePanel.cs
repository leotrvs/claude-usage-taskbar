using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

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
    public Action<IconsShown> ChangeIconsShown = delegate { };
    public Action ToggleNotifications = delegate { };
    public Action ToggleStartWithWindows = delegate { };
    public Action ToggleAutoUpdate = delegate { };
    public Action ToggleConnection = delegate { };
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

    public string Label
    {
        get { return LiteralLabel ?? Localization.T(LabelKey); }
    }
}

class UsagePanel : Form
{
    const int LogicalWidth = 300;
    const int LogicalHeight = 420;
    const int ChipTop = 300;
    const int ActionsTop = 350;
    const int MascotHeight = 34;
    const double ChartHours = 12;
    const int ReopenGuardMilliseconds = 300;
    const int WS_EX_TOOLWINDOW = 0x80;
    const int CS_DROPSHADOW = 0x20000;
    const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    const int DWMWA_BORDER_COLOR = 34;
    const int DWMWCP_ROUND = 2;

    readonly List<PanelTarget> mainTargets = new List<PanelTarget>();
    readonly List<PanelTarget> settingsTargets = new List<PanelTarget>();
    readonly float scale;
    readonly System.Windows.Forms.Timer mascotTimer = new System.Windows.Forms.Timer();
    Mascot mascot;
    readonly Font titleFont, labelFont, smallFont, tinyFont, percentFont, glyphFont;
    List<PanelTarget> targets;
    PanelState state = new PanelState();
    IconStyle iconStyle;
    Language language;
    IconsShown iconsShown;
    int hovered = -1;
    int hiddenAt;

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
        targets = mainTargets;
        mascotTimer.Tick += delegate { AdvanceMascot(); };
    }

    void BuildMainPage(PanelCallbacks callbacks)
    {
        AddStyleChip(IconStyle.Gauge, "gauge", 0, callbacks);
        AddStyleChip(IconStyle.Outline, "outline", 1, callbacks);
        mainTargets.Add(NewRow("", "settings", ActionsTop, delegate { ShowPage(settingsTargets); }));
        mainTargets.Add(NewRow("", "closeWidget", ActionsTop + 32, callbacks.CloseWidget));
    }

    void AddStyleChip(IconStyle style, string labelKey, int column, PanelCallbacks callbacks)
    {
        PanelTarget chip = new PanelTarget();
        chip.Style = style;
        chip.LabelKey = labelKey;
        chip.Bounds = ChipBounds(column, 2, ChipTop, 36);
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

    // Inter when installed, otherwise the Windows UI font.
    Font NewUiFont(int logicalSize, bool strong)
    {
        string family = HasFont("Inter") ? "Inter" : "Segoe UI";
        string semibold = family + " SemiBold";
        if (strong && HasFont(semibold)) return new Font(semibold, Px(logicalSize), GraphicsUnit.Pixel);
        return new Font(family, Px(logicalSize), strong ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Pixel);
    }

    static bool HasFont(string name)
    {
        using (InstalledFontCollection fonts = new InstalledFontCollection())
            return Array.Exists(fonts.Families, delegate(FontFamily f) { return f.Name == name; });
    }

    public void SetState(PanelState value, IconStyle style, Language selectedLanguage, IconsShown shown)
    {
        state = value;
        iconStyle = style;
        language = selectedLanguage;
        iconsShown = shown;
        if (Visible) Invalidate();
    }

    public void ShowSettingsPage()
    {
        ShowPage(settingsTargets);
    }

    void ShowPage(List<PanelTarget> page)
    {
        targets = page;
        hovered = -1;
        Invalidate();
    }

    public void Toggle()
    {
        if (Visible) { Hide(); return; }
        if (Environment.TickCount - hiddenAt < ReopenGuardMilliseconds) return; // The click that just closed it.
        ShowPanel();
    }

    public void ShowPanel()
    {
        targets = mainTargets;
        Rectangle area = Screen.FromPoint(Cursor.Position).WorkingArea;
        Location = new Point(area.Right - Width - Px(12), area.Bottom - Height - Px(12));
        Show();
        Native.SetForegroundWindow(Handle);
        Activate();
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
        int border = Theme.Border.R | (Theme.Border.G << 8) | (Theme.Border.B << 16);
        Native.DwmSetWindowAttribute(Handle, DWMWA_BORDER_COLOR, ref border, sizeof(int));
    }

    protected override void OnVisibleChanged(EventArgs e)
    {
        base.OnVisibleChanged(e);
        if (!Visible)
        {
            mascotTimer.Stop();
            return;
        }
        if (mascot == null) mascot = Mascot.Load(Px(MascotHeight));
        if (mascot != null)
        {
            mascotTimer.Interval = mascot.CurrentDelayMs;
            mascotTimer.Start();
        }
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
        hiddenAt = Environment.TickCount;
        Hide();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.KeyCode == Keys.Escape) Hide();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
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
        else PaintSettingsPage(g, left, right);

        for (int i = 0; i < targets.Count; i++)
        {
            PanelTarget target = targets[i];
            bool isHovered = i == hovered;
            if (target.Style.HasValue || target.Language.HasValue || target.Shown.HasValue) DrawChip(g, target, isHovered);
            else if (target.Toggle != null) DrawToggle(g, target, isHovered);
            else DrawRow(g, target, isHovered);
        }
    }

    void PaintMainPage(Graphics g, int left, int right)
    {
        Draw(g, App.Name, titleFont, Theme.Text, left, Px(14), right - left, TextFormatFlags.Left);
        int mascotSpace = mascot != null ? mascot.CurrentFrame.Width + Px(8) : 0;
        Draw(g, state.Status, tinyFont, Theme.Muted, left, Px(35), right - left - mascotSpace, TextFormatFlags.Left);
        if (mascot != null && targets == mainTargets)
        {
            Bitmap frame = mascot.CurrentFrame;
            g.DrawImageUnscaled(frame, right - frame.Width, Px(14));
        }
        DrawLimit(g, Localization.T("session"), state.Session, state.SessionPace, true, Px(62), left, right);
        DrawLimit(g, Localization.T("weekly"), state.Weekly, state.WeeklyPace, false, Px(138), left, right);
        Draw(g, Localization.T("history"), tinyFont, Theme.Muted, left, Px(214), right - left, TextFormatFlags.Left);
        DrawChart(g, new Rectangle(left, Px(232), right - left, Px(40)));
        Draw(g, Localization.T("taskbarIcon"), tinyFont, Theme.Muted, left, Px(280), right - left, TextFormatFlags.Left);
        DrawDivider(g, Px(344));
    }

    void DrawChart(Graphics g, Rectangle area)
    {
        using (Pen baseline = new Pen(Theme.Border))
            g.DrawLine(baseline, area.Left, area.Bottom, area.Right, area.Bottom);

        DateTime end = DateTime.Now;
        DateTime start = end.AddHours(-ChartHours);
        List<PointF> points = new List<PointF>();
        foreach (UsageSample sample in state.History)
        {
            if (sample.Time < start) continue;
            float x = area.Left + (float)((sample.Time - start).TotalHours / ChartHours) * area.Width;
            float y = area.Bottom - (float)(Math.Min(100, sample.Session) / 100) * area.Height;
            points.Add(new PointF(x, y));
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
    }

    void PaintSettingsPage(Graphics g, int left, int right)
    {
        DrawDivider(g, Px(44));
        Draw(g, Localization.T("language"), tinyFont, Theme.Muted, left, Px(52), right - left, TextFormatFlags.Left);
        Draw(g, Localization.T("taskbarIcons"), tinyFont, Theme.Muted, left, Px(122), right - left, TextFormatFlags.Left);
        Draw(g, Localization.T("version", App.Version), tinyFont, Theme.Muted, left, Px(392), right - left, TextFormatFlags.Left);
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
        bool selected = chip.Style.HasValue ? chip.Style.Value == iconStyle
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
        if (!chip.Style.HasValue)
        {
            TextRenderer.DrawText(g, chip.Label, tinyFont, chip.Bounds, textColor,
                TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPadding);
            return;
        }

        int iconSize = Px(22);
        Rectangle icon = new Rectangle(chip.Bounds.X + Px(8), chip.Bounds.Y + (chip.Bounds.Height - iconSize) / 2, iconSize, iconSize);
        int? sample = state.Session.Percent ?? 60;
        using (Bitmap preview = TrayIcon.Draw(chip.Style.Value, sample, true, state.Stale, iconSize))
            g.DrawImageUnscaled(preview, icon.Location);

        Rectangle label = new Rectangle(icon.Right + Px(7), chip.Bounds.Y, chip.Bounds.Right - icon.Right - Px(9), chip.Bounds.Height);
        TextRenderer.DrawText(g, chip.Label, smallFont, label, textColor,
            TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPadding);
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
