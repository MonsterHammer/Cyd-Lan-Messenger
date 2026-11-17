param([string]$Dotnet = 'dotnet', [ValidatePattern('^[A-Za-z0-9._-]+$')][string]$OutputName = 'CydLan-Windows')

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$outputDirectory = Join-Path $projectRoot "artifacts/$OutputName"
$archivePath = Join-Path $projectRoot "artifacts/$OutputName-x64.zip"

& $Dotnet publish (Join-Path $projectRoot 'src/CydLan.Desktop') -c Release -r win-x64 --self-contained true -o $outputDirectory -p:UseSharedCompilation=false --nologo
if ($LASTEXITCODE -ne 0) { throw 'Windows publish failed.' }

foreach ($name in @('README.md', 'LICENSE', 'THIRD-PARTY-NOTICES.md')) {
    Copy-Item -LiteralPath (Join-Path $projectRoot $name) -Destination $outputDirectory -Force
}
New-Item -ItemType Directory -Path (Join-Path $outputDirectory 'docs') -Force | Out-Null
Get-ChildItem -LiteralPath (Join-Path $projectRoot 'docs') -File | Copy-Item -Destination (Join-Path $outputDirectory 'docs') -Force
New-Item -ItemType Directory -Path (Join-Path $outputDirectory 'brand') -Force | Out-Null
Get-ChildItem -LiteralPath (Join-Path $projectRoot 'assets/brand') -File | Copy-Item -Destination (Join-Path $outputDirectory 'brand') -Force
@'
CydLan for Windows x64

Extract the whole ZIP, then open CydLan.exe. No separate .NET installation is needed.
Allow CydLan on private networks when Windows Firewall asks.
People on the same LAN appear automatically. If needed, use Preferences > Connect by IP.
Closing the window keeps chat active in the tray. Right-click the tray icon to quit.

To explore without networking, launch CydLan.exe --preview.
Read README.md for protocol limitations and docs/test-results.md for local test evidence.
'@ | Set-Content -LiteralPath (Join-Path $outputDirectory 'START-HERE.txt') -Encoding utf8

Compress-Archive -Path (Join-Path $outputDirectory '*') -DestinationPath $archivePath -Force
Write-Output "Windows app: $(Join-Path $outputDirectory 'CydLan.exe')"
Write-Output "Portable archive: $archivePath"
