# Starts a legacy LAN Messenger in Windows Sandbox and a lab CydLan bound only to the sandbox's
# virtual adapter. The lab CydLan uses %LOCALAPPDATA%\CydLan-Lab, so it has no real contacts and
# never announces on the office LAN. Put the LAN Messenger installer in tools\lab\share first.
param([switch]$SkipSandbox)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$share = Join-Path $PSScriptRoot 'share'
$ipFile = Join-Path $share 'host-ip.txt'
$exe = Join-Path $root 'src\CydLan.Desktop\bin\Debug\net10.0-windows\CydLan.exe'
New-Item -ItemType Directory -Force $share | Out-Null
# Inside the sandbox, run C:\Lab\Trace-LMC.ps1 to restart LAN Messenger with /trace; its log is mirrored to share\traces.
Copy-Item (Join-Path $PSScriptRoot 'Trace-LMC.ps1') $share -Force

if (Get-Process CydLan -ErrorAction SilentlyContinue | Where-Object { $_.MainWindowTitle -notlike '*Lab*' }) {
    throw 'Close the normal CydLan first; it also listens on port 50000.'
}
if (-not (Test-Path $exe)) { dotnet build (Join-Path $root 'src\CydLan.Desktop') -c Debug -nologo -v q }

if (-not $SkipSandbox) {
    Remove-Item $ipFile -ErrorAction SilentlyContinue
    # Inside the sandbox the default gateway is the host's end of the sandbox network.
    $logon = 'powershell -NoProfile -Command "(Get-NetIPConfiguration | Where-Object IPv4DefaultGateway | Select-Object -First 1).IPv4DefaultGateway.NextHop | Set-Content C:\Lab\host-ip.txt; Start-Process explorer C:\Lab"'
    $wsb = @"
<Configuration>
  <Networking>Enable</Networking>
  <vGPU>Disable</vGPU>
  <ClipboardRedirection>Enable</ClipboardRedirection>
  <MappedFolders>
    <MappedFolder><HostFolder>$share</HostFolder><SandboxFolder>C:\Lab</SandboxFolder><ReadOnly>false</ReadOnly></MappedFolder>
  </MappedFolders>
  <LogonCommand><Command>$([Security.SecurityElement]::Escape($logon))</Command></LogonCommand>
</Configuration>
"@
    $wsbPath = Join-Path $PSScriptRoot 'CydLanLab.wsb'
    Set-Content -Path $wsbPath -Value $wsb -Encoding UTF8
    Start-Process $wsbPath
    Write-Host 'Waiting for the sandbox to report its gateway address...'
    $deadline = (Get-Date).AddMinutes(3)
    while (-not (Test-Path $ipFile)) {
        if ((Get-Date) -gt $deadline) { throw 'The sandbox did not report its address.' }
        Start-Sleep -Seconds 2
    }
}

$hostIp = (Get-Content $ipFile -ErrorAction Stop | Select-Object -First 1).Trim()
if (-not (Get-NetIPAddress -IPAddress $hostIp -ErrorAction SilentlyContinue)) { throw "$hostIp is not an address on this PC." }
Write-Host "Starting lab CydLan on $hostIp"
Start-Process $exe -ArgumentList '--lab', $hostIp
