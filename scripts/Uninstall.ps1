[CmdletBinding(SupportsShouldProcess)]
param(
    [string]$InstallRoot = (Join-Path $env:LOCALAPPDATA 'G15Backlight'),
    [switch]$KeepSettings,
    [switch]$AllowCustomInstallRoot,
    [switch]$NoStartupTask
)

$ErrorActionPreference = 'Stop'
$taskName = 'G15 Backlight Control'
$fullRoot = [IO.Path]::GetFullPath($InstallRoot).TrimEnd('\')
$expectedRoot = [IO.Path]::GetFullPath((Join-Path $env:LOCALAPPDATA 'G15Backlight')).TrimEnd('\')
$localBoundary = [IO.Path]::GetFullPath($env:LOCALAPPDATA).TrimEnd('\') + '\'
if (-not $fullRoot.StartsWith($localBoundary, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Refusing to remove a path outside LOCALAPPDATA: $fullRoot"
}
if (-not $AllowCustomInstallRoot -and -not $fullRoot.Equals($expectedRoot, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Refusing to remove an unexpected path: $fullRoot"
}

if (-not $NoStartupTask -and $PSCmdlet.ShouldProcess($taskName, 'Unregister scheduled task')) {
    Unregister-ScheduledTask -TaskName $taskName -Confirm:$false -ErrorAction SilentlyContinue
}

$targetExe = Join-Path $fullRoot 'G15Backlight.exe'
Get-CimInstance Win32_Process -Filter "Name='G15Backlight.exe'" -ErrorAction SilentlyContinue |
    Where-Object { $_.ExecutablePath -and $_.ExecutablePath.Equals($targetExe, [StringComparison]::OrdinalIgnoreCase) } |
    ForEach-Object {
        if ($PSCmdlet.ShouldProcess("PID $($_.ProcessId)", 'Stop G15Backlight')) {
            Stop-Process -Id $_.ProcessId -Force -ErrorAction Stop
        }
    }

if (Test-Path -LiteralPath $fullRoot) {
    $item = Get-Item -LiteralPath $fullRoot -Force
    if ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) {
        throw "Refusing to traverse a reparse point: $fullRoot"
    }
    if ($KeepSettings) {
        if ($PSCmdlet.ShouldProcess($targetExe, 'Remove program file')) {
            Remove-Item -LiteralPath $targetExe -Force -ErrorAction SilentlyContinue
        }
    } elseif ($PSCmdlet.ShouldProcess($fullRoot, 'Remove application and settings')) {
        Remove-Item -LiteralPath $fullRoot -Recurse -Force
    }
}

[pscustomobject]@{ Uninstalled = $true; SettingsKept = [bool]$KeepSettings }
