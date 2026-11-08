using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

// Frames for the README animations, turned into GIFs by scripts\make-gifs.py. One GIF shows one feature.
static partial class Preview
{
    class Cell
    {
        public MascotStyle Style;
        public MascotMood Mood;
        public MascotAction? Action;
        public double Start;

        public Cell(MascotStyle style, MascotMood mood, MascotAction? action, double start)
        {
            Style = style;
            Mood = mood;
            Action = action;
            Start = start;
        }
    }

    class Walker
    {
        public MascotStyle Style;
        public bool Panic;
        public bool Special;
        public int Steps;

        public Walker(MascotStyle style, bool panic, bool special, int steps)
        {
            Style = style;
            Panic = panic;
            Special = special;
            Steps = steps;
        }
    }

    public static void RenderGifFrames(string directory)
    {
        Theme.Colors = ColorTemplate.Blue;
        Theme.Panel = PanelTheme.Black;
        string frames = directory;
        MascotStyle plain = MascotStyle.Pixel;

        MascotGrid(Path.Combine(frames, "mascot-moods"), 5, 40, 100, new[]
        {
            new Cell(plain, MascotMood.Sleep, null, 0), new Cell(plain, MascotMood.Idle, null, 0), new Cell(plain, MascotMood.Work, null, 0),
            new Cell(plain, MascotMood.Tired, null, 0), new Cell(plain, MascotMood.Down, null, 0), new Cell(plain, MascotMood.Night, null, 0),
            new Cell(plain, MascotMood.Waiting, null, 0), new Cell(plain, MascotMood.Panic, null, 0), new Cell(plain, MascotMood.Coffee, null, 0),
            new Cell(plain, MascotMood.Juggle, null, 0)
        });

        MascotGrid(Path.Combine(frames, "mascot-working"), 5, 36, 110, new[]
        {
            new Cell(plain, MascotMood.Work, null, 0), new Cell(plain, MascotMood.Work, null, 14000),
            new Cell(plain, MascotMood.Work, null, 14000 * 3), new Cell(plain, MascotMood.Work, null, 14000 * 5),
            new Cell(plain, MascotMood.Work, null, 14000 * 7)
        });

        MascotGrid(Path.Combine(frames, "mascot-moves"), 4, 32, 60, new[]
        {
            new Cell(plain, MascotMood.Idle, MascotAction.Hop, 0), new Cell(plain, MascotMood.Idle, MascotAction.Rear, 0),
            new Cell(plain, MascotMood.Idle, MascotAction.Celebrate, 0), new Cell(plain, MascotMood.Lift, null, 0),
            new Cell(plain, MascotMood.Flat, null, 0), new Cell(MascotStyle.Santa, MascotMood.Idle, MascotAction.Hop, 0),
            new Cell(MascotStyle.Chef, MascotMood.Idle, MascotAction.Celebrate, 0)
        });

        MascotGrid(Path.Combine(frames, "mascot-outfits"), 3, 40, 100, new[]
        {
            new Cell(MascotStyle.Santa, MascotMood.Idle, null, 0), new Cell(MascotStyle.Headphones, MascotMood.Idle, null, 0),
            new Cell(MascotStyle.Chef, MascotMood.Idle, null, 0), new Cell(MascotStyle.Scientist, MascotMood.Idle, null, 0),
            new Cell(MascotStyle.Detective, MascotMood.Idle, null, 0), new Cell(MascotStyle.Winter, MascotMood.Idle, null, 0),
            new Cell(MascotStyle.Racer, MascotMood.Idle, null, 0), new Cell(MascotStyle.Halloween, MascotMood.Idle, null, 0),
            new Cell(MascotStyle.Carnival, MascotMood.Idle, null, 0)
        });

        MascotGrid(Path.Combine(frames, "mascot-reactions"), 4, 40, 100, new[]
        {
            new Cell(MascotStyle.Scientist, MascotMood.Work, null, 0), new Cell(MascotStyle.Scientist, MascotMood.Panic, null, 0),
            new Cell(MascotStyle.Detective, MascotMood.Waiting, null, 0), new Cell(MascotStyle.Winter, MascotMood.Idle, null, 0),
            new Cell(MascotStyle.Racer, MascotMood.Panic, null, 0), new Cell(MascotStyle.Racer, MascotMood.Idle, MascotAction.Celebrate, 0),
            new Cell(plain, MascotMood.Idle, MascotAction.Love, 0), new Cell(MascotStyle.Scientist, MascotMood.Idle, MascotAction.Celebrate, 0),
            new Cell(MascotStyle.Scientist, MascotMood.Sleep, null, 0), new Cell(MascotStyle.Detective, MascotMood.Sleep, null, 0),
            new Cell(MascotStyle.Detective, MascotMood.Idle, MascotAction.Celebrate, 0), new Cell(MascotStyle.Winter, MascotMood.Sleep, null, 0),
            new Cell(MascotStyle.Winter, MascotMood.Panic, null, 0), new Cell(MascotStyle.Racer, MascotMood.Waiting, null, 0),
            new Cell(MascotStyle.Carnival, MascotMood.Sleep, null, 0), new Cell(MascotStyle.Halloween, MascotMood.Sleep, null, 0)
        });

        WalkStack(Path.Combine(frames, "mascot-walks"), new[]
        {
            new Walker(MascotStyle.Detective, false, false, 68), new Walker(plain, true, false, 18), new Walker(MascotStyle.Racer, false, false, 48)
        });
        WalkStack(Path.Combine(frames, "mascot-acts-1"), new[]
        {
            new Walker(MascotStyle.Santa, false, true, 36), new Walker(plain, false, true, 72),
            new Walker(MascotStyle.Headphones, false, true, 72), new Walker(MascotStyle.Chef, false, true, 72)
        });
        WalkStack(Path.Combine(frames, "mascot-acts-2"), new[]
        {
            new Walker(MascotStyle.Scientist, false, true, 72), new Walker(MascotStyle.Winter, false, true, 47),
            new Walker(MascotStyle.Halloween, false, true, 47), new Walker(MascotStyle.Carnival, false, true, 72)
        });

        RenderIconFrames(Path.Combine(frames, "icon-styles"));
        RenderLanguageFrames(frames, Language.English, "en");
        RenderLanguageFrames(frames, Language.Portuguese, "pt-BR");
    }

    static void SaveFrame(Bitmap bitmap, string folder, int index)
    {
        Directory.CreateDirectory(folder);
        bitmap.Save(Path.Combine(folder, index.ToString("000") + ".png"), ImageFormat.Png);
    }

    // Several mascots at once, each playing its own animation from the given start time.
    static void MascotGrid(string folder, int columns, int frames, int stepMs, Cell[] cells)
    {
        const int Pixel = 4;
        const int Width = 128;
        const int Height = 96;
        const int Gap = 12;
        int rows = (cells.Length + columns - 1) / columns;
        for (int f = 0; f < frames; f++)
            using (Bitmap sheet = new Bitmap(columns * (Width + Gap) + Gap, rows * (Height + Gap) + Gap))
            using (Graphics g = Graphics.FromImage(sheet))
            {
                g.Clear(Color.FromArgb(10, 10, 10));
                for (int i = 0; i < cells.Length; i++)
                    using (Bitmap frame = PixelMascot.RenderAt(cells[i].Style, cells[i].Mood, Pixel, cells[i].Action, cells[i].Start + f * stepMs))
                        g.DrawImageUnscaled(frame, Gap + i % columns * (Width + Gap), Gap + i / columns * (Height + Gap));
                SaveFrame(sheet, folder, f);
            }
    }

    static UsagePanel WalkPanel(Walker walker)
    {
        Localization.Apply(Language.English);
        PanelState state = new PanelState();
        state.Stale = false;
        state.Status = Localization.T("updatedAt", "14:32");
        state.Sessions.Running = 1;
        state.Session.Percent = walker.Panic ? 91 : 42;
        state.Weekly.Percent = 67;
        if (walker.Panic) state.MinutesToLimit = 12;
        state.History = new List<UsageSample>();
        UsagePanel panel = new UsagePanel(new PanelCallbacks());
        panel.ChangeMascot(walker.Style);
        panel.SetState(state, IconStyle.Ring, Language.English, IconsShown.Both);
        panel.StartMascotWalk(walker.Special);
        return panel;
    }

    // The panel headers of several outfits stacked, each doing its own walk.
    static void WalkStack(string folder, Walker[] walkers)
    {
        const int HeaderHeight = 64;
        const int Gap = 8;
        UsagePanel[] panels = new UsagePanel[walkers.Length];
        int frames = 0;
        try
        {
            for (int i = 0; i < walkers.Length; i++)
            {
                panels[i] = WalkPanel(walkers[i]);
                frames = Math.Max(frames, walkers[i].Steps);
            }
            int width = panels[0].Width;
            for (int f = 0; f < frames; f++)
            {
                using (Bitmap sheet = new Bitmap(width + Gap * 2, HeaderHeight * walkers.Length + Gap * (walkers.Length + 1)))
                using (Graphics g = Graphics.FromImage(sheet))
                {
                    g.Clear(Color.FromArgb(10, 10, 10));
                    for (int i = 0; i < panels.Length; i++)
                    {
                        if (f < walkers[i].Steps) panels[i].StepMascot();
                        using (Bitmap shot = new Bitmap(width, panels[i].Height))
                        {
                            panels[i].DrawToBitmap(shot, new Rectangle(Point.Empty, panels[i].Size));
                            g.DrawImage(shot, new Rectangle(Gap, Gap + i * (HeaderHeight + Gap), width, HeaderHeight),
                                new Rectangle(0, 0, width, HeaderHeight), GraphicsUnit.Pixel);
                        }
                    }
                    SaveFrame(sheet, folder, f);
                }
            }
        }
        finally
        {
            foreach (UsagePanel panel in panels) if (panel != null) panel.Dispose();
        }
    }

    static void RenderIconFrames(string folder)
    {
        const int Size = 64;
        const int Gap = 12;
        const int Frames = 24;
        ColorTemplate[] colors = { ColorTemplate.Blue, ColorTemplate.Green, ColorTemplate.Purple };
        IconStyle[] styles = { IconStyle.Gauge, IconStyle.Ring, IconStyle.Needle };
        for (int f = 0; f < Frames; f++)
            using (Bitmap sheet = new Bitmap(colors.Length * (Size + Gap) + Gap, styles.Length * (Size + Gap) + Gap))
            using (Graphics g = Graphics.FromImage(sheet))
            {
                g.Clear(Color.FromArgb(10, 10, 10));
                int percent = Math.Min(65, f * 3);
                for (int c = 0; c < colors.Length; c++)
                {
                    Theme.Colors = colors[c];
                    for (int i = 0; i < styles.Length; i++)
                        DrawIcon(g, styles[i], percent, Gap + c * (Size + Gap), Gap + i * (Size + Gap));
                }
                SaveFrame(sheet, folder, f);
            }
        Theme.Colors = ColorTemplate.Blue;
    }

    static PanelState SampleState(Language language)
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
        return state;
    }

    static Bitmap Shot(UsagePanel panel)
    {
        Bitmap bitmap = new Bitmap(panel.Width, panel.Height);
        panel.DrawToBitmap(bitmap, new Rectangle(Point.Empty, panel.Size));
        return bitmap;
    }

    // The chart sweeping over each range, the colors and themes, and the pages of the panel.
    static void RenderLanguageFrames(string directory, Language language, string suffix)
    {
        PanelState state = SampleState(language);
        using (UsagePanel panel = new UsagePanel(new PanelCallbacks()))
        {
            panel.ChangeMascot(MascotStyle.Classic);
            panel.SetState(state, IconStyle.Ring, language, IconsShown.Both);

            string chart = Path.Combine(Path.Combine(directory, "chart"), suffix);
            ChartRange[] ranges = { ChartRange.Hours12, ChartRange.Hours24, ChartRange.Days7 };
            const int ChartGap = 8;
            const int ChartSteps = 22;
            int unit = panel.Width / 300;
            for (int step = 0; step < ChartSteps; step++)
                using (Bitmap sheet = new Bitmap(ranges.Length * (panel.Width + ChartGap) + ChartGap, 66 * unit))
                using (Graphics g = Graphics.FromImage(sheet))
                {
                    g.Clear(Color.FromArgb(10, 10, 10));
                    for (int r = 0; r < ranges.Length; r++)
                    {
                        panel.ShowChartRange(ranges[r]);
                        panel.ShowChartHover(0.04 + 0.92 * step / (ChartSteps - 1.0));
                        using (Bitmap bitmap = Shot(panel))
                            g.DrawImage(bitmap, new Rectangle(ChartGap + r * (panel.Width + ChartGap), 0, panel.Width, 66 * unit),
                                new Rectangle(0, 197 * unit, panel.Width, 66 * unit), GraphicsUnit.Pixel);
                    }
                    SaveFrame(sheet, chart, step);
                }
            panel.ShowChartRange(ChartRange.Hours12);

            string themes = Path.Combine(Path.Combine(directory, "themes"), suffix);
            ColorTemplate[] colors = { ColorTemplate.Blue, ColorTemplate.Green, ColorTemplate.Purple };
            PanelTheme[] looks = { PanelTheme.Black, PanelTheme.Graphite, PanelTheme.Light };
            const int ThemeGap = 16;
            for (int c = 0; c < colors.Length; c++)
                using (Bitmap sheet = new Bitmap(looks.Length * (panel.Width + ThemeGap) + ThemeGap, panel.Height + ThemeGap * 2))
                using (Graphics g = Graphics.FromImage(sheet))
                {
                    g.Clear(Color.FromArgb(16, 16, 16));
                    for (int look = 0; look < looks.Length; look++)
                    {
                        Theme.Colors = colors[c];
                        Theme.Panel = looks[look];
                        panel.ApplyTheme();
                        using (Bitmap bitmap = Shot(panel))
                            g.DrawImageUnscaled(bitmap, ThemeGap + look * (panel.Width + ThemeGap), ThemeGap);
                    }
                    SaveFrame(sheet, themes, c);
                }
            Theme.Colors = ColorTemplate.Blue;
            Theme.Panel = PanelTheme.Black;
            panel.ApplyTheme();

            string pages = Path.Combine(Path.Combine(directory, "settings"), suffix);
            panel.ShowMainPage();
            using (Bitmap bitmap = Shot(panel)) SaveFrame(bitmap, pages, 0);
            panel.ShowSettingsPage();
            using (Bitmap bitmap = Shot(panel)) SaveFrame(bitmap, pages, 1);
            panel.ShowAppearancePage();
            using (Bitmap bitmap = Shot(panel)) SaveFrame(bitmap, pages, 2);
            panel.ShowMainPage();
        }
    }
}
