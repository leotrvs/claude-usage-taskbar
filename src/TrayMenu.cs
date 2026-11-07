using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

class TrayMenuActions
{
    public Action OpenPanel = delegate { };
    public Action Refresh = delegate { };
    public Action OpenSettings = delegate { };
    public Action Quit = delegate { };
}

// The right-click menu of the taskbar icons, drawn like the panel instead of as a standard Windows menu.
class TrayMenu : Form
{
    const int LogicalWidth = 196;
    const int RowHeight = 34;
    const int Inset = 6;
    const int SeparatorHeight = 9;
    const int ScreenGap = 10;
    const int OpenMs = 130;
    const int CloseMs = 90;
    const float HoverMs = 110;
    const int Slide = 8;
    const int TickMs = 15;
    const int LongestFrameMs = 50;
    const int ReopenGuardMs = 300;
    const int WS_EX_TOOLWINDOW = 0x80;
    const int WS_EX_TOPMOST = 0x8;
    const int CS_DROPSHADOW = 0x20000;
    const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    const int DWMWA_BORDER_COLOR = 34;
    const int DWMWCP_ROUND = 2;

    class Row
    {
        public string Glyph;
        public string LabelKey;
        public Action Run;
        public Rectangle Bounds;
        public float Hover;
    }

    readonly List<Row> rows = new List<Row>();
    readonly Timer timer = new Timer();
    readonly float scale;
    readonly Font labelFont;
    readonly Font glyphFont;
    int separatorY;
    int hovered = -1;
    int restingTop;
    int motionStart;
    int lastTick;
    int hiddenAt;
    bool closing;

    public TrayMenu(TrayMenuActions actions)
    {
        using (Graphics g = CreateGraphics()) scale = g.DpiX / 96f;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        DoubleBuffered = true;
        KeyPreview = true;
        labelFont = EmbeddedFonts.New(Px(13), false);
        glyphFont = new Font("Segoe MDL2 Assets", Px(14), GraphicsUnit.Pixel);

        AddRow("", "openPanel", actions.OpenPanel);
        AddRow("", "refreshNow", actions.Refresh);
        AddRow("", "settings", actions.OpenSettings);
        AddRow("", "closeWidget", actions.Quit);
        LayOutRows();

        timer.Interval = TickMs;
        timer.Tick += delegate { Advance(); };
    }

    void AddRow(string glyph, string labelKey, Action run)
    {
        Row row = new Row();
        row.Glyph = glyph;
        row.LabelKey = labelKey;
        row.Run = run;
        rows.Add(row);
    }

    // The last row is set apart, because it closes the app.
    void LayOutRows()
    {
        int width = Px(LogicalWidth);
        int y = Px(Inset);
        for (int i = 0; i < rows.Count; i++)
        {
            if (i == rows.Count - 1)
            {
                separatorY = y + Px(SeparatorHeight) / 2;
                y += Px(SeparatorHeight);
            }
            rows[i].Bounds = new Rectangle(Px(Inset), y, width - Px(Inset) * 2, Px(RowHeight));
            y += Px(RowHeight);
        }
        ClientSize = new Size(width, y + Px(Inset));
    }

    int Px(int logical) { return (int)Math.Round(logical * scale); }

    static float Ease(float value)
    {
        return value * value * (3 - 2 * value);
    }

    public void ShowAt(Point cursor)
    {
        if (Environment.TickCount - hiddenAt < ReopenGuardMs) return; // The click that just closed it.
        ApplyTheme();
        foreach (Row row in rows) row.Hover = 0;
        hovered = -1;
        closing = false;

        Rectangle area = Screen.FromPoint(cursor).WorkingArea;
        int gap = Px(ScreenGap);
        int left = Math.Max(area.Left + gap, Math.Min(cursor.X - Width / 2, area.Right - Width - gap));
        restingTop = area.Bottom - Height - gap;
        bool animate = SystemInformation.UIEffectsEnabled;
        Location = new Point(left, restingTop + (animate ? Px(Slide) : 0));
        Opacity = animate ? 0 : 1;
        Show();
        Native.SetForegroundWindow(Handle);
        Activate();
        StartMotion();
    }

    void ApplyTheme()
    {
        BackColor = Theme.Background;
        int border = Theme.Border.R | (Theme.Border.G << 8) | (Theme.Border.B << 16);
        Native.DwmSetWindowAttribute(Handle, DWMWA_BORDER_COLOR, ref border, sizeof(int));
    }

    void StartMotion()
    {
        motionStart = Environment.TickCount;
        lastTick = motionStart;
        timer.Start();
    }

    void CloseMenu()
    {
        if (!Visible || closing) return;
        hiddenAt = Environment.TickCount;
        closing = true;
        if (SystemInformation.UIEffectsEnabled)
        {
            StartMotion();
            return;
        }
        FinishClosing();
    }

    void FinishClosing()
    {
        timer.Stop();
        closing = false;
        Hide();
    }

    void Advance()
    {
        int now = Environment.TickCount;
        float elapsed = Math.Min(LongestFrameMs, now - lastTick);
        lastTick = now;

        int duration = closing ? CloseMs : OpenMs;
        float progress = Math.Min(1f, (now - motionStart) / (float)duration);
        float eased = Ease(progress);
        if (closing)
        {
            Opacity = 1 - eased;
            Top = restingTop + (int)(Px(Slide) / 2 * eased);
            if (progress >= 1) FinishClosing();
        }
        else
        {
            Opacity = eased;
            Top = restingTop + (int)(Px(Slide) * (1 - eased));
        }

        bool moving = progress < 1;
        for (int i = 0; i < rows.Count; i++)
        {
            float goal = i == hovered ? 1 : 0;
            float step = elapsed / HoverMs;
            float value = rows[i].Hover < goal ? Math.Min(goal, rows[i].Hover + step) : Math.Max(goal, rows[i].Hover - step);
            moving |= value != rows[i].Hover;
            rows[i].Hover = value;
        }
        if (!moving) timer.Stop();
        Invalidate();
    }

    protected override CreateParams CreateParams
    {
        get
        {
            CreateParams parameters = base.CreateParams;
            parameters.ExStyle |= WS_EX_TOOLWINDOW | WS_EX_TOPMOST;
            parameters.ClassStyle |= CS_DROPSHADOW;
            return parameters;
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        int corners = DWMWCP_ROUND;
        Native.DwmSetWindowAttribute(Handle, DWMWA_WINDOW_CORNER_PREFERENCE, ref corners, sizeof(int));
    }

    protected override void OnDeactivate(EventArgs e)
    {
        base.OnDeactivate(e);
        CloseMenu();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.KeyCode == Keys.Escape) CloseMenu();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        int index = rows.FindIndex(delegate(Row row) { return row.Bounds.Contains(e.Location); });
        if (index == hovered) return;
        hovered = index;
        Cursor = index >= 0 ? Cursors.Hand : Cursors.Default;
        if (!timer.Enabled) lastTick = Environment.TickCount;
        if (!timer.Enabled) motionStart = Environment.TickCount - OpenMs;
        timer.Start();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        hovered = -1;
        if (!timer.Enabled) lastTick = Environment.TickCount;
        if (!timer.Enabled) motionStart = Environment.TickCount - OpenMs;
        timer.Start();
    }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        base.OnMouseClick(e);
        if (hovered < 0) return;
        Action run = rows[hovered].Run;
        CloseMenu();
        run();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        TextFormatFlags flags = TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding;
        foreach (Row row in rows)
        {
            if (row.Hover > 0)
                using (Brush brush = new SolidBrush(Color.FromArgb((int)(255 * Ease(row.Hover)), Theme.Hover)))
                using (GraphicsPath path = Theme.Rounded(row.Bounds, Px(6)))
                    g.FillPath(brush, path);

            Rectangle glyph = new Rectangle(row.Bounds.X + Px(10), row.Bounds.Y, Px(18), row.Bounds.Height);
            Rectangle label = new Rectangle(glyph.Right + Px(10), row.Bounds.Y, row.Bounds.Right - glyph.Right - Px(10), row.Bounds.Height);
            TextRenderer.DrawText(g, row.Glyph, glyphFont, glyph, Theme.Accent, flags | TextFormatFlags.HorizontalCenter);
            TextRenderer.DrawText(g, Localization.T(row.LabelKey), labelFont, label, Theme.Text, flags | TextFormatFlags.Left);
        }
        using (Pen divider = new Pen(Theme.Border))
            g.DrawLine(divider, Px(Inset + 4), separatorY, ClientSize.Width - Px(Inset + 4), separatorY);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) timer.Dispose();
        base.Dispose(disposing);
    }
}
