# Builds bin\ClaudeUsageTaskbar.exe with the C# compiler included in Windows. No SDK is needed.
#   -UpdateTest  points the updater at a local test server. Never publish that build.
param(
    [switch]$UpdateTest,
    [string]$OutDir = (Join-Path $PSScriptRoot '..\bin')
)

$ErrorActionPreference = 'Stop'
$root = Resolve-Path (Join-Path $PSScriptRoot '..')
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
$OutDir = (Resolve-Path $OutDir).Path

$icon = Join-Path $OutDir 'ClaudeUsageTaskbar.ico'
& (Join-Path $PSScriptRoot 'generate-icon.ps1') -OutFile $icon

$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$arguments = @(
    '/nologo', '/target:winexe', '/optimize+', '/codepage:65001',
    "/out:$(Join-Path $OutDir 'ClaudeUsageTaskbar.exe')", "/win32icon:$icon",
    '/r:System.Windows.Forms.dll', '/r:System.Drawing.dll', '/r:System.Web.Extensions.dll',
    '/r:Microsoft.CSharp.dll', '/r:System.Core.dll'
)
$arguments += "/resource:$(Join-Path $root 'assets\mascots\default.gif'),mascot.default.gif"
$arguments += "/resource:$(Join-Path $root 'assets\fonts\UsageInter-Regular.ttf'),font.regular.ttf"
$arguments += "/resource:$(Join-Path $root 'assets\fonts\UsageInter-SemiBold.ttf'),font.semibold.ttf"
if ($UpdateTest) { $arguments += '/define:UPDATE_TEST' }
$arguments += (Get-ChildItem (Join-Path $root 'src') -Filter '*.cs').FullName

& $csc @arguments
if ($LASTEXITCODE -ne 0) { throw "Build failed (exit code $LASTEXITCODE)." }
"Built $(Join-Path $OutDir 'ClaudeUsageTaskbar.exe')"
