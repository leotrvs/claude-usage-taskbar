using System;
using Microsoft.Win32;

enum IconStyle { Gauge, Ring, Needle }
enum IconsShown { Both, Session, Weekly }

static class Settings
{
    public const string KeyPath = @"Software\" + App.Id;
    const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    const string PersonalizeKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

    public static IconStyle IconStyle
    {
        get
        {
            string value = Read("IconStyle") as string;
            if ("Gauge".Equals(value)) return IconStyle.Gauge;
            return "Needle".Equals(value) ? IconStyle.Needle : IconStyle.Ring;
        }
        set { Write("IconStyle", value.ToString()); }
    }

    public static Language Language
    {
        get
        {
            Language language;
            return Enum.TryParse(Read("Language") as string, out language) ? language : Language.Auto;
        }
        set { Write("Language", value.ToString()); }
    }

    public static IconsShown IconsShown
    {
        get
        {
            IconsShown shown;
            return Enum.TryParse(Read("IconsShown") as string, out shown) ? shown : IconsShown.Both;
        }
        set { Write("IconsShown", value.ToString()); }
    }

    public static ColorTemplate Colors
    {
        get
        {
            ColorTemplate colors;
            return Enum.TryParse(Read("Colors") as string, out colors) ? colors : ColorTemplate.Blue;
        }
        set { Write("Colors", value.ToString()); }
    }

    public static PanelTheme PanelTheme
    {
        get
        {
            PanelTheme theme;
            return Enum.TryParse(Read("PanelTheme") as string, out theme) ? theme : PanelTheme.Graphite;
        }
        set { Write("PanelTheme", value.ToString()); }
    }

    public static MascotStyle MascotStyle
    {
        get
        {
            MascotStyle style;
            return Enum.TryParse(Read("MascotStyle") as string, out style) ? style : MascotStyle.Classic;
        }
        set { Write("MascotStyle", value.ToString()); }
    }

    // The icons have no background, so they follow the color of the taskbar behind them.
    public static bool LightTaskbar
    {
        get
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(PersonalizeKeyPath))
                return key != null && 1.Equals(key.GetValue("SystemUsesLightTheme"));
        }
    }

    public static bool Notifications
    {
        get { return !"False".Equals(Read("Notifications")); }
        set { Write("Notifications", value.ToString()); }
    }

    // Remembers the alert already shown for a limit period, so a restart does not repeat it.
    public static string AlertState(string limit)
    {
        return Read("Alert" + limit) as string;
    }

    public static void SetAlertState(string limit, string value)
    {
        Write("Alert" + limit, value);
    }

    public static bool AutoUpdate
    {
        get { return !"False".Equals(Read("AutoUpdate")); }
        set { Write("AutoUpdate", value.ToString()); }
    }

    public static bool StartWithWindows
    {
        get
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKeyPath))
                return key != null && key.GetValue(App.Id) != null;
        }
        set
        {
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RunKeyPath))
            {
                if (value) key.SetValue(App.Id, "\"" + App.InstalledExe + "\" --startup");
                else key.DeleteValue(App.Id, false);
            }
        }
    }

    static object Read(string name)
    {
        using (RegistryKey key = Registry.CurrentUser.OpenSubKey(KeyPath))
            return key == null ? null : key.GetValue(name);
    }

    static void Write(string name, string value)
    {
        using (RegistryKey key = Registry.CurrentUser.CreateSubKey(KeyPath))
            key.SetValue(name, value);
    }
}
