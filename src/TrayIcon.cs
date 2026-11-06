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
    static readonly Color GaugeTrack = Color.FromArgb(78, 72, 68);
    static readonly Color GaugeDanger = Color.FromArgb(150, 56, 50);

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
            float stroke = Math.Max(1.5f, size * 0.095f);

            using (GraphicsPath tile = Theme.Rounded(new RectangleF(0, 0, size - 1, size - 1), size * 0.24f))
            using (Brush brush = new SolidBrush(Tile))
                g.FillPath(brush, tile);

            if (style == IconStyle.Gauge) DrawGauge(g, size, stroke, progress, color);
            else DrawOutline(g, size, stroke, progress, color);

            DrawNumber(g, style, percent, primary && !stale ? Theme.Text : color, size, stroke);
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

    static void DrawGauge(Graphics g, int size, float stroke, float progress, Color color)
    {
        const float StartAngle = 135;
        const float FullSweep = 270;
        float inset = size * 0.04f + stroke / 2;
        float diameter = size - 1 - inset * 2;
        const float DangerStart = 0.9f;
        using (Pen track = NewRoundPen(GaugeTrack, stroke))
            g.DrawArc(track, inset, inset, diameter, diameter, StartAngle, FullSweep);
        using (Pen danger = NewRoundPen(GaugeDanger, stroke))
            g.DrawArc(danger, inset, inset, diameter, diameter, StartAngle + FullSweep * DangerStart, FullSweep * (1 - DangerStart));
        if (progress <= 0) return;
        using (Pen value = NewRoundPen(color, stroke))
            g.DrawArc(value, inset, inset, diameter, diameter, StartAngle, Math.Max(1, FullSweep * progress));
    }

    // A line that starts at the top and runs clockwise around the edge; at 0% there is none.
    static void DrawOutline(Graphics g, int size, float stroke, float progress, Color color)
    {
        if (progress <= 0) return;
        float left = stroke / 2;
        float right = size - 1 - stroke / 2;
        float center = (size - 1) / 2f;
        float diameter = size * 0.44f;
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

            PointF[] points = TakeFraction(path.PathPoints, progress);
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
        using (StringFormat format = (StringFormat)StringFormat.GenericTypographic.Clone())
        using (Brush brush = new SolidBrush(color))
        {
            format.Alignment = StringAlignment.Center;
            format.LineAlignment = StringAlignment.Center;
            format.FormatFlags = StringFormatFlags.NoWrap | StringFormatFlags.NoClip;
            using (Font font = NewFittedFont(g, text, style, size, stroke, format))
                g.DrawString(text, font, brush, new RectangleF(0, 0, size, size), format);
        }
    }

    // Shrinks the number until it clears the ring or line around it, whatever the digits are.
    static Font NewFittedFont(Graphics g, string text, IconStyle style, int size, float stroke, StringFormat format)
    {
        const float DigitHeight = 0.74f;
        float margin = Math.Max(0.75f, size * 0.04f);
        float inner = style == IconStyle.Gauge
            ? size - 1 - size * 0.08f - stroke * 2
            : size - 1 - stroke * 2;

        for (float pixels = size * 0.62f; pixels > size * 0.3f; pixels -= 0.25f)
        {
            Font font = new Font("Segoe UI", pixels, FontStyle.Bold, GraphicsUnit.Pixel);
            float height = pixels * DigitHeight;
            float allowed = style == IconStyle.Gauge
                ? 2 * (float)Math.Sqrt(Math.Max(0, inner * inner / 4 - height * height / 4)) - margin
                : inner - margin * 2;
            if (g.MeasureString(text, font, PointF.Empty, format).Width <= allowed) return font;
            font.Dispose();
        }
        return new Font("Segoe UI", size * 0.3f, FontStyle.Bold, GraphicsUnit.Pixel);
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
