#Requires -RunAsAdministrator
#Requires -Modules Hyper-V
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$WindowsIso,
    [string]$LabPath = (Join-Path $env:LOCALAPPDATA 'CydLan-VM-Lab')
)

$ErrorActionPreference = 'Stop'
$iso = Get-Item -LiteralPath $WindowsIso
if ($iso.PSIsContainer -or $iso.Extension -ne '.iso') {
    throw 'Provide a Windows installation ISO.'
}

$switchName = 'CydLan Lab Private'
$vmNames = 'CydLan-Lab-A', 'CydLan-Lab-B', 'CydLan-Lab-Legacy'
$labSwitch = Get-VMSwitch -Name $switchName -ErrorAction SilentlyContinue
if ($labSwitch -and $labSwitch.SwitchType -ne 'Private') {
    throw "The existing '$switchName' switch is not private."
}
foreach ($name in $vmNames) {
    if ((Get-VM -Name $name -ErrorAction SilentlyContinue) -or
        (Test-Path -LiteralPath (Join-Path $LabPath $name))) {
        throw "A VM or lab directory already exists for '$name'."
    }
}

if (!$labSwitch) {
    New-VMSwitch -Name $switchName -SwitchType Private | Out-Null
}
foreach ($name in $vmNames) {
    $path = Join-Path $LabPath $name
    New-Item -ItemType Directory -Path $path | Out-Null
    $vm = New-VM -Name $name -Generation 2 -Path $path -SwitchName $switchName `
        -MemoryStartupBytes 4GB -NewVHDPath (Join-Path $path "$name.vhdx") -NewVHDSizeBytes 64GB
    Set-VM -VM $vm -ProcessorCount 2 -AutomaticStartAction Nothing -AutomaticCheckpointsEnabled $false
    Set-VMKeyProtector -VMName $name -NewLocalKeyProtector
    Enable-VMTPM -VMName $name
    $dvd = Add-VMDvdDrive -VMName $name -Path $iso.FullName -Passthru
    Set-VMFirmware -VMName $name -FirstBootDevice $dvd
}
Get-VMNetworkAdapter -VMName $vmNames | Select-Object VMName, SwitchName
