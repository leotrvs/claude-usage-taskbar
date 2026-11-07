using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

// Reads the limits from Claude Code's own /usage command. It makes no model call and spends no
// tokens, and it works without a terminal open, so the numbers keep updating between responses.
static class UsageProbe
{
    const string Arguments = "-p /usage --output-format text --no-session-persistence";
    const int TimeoutMs = 30 * 1000;
    const int OldestResetDays = 30; // The output has no year, so an older date belongs to next year.

    public static volatile bool ClaudeFound = true;

    static readonly Regex Percent = new Regex(@":\s*(\d+)%");
    static readonly Regex Reset = new Regex(
        @"resets\s+([a-z]{3})\s+(\d{1,2}),?\s+(?:at\s+)?(\d{1,2})(?::(\d{2}))?\s*(am|pm)", RegexOptions.IgnoreCase);

    // Runs on a worker thread. A failed read keeps the last saved numbers.
    public static void Run()
    {
        try
        {
            string text = ReadOutput();
            if (text == null) return;
            Dictionary<string, object> session = ParseWindow(text, "Current session", DateTime.Now);
            Dictionary<string, object> weekly = ParseWindow(text, "Current week (all models)", DateTime.Now);
            if (session != null) UsageStore.Save(session, weekly);
        }
        catch (Exception)
        {
            // Tried again on the next timer tick.
        }
    }

    static string ReadOutput()
    {
        ProcessStartInfo start = NewStartInfo();
        ClaudeFound = start != null;
        if (start == null) return null;
        start.UseShellExecute = false;
        start.CreateNoWindow = true;
        start.RedirectStandardOutput = true;
        start.RedirectStandardError = true;
        start.StandardOutputEncoding = Encoding.UTF8;
        start.WorkingDirectory = Path.GetTempPath(); // Outside any project, so none of its settings apply.

        using (Process process = Process.Start(start))
        {
            Task<string> output = process.StandardOutput.ReadToEndAsync();
            process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(TimeoutMs))
            {
                process.Kill();
                return null;
            }
            return process.ExitCode == 0 ? output.Result : null;
        }
    }

    static ProcessStartInfo NewStartInfo()
    {
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string installed = Path.Combine(home, @".local\bin\claude.exe");
        if (File.Exists(installed)) return new ProcessStartInfo(installed, Arguments);

        foreach (string folder in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(';'))
        {
            if (folder.Length == 0 || folder.IndexOfAny(Path.GetInvalidPathChars()) >= 0) continue;
            string exe = Path.Combine(folder, "claude.exe");
            if (File.Exists(exe)) return new ProcessStartInfo(exe, Arguments);
            string script = Path.Combine(folder, "claude.cmd"); // An npm install.
            if (File.Exists(script)) return new ProcessStartInfo("cmd.exe", "/c \"\"" + script + "\" " + Arguments + "\"");
        }
        return null;
    }

    // Same shape as the status line input. A line with no reset time means the window has not started.
    public static Dictionary<string, object> ParseWindow(string text, string label, DateTime now)
    {
        int start = text.IndexOf(label, StringComparison.Ordinal);
        if (start < 0) return null;
        int end = text.IndexOf('\n', start);
        string line = end < 0 ? text.Substring(start) : text.Substring(start, end - start);

        Match percent = Percent.Match(line);
        if (!percent.Success) return null;
        Dictionary<string, object> window = new Dictionary<string, object>();
        window["used_percentage"] = int.Parse(percent.Groups[1].Value, CultureInfo.InvariantCulture);
        DateTime? resetsAt = ParseReset(line, now);
        window["resets_at"] = resetsAt.HasValue ? (object)new DateTimeOffset(resetsAt.Value).ToUnixTimeSeconds() : null;
        return window;
    }

    static DateTime? ParseReset(string line, DateTime now)
    {
        Match match = Reset.Match(line);
        if (!match.Success) return null;

        int month = 1 + Array.FindIndex(CultureInfo.InvariantCulture.DateTimeFormat.AbbreviatedMonthNames,
            delegate(string name) { return name.Equals(match.Groups[1].Value, StringComparison.OrdinalIgnoreCase); });
        int day = int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
        int hour = int.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture) % 12;
        if (match.Groups[5].Value.Equals("pm", StringComparison.OrdinalIgnoreCase)) hour += 12;
        int minute = match.Groups[4].Success ? int.Parse(match.Groups[4].Value, CultureInfo.InvariantCulture) : 0;
        if (month == 0 || day < 1 || day > DateTime.DaysInMonth(now.Year, month) || minute > 59) return null;

        DateTime resetsAt = new DateTime(now.Year, month, day, hour, minute, 0, DateTimeKind.Local);
        return resetsAt < now.AddDays(-OldestResetDays) ? resetsAt.AddYears(1) : resetsAt;
    }
}
