using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

// Adds or removes this app's status line in Claude Code's user settings. Nothing else in that file is changed.
static class ClaudeCodeConnection
{
    const string StatusLineKey = "statusLine";
    const string CommandKey = "command";
    const string Flag = "--statusline";

    public static string ConfigDirectory
    {
        get
        {
            string configured = Environment.GetEnvironmentVariable("CLAUDE_CONFIG_DIR");
            return !string.IsNullOrEmpty(configured)
                ? configured
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude");
        }
    }

    static string SettingsPath
    {
        get { return Path.Combine(ConfigDirectory, "settings.json"); }
    }

    static string PreviousPath
    {
        get { return Path.Combine(App.DataDir, "previous-statusline.json"); }
    }

    public static bool IsConnected
    {
        get
        {
            try { return IsOurs(Json.Get(ReadSettings(), StatusLineKey)); }
            catch (Exception) { return false; }
        }
    }

    public static bool Connect(string exePath)
    {
        try
        {
            Dictionary<string, object> settings = ReadSettings();
            object current = Json.Get(settings, StatusLineKey);
            // Forward slashes work in both shells Claude Code may use on Windows.
            string command = "\"" + exePath.Replace('\\', '/') + "\" " + Flag;
            if (command.Equals(Json.Get(Json.Object(current), CommandKey))) return true;

            if (Json.Object(current) != null && !IsOurs(current))
            {
                Directory.CreateDirectory(App.DataDir);
                File.WriteAllText(PreviousPath, Json.Format(current), new UTF8Encoding(false));
            }

            Dictionary<string, object> statusLine = new Dictionary<string, object>();
            statusLine["type"] = "command";
            statusLine[CommandKey] = command;
            settings[StatusLineKey] = statusLine;
            WriteSettings(settings);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public static bool Disconnect()
    {
        try
        {
            Dictionary<string, object> settings = ReadSettings();
            if (!IsOurs(Json.Get(settings, StatusLineKey))) return true;

            if (File.Exists(PreviousPath))
                settings[StatusLineKey] = Json.Parse(File.ReadAllText(PreviousPath, Encoding.UTF8));
            else
                settings.Remove(StatusLineKey);

            WriteSettings(settings);
            if (File.Exists(PreviousPath)) File.Delete(PreviousPath);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public static string PreviousCommand()
    {
        try
        {
            if (!File.Exists(PreviousPath)) return null;
            Dictionary<string, object> previous = Json.Object(Json.Parse(File.ReadAllText(PreviousPath, Encoding.UTF8)));
            return Json.Get(previous, CommandKey) as string;
        }
        catch (Exception)
        {
            return null;
        }
    }

    static bool IsOurs(object statusLine)
    {
        string command = Json.Get(Json.Object(statusLine), CommandKey) as string;
        return command != null
            && command.IndexOf(App.ExeName, StringComparison.OrdinalIgnoreCase) >= 0
            && command.Contains(Flag);
    }

    // Invalid JSON throws, so a file the user is editing is never overwritten.
    static Dictionary<string, object> ReadSettings()
    {
        if (!File.Exists(SettingsPath)) return new Dictionary<string, object>();
        string text = File.ReadAllText(SettingsPath, Encoding.UTF8);
        if (text.Trim().Length == 0) return new Dictionary<string, object>();
        Dictionary<string, object> settings = Json.Object(Json.Parse(text));
        if (settings == null) throw new InvalidDataException();
        return settings;
    }

    static void WriteSettings(Dictionary<string, object> settings)
    {
        string path = SettingsPath;
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        string temporary = path + "." + App.Id + ".tmp";
        File.WriteAllText(temporary, Json.Format(settings), new UTF8Encoding(false));
        if (File.Exists(path))
        {
            File.Copy(path, path + "." + App.Id + ".bak", true);
            File.Delete(path);
        }
        File.Move(temporary, path);
    }
}
