[CmdletBinding()]
param(
    [ValidateSet('Audit', 'UninstallRegistered')]
    [string]$Mode = 'Audit',
    [switch]$ConfirmAwccRemoval
)

$ErrorActionPreference = 'Stop'
$diagnosis = & (Join-Path $PSScriptRoot 'Diagnose.ps1') -Json | ConvertFrom-Json
if ($Mode -eq 'Audit') {
    $diagnosis
    return
}
if (-not $ConfirmAwccRemoval) {
    throw 'UninstallRegistered requires -ConfirmAwccRemoval. Audit is the default.'
}

$results = @()
foreach ($package in @($diagnosis.AwccRegisteredPackages)) {
    $productCode = [string]$package.PSChildName
    if ($package.WindowsInstaller -eq 1 -and $productCode -match '^\{[0-9A-Fa-f-]{36}\}$') {
        $log = Join-Path $env:TEMP ("G15Backlight-AWCC-{0}.log" -f $productCode.Trim('{}'))
        $process = Start-Process msiexec.exe -ArgumentList @('/x', $productCode, '/qn', '/norestart', '/L*v', $log) -Wait -PassThru
        $results += [pscustomobject]@{ Name = $package.DisplayName; Kind = 'MSI'; ExitCode = $process.ExitCode; Log = $log }
    } else {
        $results += [pscustomobject]@{ Name = $package.DisplayName; Kind = 'Unsupported registration'; ExitCode = $null; Log = $null }
    }
}

foreach ($package in @($diagnosis.AwccAppxPackages)) {
    try {
        Remove-AppxPackage -Package $package.PackageFullName -ErrorAction Stop
        $results += [pscustomobject]@{ Name = $package.Name; Kind = 'Appx'; ExitCode = 0; Log = $null }
    } catch {
        $results += [pscustomobject]@{ Name = $package.Name; Kind = 'Appx'; ExitCode = 1; Log = $_.Exception.Message }
    }
}

$results
Write-Warning 'No registry keys, driver packages, services, or residual folders were force-deleted. Run Diagnose.ps1 again and handle remaining vendor components by exact identity.'
