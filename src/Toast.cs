using System;
using System.Drawing;
using System.Windows.Forms;

// A notification in the panel's style, with the mascot. It never takes the focus from what you are doing.
class Toast : Form
{
    const int LogicalWidth = 390;
    const int LogicalHeight = 88;
    const int ScreenGap = 12;
    const int Slide = 14;
    const int OpenMs = 180;
    const int CloseMs = 130;
    const int VisibleMs = 7000;
    const int TickMs = 15;
    const int MascotPixelSize = 2;
    const string CloseGlyph = "";
    const int WS_EX_TOOLWINDOW = 0x80;
    const int WS_EX_TOPMOST = 0x8;
    const int WS_EX_NOACTIVATE = 0x8000000;
    const int CS_DROPSHADOW = 0x20000;
    const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    const int DWMWA_BORDER_COLOR = 34;
    const int DWMWCP_ROUND = 2;

    readonly Timer motion = new Timer();
    readonly Timer frames = new Timer();
    readonly float scale;
    readonly Font titleFont;
    readonly Font bodyFont;
    readonly Font glyphFont;
    readonly Action onOpen;
    PixelMascot mascot;
    MascotAction? repeatedAction;
    string title = "";
    string body = "";
    int restingTop;
    int motionStart;
    bool closing;

    public Toast(Action onOpen)
    {
        this.onOpen = onOpen;
        using (Graphics g = CreateGraphics()) scale = g.DpiX / 96f;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        DoubleBuffered = true;
        ClientSize = new Size(Px(LogicalWidth), Px(LogicalHeight));
        titleFont = EmbeddedFonts.New(Px(14), true);
        bodyFont = EmbeddedFonts.New(Px(12), false);
        glyphFont = new Font("Segoe MDL2 Assets", Px(10), GraphicsUnit.Pixel);
        motion.Interval = TickMs;
        motion.Tick += delegate { AdvanceMotion(); };
        frames.Tick += delegate { AdvanceMascot(); };
    }

    int Px(int logical) { return (int)Math.Round(logical * scale); }

    static float Ease(float value)
    {
        return value * value * (3 - 2 * value);
    }

    public void Prepare(string newTitle, string newBody, MascotMood mood, MascotAction? action)
    {
        title = newTitle;
        body = newBody;
        repeatedAction = action;
        BackColor = Theme.Background;

        MascotStyle style = Settings.MascotStyle == MascotStyle.Classic ? MascotStyle.Pixel : Settings.MascotStyle;
        if (mascot != null) mascot.Dispose();
        mascot = new PixelMascot(style, Math.Max(1, (int)(MascotPixelSize * scale)));
        mascot.Mood = mood;
        if (action.HasValue) mascot.Play(action.Value);
    }

    public void Present(string newTitle, string newBody, MascotMood mood, MascotAction? action)
    {
        Prepare(newTitle, newBody, mood, action);
        int border = Theme.Border.R | (Theme.Border.G << 8) | (Theme.Border.B << 16);
        Native.DwmSetWindowAttribute(Handle, DWMWA_BORDER_COLOR, ref border, sizeof(int));

        Rectangle area = Screen.PrimaryScreen.WorkingArea;
        restingTop = area.Bottom - Height - Px(ScreenGap);
        bool animate = SystemInformation.UIEffectsEnabled;
        Location = new Point(area.Right - Width - Px(ScreenGap), restingTop + (animate ? Px(Slide) : 0));
        Opacity = animate ? 0 : 1;
        closing = false;
        motionStart = Environment.TickCount;
        Show();
        motion.Start();
        frames.Interval = mascot.CurrentDelayMs;
        frames.Start();
        Invalidate();
    }

    void AdvanceMotion()
    {
        int age = Environment.TickCount - motionStart;
        if (closing)
        {
            float progress = Math.Min(1f, age / (float)CloseMs);
            Opacity = 1 - Ease(progress);
            Top = restingTop + (int)(Px(Slide) / 2 * Ease(progress));
            if (progress >= 1) Finish();
            return;
        }
        float opening = Math.Min(1f, age / (float)OpenMs);
        Opacity = Ease(opening);
        Top = restingTop + (int)(Px(Slide) * (1 - Ease(opening)));
        if (age >= VisibleMs && !Bounds.Contains(Cursor.Position)) Dismiss();
    }

    void AdvanceMascot()
    {
        mascot.Advance();
        if (repeatedAction.HasValue && !mascot.IsPlaying) mascot.Play(repeatedAction.Value);
        frames.Interval = mascot.CurrentDelayMs;
        Invalidate();
    }

    void Dismiss()
    {
        if (closing) return;
        closing = true;
        motionStart = Environment.TickCount;
    }

    void Finish()
    {
        motion.Stop();
        frames.Stop();
        Hide();
    }

    Rectangle CloseArea
    {
        get { return new Rectangle(ClientSize.Width - Px(26), Px(6), Px(20), Px(20)); }
    }

    protected override bool ShowWithoutActivation { get { return true; } }

    protected override CreateParams CreateParams
    {
        get
        {
            CreateParams parameters = base.CreateParams;
            parameters.ExStyle |= WS_EX_TOOLWINDOW | WS_EX_TOPMOST | WS_EX_NOACTIVATE;
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

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        Cursor = Cursors.Hand;
    }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        base.OnMouseClick(e);
        Dismiss();
        if (!CloseArea.Contains(e.Location)) onOpen();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        Bitmap frame = mascot.CurrentFrame;
        int mascotLeft = Px(12);
        g.DrawImageUnscaled(frame, mascotLeft, (ClientSize.Height - frame.Height) / 2);

        int left = mascotLeft + frame.Width + Px(12);
        int width = ClientSize.Width - left - Px(30);
        TextFormatFlags flags = TextFormatFlags.Left | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis;
        TextRenderer.DrawText(g, title, titleFont, new Rectangle(left, Px(16), width, Px(20)), Theme.Text, flags);
        TextRenderer.DrawText(g, body, bodyFont, new Rectangle(left, Px(38), ClientSize.Width - left - Px(14), Px(36)), Theme.Muted,
            flags | TextFormatFlags.WordBreak);
        TextRenderer.DrawText(g, CloseGlyph, glyphFont, CloseArea, Theme.Muted,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            motion.Dispose();
            frames.Dispose();
            if (mascot != null) mascot.Dispose();
        }
        base.Dispose(disposing);
    }
}
