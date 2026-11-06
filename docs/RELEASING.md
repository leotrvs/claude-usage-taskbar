# Releasing

Automatic updates need a **public** repository, because the app downloads release files without signing in.

## First time

```powershell
pwsh -File scripts/new-signing-key.ps1
```

This writes the private key to `~\.claude-usage-taskbar-signing\` and the public key to `src\UpdateKey.cs`. Commit `UpdateKey.cs`, and back up the private key somewhere safe, such as a password manager.

**Never commit the private key.** If it is lost, installed copies can no longer update automatically and users must reinstall once. If it leaks, remove every release signed with it, publish a new key in a new version, and tell users to reinstall.

Forks must create their own key and change `BaseUrl` in `src\Updater.cs`.

## Each release

1. Update `Version` in `src\App.cs` and add an entry to `CHANGELOG.md`.
2. Commit and push.
3. Build and sign without publishing, then test `bin\release\ClaudeUsageTaskbar.exe`:

   ```powershell
   pwsh -File scripts/release.ps1
   ```

4. Publish:

   ```powershell
   pwsh -File scripts/release.ps1 -Publish
   ```

5. Add the SHA-256 printed by the script to the release notes.

The release must include exactly these files: `ClaudeUsageTaskbar.exe`, `update.json` and `update.json.sig`.

Release from a local machine only. Do not put the private key in GitHub Actions or any other online service.
