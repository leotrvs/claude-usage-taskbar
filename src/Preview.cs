using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

// Renders documentation images from fixed sample data, so screenshots never capture a real desktop.
static class Preview
{
    public static void Render(string directory)
    {
        Directory.CreateDirectory(directory);
        Theme.Colors = ColorTemplate.Blue;
        Theme.Panel = PanelTheme.Black;
        RenderPanels(directory, Language.English, "en");
        RenderPanels(directory, Language.Portuguese, "pt-BR");
        RenderIconStyles(Path.Combine(directory, "icon-styles.png"));
        RenderMascots(Path.Combine(directory, "mascots.png"));
    }

    static void RenderMascots(string path)
    {
        MascotMood[] moods = { MascotMood.Sleep, MascotMood.Idle, MascotMood.Work, MascotMood.Tired, MascotMood.Down };
        MascotStyle[] styles = { MascotStyle.Pixel, MascotStyle.Santa };
        const int Pixel = 4;
        const int Width = 128;
        const int Height = 88;
        const int Gap = 16;
        using (Bitmap sheet = new Bitmap(moods.Length * (Width + Gap) + Gap, styles.Length * (Height + Gap) + Gap))
        using (Graphics g = Graphics.FromImage(sheet))
        {
            g.Clear(Color.FromArgb(10, 10, 10));
            for (int row = 0; row < styles.Length; row++)
            {
                for (int column = 0; column < moods.Length; column++)
                {
                    using (Bitmap frame = PixelMascot.Render(styles[row], moods[column], 3, Pixel))
                        g.DrawImageUnscaled(frame, Gap + column * (Width + Gap), Gap + row * (Height + Gap));
                }
            }
            sheet.Save(path, ImageFormat.Png);
        }
    }

    // Frames for the README demo, turned into a GIF by scripts\make-demo.py.
    public static void RenderDemo(string directory)
    {
        Theme.Colors = ColorTemplate.Blue;
        Theme.Panel = PanelTheme.Black;
        RenderDemoFrames(Path.Combine(directory, "en"), Language.English);
        RenderDemoFrames(Path.Combine(directory, "pt-BR"), Language.Portuguese);
    }

    static void RenderDemoFrames(string directory, Language language)
    {
        const int Width = 420;
        const int Height = 408;
        const int TaskbarHeight = 44;
        const int PanelLeft = 108;
        const int PanelTop = 20;
        const int FirstRise = 18;
        const int LastRise = 78;
        const int LastFrame = 100;
        Directory.CreateDirectory(directory);
        Localization.Apply(language);

        PanelState state = new PanelState();
        state.Stale = false;
        state.Status = Localization.T("updatedAt", "14:32");
        state.Session.ResetsAt = DateTime.Now.AddHours(3).AddMinutes(12);
        state.Weekly.ResetsAt = DateTime.Now.AddDays(3);
        state.History = SampleHistory();

        using (UsagePanel panel = new UsagePanel(new PanelCallbacks()))
        {
            panel.ChangeMascot(MascotStyle.Classic);
            for (int frame = 0; frame <= LastFrame; frame++)
            {
                float rise = Math.Max(0, Math.Min(1, (frame - FirstRise) / (float)(LastRise - FirstRise)));
                int session = 42 + (int)(54 * rise);
                state.Session.Percent = session;
                state.Weekly.Percent = 61 + (int)(16 * rise);
                state.SessionPace = session >= 80 ? Localization.T("paceLimit", "16:05") : Localization.T("paceReset", session + 29);
                state.WeeklyPace = Localization.T("paceAhead", 8 + (int)(9 * rise));
                state.History[state.History.Count - 1].Session = session;
                panel.SetState(state, IconStyle.Gauge, language, IconsShown.Both);
                if (frame % 2 == 0) panel.StepMascot();

                using (Bitmap canvas = new Bitmap(Width, Height))
                using (Graphics g = Graphics.FromImage(canvas))
                using (Bitmap shot = new Bitmap(panel.Width, panel.Height))
                {
                    g.Clear(Color.FromArgb(22, 22, 28));
                    float opening = Math.Min(1, Math.Max(0, (frame - 12) / 5f));
                    panel.DrawToBitmap(shot, new Rectangle(Point.Empty, panel.Size));
                    DrawFaded(g, shot, PanelLeft, PanelTop + (int)(12 * (1 - opening)), opening);
                    DrawDemoTaskbar(g, Width, Height, TaskbarHeight, state);
                    DrawDemoCursor(g, frame, Width, Height);
                    canvas.Save(Path.Combine(directory, frame.ToString("000") + ".png"), ImageFormat.Png);
                }
            }
        }
    }

    static void DrawFaded(Graphics g, Bitmap image, int x, int y, float opacity)
    {
        if (opacity <= 0) return;
        ColorMatrix matrix = new ColorMatrix();
        matrix.Matrix33 = opacity;
        using (ImageAttributes attributes = new ImageAttributes())
        {
            attributes.SetColorMatrix(matrix);
            g.DrawImage(image, new Rectangle(x, y, image.Width, image.Height), 0, 0, image.Width, image.Height, GraphicsUnit.Pixel, attributes);
        }
    }

    static void DrawDemoTaskbar(Graphics g, int width, int height, int taskbarHeight, PanelState state)
    {
        const int Icon = 32;
        using (Brush bar = new SolidBrush(Color.FromArgb(32, 32, 34)))
            g.FillRectangle(bar, 0, height - taskbarHeight, width, taskbarHeight);
        using (Bitmap session = TrayIcon.Draw(IconStyle.Gauge, state.Session.Percent, true, false, Icon, false))
            g.DrawImageUnscaled(session, width - 96, height - taskbarHeight + (taskbarHeight - Icon) / 2);
        using (Bitmap weekly = TrayIcon.Draw(IconStyle.Gauge, state.Weekly.Percent, false, false, Icon, false))
            g.DrawImageUnscaled(weekly, width - 52, height - taskbarHeight + (taskbarHeight - Icon) / 2);
    }

    static void DrawDemoCursor(Graphics g, int frame, int width, int height)
    {
        float travel = Math.Min(1, frame / 11f);
        float x = 60 + (width - 140) * travel * travel * (3 - 2 * travel);
        float y = 90 + (height - 120) * travel * travel * (3 - 2 * travel);
        if (frame > 20) return;
        PointF[] arrow =
        {
            new PointF(x, y), new PointF(x, y + 17), new PointF(x + 4.5f, y + 13),
            new PointF(x + 8, y + 20), new PointF(x + 11, y + 19), new PointF(x + 7.5f, y + 12), new PointF(x + 13, y + 12)
        };
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.FillPolygon(Brushes.White, arrow);
        g.DrawPolygon(Pens.Black, arrow);
    }

    static void RenderPanels(string directory, Language language, string suffix)
    {
        Localization.Apply(language);
        PanelState state = new PanelState();
        state.Stale = false;
        state.Status = Localization.T("updatedAt", "14:32");
        state.Session.Percent = 42;
        state.Session.ResetsAt = DateTime.Now.AddHours(3).AddMinutes(12);
        state.Weekly.Percent = 67;
        state.Weekly.ResetsAt = DateTime.Now.AddDays(3);
        state.SessionPace = Localization.T("paceReset", 71);
        state.WeeklyPace = Localization.T("paceAhead", 8);
        state.History = SampleHistory();

        using (UsagePanel panel = new UsagePanel(new PanelCallbacks()))
        {
            panel.ChangeMascot(MascotStyle.Classic);
            RenderMenu(Path.Combine(directory, "menu-" + suffix + ".png"));
            panel.SetState(state, IconStyle.Ring, language, IconsShown.Both);
            SaveTheme(panel, PanelTheme.Graphite, Path.Combine(directory, "panel-" + suffix + ".png"));
            SaveTheme(panel, PanelTheme.Black, Path.Combine(directory, "panel-black-" + suffix + ".png"));
            SaveTheme(panel, PanelTheme.Light, Path.Combine(directory, "panel-light-" + suffix + ".png"));
            panel.ShowChartDetails();
            SaveChart(panel, Path.Combine(directory, "chart-" + suffix + ".png"));
            panel.ShowMainPage();
            RenderThemes(panel, Path.Combine(directory, "themes-" + suffix + ".png"));
            panel.ShowSettingsPage();
            Save(panel, Path.Combine(directory, "settings-" + suffix + ".png"));
            panel.ShowAppearancePage();
            Save(panel, Path.Combine(directory, "appearance-" + suffix + ".png"));
            panel.ShowSettingsPage();
            panel.SetUpdate(UpdatePhase.Checking, null);
            Save(panel, Path.Combine(directory, "update-checking-" + suffix + ".png"));
            panel.SetUpdate(UpdatePhase.Available, "1.1.0");
            Save(panel, Path.Combine(directory, "update-settings-" + suffix + ".png"));
            panel.ShowMainPage();
            Save(panel, Path.Combine(directory, "update-" + suffix + ".png"));
        }
    }

    static void RenderMenu(string path)
    {
        using (TrayMenu menu = new TrayMenu(new TrayMenuActions()))
        using (Bitmap bitmap = new Bitmap(menu.Width, menu.Height))
        {
            menu.BackColor = Theme.Background;
            menu.DrawToBitmap(bitmap, new Rectangle(Point.Empty, menu.Size));
            bitmap.Save(path, ImageFormat.Png);
        }
    }

    static void SaveTheme(UsagePanel panel, PanelTheme theme, string path)
    {
        Theme.Panel = theme;
        panel.ApplyTheme();
        Save(panel, path);
        Theme.Panel = PanelTheme.Black;
        panel.ApplyTheme();
    }

    static void RenderThemes(UsagePanel panel, string path)
    {
        ColorTemplate[] colors = { ColorTemplate.Blue, ColorTemplate.Green, ColorTemplate.Purple };
        PanelTheme[] themes = { PanelTheme.Graphite, PanelTheme.Black, PanelTheme.Light };
        const int Gap = 16;
        using (Bitmap sheet = new Bitmap(colors.Length * (panel.Width + Gap) + Gap, panel.Height + Gap * 2))
        using (Graphics g = Graphics.FromImage(sheet))
        {
            g.Clear(Color.FromArgb(16, 16, 16));
            for (int i = 0; i < colors.Length; i++)
            {
                Theme.Colors = colors[i];
                Theme.Panel = themes[i];
                panel.ApplyTheme();
                using (Bitmap shot = new Bitmap(panel.Width, panel.Height))
                {
                    panel.DrawToBitmap(shot, new Rectangle(Point.Empty, panel.Size));
                    g.DrawImageUnscaled(shot, Gap + i * (panel.Width + Gap), Gap);
                }
            }
            sheet.Save(path, ImageFormat.Png);
        }
        Theme.Colors = ColorTemplate.Blue;
        Theme.Panel = PanelTheme.Black;
        panel.ApplyTheme();
    }

    static void SaveChart(UsagePanel panel, string path)
    {
        int unit = panel.Width / 300;
        using (Bitmap bitmap = new Bitmap(panel.Width, panel.Height))
        {
            panel.DrawToBitmap(bitmap, new Rectangle(Point.Empty, panel.Size));
            using (Bitmap chart = bitmap.Clone(new Rectangle(0, 197 * unit, panel.Width, 66 * unit), bitmap.PixelFormat))
                chart.Save(path, ImageFormat.Png);
        }
    }

    static List<UsageSample> SampleHistory()
    {
        double[] values = { 2, 8, 15, 21, 30, 38, 2, 6, 14, 22, 29, 36, 42 };
        List<UsageSample> samples = new List<UsageSample>();
        for (int i = 0; i < values.Length; i++)
        {
            UsageSample sample = new UsageSample();
            sample.Time = DateTime.Now.AddHours(i - values.Length + 1);
            sample.Session = values[i];
            samples.Add(sample);
        }
        return samples;
    }

    static void Save(UsagePanel panel, string path)
    {
        using (Bitmap bitmap = new Bitmap(panel.Width, panel.Height))
        {
            panel.DrawToBitmap(bitmap, new Rectangle(Point.Empty, panel.Size));
            bitmap.Save(path, ImageFormat.Png);
        }
    }

    static void RenderIconStyles(string path)
    {
        int[] samples = { 0, 25, 67, 85, 96 };
        const int Size = 64;
        const int Gap = 12;
        int width = samples.Length * (Size + Gap) + Gap;
        using (Bitmap sheet = new Bitmap(width, Size * 3 + Gap * 4))
        using (Graphics g = Graphics.FromImage(sheet))
        {
            g.Clear(Color.FromArgb(10, 10, 10));
            for (int i = 0; i < samples.Length; i++)
            {
                int x = Gap + i * (Size + Gap);
                DrawIcon(g, IconStyle.Gauge, samples[i], x, Gap);
                DrawIcon(g, IconStyle.Ring, samples[i], x, Size + Gap * 2);
                DrawIcon(g, IconStyle.Needle, samples[i], x, Size * 2 + Gap * 3);
            }
            sheet.Save(path, ImageFormat.Png);
        }
    }

    static void DrawIcon(Graphics g, IconStyle style, int percent, int x, int y)
    {
        using (Bitmap icon = TrayIcon.Draw(style, percent, true, false, 64, false))
            g.DrawImageUnscaled(icon, x, y);
    }
}
