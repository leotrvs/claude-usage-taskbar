using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks;

// Called by Claude Code as a status line command. Stdin carries session data such as paths and
// the model; only rate_limits is kept, so nothing else from the session ever reaches the disk.
static class StatusLineBridge
{
    const int MaxInputBytes = 1024 * 1024;
    const int PreviousCommandTimeoutMs = 5000;

    public static void Run()
    {
        string input = ReadInput();
        string defaultLine = "";
        try
        {
            Dictionary<string, object> root = Json.Object(Json.Parse(input));
            Dictionary<string, object> limits = Json.Object(Json.Get(root, "rate_limits"));
            if (limits != null)
            {
                object fiveHour = Json.Get(limits, "five_hour");
                object sevenDay = Json.Get(limits, "seven_day");
                if (fiveHour != null || sevenDay != null) UsageStore.Save(fiveHour, sevenDay);
                defaultLine = Describe("5h", fiveHour) + " " + Describe("7d", sevenDay);
            }
        }
        catch (Exception)
        {
            // A status line failure must never disturb the Claude Code session.
        }

        string previous = ClaudeCodeConnection.PreviousCommand();
        string line = previous == null ? defaultLine : RunPrevious(previous, input, defaultLine);
        byte[] bytes = new UTF8Encoding(false).GetBytes(line.Trim() + "\n");
        using (Stream output = Console.OpenStandardOutput())
            output.Write(bytes, 0, bytes.Length);
    }

    static string Describe(string label, object window)
    {
        object percent = Json.Get(Json.Object(window), "used_percentage");
        if (percent == null) return "";
        return label + " " + Math.Round(Convert.ToDouble(percent, CultureInfo.InvariantCulture)).ToString(CultureInfo.InvariantCulture) + "%";
    }

    static string ReadInput()
    {
        using (Stream stream = Console.OpenStandardInput())
        using (MemoryStream buffer = new MemoryStream())
        {
            byte[] chunk = new byte[8192];
            int read;
            while (buffer.Length < MaxInputBytes && (read = stream.Read(chunk, 0, chunk.Length)) > 0)
                buffer.Write(chunk, 0, read);
            return Encoding.UTF8.GetString(buffer.ToArray());
        }
    }

    // Keeps a status line the user already had: it gets the same input and its output is shown as before.
    static string RunPrevious(string command, string input, string fallback)
    {
        try
        {
            string bash = FindBash();
            ProcessStartInfo start = bash != null
                ? new ProcessStartInfo(bash, "-c \"" + command.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"")
                : new ProcessStartInfo("cmd.exe", "/s /c \"" + command + "\"");
            start.UseShellExecute = false;
            start.CreateNoWindow = true;
            start.RedirectStandardInput = true;
            start.RedirectStandardOutput = true;
            start.StandardOutputEncoding = Encoding.UTF8;

            using (Process process = Process.Start(start))
            {
                Task<string> output = process.StandardOutput.ReadToEndAsync();
                byte[] bytes = new UTF8Encoding(false).GetBytes(input);
                process.StandardInput.BaseStream.Write(bytes, 0, bytes.Length);
                process.StandardInput.Close();
                if (!output.Wait(PreviousCommandTimeoutMs))
                {
                    process.Kill();
                    return fallback;
                }
                return output.Result;
            }
        }
        catch (Exception)
        {
            return fallback;
        }
    }

    static string FindBash()
    {
        string configured = Environment.GetEnvironmentVariable("CLAUDE_CODE_GIT_BASH_PATH");
        if (!string.IsNullOrEmpty(configured) && File.Exists(configured)) return configured;
        string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        string standard = Path.Combine(programFiles, @"Git\bin\bash.exe");
        return File.Exists(standard) ? standard : null;
    }
}
