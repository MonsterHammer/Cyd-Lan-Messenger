# Run inside Windows Sandbox: restarts LAN Messenger with /trace and mirrors its logs to C:\Lab\traces.
$exe = Get-ChildItem 'C:\Program Files (x86)', 'C:\Program Files' -Recurse -Filter lmc.exe -ErrorAction SilentlyContinue | Select-Object -First 1 -ExpandProperty FullName
if (-not $exe) { throw 'lmc.exe not found' }
Get-Process lmc -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep 2
Start-Process $exe -ArgumentList '/trace'
$out = 'C:\Lab\traces'; New-Item -ItemType Directory -Force $out | Out-Null
while ($true) {
    Get-ChildItem $env:LOCALAPPDATA, $env:APPDATA -Recurse -Filter 'lmc_*.log' -ErrorAction SilentlyContinue | Copy-Item -Destination $out -Force
    $p = Get-Process lmc -ErrorAction SilentlyContinue
    "$(Get-Date -Format s) lmc running=$([bool]$p)" | Add-Content "$out\watch.txt"
    Start-Sleep 10
}
