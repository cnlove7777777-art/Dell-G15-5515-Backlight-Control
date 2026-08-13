[CmdletBinding()]
param(
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$sourcePath = Join-Path $projectRoot 'src\G15Backlight.cs'
$distDir = Join-Path $projectRoot 'dist'
$outputPath = Join-Path $distDir 'G15Backlight.exe'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'

if (-not (Test-Path -LiteralPath $sourcePath -PathType Leaf)) {
    throw "Source file not found: $sourcePath"
}
if (-not (Test-Path -LiteralPath $compiler -PathType Leaf)) {
    throw 'The .NET Framework 4.x C# compiler was not found.'
}

New-Item -ItemType Directory -Path $distDir -Force | Out-Null
& $compiler /nologo /target:winexe /optimize+ /platform:anycpu /out:$outputPath `
    /reference:System.Windows.Forms.dll /reference:System.Drawing.dll $sourcePath
if ($LASTEXITCODE -ne 0) {
    throw "Compilation failed with exit code $LASTEXITCODE."
}

$hash = Get-FileHash -LiteralPath $outputPath -Algorithm SHA256
[pscustomobject]@{
    Configuration = $Configuration
    File = $outputPath
    Bytes = (Get-Item -LiteralPath $outputPath).Length
    SHA256 = $hash.Hash
}
