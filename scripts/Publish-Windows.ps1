param(
    [string]$Dotnet = 'dotnet',
    [ValidatePattern('^[A-Za-z0-9][A-Za-z0-9._-]*$')][string]$OutputName = 'CydLan-Windows',
    [switch]$SkipArchive
)

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
# Remove the instruction sheet left by older versions of this script.
$legacyInstructions = Join-Path $outputDirectory 'START-HERE.txt'
if (Test-Path -LiteralPath $legacyInstructions) { Remove-Item -LiteralPath $legacyInstructions }

Write-Output "Windows app: $(Join-Path $outputDirectory 'CydLan.exe')"
if (!$SkipArchive) {
    Compress-Archive -Path (Join-Path $outputDirectory '*') -DestinationPath $archivePath -Force
    Write-Output "Portable archive: $archivePath"
}
