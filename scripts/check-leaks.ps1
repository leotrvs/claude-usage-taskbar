# Blocks commits and pushes that contain secrets or personal data. Runs from .githooks.
# Personal terms stay out of the repository: list them one per line in .git\info\leak-patterns.txt.
param([switch]$Staged)

$ErrorActionPreference = 'Stop'
$root = (git rev-parse --show-toplevel).Trim()
Set-Location $root

$patterns = @(
    'sk-ant-[a-zA-Z0-9_-]+',
    'BEGIN [A-Z ]*PRIVATE KEY',
    '<D>[A-Za-z0-9+/=]{20,}',
    '<P>[A-Za-z0-9+/=]{20,}',
    'gh[pousr]_[A-Za-z0-9]{20,}',
    '[A-Za-z]:\\Users\\[A-Za-z0-9._-]+'
)
$blockedFiles = '\.exe$|\.pfx$|\.pem$|\.key$|signing-key.*\.xml$|credentials|\.env($|\.)'
$self = 'scripts/check-leaks.ps1'

$localList = Join-Path (git rev-parse --git-dir).Trim() 'info\leak-patterns.txt'
if (Test-Path $localList) {
    $patterns += Get-Content $localList | Where-Object { $_.Trim() } | ForEach-Object { [regex]::Escape($_.Trim()) }
}

$files = if ($Staged) { git diff --cached --name-only --diff-filter=ACMR } else { git ls-files }
$problems = @()
foreach ($file in $files) {
    if ($file -match $blockedFiles) { $problems += "blocked file: $file" }
    if ($file -eq $self -or -not (Test-Path $file)) { continue }
    $text = if ($Staged) { git show ":$file" | Out-String } else { Get-Content $file -Raw }
    foreach ($pattern in $patterns) {
        if ($text -match "(?i)$pattern") { $problems += "${file}: matched a blocked pattern ($pattern)" }
    }
}

if (-not $Staged) {
    $emails = git log --format='%ae%n%ce' | Sort-Object -Unique
    foreach ($email in $emails) {
        if ($email -notmatch '@users\.noreply\.github\.com$|^noreply@github\.com$') { $problems += "commit identity is not a noreply address" }
    }
    $messages = git log --format='%an%n%cn%n%B' | Out-String
    foreach ($pattern in $patterns) {
        if ($messages -match "(?i)$pattern") { $problems += "commit metadata matched a blocked pattern" }
    }
}

if ($problems) {
    $problems | Sort-Object -Unique | ForEach-Object { Write-Host "BLOCKED: $_" }
    exit 1
}
'No secrets or personal data found.'
