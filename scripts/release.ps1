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

gh release create "v$version" $exe "$out\update.json" "$out\update.json.sig" --title "v$version" --notes-file (Join-Path $root 'CHANGELOG.md')
if ($LASTEXITCODE -ne 0) { throw 'Could not create the release.' }
