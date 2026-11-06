# Contributing

## Setup

```powershell
git clone https://github.com/ggleoo/claude-usage-taskbar.git
cd claude-usage-taskbar
git config core.hooksPath .githooks
pwsh -File scripts/build.ps1
.\bin\ClaudeUsageTaskbar.exe --portable
```

The hooks stop commits and pushes that contain secrets or personal data. To add your own terms, such as your name, list them one per line in `.git\info\leak-patterns.txt`. That file is never committed.

## Rules

- **Never read Claude credentials or call Anthropic's private endpoints.** Usage data comes only from the official status line. Pull requests that change this will be declined.
- **Keep the app free of dependencies.** It must build with the compiler included in Windows, which supports C# 5.
- **Add every user-visible text in both English and Portuguese** in `src\Localization.cs`.
- **Do not use real screenshots.** Generate documentation images from sample data:

  ```powershell
  .\bin\ClaudeUsageTaskbar.exe --preview docs\images
  ```

## Project layout

| Path | Purpose |
|---|---|
| `src\StatusLineBridge.cs` | Receives Claude Code data and keeps only the limits |
| `src\ClaudeCodeConnection.cs` | Changes the `statusLine` entry |
| `src\UsagePanel.cs` | The panel |
| `src\TrayIcon.cs` | Taskbar icons |
| `src\PaceEstimator.cs` | Pace forecast |
| `src\Updater.cs` | Signed updates |
| `src\Installer.cs` | Install and uninstall |
| `scripts\` | Build, signing, release and leak checks |

Maintainers should see [docs/RELEASING.md](docs/RELEASING.md).
