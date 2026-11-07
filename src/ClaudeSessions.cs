using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;

class SessionActivity
{
    public int Running;
    public int Busy;
}

// Claude Code keeps one small file per running session. Only its status is read, never its folder or name.
static class ClaudeSessions
{
    public static SessionActivity Read()
    {
        SessionActivity activity = new SessionActivity();
        string folder = Path.Combine(ClaudeCodeConnection.ConfigDirectory, "sessions");
        if (!Directory.Exists(folder)) return activity;

        foreach (string file in Directory.GetFiles(folder, "*.json"))
        {
            try { Count(file, activity); }
            catch (Exception)
            {
                // Files are rewritten while sessions run, so an unreadable one is skipped this time.
            }
        }
        return activity;
    }

    static void Count(string file, SessionActivity activity)
    {
        string text;
        using (FileStream stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
        using (StreamReader reader = new StreamReader(stream))
            text = reader.ReadToEnd();

        Dictionary<string, object> session = Json.Object(Json.Parse(text));
        if (!"interactive".Equals(Json.Get(session, "kind"))) return; // The app's own /usage reads are not sessions.
        if (!IsRunning(Convert.ToInt32(Json.Get(session, "pid"), CultureInfo.InvariantCulture))) return; // Files outlive their process.

        activity.Running++;
        if ("busy".Equals(Json.Get(session, "status"))) activity.Busy++;
    }

    static bool IsRunning(int processId)
    {
        try
        {
            using (Process.GetProcessById(processId)) return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}
