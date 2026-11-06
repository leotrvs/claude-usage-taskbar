# Writes update.json and its signature next to the exe.
param(
    [Parameter(Mandatory)][string]$ExePath,
    [Parameter(Mandatory)][string]$Version,
    [string]$KeyPath = (Join-Path $HOME '.claude-usage-taskbar-signing\update-signing-key.xml')
)

$ErrorActionPreference = 'Stop'
if (-not (Test-Path $KeyPath)) { throw "Signing key not found at $KeyPath. See docs/RELEASING.md." }

$directory = Split-Path (Resolve-Path $ExePath)
$hash = (Get-FileHash $ExePath -Algorithm SHA256).Hash
$manifestPath = Join-Path $directory 'update.json'
$manifest = "{`n  `"version`": `"$Version`",`n  `"sha256`": `"$hash`"`n}`n"
[IO.File]::WriteAllText($manifestPath, $manifest, (New-Object Text.UTF8Encoding $false))

$rsa = New-Object System.Security.Cryptography.RSACryptoServiceProvider
$rsa.PersistKeyInCsp = $false
$rsa.FromXmlString([IO.File]::ReadAllText($KeyPath))
$signature = $rsa.SignData([IO.File]::ReadAllBytes($manifestPath), 'SHA256')
[IO.File]::WriteAllBytes("$manifestPath.sig", $signature)
"SHA-256: $hash"
