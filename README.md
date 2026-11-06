# Claude Usage Taskbar

**English** | [Português (Brasil)](README.pt-BR.md)

See your Claude Code usage limits in the Windows taskbar, without opening anything.

![The usage panel](docs/images/panel-en.png)

> Unofficial community project. It is not affiliated with, endorsed by, or supported by Anthropic. "Claude" is a trademark of Anthropic.

## Features

- Taskbar icons with your **5-hour** and **weekly** usage percentages. Show both, or only one
- Two icon styles: **Gauge** and **Outline**, which fills up as you use your limits
- Colors by level: green, then amber at 70%, then red at 90%
- A dark panel with reset times, a 12-hour chart and a pace forecast
- Optional alerts at 80% and 90% of a limit
- English and Brazilian Portuguese, following your Windows language by default
- Starts with Windows, with Desktop and Start menu shortcuts
- Signed automatic updates
- **Never reads your Claude login or token**

![Icon styles](docs/images/icon-styles.png)

## Requirements

- Windows 10 or 11
- [Claude Code](https://code.claude.com/docs) signed in with a Pro, Max or Ultra plan

## Install

1. If you have not already, install Claude Code, run `claude` in a terminal and sign in.
2. Download `ClaudeUsageTaskbar.exe` from the [latest release](https://github.com/ggleoo/claude-usage-taskbar/releases/latest).
3. Run it. Windows may show a SmartScreen warning because the app has no paid code-signing certificate. Choose **More info**, then **Run anyway**. If you prefer, [check the file](SECURITY.md#verify-a-download) or [build it yourself](#build-from-source) first.
4. Choose **Yes** to install. The app is installed for your Windows user only, and you can delete the downloaded file afterwards.
5. Keep the icons visible: drag them from the **^** menu to the taskbar, or go to **Settings > Personalization > Taskbar > Other system tray icons**.
6. Send one message in Claude Code. The percentages appear right after the response.

The installer connects to Claude Code for you. See [how the connection works](#how-the-connection-works).

## Use

- **Hover** over an icon to see the percentage and reset time.
- **Click** an icon to open the panel. Choose the icon style there.
- **Settings** in the panel holds the language, which icons appear, automatic updates, usage alerts, Start with Windows and the Claude Code connection.
- **Close widget** only closes this app. It does not sign you out of Claude. Open it again from the Start menu or Desktop.

On icons, the 5-hour limit has a white number and the weekly limit has a colored number.

### Pace forecast

- **5-hour limit:** the forecast blends your average for the whole window with your recent trend, so one burst does not dominate it. It appears after at least 5% usage and 15 minutes of data.
- **Weekly limit:** it compares your usage with an even pace across the week. It shows a difference, not a prediction.

### Custom mascot

To show an animated image in the panel, save a GIF as `%LOCALAPPDATA%\ClaudeUsageTaskbar\mascot.gif` and reopen the panel. Gray and black pixels are treated as background and removed. No mascot is included with the app.

## How the connection works

Claude Code can send session data to a [status line](https://code.claude.com/docs/en/statusline) command, and that data includes your limit percentages. The app uses this official feature:

1. During install, it adds one `statusLine` entry to `~/.claude/settings.json`, and saves a backup of the file next to it.
2. Claude Code runs the app in the background and sends it the session data.
3. The app keeps only the 5-hour and weekly percentages and reset times in `%LOCALAPPDATA%\ClaudeUsageTaskbar`, and discards the rest.
4. The widget reads that file.

Good to know:

- **Percentages update only while Claude Code is running.** They refresh after each response, so usage from other places, such as claude.ai, appears on your next Claude Code response.
- **An existing status line is kept.** It keeps running and showing what it did before. Uninstalling restores it.
- **To disconnect,** turn off **Connected to Claude Code** in Settings.

## Security and privacy

- It does not read your login, token or credentials file.
- It does not store your prompts, session data or project paths.
- It has no analytics or telemetry, and no account.
- Its only network access is checking GitHub for signed updates, which you can turn off.

Details are in [SECURITY.md](SECURITY.md).

## Updates

Every 6 hours, the app checks the latest GitHub release. It installs an update only if its digital signature matches the key built into the app, so a tampered file is rejected. You can turn this off in Settings.

## Uninstall

Go to **Windows Settings > Apps > Installed apps > Claude Usage Taskbar > Uninstall**. This removes the app, shortcuts and settings, and restores your Claude Code status line.

## Troubleshooting

| Problem | What to do |
|---|---|
| "Waiting for Claude Code" | Send a message in Claude Code. Limit data arrives after the first response. |
| Still no percentages | Check that **Connected to Claude Code** is on, then restart Claude Code. Data is available on Pro, Max and Ultra plans. |
| Icons are hidden | Follow step 5 of [Install](#install). |
| Connection could not be turned on | `~/.claude/settings.json` may have invalid JSON. Fix it and try again. |
| A percentage looks old | It is the last value Claude Code reported. It refreshes on the next response. |

## Build from source

No SDK is needed, because Windows includes the C# compiler.

```powershell
git clone https://github.com/ggleoo/claude-usage-taskbar.git
cd claude-usage-taskbar
pwsh -File scripts/build.ps1
```

The app is written to `bin\ClaudeUsageTaskbar.exe`. Use `--portable` to run it without installing.

Contributors should read [CONTRIBUTING.md](CONTRIBUTING.md).

## License

[MIT](LICENSE)
