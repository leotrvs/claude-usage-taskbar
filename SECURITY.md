# Security

**English** | [Português (Brasil)](docs/SECURITY.pt-BR.md)

## What the app touches

| Item | Access | Why |
|---|---|---|
| `~/.claude/settings.json` | Reads and changes only the `statusLine` entry, after your consent | Connects to Claude Code. A `.bak` copy is saved beside it. |
| Status line data from Claude Code | Reads it in memory | Keeps only limit percentages and reset times |
| Claude Code's `/usage` command | Runs it every 2 minutes and when you open the panel, while connected | Keeps only limit percentages and reset times. It makes no model call and spends no tokens. |
| `%LOCALAPPDATA%\ClaudeUsageTaskbar` | Reads and writes | Stores `usage.json`, `history.csv` (24 hours of times and 5-hour percentages), and your earlier status line if you had one |
| `%LOCALAPPDATA%\Programs\ClaudeUsageTaskbar` | Reads and writes | Holds the installed app |
| `HKCU\Software\ClaudeUsageTaskbar` | Reads and writes | Stores preferences |
| Windows Run key, Apps list, shortcuts | Reads and writes | Start with Windows, uninstall entry and shortcuts |

## What the app never does

- Read `~/.claude/.credentials.json` or any other Claude login, token or cookie
- Call Anthropic's APIs
- Store prompts, responses, session IDs or project paths
- Send analytics or telemetry
- Require administrator rights

## Network access

The app itself makes one kind of connection: when it opens and then every hour it downloads release files from this repository on GitHub, unless you turn off automatic updates, and it does the same when you click **Check for updates**. GitHub can see your IP address, as with any download. No Claude data is sent.

The `/usage` command is part of Claude Code, which connects to Anthropic with your own login. The app never sees that login.

## Automatic update protection

Each release has a manifest with its version and SHA-256 hash, signed with an RSA 3072-bit key. The private key stays on the maintainer's computer, outside this repository and GitHub, and the public key is built into the app.

Before installing an update, the app checks that:

1. The manifest signature is valid.
2. The version is newer than the installed one, which blocks downgrades.
3. The downloaded app matches the signed hash.

If any check fails, the update is discarded. An attacker who controls the GitHub repository still cannot deliver an update without the private key.

## Verify a download

Each release lists the SHA-256 hash of the app. Compare it with your file:

```powershell
Get-FileHash .\ClaudeUsageTaskbar.exe -Algorithm SHA256
```

The app does not have a paid code-signing certificate, so Windows SmartScreen may warn about it. The update signature above is separate and is not visible to SmartScreen.

## Known limits

- `usage.json` and `history.csv` are not encrypted. They hold only percentages and times, and any program running as your Windows user can read them.
- Your earlier status line command, if any, is run with the same input it received before.
- Other programs running as your Windows user could change the installed app. That is true of any app installed per user.

## Report a vulnerability

Please use [private vulnerability reporting](https://github.com/leotrvs/claude-usage-taskbar/security/advisories/new) instead of a public issue.
