using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

class UsageWindow
{
    public int? Percent;
    public DateTime? ResetsAt;
}

class UsageSample
{
    public DateTime Time;
    public double Session;
    public double? Weekly; // Older samples were saved without it.
}

class UsageSnapshot
{
    public UsageWindow Session = new UsageWindow();
    public UsageWindow Weekly = new UsageWindow();
    public DateTime UpdatedAt;
}

// Local hand-off between the status line bridge and the widget. It only ever holds percentages and reset times.
static class UsageStore
{
    static readonly TimeSpan HistoryLength = TimeSpan.FromDays(7);
    static readonly TimeSpan MinimumSampleGap = TimeSpan.FromMinutes(1);

    static string HistoryPath
    {
        get { return Path.Combine(App.DataDir, "history.csv"); }
    }

    static string FilePath
    {
        get { return Path.Combine(App.DataDir, "usage.json"); }
    }

    public static DateTime LastWriteUtc
    {
        get { return File.Exists(FilePath) ? File.GetLastWriteTimeUtc(FilePath) : DateTime.MinValue; }
    }

    public static void Save(object fiveHour, object sevenDay)
    {
        Dictionary<string, object> data = new Dictionary<string, object>();
        data["five_hour"] = OnlyLimitFields(fiveHour);
        data["seven_day"] = OnlyLimitFields(sevenDay);

        Directory.CreateDirectory(App.DataDir);
        string temporary = FilePath + "." + System.Diagnostics.Process.GetCurrentProcess().Id + ".tmp";
        File.WriteAllText(temporary, Json.Format(data), new UTF8Encoding(false));
        File.Copy(temporary, FilePath, true);
        File.Delete(temporary);

        object percent = Json.Get(Json.Object(fiveHour), "used_percentage");
        object weekly = Json.Get(Json.Object(sevenDay), "used_percentage");
        if (percent != null)
            AppendHistory(Convert.ToDouble(percent, CultureInfo.InvariantCulture),
                weekly == null ? (double?)null : Convert.ToDouble(weekly, CultureInfo.InvariantCulture));
    }

    public static List<UsageSample> LoadHistory()
    {
        List<UsageSample> samples = new List<UsageSample>();
        if (!File.Exists(HistoryPath)) return samples;
        DateTime oldest = DateTime.Now - HistoryLength;
        foreach (string line in ReadHistoryLines())
        {
            string[] parts = line.Split(',');
            long seconds;
            double session;
            double weeklyValue;
            if (parts.Length != 2 && parts.Length != 3) continue;
            if (!long.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out seconds)) continue;
            if (!double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out session)) continue;
            DateTime time = DateTimeOffset.FromUnixTimeSeconds(seconds).LocalDateTime;
            if (time < oldest) continue;
            UsageSample sample = new UsageSample();
            sample.Time = time;
            sample.Session = session;
            if (parts.Length == 3 && double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out weeklyValue))
                sample.Weekly = weeklyValue;
            samples.Add(sample);
        }
        return samples;
    }

    static string[] ReadHistoryLines()
    {
        try { return File.ReadAllLines(HistoryPath); }
        catch (IOException) { return new string[0]; }
    }

    // One line per minute at most: the time and the 5-hour and weekly percentages, kept for a week.
    static void AppendHistory(double session, double? weekly)
    {
        List<UsageSample> samples = LoadHistory();
        if (samples.Count > 0 && DateTime.Now - samples[samples.Count - 1].Time < MinimumSampleGap) return;

        StringBuilder text = new StringBuilder();
        foreach (UsageSample sample in samples) AppendHistoryLine(text, sample.Time, sample.Session, sample.Weekly);
        AppendHistoryLine(text, DateTime.Now, session, weekly);
        File.WriteAllText(HistoryPath, text.ToString(), new UTF8Encoding(false));
    }

    static void AppendHistoryLine(StringBuilder text, DateTime time, double session, double? weekly)
    {
        text.Append(new DateTimeOffset(time).ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture));
        text.Append(',').Append(session.ToString(CultureInfo.InvariantCulture));
        if (weekly.HasValue) text.Append(',').Append(weekly.Value.ToString(CultureInfo.InvariantCulture));
        text.Append('\n');
    }

    public static UsageSnapshot Load()
    {
        if (!File.Exists(FilePath)) return null;
        try
        {
            string text;
            using (FileStream stream = new FileStream(FilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
                text = reader.ReadToEnd();

            Dictionary<string, object> root = Json.Object(Json.Parse(text));
            UsageSnapshot snapshot = new UsageSnapshot();
            snapshot.Session = ParseWindow(Json.Object(Json.Get(root, "five_hour")));
            snapshot.Weekly = ParseWindow(Json.Object(Json.Get(root, "seven_day")));
            snapshot.UpdatedAt = File.GetLastWriteTime(FilePath);
            return snapshot;
        }
        catch (Exception)
        {
            return null; // A partial write is retried on the next tick.
        }
    }

    static Dictionary<string, object> OnlyLimitFields(object window)
    {
        Dictionary<string, object> source = Json.Object(window);
        if (source == null) return null;
        Dictionary<string, object> kept = new Dictionary<string, object>();
        kept["used_percentage"] = Json.Get(source, "used_percentage");
        kept["resets_at"] = Json.Get(source, "resets_at");
        return kept;
    }

    static UsageWindow ParseWindow(Dictionary<string, object> data)
    {
        UsageWindow window = new UsageWindow();
        object percent = Json.Get(data, "used_percentage");
        object resetsAt = Json.Get(data, "resets_at");
        if (percent != null) window.Percent = (int)Math.Round(Convert.ToDouble(percent, CultureInfo.InvariantCulture));
        if (resetsAt != null)
            window.ResetsAt = DateTimeOffset.FromUnixTimeSeconds(Convert.ToInt64(resetsAt, CultureInfo.InvariantCulture)).LocalDateTime;

        // Claude Code only reports again after its next response, so a passed window is shown as reset.
        if (window.ResetsAt.HasValue && window.ResetsAt.Value <= DateTime.Now)
        {
            window.Percent = 0;
            window.ResetsAt = null;
        }
        return window;
    }
}
