[CmdletBinding()]
param([switch]$Json)

$ErrorActionPreference = 'Stop'

function Get-UninstallEntries {
    $paths = @(
        'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\*',
        'HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\*',
        'HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\*'
    )
    Get-ItemProperty -Path $paths -ErrorAction SilentlyContinue |
        Where-Object {
            $_.DisplayName -and $_.DisplayName -match '(?i)Alienware Command Center|Alienware OC Controls|AWCC|Alienware.*FX'
        } |
        Select-Object DisplayName, DisplayVersion, Publisher, PSChildName, WindowsInstaller, UninstallString
}

$computer = Get-CimInstance Win32_ComputerSystem
$controller = @()
if (Get-Command Get-PnpDevice -ErrorAction SilentlyContinue) {
    $controller = @(Get-PnpDevice -PresentOnly -ErrorAction SilentlyContinue |
        Where-Object { $_.InstanceId -match '(?i)VID_187C&PID_0550' } |
        Select-Object Status, Class, FriendlyName, InstanceId)
}
$awccPackages = @(Get-UninstallEntries)
$awccAppx = @(Get-AppxPackage -ErrorAction SilentlyContinue |
    Where-Object { $_.Name -match '(?i)Alienware|AWCC' } |
    Select-Object Name, Version, PackageFullName)
$awccProcesses = @(Get-Process -ErrorAction SilentlyContinue |
    Where-Object { $_.ProcessName -match '(?i)AWCC|Alienware|OCControl' } |
    Select-Object ProcessName, Id)
$awccServices = @(Get-Service -ErrorAction SilentlyContinue |
    Where-Object { $_.Name -match '(?i)AWCC|Alienware|OCControl' -or $_.DisplayName -match '(?i)AWCC|Alienware|OC Control' } |
    Select-Object Name, DisplayName, Status, StartType)

$result = [ordered]@{
    Timestamp = (Get-Date).ToString('o')
    Computer = [ordered]@{
        Manufacturer = $computer.Manufacturer
        Model = $computer.Model
        Windows = [Environment]::OSVersion.VersionString
        Is64Bit = [Environment]::Is64BitOperatingSystem
    }
    SupportedControllerPresent = ($controller.Count -gt 0)
    Controller = $controller
    AwccConflictDetected = (($awccPackages.Count + $awccAppx.Count + $awccProcesses.Count + $awccServices.Count) -gt 0)
    AwccRegisteredPackages = $awccPackages
    AwccAppxPackages = $awccAppx
    AwccProcesses = $awccProcesses
    AwccServices = $awccServices
    LegacySettingsPresent = Test-Path -LiteralPath (Join-Path $env:LOCALAPPDATA 'AlienFX-Backlight\settings.ini')
}

if ($Json) {
    [pscustomobject]$result | ConvertTo-Json -Depth 6
} else {
    [pscustomobject]$result
}
