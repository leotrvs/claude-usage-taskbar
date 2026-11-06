using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Globalization;
using System.Windows.Forms;

static class TrayIcon
{
    static readonly Color Tile = Color.FromArgb(34, 30, 28);
    static readonly Color Track = Color.FromArgb(92, 85, 80);
    const float DialStart = 135;
    const float DialSweep = 270;
    const float GaugeHeight = 0.64f;
    const float MinimumProgress = 0.09f; // Keeps a low value visible at 16 pixels.
    const string NumberFont = "Segoe UI";

    // primary = 5-hour limit (white number); the weekly icon colors its number by level.
    public static Bitmap Draw(IconStyle style, int? percent, bool primary, bool stale, int size)
    {
        Bitmap bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using (Graphics g = Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

            Color color = Theme.ForPercent(percent, stale);
            float progress = percent.HasValue ? Math.Max(0, Math.Min(100, percent.Value)) / 100f : 0;
            float stroke = Math.Max(2f, size * 0.12f);

            using (GraphicsPath tile = Theme.Rounded(new RectangleF(0, 0, size, size), size * 0.2f))
            using (Brush brush = new SolidBrush(Tile))
                g.FillPath(brush, tile);

            if (style == IconStyle.Outline) DrawOutline(g, size, stroke, progress, color);
            else if (style == IconStyle.Gauge) DrawGauge(g, size, stroke, progress, color);
            else DrawDial(g, size, stroke, progress, color);

            Color foreground = primary && !stale ? Theme.Text : color;
            if (style == IconStyle.Needle && percent.HasValue) DrawNeedle(g, size, progress, foreground);
            else DrawNumber(g, style, percent, foreground, size, stroke);
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

    static float ArcInset(int size, float stroke)
    {
        return stroke / 2 + size * 0.02f;
    }

    // A half circle over the number, so the two never touch.
    static void DrawGauge(Graphics g, int size, float stroke, float progress, Color color)
    {
        float inset = ArcInset(size, stroke);
        DrawArc(g, new RectangleF(inset, inset, size - inset * 2, size * GaugeHeight), 180, 180, stroke, progress, color);
    }

    // The same 270-degree dial as the app icon, open at the bottom.
    static void DrawDial(Graphics g, int size, float stroke, float progress, Color color)
    {
        float inset = ArcInset(size, stroke);
        float diameter = size - inset * 2;
        DrawArc(g, new RectangleF(inset, inset, diameter, diameter), DialStart, DialSweep, stroke, progress, color);
    }

    static void DrawArc(Graphics g, RectangleF box, float start, float sweep, float stroke, float progress, Color color)
    {
        using (Pen track = NewRoundPen(Track, stroke))
            g.DrawArc(track, box.X, box.Y, box.Width, box.Height, start, sweep);
        if (progress <= 0) return;
        using (Pen value = NewRoundPen(color, stroke))
            g.DrawArc(value, box.X, box.Y, box.Width, box.Height, start, sweep * Math.Max(MinimumProgress, progress));
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

    // A line that starts at the top and runs clockwise around the edge, over a track.
    static void DrawOutline(Graphics g, int size, float stroke, float progress, Color color)
    {
        float left = stroke / 2;
        float right = size - stroke / 2;
        float center = size / 2f;
        float diameter = size * 0.36f;
        float radius = diameter / 2;

        using (GraphicsPath path = new GraphicsPath())
        {
            path.AddLine(center, left, right - radius, left);
            path.AddArc(right - diameter, left, diameter, diameter, 270, 90);
            path.AddArc(right - diameter, right - diameter, diameter, diameter, 0, 90);
            path.AddArc(left, right - diameter, diameter, diameter, 90, 90);
            path.AddArc(left, left, diameter, diameter, 180, 90);
            path.AddLine(left + radius, left, center, left);
            path.Flatten(null, 0.1f);

            using (Pen track = NewRoundPen(Track, stroke))
                g.DrawLines(track, path.PathPoints);
            if (progress <= 0) return;

            PointF[] points = TakeFraction(path.PathPoints, Math.Max(MinimumProgress, progress));
            if (points.Length < 2) return;
            using (Pen pen = NewRoundPen(color, stroke))
                g.DrawLines(pen, points);
        }
    }

    static PointF[] TakeFraction(PointF[] points, float fraction)
    {
        float total = 0;
        for (int i = 1; i < points.Length; i++) total += Distance(points[i - 1], points[i]);

        float remaining = total * fraction;
        List<PointF> taken = new List<PointF>();
        taken.Add(points[0]);
        for (int i = 1; i < points.Length && remaining > 0; i++)
        {
            float length = Distance(points[i - 1], points[i]);
            if (length <= remaining)
            {
                taken.Add(points[i]);
            }
            else
            {
                float t = remaining / length;
                taken.Add(new PointF(
                    points[i - 1].X + (points[i].X - points[i - 1].X) * t,
                    points[i - 1].Y + (points[i].Y - points[i - 1].Y) * t));
            }
            remaining -= length;
        }
        return taken.ToArray();
    }

    static float Distance(PointF a, PointF b)
    {
        float x = b.X - a.X;
        float y = b.Y - a.Y;
        return (float)Math.Sqrt(x * x + y * y);
    }

    static void DrawNumber(Graphics g, IconStyle style, int? percent, Color color, int size, float stroke)
    {
        string text = percent.HasValue ? percent.Value.ToString(CultureInfo.InvariantCulture) : "?";
        RectangleF area = NumberArea(style, size, stroke);

        using (FontFamily family = new FontFamily(NumberFont))
        using (StringFormat format = (StringFormat)StringFormat.GenericTypographic.Clone())
        using (Brush brush = new SolidBrush(color))
        {
            format.FormatFlags |= StringFormatFlags.NoClip;
            // Whole pixel sizes keep the digits sharp; the measure is the drawn shape, not the font's box.
            for (int pixels = size; pixels > 4; pixels--)
            {
                RectangleF ink = MeasureInk(text, family, pixels, format);
                if (ink.Width > area.Width || ink.Height > area.Height) continue;

                float x = (float)Math.Round(area.X + (area.Width - ink.Width) / 2 - ink.X);
                float y = (float)Math.Round(area.Y + (area.Height - ink.Height) / 2 - ink.Y);
                using (Font font = new Font(family, pixels, FontStyle.Bold, GraphicsUnit.Pixel))
                    g.DrawString(text, font, brush, x, y, format);
                return;
            }
        }
    }

    // Below the gauge's arc, or inside the outline's line.
    static RectangleF NumberArea(IconStyle style, int size, float stroke)
    {
        if (style == IconStyle.Gauge)
        {
            float side = size * 0.06f;
            float top = ArcInset(size, stroke) + size * GaugeHeight / 2 + stroke * 0.35f;
            return new RectangleF(side, top, size - side * 2, size * 0.98f - top);
        }
        float inset = stroke + size * 0.03f;
        return new RectangleF(inset, inset, size - inset * 2, size - inset * 2);
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
        string key = style + "|" + (percent.HasValue ? percent.Value.ToString(CultureInfo.InvariantCulture) : "?") + "|" + stale;
        if (key != renderedKey)
        {
            renderedKey = key;
            using (Bitmap bitmap = TrayIcon.Draw(style, percent, primary, stale, SystemInformation.SmallIconSize.Width))
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

    public void Dispose()
    {
        notifyIcon.Visible = false;
        notifyIcon.Dispose();
        if (iconHandle != IntPtr.Zero) Native.DestroyIcon(iconHandle);
    }
}
