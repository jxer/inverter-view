param([switch]$SkipBuild)
$ErrorActionPreference = 'Stop'
$projectRoot = $PSScriptRoot
$version = [IO.File]::ReadAllText((Join-Path $projectRoot 'VERSION')).Trim()
if (-not $SkipBuild) { & "$projectRoot\build.ps1" }
$portableDir = Join-Path $projectRoot "build\$version\InverterView"
if ((Get-Item -LiteralPath "$portableDir\InverterView.exe").VersionInfo.FileVersion -ne "$version.0") { throw 'The executable version does not match VERSION. Rebuild before packaging.' }
$distDir = Join-Path $projectRoot 'dist'
New-Item -ItemType Directory -Force -Path $distDir | Out-Null
Copy-Item -LiteralPath "$projectRoot\docs" -Destination $portableDir -Recurse -Force
$zipName = "InverterView-$version-portable.zip"
$zipPath = Join-Path $distDir $zipName
Compress-Archive -LiteralPath "$portableDir\InverterView.exe","$portableDir\InverterView.exe.config","$portableDir\README.md","$portableDir\LICENSE","$portableDir\CHANGELOG.md","$portableDir\docs" -DestinationPath $zipPath -Force
$hash = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash.ToLowerInvariant()
"$hash  $zipName" | Set-Content -LiteralPath "$distDir\SHA256SUMS.txt" -Encoding ASCII
Write-Output "Packaged $zipPath"
Write-Output "SHA256 $hash"
