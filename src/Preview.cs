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
        Theme.Panel = PanelTheme.Graphite;
        RenderPanels(directory, Language.English, "en");
        RenderPanels(directory, Language.Portuguese, "pt-BR");
        RenderIconStyles(Path.Combine(directory, "icon-styles.png"));
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
            panel.LoadMascot();
            panel.SetState(state, IconStyle.Ring, language, IconsShown.Both);
            Save(panel, Path.Combine(directory, "panel-" + suffix + ".png"));
            panel.ShowSettingsPage();
            Save(panel, Path.Combine(directory, "settings-" + suffix + ".png"));
            panel.ShowAppearancePage();
            Save(panel, Path.Combine(directory, "appearance-" + suffix + ".png"));
            panel.SetUpdate(UpdatePhase.Available, "1.1.0");
            Save(panel, Path.Combine(directory, "update-settings-" + suffix + ".png"));
            panel.ShowMainPage();
            Save(panel, Path.Combine(directory, "update-" + suffix + ".png"));
            Theme.Panel = PanelTheme.Black;
            panel.ApplyTheme();
            Save(panel, Path.Combine(directory, "black-" + suffix + ".png"));
            Theme.Panel = PanelTheme.Graphite;
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
            g.Clear(Color.FromArgb(20, 20, 20));
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
