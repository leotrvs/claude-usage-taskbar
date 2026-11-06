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
    const float GaugeArcHeight = 0.38f;
    static string numberFont;

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
            float stroke = style == IconStyle.Gauge ? Math.Max(1.25f, size * 0.08f) : Math.Max(1f, size * 0.07f);

            using (GraphicsPath tile = Theme.Rounded(new RectangleF(0, 0, size, size), size * 0.2f))
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

    // A flat arc along the top, so the number keeps the full width below it.
    static void DrawGauge(Graphics g, int size, float stroke, float progress, Color color)
    {
        const float StartAngle = 180;
        const float FullSweep = 180;
        const float DangerStart = 0.9f;
        float inset = stroke / 2 + 0.25f;
        float width = size - inset * 2;
        float height = size * GaugeArcHeight;
        using (Pen track = NewRoundPen(GaugeTrack, stroke))
            g.DrawArc(track, inset, inset, width, height, StartAngle, FullSweep);
        using (Pen danger = NewRoundPen(GaugeDanger, stroke))
            g.DrawArc(danger, inset, inset, width, height, StartAngle + FullSweep * DangerStart, FullSweep * (1 - DangerStart));
        if (progress <= 0) return;
        using (Pen value = NewRoundPen(color, stroke))
            g.DrawArc(value, inset, inset, width, height, StartAngle, Math.Max(2, FullSweep * progress));
    }

    // A line that starts at the top and runs clockwise around the edge; at 0% there is none.
    static void DrawOutline(Graphics g, int size, float stroke, float progress, Color color)
    {
        if (progress <= 0) return;
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
        // The gauge's arc only uses the top, while the outline's line runs around every side.
        float gap = Math.Max(0.5f, size * 0.03f);
        float top = (style == IconStyle.Gauge ? size * GaugeArcHeight / 2 + stroke : stroke) + gap;
        float bottom = size - (style == IconStyle.Gauge ? gap : stroke + gap);
        float side = style == IconStyle.Gauge ? gap : stroke + gap;
        RectangleF area = new RectangleF(side, top, size - side * 2, bottom - top);

        using (GraphicsPath path = new GraphicsPath())
        using (FontFamily family = new FontFamily(NumberFontFamily()))
        using (StringFormat format = (StringFormat)StringFormat.GenericTypographic.Clone())
        using (Matrix fit = new Matrix())
        using (Brush brush = new SolidBrush(color))
        {
            path.AddString(text, family, (int)FontStyle.Regular, 100, PointF.Empty, format);

            // Fits the drawn shape of the digits, not the font's box, so they can never reach the edge.
            RectangleF ink = path.GetBounds();
            float scale = Math.Min(area.Width / ink.Width, area.Height / ink.Height);
            fit.Translate(area.X + (area.Width - ink.Width * scale) / 2, area.Y + (area.Height - ink.Height * scale) / 2);
            fit.Scale(scale, scale);
            fit.Translate(-ink.X, -ink.Y);
            path.Transform(fit);
            g.FillPath(brush, path);
        }
    }

    // A condensed font fits taller digits in the same width. It ships with Windows 10 and 11.
    static string NumberFontFamily()
    {
        if (numberFont != null) return numberFont;
        using (InstalledFontCollection fonts = new InstalledFontCollection())
        {
            bool available = Array.Exists(fonts.Families, delegate(FontFamily f) { return f.Name == "Bahnschrift SemiBold Condensed"; });
            numberFont = available ? "Bahnschrift SemiBold Condensed" : "Segoe UI";
        }
        return numberFont;
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
