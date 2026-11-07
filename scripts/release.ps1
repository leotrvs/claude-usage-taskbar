# Builds, signs and publishes a release from a clean checkout. See docs/RELEASING.md.
#   Without -Publish it stops after building and signing, so the result can be tested first.
param([switch]$Publish)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path

$version = [regex]::Match((Get-Content (Join-Path $root 'src\App.cs') -Raw), 'Version = "([\d.]+)"').Groups[1].Value
if (-not $version) { throw 'Version not found in src\App.cs.' }

if (git -C $root status --porcelain) { throw 'The working tree has uncommitted changes.' }
& (Join-Path $PSScriptRoot 'check-leaks.ps1')

$out = Join-Path $root 'bin\release'
Remove-Item $out -Recurse -Force -ErrorAction SilentlyContinue
& (Join-Path $PSScriptRoot 'build.ps1') -OutDir $out
$exe = Join-Path $out 'ClaudeUsageTaskbar.exe'
& (Join-Path $PSScriptRoot 'sign-update.ps1') -ExePath $exe -Version $version

if (-not $Publish) {
    "Release files are in $out. Run again with -Publish to create v$version."
    return
}

$changelog = Get-Content (Join-Path $root 'CHANGELOG.md') -Raw
$section = [regex]::Match($changelog, "(?ms)^## $([regex]::Escape($version))\r?\n(.*?)(?=^## |\z)")
if (-not $section.Success) { throw "CHANGELOG.md has no section for $version." }
$hash = (Get-FileHash $exe -Algorithm SHA256).Hash
$notes = "$($section.Groups[1].Value.Trim())`n`nSHA-256 of ``ClaudeUsageTaskbar.exe``: ``$hash``"

gh release create "v$version" $exe "$out\update.json" "$out\update.json.sig" --title "v$version" --notes $notes
if ($LASTEXITCODE -ne 0) { throw 'Could not create the release.' }
