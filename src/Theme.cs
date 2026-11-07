using System;
using System.Drawing;
using System.Drawing.Drawing2D;

enum ColorTemplate { Green, Blue, Purple }
enum PanelTheme { Graphite, Black, Light, Auto }

static class Theme
{
    public static ColorTemplate Colors = Settings.Colors;
    public static PanelTheme Panel = Settings.PanelTheme;

    static bool systemIsLight = Settings.AppsUseLightTheme;

    // Read once and refreshed when Windows reports a change, because colors are asked for on every paint.
    public static void RefreshSystemTheme()
    {
        systemIsLight = Settings.AppsUseLightTheme;
    }

    static PanelTheme Shown
    {
        get
        {
            if (Panel != PanelTheme.Auto) return Panel;
            return systemIsLight ? PanelTheme.Light : PanelTheme.Black;
        }
    }

    public static bool IsLight { get { return Shown == PanelTheme.Light; } }

    static Color Pick(Color graphite, Color black, Color light)
    {
        return Shown == PanelTheme.Light ? light : Shown == PanelTheme.Black ? black : graphite;
    }

    public static Color Background { get { return Pick(Color.FromArgb(32, 32, 32), Color.FromArgb(10, 10, 10), Color.FromArgb(250, 249, 245)); } }
    public static Color Hover { get { return Pick(Color.FromArgb(52, 52, 52), Color.FromArgb(34, 34, 34), Color.FromArgb(238, 236, 229)); } }
    public static Color Border { get { return Pick(Color.FromArgb(62, 62, 62), Color.FromArgb(46, 46, 46), Color.FromArgb(222, 219, 210)); } }
    public static Color Track { get { return Pick(Color.FromArgb(58, 58, 58), Color.FromArgb(44, 44, 44), Color.FromArgb(226, 223, 214)); } }
    public static Color Text { get { return Pick(TrayText, TrayText, Color.FromArgb(31, 30, 29)); } }
    public static Color Muted { get { return Pick(Color.FromArgb(160, 160, 160), Color.FromArgb(160, 160, 160), Color.FromArgb(107, 105, 99)); } }
    public static readonly Color TrayText = Color.FromArgb(242, 242, 242); // The taskbar stays dark whatever the panel theme is.
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

    // The brightest level colors are too faint on the light panel, so its bars and numbers use darker ones.
    public static Color ForPanel(int? percent, bool stale)
    {
        Color color = ForPercent(percent, stale);
        if (!IsLight) return color;
        if (color == Yellow || color == Amber) return Color.FromArgb(202, 138, 4);
        if (color == NormalColor(ColorTemplate.Green)) return Color.FromArgb(22, 163, 74);
        if (color == NormalColor(ColorTemplate.Purple)) return Color.FromArgb(116, 72, 226);
        return color;
    }

    public static Color NormalColor(ColorTemplate template)
    {
        if (template == ColorTemplate.Blue) return Color.FromArgb(72, 150, 245);
        if (template == ColorTemplate.Purple) return Color.FromArgb(139, 92, 246);
        return Color.FromArgb(52, 208, 106);
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
