[CmdletBinding()]
param([string]$Version = '0.1.0')

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
& (Join-Path $PSScriptRoot 'Build.ps1') | Out-Host

$releaseDir = Join-Path $projectRoot 'release'
$stageDir = Join-Path $releaseDir ("G15Backlight-$Version")
$zipPath = "$stageDir.zip"
if (Test-Path -LiteralPath $stageDir) { Remove-Item -LiteralPath $stageDir -Recurse -Force }
if (Test-Path -LiteralPath $zipPath) { Remove-Item -LiteralPath $zipPath -Force }
New-Item -ItemType Directory -Path (Join-Path $stageDir 'dist') -Force | Out-Null
New-Item -ItemType Directory -Path (Join-Path $stageDir 'scripts') -Force | Out-Null

Copy-Item -LiteralPath (Join-Path $projectRoot 'dist\G15Backlight.exe') -Destination (Join-Path $stageDir 'dist\G15Backlight.exe')
Copy-Item -LiteralPath (Join-Path $projectRoot 'scripts\Install.ps1') -Destination (Join-Path $stageDir 'scripts\Install.ps1')
Copy-Item -LiteralPath (Join-Path $projectRoot 'scripts\Uninstall.ps1') -Destination (Join-Path $stageDir 'scripts\Uninstall.ps1')
Copy-Item -LiteralPath (Join-Path $projectRoot 'scripts\Diagnose.ps1') -Destination (Join-Path $stageDir 'scripts\Diagnose.ps1')
Copy-Item -LiteralPath (Join-Path $projectRoot 'scripts\Remove-AWCC.ps1') -Destination (Join-Path $stageDir 'scripts\Remove-AWCC.ps1')
Copy-Item -LiteralPath (Join-Path $projectRoot 'Install.cmd') -Destination $stageDir
Copy-Item -LiteralPath (Join-Path $projectRoot 'Uninstall.cmd') -Destination $stageDir
Copy-Item -LiteralPath (Join-Path $projectRoot 'Diagnose.cmd') -Destination $stageDir
Copy-Item -LiteralPath (Join-Path $projectRoot 'README.md') -Destination $stageDir
Copy-Item -LiteralPath (Join-Path $projectRoot 'README.zh-CN.md') -Destination $stageDir
Copy-Item -LiteralPath (Join-Path $projectRoot 'LICENSE') -Destination $stageDir
Copy-Item -LiteralPath (Join-Path $projectRoot 'THIRD-PARTY-NOTICES.md') -Destination $stageDir

Compress-Archive -LiteralPath $stageDir -DestinationPath $zipPath -CompressionLevel Optimal
[pscustomobject]@{
    Release = $zipPath
    SHA256 = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash
    Bytes = (Get-Item -LiteralPath $zipPath).Length
}
