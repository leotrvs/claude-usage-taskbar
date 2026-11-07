using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Globalization;
using System.Windows.Forms;

static class TrayIcon
{
    static readonly Color Track = Color.FromArgb(120, 128, 128, 128); // Reads on dark and light taskbars.
    static readonly Color StrongTrack = Color.FromArgb(170, 128, 128, 128);
    static readonly Color DarkForeground = Color.FromArgb(32, 32, 32);
    const float DialStart = 135;
    const float DialSweep = 270;
    const float GaugeStart = 120; // A wider arc than the needle's, so it reads as the same size as the ring.
    const float GaugeSweep = 300;
    const float RingStart = -90; // The top of the circle.
    const float RingSweep = 360;
    const float MinimumProgress = 0.09f; // Keeps a low value visible at 16 pixels.
    const string NumberFont = "Segoe UI";
    const int LargestPixelDigits = 20; // Below 125% scaling a font has too few pixels to stay readable.

    // Drawn by hand for the taskbar, as large as the circle allows. Rows are separated by a slash.
    // One pixel between two digits makes an odd width, so the icon is one pixel smaller to have a center pixel.
    static readonly string[] PixelDigits =
    {
        ".##./#..#/#..#/#..#/#..#/#..#/.##.",
        "..#./.##./..#./..#./..#./..#./####",
        ".##./#..#/...#/..#./.#../#.../####",
        "###./...#/...#/.##./...#/...#/###.",
        "#..#/#..#/#..#/####/...#/...#/...#",
        "####/#.../#.../###./...#/...#/###.",
        ".##./#.../#.../###./#..#/#..#/.##.",
        "####/...#/...#/..#./..#./.#../.#..",
        ".##./#..#/#..#/.##./#..#/#..#/.##.",
        ".##./#..#/#..#/.###/...#/...#/.##."
    };
    const string UnitsOne = ".#../##../.#../.#../.#../.#../####"; // Mirrors the stem of a tens 1, so 11 stays balanced.
    // A single digit is five pixels wide, also an odd width.
    static readonly string[] PixelSingleDigits =
    {
        ".###./#...#/#...#/#...#/#...#/#...#/.###.",
        "..#../.##../..#../..#../..#../..#../.###.",
        ".###./#...#/....#/...#./..#../.#.../#####",
        "####./....#/....#/.###./....#/....#/####.",
        "#...#/#...#/#...#/#####/....#/....#/....#",
        "#####/#..../#..../####./....#/....#/####.",
        ".###./#..../#..../####./#...#/#...#/.###.",
        "#####/....#/....#/...#./..#../..#../..#..",
        ".###./#...#/#...#/.###./#...#/#...#/.###.",
        ".###./#...#/#...#/.####/....#/....#/.###."
    };
    const string PixelUnknown = ".###./#...#/....#/...#./..#../...../..#..";
    const string NarrowOne = "#/#/#/#/#/#/#"; // 100 has no room for full digits.
    const string NarrowZero = "###/#.#/#.#/#.#/#.#/#.#/###";
    const float PixelGaugeStroke = 1.5f; // A thin line leaves the digits room inside the small dial.

    // primary = 5-hour limit (white number); the weekly icon colors its number by level.
    public static Bitmap Draw(IconStyle style, int? percent, bool primary, bool stale, int size, bool onLight)
    {
        Bitmap bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        int box = size <= LargestPixelDigits ? size - 1 : size;
        using (Graphics g = Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.Half; // Keeps the circle centered on the pixel grid.
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

            Color color = Theme.ForPercent(percent, stale);
            float progress = percent.HasValue ? Math.Max(0, Math.Min(100, percent.Value)) / 100f : 0;
            float gaugeStroke = box < size ? PixelGaugeStroke : Math.Max(1.75f, box * 0.1f);
            float stroke = style == IconStyle.Gauge ? gaugeStroke : Math.Max(2f, box * 0.12f);

            if (style == IconStyle.Ring) DrawArc(g, box, stroke, RingStart, RingSweep, progress, color, false);
            else if (style == IconStyle.Gauge) DrawArc(g, box, stroke, GaugeStart, GaugeSweep, progress, color, box < size);
            else DrawArc(g, box, stroke, DialStart, DialSweep, progress, color, false);

            Color foreground = primary && !stale ? (onLight ? DarkForeground : Theme.TrayText) : color;
            if (style == IconStyle.Needle && percent.HasValue) DrawNeedle(g, box, progress, foreground);
            else if (style == IconStyle.Gauge || !percent.HasValue) DrawNumber(g, percent, foreground, box);
        }
        return bitmap;
    }

    static Pen NewRoundPen(Color color, float width)
    {
        Pen pen = new Pen(color, width);
        pen.StartCap = LineCap.Round;
        pen.EndCap = LineCap.Round;
        pen.LineJoin = LineJoin.Round;
        return pen;
    }

    // A line this thin falls between two pixels and fades, so a strong one is drawn twice to fill them in.
    static void DrawArc(Graphics g, int size, float stroke, float start, float sweep, float progress, Color color, bool strong)
    {
        float inset = stroke / 2;
        float diameter = size - inset * 2;
        using (Pen track = NewRoundPen(strong ? StrongTrack : Track, stroke))
            g.DrawArc(track, inset, inset, diameter, diameter, start, sweep);
        if (progress <= 0) return;
        using (Pen value = NewRoundPen(color, stroke))
        {
            float amount = sweep * Math.Max(MinimumProgress, progress);
            g.DrawArc(value, inset, inset, diameter, diameter, start, amount);
            if (strong) g.DrawArc(value, inset, inset, diameter, diameter, start, amount);
        }
    }

    static void DrawNeedle(Graphics g, int size, float progress, Color color)
    {
        double angle = (DialStart + DialSweep * progress) * Math.PI / 180;
        float center = size / 2f;
        float length = size * 0.3f;
        float hub = size * 0.09f;
        using (Pen needle = NewRoundPen(color, Math.Max(1.5f, size * 0.09f)))
            g.DrawLine(needle, center, center, center + length * (float)Math.Cos(angle), center + length * (float)Math.Sin(angle));
        using (Brush brush = new SolidBrush(color))
            g.FillEllipse(brush, center - hub, center - hub, hub * 2, hub * 2);
    }

    static void DrawNumber(Graphics g, int? percent, Color color, int size)
    {
        string text = percent.HasValue ? percent.Value.ToString(CultureInfo.InvariantCulture) : "?";
        g.PixelOffsetMode = PixelOffsetMode.Default; // The half-pixel shift would blur hinted digits.
        if (size <= LargestPixelDigits)
        {
            DrawPixelNumber(g, percent, color, size);
            return;
        }
        // Inside the dial, leaving a clear gap to the arc on both sides.
        float width = size * (text.Length > 2 ? 0.7f : 0.56f); // 100 needs the extra room to stay readable.
        RectangleF area = new RectangleF((size - width) / 2, size * 0.3f, width, size * 0.42f);

        using (FontFamily family = new FontFamily(NumberFont))
        using (StringFormat format = (StringFormat)StringFormat.GenericTypographic.Clone())
        using (Brush brush = new SolidBrush(color))
        {
            format.FormatFlags |= StringFormatFlags.NoClip;
            // Whole pixel sizes keep the digits sharp; the measure is the drawn shape, not the font's box.
            for (int pixels = size; pixels > 4; pixels--)
            {
                // Sized by the widest digit, so every value with the same digit count looks alike.
                RectangleF widest = MeasureInk(new string('8', text.Length), family, pixels, format);
                if (widest.Width > area.Width || widest.Height > area.Height) continue;

                using (Font font = new Font(family, pixels, FontStyle.Bold, GraphicsUnit.Pixel))
                {
                    Rectangle ink = RenderedInk(text, font, format, size);
                    float x = (float)Math.Round((size - ink.Width) / 2f - ink.X);
                    float y = (float)Math.Round(area.Y + (area.Height - ink.Height) / 2 - ink.Y);
                    g.DrawString(text, font, brush, x, y, format);
                }
                return;
            }
        }
    }

    static void DrawPixelNumber(Graphics g, int? percent, Color color, int size)
    {
        string[] glyphs = PixelGlyphs(percent);
        int width = glyphs.Length - 1;
        foreach (string glyph in glyphs) width += glyph.IndexOf('/');
        int height = glyphs[0].Split('/').Length;

        g.SmoothingMode = SmoothingMode.None;
        using (Brush brush = new SolidBrush(color))
        {
            int x = (size - width) / 2;
            int top = (size - height) / 2;
            foreach (string glyph in glyphs)
            {
                string[] rows = glyph.Split('/');
                for (int y = 0; y < rows.Length; y++)
                    for (int column = 0; column < rows[y].Length; column++)
                        if (rows[y][column] == '#') g.FillRectangle(brush, x + column, top + y, 1, 1);
                x += rows[0].Length + 1;
            }
        }
    }

    static string[] PixelGlyphs(int? percent)
    {
        if (!percent.HasValue) return new[] { PixelUnknown };
        int value = Math.Max(0, Math.Min(100, percent.Value));
        if (value == 100) return new[] { NarrowOne, NarrowZero, NarrowZero };
        if (value < 10) return new[] { PixelSingleDigits[value] };
        return new[] { PixelDigits[value / 10], value % 10 == 1 ? UnitsOne : PixelDigits[value % 10] };
    }

    // Hinting moves digits away from their measured outline, so centering uses the real pixels.
    static Rectangle RenderedInk(string text, Font font, StringFormat format, int size)
    {
        const int Visible = 100;
        int origin = size / 2;
        using (Bitmap canvas = new Bitmap(size * 2, size * 2, PixelFormat.Format32bppArgb))
        {
            using (Graphics g = Graphics.FromImage(canvas))
            {
                g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                g.DrawString(text, font, Brushes.White, origin, origin, format);
            }
            int left = canvas.Width, top = canvas.Height, right = -1, bottom = -1;
            for (int y = 0; y < canvas.Height; y++)
            {
                for (int x = 0; x < canvas.Width; x++)
                {
                    if (canvas.GetPixel(x, y).A < Visible) continue;
                    left = Math.Min(left, x);
                    right = Math.Max(right, x);
                    top = Math.Min(top, y);
                    bottom = Math.Max(bottom, y);
                }
            }
            return right < 0 ? Rectangle.Empty : Rectangle.FromLTRB(left - origin, top - origin, right + 1 - origin, bottom + 1 - origin);
        }
    }

    static RectangleF MeasureInk(string text, FontFamily family, int pixels, StringFormat format)
    {
        using (GraphicsPath path = new GraphicsPath())
        {
            path.AddString(text, family, (int)FontStyle.Bold, pixels, PointF.Empty, format);
            return path.GetBounds();
        }
    }
}

class TraySlot : IDisposable
{
    readonly NotifyIcon notifyIcon = new NotifyIcon();
    readonly bool primary;
    string renderedKey;
    IntPtr iconHandle = IntPtr.Zero;

    public TraySlot(bool primary, MouseEventHandler onMouseUp)
    {
        this.primary = primary;
        notifyIcon.MouseUp += onMouseUp;
    }

    public void Update(IconStyle style, int? percent, bool stale, string tooltip, bool visible)
    {
        bool onLight = Settings.LightTaskbar;
        string key = style + "|" + (percent.HasValue ? percent.Value.ToString(CultureInfo.InvariantCulture) : "?") + "|" + stale + "|" + Theme.Colors + "|" + onLight;
        if (key != renderedKey)
        {
            renderedKey = key;
            using (Bitmap bitmap = TrayIcon.Draw(style, percent, primary, stale, SystemInformation.SmallIconSize.Width, onLight))
            {
                IntPtr handle = bitmap.GetHicon();
                notifyIcon.Icon = Icon.FromHandle(handle);
                if (iconHandle != IntPtr.Zero) Native.DestroyIcon(iconHandle);
                iconHandle = handle;
            }
        }
        notifyIcon.Text = tooltip.Length > 63 ? tooltip.Substring(0, 63) : tooltip;
        notifyIcon.Visible = visible;
    }

    public bool IsShown { get { return notifyIcon.Visible; } }

    public void Notify(string title, string text)
    {
        notifyIcon.ShowBalloonTip(0, title, text, ToolTipIcon.Warning);
    }

    public void NotifyInfo(string title, string text)
    {
        notifyIcon.ShowBalloonTip(0, title, text, ToolTipIcon.Info);
    }

    public void Dispose()
    {
        notifyIcon.Visible = false;
        notifyIcon.Dispose();
        if (iconHandle != IntPtr.Zero) Native.DestroyIcon(iconHandle);
    }
}
