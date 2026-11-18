param(
    [string]$Dotnet = 'dotnet',
    [string]$Iscc
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot

if (!$Iscc) {
    $compilerCommand = Get-Command ISCC.exe -ErrorAction SilentlyContinue
    if ($compilerCommand) { $Iscc = $compilerCommand.Source }
    else {
        $candidates = @(
            "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
            "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
        )
        $Iscc = $candidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
    }
}
if (!$Iscc -or !(Test-Path -LiteralPath $Iscc)) {
    throw 'Inno Setup 6 compiler was not found. Supply its path with -Iscc.'
}

[xml]$project = Get-Content -LiteralPath (Join-Path $projectRoot 'src/CydLan.Desktop/CydLan.Desktop.csproj') -Raw
$version = [string]$project.Project.PropertyGroup.Version
if ($version -notmatch '^\d+\.\d+\.\d+(\.\d+)?$') { throw "Invalid installer version: $version" }

# A fresh payload prevents files removed from the app from entering later installers.
$payloadName = 'CydLan-Setup-Payload-' + [Guid]::NewGuid().ToString('N')
$payloadDirectory = Join-Path $projectRoot "artifacts/$payloadName"
$installerDirectory = Join-Path $projectRoot 'artifacts/installers'
& (Join-Path $PSScriptRoot 'Publish-Windows.ps1') -Dotnet $Dotnet -OutputName $payloadName -SkipArchive

New-Item -ItemType Directory -Path $installerDirectory -Force | Out-Null
& $Iscc "/DPublishDir=$payloadDirectory" "/DAppVersion=$version" "/DOutputDir=$installerDirectory" (Join-Path $projectRoot 'installer/CydLan.iss')
if ($LASTEXITCODE -ne 0) { throw 'Windows installer compilation failed.' }

$installerPath = Join-Path $installerDirectory "CydLan-$version-Setup-x64.exe"
if (!(Test-Path -LiteralPath $installerPath)) { throw 'The installer compiler produced no installer.' }
$hash = (Get-FileHash -LiteralPath $installerPath -Algorithm SHA256).Hash.ToLowerInvariant()
"$hash  $([System.IO.Path]::GetFileName($installerPath))" | Set-Content -LiteralPath "$installerPath.sha256" -Encoding ascii
Write-Output "Installer: $installerPath"
Write-Output "SHA256: $hash"
