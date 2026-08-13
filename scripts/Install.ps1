[CmdletBinding()]
param(
    [string]$InstallRoot = (Join-Path $env:LOCALAPPDATA 'G15Backlight'),
    [switch]$AllowAwccConflict,
    [switch]$ForceUnsupportedController,
    [switch]$NoStartup,
    [switch]$NoLaunch
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$sourceExe = Join-Path $projectRoot 'dist\G15Backlight.exe'
$taskName = 'G15 Backlight Control'

if (-not (Test-Path -LiteralPath $sourceExe -PathType Leaf)) {
    & (Join-Path $PSScriptRoot 'Build.ps1') | Out-Host
}

$diagnosis = & (Join-Path $PSScriptRoot 'Diagnose.ps1') -Json | ConvertFrom-Json
if (-not $diagnosis.SupportedControllerPresent -and -not $ForceUnsupportedController) {
    throw 'Supported controller VID_187C&PID_0550 was not found. Nothing was installed.'
}
if ($diagnosis.AwccConflictDetected -and -not $AllowAwccConflict) {
    throw 'AWCC components are present and may compete for the same HID controller. Run Remove-AWCC.ps1 explicitly, or rerun with -AllowAwccConflict if coexistence is intentional.'
}

$fullRoot = [IO.Path]::GetFullPath($InstallRoot)
$localRoot = [IO.Path]::GetFullPath($env:LOCALAPPDATA).TrimEnd('\') + '\'
if (-not $fullRoot.StartsWith($localRoot, [StringComparison]::OrdinalIgnoreCase)) {
    throw "InstallRoot must be inside LOCALAPPDATA: $localRoot"
}

New-Item -ItemType Directory -Path $fullRoot -Force | Out-Null
$targetExe = Join-Path $fullRoot 'G15Backlight.exe'
Copy-Item -LiteralPath $sourceExe -Destination $targetExe -Force

$targetSettings = Join-Path $fullRoot 'settings.ini'
$legacySettings = Join-Path $env:LOCALAPPDATA 'AlienFX-Backlight\settings.ini'
if (-not (Test-Path -LiteralPath $targetSettings) -and (Test-Path -LiteralPath $legacySettings)) {
    Copy-Item -LiteralPath $legacySettings -Destination $targetSettings
}

if (-not $NoStartup) {
    $service = New-Object -ComObject 'Schedule.Service'
    $service.Connect()
    $folder = $service.GetFolder('\')
    $task = $service.NewTask(0)
    $task.RegistrationInfo.Description = 'Starts the lightweight G15 four-zone backlight controller at user logon.'
    $task.Settings.Enabled = $true
    $task.Settings.Hidden = $true
    $task.Settings.StartWhenAvailable = $true
    $task.Settings.DisallowStartIfOnBatteries = $false
    $task.Settings.StopIfGoingOnBatteries = $false
    $task.Settings.ExecutionTimeLimit = 'PT0S'
    $task.Settings.MultipleInstances = 2
    $trigger = $task.Triggers.Create(9)
    $trigger.Enabled = $true
    $trigger.UserId = [Security.Principal.WindowsIdentity]::GetCurrent().Name
    $action = $task.Actions.Create(0)
    $action.Path = $targetExe
    $task.Principal.UserId = [Security.Principal.WindowsIdentity]::GetCurrent().Name
    $task.Principal.LogonType = 3
    $task.Principal.RunLevel = 0
    $null = $folder.RegisterTaskDefinition($taskName, $task, 6, $null, $null, 3, $null)
}

if (-not $NoLaunch) {
    Start-Process -FilePath $targetExe -ArgumentList '--settings'
}

[pscustomobject]@{
    Installed = $true
    InstallRoot = $fullRoot
    StartupTask = if ($NoStartup) { $null } else { $taskName }
    SHA256 = (Get-FileHash -LiteralPath $targetExe -Algorithm SHA256).Hash
    LegacySettingsMigrated = ((Test-Path -LiteralPath $legacySettings) -and (Test-Path -LiteralPath $targetSettings))
}
