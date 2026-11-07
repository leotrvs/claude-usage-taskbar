using System;
using System.Drawing;
using System.Drawing.Drawing2D;

enum ColorTemplate { Green, Blue, Purple }
enum PanelTheme { Graphite, Black }

static class Theme
{
    public static ColorTemplate Colors = Settings.Colors;
    public static PanelTheme Panel = Settings.PanelTheme;

    public static Color Background { get { return Panel == PanelTheme.Black ? Color.FromArgb(10, 10, 10) : Color.FromArgb(32, 32, 32); } }
    public static Color Hover { get { return Panel == PanelTheme.Black ? Color.FromArgb(34, 34, 34) : Color.FromArgb(52, 52, 52); } }
    public static Color Border { get { return Panel == PanelTheme.Black ? Color.FromArgb(46, 46, 46) : Color.FromArgb(62, 62, 62); } }
    public static Color Track { get { return Panel == PanelTheme.Black ? Color.FromArgb(44, 44, 44) : Color.FromArgb(58, 58, 58); } }
    public static readonly Color Text = Color.FromArgb(242, 242, 242);
    public static readonly Color Muted = Color.FromArgb(160, 160, 160);
    public static readonly Color Accent = Color.FromArgb(222, 110, 73);
    public static readonly Color Red = Color.FromArgb(239, 68, 68);
    static readonly Color Amber = Color.FromArgb(245, 158, 11);
    static readonly Color Yellow = Color.FromArgb(250, 204, 21);
    public static readonly Color Stale = Color.FromArgb(130, 130, 130);

    public static Color ForPercent(int? percent, bool stale)
    {
        if (stale || !percent.HasValue) return Stale;
        if (percent.Value >= 90) return Red;
        if (percent.Value >= 70) return Colors == ColorTemplate.Green ? Amber : Yellow;
        return NormalColor(Colors);
    }

    public static Color NormalColor(ColorTemplate template)
    {
        if (template == ColorTemplate.Blue) return Color.FromArgb(72, 150, 245);
        if (template == ColorTemplate.Purple) return Color.FromArgb(167, 139, 250);
        return Color.FromArgb(74, 222, 128);
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
