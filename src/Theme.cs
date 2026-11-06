using System;
using System.Drawing;
using System.Drawing.Drawing2D;

static class Theme
{
    public static readonly Color Background = Color.FromArgb(32, 32, 32);
    public static readonly Color Hover = Color.FromArgb(52, 52, 52);
    public static readonly Color Border = Color.FromArgb(62, 62, 62);
    public static readonly Color Track = Color.FromArgb(58, 58, 58);
    public static readonly Color Text = Color.FromArgb(242, 242, 242);
    public static readonly Color Muted = Color.FromArgb(160, 160, 160);
    public static readonly Color Accent = Color.FromArgb(224, 138, 91);
    public static readonly Color Green = Color.FromArgb(74, 222, 128);
    public static readonly Color Amber = Color.FromArgb(245, 158, 11);
    public static readonly Color Red = Color.FromArgb(239, 68, 68);
    public static readonly Color Stale = Color.FromArgb(130, 130, 130);

    public static Color ForPercent(int? percent, bool stale)
    {
        if (stale || !percent.HasValue) return Stale;
        if (percent.Value >= 90) return Red;
        if (percent.Value >= 70) return Amber;
        return Green;
    }

    public static GraphicsPath Rounded(RectangleF area, float radius)
    {
        float d = Math.Min(radius * 2, Math.Min(area.Width, area.Height));
        GraphicsPath path = new GraphicsPath();
        path.AddArc(area.X, area.Y, d, d, 180, 90);
        path.AddArc(area.Right - d, area.Y, d, d, 270, 90);
        path.AddArc(area.Right - d, area.Bottom - d, d, d, 0, 90);
        path.AddArc(area.X, area.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}
