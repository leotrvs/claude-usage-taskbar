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
        RenderOutfits(Path.Combine(directory, "mascot-outfits.png"));
        RenderReactions(Path.Combine(directory, "mascot-reactions.png"));
        RenderWalk(Path.Combine(directory, "mascot-walk.png"), MascotStyle.Detective, false, new[] { 0, 14, 26, 40, 54, 68 }, false);
        RenderWalk(Path.Combine(directory, "mascot-panic-walk.png"), MascotStyle.Pixel, true, new[] { 0, 3, 6, 9, 12, 15, 18 }, false);
        RenderWalk(Path.Combine(directory, "mascot-racer-drive.png"), MascotStyle.Racer, false, new[] { 4, 13, 17, 21, 25, 32, 40, 48 }, false);
        RenderActs(directory);
    }

    static void RenderMascots(string path)
    {
        MascotMood[] moods =
        {
            MascotMood.Sleep, MascotMood.Idle, MascotMood.Work, MascotMood.Tired, MascotMood.Down,
            MascotMood.Night, MascotMood.Waiting, MascotMood.Panic, MascotMood.Coffee, MascotMood.Juggle
        };
        DrawMascotSheet(path, moods, 5, MascotStyle.Pixel);
    }

    static void RenderOutfits(string path)
    {
        MascotStyle[] styles =
        {
            MascotStyle.Santa, MascotStyle.Headphones, MascotStyle.Chef, MascotStyle.Scientist,
            MascotStyle.Detective, MascotStyle.Winter, MascotStyle.Racer, MascotStyle.Halloween, MascotStyle.Carnival
        };
        const int Columns = 4;
        const int Pixel = 4;
        const int Width = 128;
        const int Height = 88;
        const int Gap = 16;
        int rows = (styles.Length + Columns - 1) / Columns;
        using (Bitmap sheet = new Bitmap(Columns * (Width + Gap) + Gap, rows * (Height + Gap) + Gap))
        using (Graphics g = Graphics.FromImage(sheet))
        {
            g.Clear(Color.FromArgb(10, 10, 10));
            for (int i = 0; i < styles.Length; i++)
                using (Bitmap frame = PixelMascot.Render(styles[i], MascotMood.Idle, 3, Pixel))
                    g.DrawImageUnscaled(frame, Gap + i % Columns * (Width + Gap), Gap + i / Columns * (Height + Gap));
            sheet.Save(path, ImageFormat.Png);
        }
    }

    // Each outfit's own walk: the ones that dance, skate, ride or carry something.
    static void RenderActs(string directory)
    {
        int[] slow = { 3, 14, 26, 34, 40, 46, 58, 72 };
        int[] fast = { 3, 8, 14, 20, 28, 36, 42, 47 };
        int[] ride = { 2, 5, 9, 14, 19, 26, 33, 36 };
        RenderWalk(Path.Combine(directory, "mascot-act-santa.png"), MascotStyle.Santa, false, ride, true);
        RenderWalk(Path.Combine(directory, "mascot-act-headphones.png"), MascotStyle.Headphones, false, slow, true);
        RenderWalk(Path.Combine(directory, "mascot-act-chef.png"), MascotStyle.Chef, false, slow, true);
        RenderWalk(Path.Combine(directory, "mascot-act-scientist.png"), MascotStyle.Scientist, false, slow, true);
        RenderWalk(Path.Combine(directory, "mascot-act-winter.png"), MascotStyle.Winter, false, fast, true);
        RenderWalk(Path.Combine(directory, "mascot-act-halloween.png"), MascotStyle.Halloween, false, fast, true);
        RenderWalk(Path.Combine(directory, "mascot-act-carnival.png"), MascotStyle.Carnival, false, slow, true);
    }

    // Reactions that belong to one outfit, and the melting after many clicks.
    static void RenderReactions(string path)
    {
        const int Columns = 4;
        const int Pixel = 4;
        const int Width = 128;
        const int Height = 88;
        const int Gap = 16;
        Func<Bitmap>[] cells =
        {
            delegate { return PixelMascot.Render(MascotStyle.Scientist, MascotMood.Work, 5, Pixel); },
            delegate { return PixelMascot.Render(MascotStyle.Scientist, MascotMood.Panic, 0, Pixel); },
            delegate { return PixelMascot.Render(MascotStyle.Detective, MascotMood.Waiting, 1, Pixel); },
            delegate { return PixelMascot.Render(MascotStyle.Winter, MascotMood.Idle, 4, Pixel); },
            delegate { return PixelMascot.Render(MascotStyle.Racer, MascotMood.Panic, 0, Pixel); },
            delegate { return PixelMascot.Render(MascotStyle.Racer, MascotMood.Idle, 3, Pixel, MascotAction.Celebrate, 3); },
            delegate { return PixelMascot.Render(MascotStyle.Pixel, MascotMood.Idle, 3, Pixel, MascotAction.Love, 10); },
            delegate { return PixelMascot.Render(MascotStyle.Pixel, MascotMood.Idle, 3, Pixel, MascotAction.Love, 17); },
            delegate { return PixelMascot.Render(MascotStyle.Scientist, MascotMood.Idle, 5, Pixel, MascotAction.Celebrate, 5); },
            delegate { return PixelMascot.Render(MascotStyle.Scientist, MascotMood.Sleep, 2, Pixel); },
            delegate { return PixelMascot.Render(MascotStyle.Detective, MascotMood.Sleep, 2, Pixel); },
            delegate { return PixelMascot.Render(MascotStyle.Detective, MascotMood.Idle, 6, Pixel, MascotAction.Celebrate, 4); },
            delegate { return PixelMascot.Render(MascotStyle.Winter, MascotMood.Sleep, 2, Pixel); },
            delegate { return PixelMascot.Render(MascotStyle.Winter, MascotMood.Panic, 1, Pixel); },
            delegate { return PixelMascot.Render(MascotStyle.Racer, MascotMood.Waiting, 2, Pixel); },
            delegate { return PixelMascot.Render(MascotStyle.Carnival, MascotMood.Sleep, 2, Pixel); }
        };
        int rows = (cells.Length + Columns - 1) / Columns;
        using (Bitmap sheet = new Bitmap(Columns * (Width + Gap) + Gap, rows * (Height + Gap) + Gap))
        using (Graphics g = Graphics.FromImage(sheet))
        {
            g.Clear(Color.FromArgb(10, 10, 10));
            for (int i = 0; i < cells.Length; i++)
                using (Bitmap frame = cells[i]())
                    g.DrawImageUnscaled(frame, Gap + i % Columns * (Width + Gap), Gap + i / Columns * (Height + Gap));
            sheet.Save(path, ImageFormat.Png);
        }
    }

    // The idle character walking across the empty part of the panel header, as snapshots of one walk.
    // 'at' lists the ticks of the walk to photograph; 'special' makes the outfit do its own act.
    static void RenderWalk(string path, MascotStyle style, bool panic, int[] at, bool special)
    {
        const int HeaderHeight = 64;
        const int Gap = 8;
        int[] steps = new int[at.Length];
        for (int i = 0; i < at.Length; i++) steps[i] = at[i] - (i > 0 ? at[i - 1] : 0);
        Localization.Apply(Language.English);
        PanelState state = new PanelState();
        state.Stale = false;
        state.Status = Localization.T("updatedAt", "14:32");
        state.Sessions.Running = 1;
        state.Session.Percent = panic ? 91 : 42;
        state.Weekly.Percent = 67;
        if (panic) state.MinutesToLimit = 12;
        state.History = new List<UsageSample>();

        using (UsagePanel panel = new UsagePanel(new PanelCallbacks()))
        {
            panel.ChangeMascot(style);
            panel.SetState(state, IconStyle.Ring, Language.English, IconsShown.Both);
            panel.StartMascotWalk(special);
            int height = HeaderHeight * steps.Length + Gap * (steps.Length + 1);
            using (Bitmap sheet = new Bitmap(panel.Width + Gap * 2, height))
            using (Graphics g = Graphics.FromImage(sheet))
            {
                g.Clear(Color.FromArgb(10, 10, 10));
                for (int i = 0; i < steps.Length; i++)
                {
                    for (int s = 0; s < steps[i]; s++) panel.StepMascot();
                    using (Bitmap shot = new Bitmap(panel.Width, panel.Height))
                    {
                        panel.DrawToBitmap(shot, new Rectangle(Point.Empty, panel.Size));
                        g.DrawImage(shot, new Rectangle(Gap, Gap + i * (HeaderHeight + Gap), panel.Width, HeaderHeight),
                            new Rectangle(0, 0, panel.Width, HeaderHeight), GraphicsUnit.Pixel);
                    }
                }
                sheet.Save(path, ImageFormat.Png);
            }
        }
    }

    static void DrawMascotSheet(string path, MascotMood[] moods, int columns, MascotStyle style)
    {
        const int Pixel = 4;
        const int Width = 128;
        const int Height = 88;
        const int Gap = 16;
        int rows = (moods.Length + columns - 1) / columns;
        using (Bitmap sheet = new Bitmap(columns * (Width + Gap) + Gap, rows * (Height + Gap) + Gap))
        using (Graphics g = Graphics.FromImage(sheet))
        {
            g.Clear(Color.FromArgb(10, 10, 10));
            for (int i = 0; i < moods.Length; i++)
                using (Bitmap frame = PixelMascot.Render(style, moods[i], 3, Pixel))
                    g.DrawImageUnscaled(frame, Gap + i % columns * (Width + Gap), Gap + i / columns * (Height + Gap));
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
        const int Width = 324;
        const int Height = 414;
        const int TaskbarHeight = 44;
        const int PanelLeft = 12;
        const int PanelTop = 12;
        const int LastRise = 78;
        const int LastFrame = 100;
        IconStyle[] styles = { IconStyle.Ring, IconStyle.Gauge, IconStyle.Needle };
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
                float rise = Math.Min(1, frame / (float)LastRise);
                IconStyle style = styles[frame * styles.Length / (LastFrame + 1)];
                int session = 42 + (int)(54 * rise);
                state.Session.Percent = session;
                state.Weekly.Percent = 61 + (int)(16 * rise);
                state.SessionPace = session >= 80 ? Localization.T("paceLimit", "16:05") : Localization.T("paceReset", session + 29);
                state.WeeklyPace = Localization.T("paceAhead", 8 + (int)(9 * rise));
                state.History[state.History.Count - 1].Session = session;
                panel.SetState(state, style, language, IconsShown.Session);
                if (frame % 2 == 0) panel.StepMascot();

                using (Bitmap canvas = new Bitmap(Width, Height))
                using (Graphics g = Graphics.FromImage(canvas))
                using (Bitmap shot = new Bitmap(panel.Width, panel.Height))
                {
                    g.Clear(Color.FromArgb(22, 22, 28));
                    panel.DrawToBitmap(shot, new Rectangle(Point.Empty, panel.Size));
                    g.DrawImageUnscaled(shot, PanelLeft, PanelTop);
                    DrawDemoTaskbar(g, Width, Height, TaskbarHeight, state, style);
                    canvas.Save(Path.Combine(directory, frame.ToString("000") + ".png"), ImageFormat.Png);
                }
            }
        }
    }

    static void DrawDemoTaskbar(Graphics g, int width, int height, int taskbarHeight, PanelState state, IconStyle style)
    {
        const int Icon = 32;
        using (Brush bar = new SolidBrush(Color.FromArgb(32, 32, 34)))
            g.FillRectangle(bar, 0, height - taskbarHeight, width, taskbarHeight);
        using (Bitmap icon = TrayIcon.Draw(style, state.Session.Percent, true, false, Icon, false))
            g.DrawImageUnscaled(icon, width - 60, height - taskbarHeight + (taskbarHeight - Icon) / 2);
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
            RenderToast(Path.Combine(directory, "toast-update-" + suffix + ".png"),
                Localization.T("updatedTitle"), Localization.T("updatedBody", App.Name, "1.6.1"), MascotMood.Idle, MascotAction.Celebrate);
            RenderToast(Path.Combine(directory, "toast-alert-" + suffix + ".png"),
                Localization.T("alertTitle", Localization.T("session"), 90), Localization.T("alertBody", "sex 02:00"), MascotMood.Panic, null);
            RenderToast(Path.Combine(directory, "toast-renewed-" + suffix + ".png"),
                Localization.T("renewedTitle", Localization.T("session")), Localization.T("renewedBody"), MascotMood.Idle, MascotAction.Celebrate);
            panel.SetState(state, IconStyle.Ring, language, IconsShown.Both);
            SaveTheme(panel, PanelTheme.Graphite, Path.Combine(directory, "panel-" + suffix + ".png"));
            SaveTheme(panel, PanelTheme.Black, Path.Combine(directory, "panel-black-" + suffix + ".png"));
            SaveTheme(panel, PanelTheme.Light, Path.Combine(directory, "panel-light-" + suffix + ".png"));
            panel.ShowChartDetails();
            SaveChart(panel, Path.Combine(directory, "chart-" + suffix + ".png"));
            panel.ShowChartRange(ChartRange.Days7);
            SaveChart(panel, Path.Combine(directory, "chart-week-" + suffix + ".png"));
            panel.ShowChartRange(ChartRange.Hours24);
            SaveChart(panel, Path.Combine(directory, "chart-day-" + suffix + ".png"));
            panel.ShowChartRange(ChartRange.Hours12);
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

    static void RenderToast(string path, string title, string body, MascotMood mood, MascotAction? action)
    {
        using (Toast toast = new Toast(delegate { }))
        using (Bitmap bitmap = new Bitmap(toast.Width, toast.Height))
        {
            toast.Prepare(title, body, mood, action);
            toast.DrawToBitmap(bitmap, new Rectangle(Point.Empty, toast.Size));
            bitmap.Save(path, ImageFormat.Png);
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
        List<UsageSample> samples = new List<UsageSample>();

        // Six earlier days of the week, a few readings per day, so the week chart has a shape.
        double[] week = { 4, 9, 15, 22, 28, 36, 44, 50, 57, 61 };
        for (int day = 6; day >= 1; day--)
            for (int part = 0; part < 4; part++)
            {
                UsageSample earlier = new UsageSample();
                earlier.Time = DateTime.Now.AddDays(-day).AddHours(part * 3);
                earlier.Session = 20 + (day * 7 + part * 13) % 60;
                earlier.Weekly = week[(6 - day) * 10 / 7 + part % 2] ;
                samples.Add(earlier);
            }

        // The last 12 hours, one reading every 15 minutes. Usage comes in bursts with pauses between them,
        // and one session ends five hours ago (28 readings in the first, 21 in the second).
        int[] rises =
        {
            2, 3, 4, 3, 0, 0, 0, 0, 1, 3, 4, 3, 0, 0, 0, 0, 0, 1, 3, 3, 2, 0, 0, 0, 2, 3, 2, 1,
            0, 1, 3, 6, 5, 0, 0, 0, 0, 2, 4, 6, 3, 0, 0, 0, 1, 3, 5, 2, 1
        };
        const int FirstSession = 28;
        double value = 0;
        for (int i = 0; i < rises.Length; i++)
        {
            if (i == FirstSession) value = 0;
            value += rises[i];
            UsageSample sample = new UsageSample();
            sample.Time = DateTime.Now.AddMinutes((i - (rises.Length - 1)) * 15);
            sample.Session = value;
            sample.Weekly = 55 + i * 12.0 / (rises.Length - 1);
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
