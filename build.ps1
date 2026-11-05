param([switch]$SkipTests,[string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
$projectRoot = $PSScriptRoot
$version = [IO.File]::ReadAllText((Join-Path $projectRoot 'VERSION')).Trim()
if ($version -notmatch '^\d+\.\d+\.\d+$') { throw 'VERSION must contain a major.minor.patch version.' }
if (-not [regex]::IsMatch([IO.File]::ReadAllText((Join-Path $projectRoot 'CHANGELOG.md')), '(?m)^## ' + [regex]::Escape($version) + '(?:\s|$)')) { throw "Add a CHANGELOG.md entry for $version before building." }
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) { $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe' }
if (-not (Test-Path -LiteralPath $compiler)) { throw '.NET Framework 4.x C# compiler not found.' }
$buildDir = if ([string]::IsNullOrWhiteSpace($OutputDirectory)) { Join-Path $projectRoot "build\$version" } else { [IO.Path]::GetFullPath($OutputDirectory) }
$portableDir = Join-Path $buildDir 'InverterView'
New-Item -ItemType Directory -Force -Path $portableDir | Out-Null
$versionSource = Join-Path $buildDir 'AppInfo.cs'
@"
using System.Reflection;
[assembly: AssemblyVersion("$version.0")]
[assembly: AssemblyFileVersion("$version.0")]
[assembly: AssemblyInformationalVersion("$version")]
namespace InverterDesk { public static class AppInfo { public const string Version = "$version"; } }
"@ | Set-Content -LiteralPath $versionSource -Encoding UTF8
$manifestPath = Join-Path $buildDir 'app.manifest'
[IO.File]::WriteAllText($manifestPath, [IO.File]::ReadAllText("$projectRoot\src\app.manifest").Replace('@VERSION@', "$version.0"))
$references = @('/r:System.dll','/r:System.Core.dll','/r:System.Drawing.dll','/r:System.Windows.Forms.dll','/r:System.Web.Extensions.dll')
$sharedSources = @('Core.cs','MainForm.cs','HelpGuide.cs','SettingsFile.cs','RestoreDialog.cs','Theme.cs') | ForEach-Object { Join-Path "$projectRoot\src" $_ }
$sharedSources += $versionSource
& $compiler /nologo /optimize+ /target:winexe /platform:anycpu /utf8output @references "/win32manifest:$manifestPath" "/win32icon:$projectRoot\assets\InverterView.ico" "/out:$portableDir\InverterView.exe" @sharedSources "$projectRoot\src\Program.cs"
if ($LASTEXITCODE -ne 0) { throw 'Application compilation failed.' }
@'
<?xml version="1.0" encoding="utf-8"?>
<configuration><startup useLegacyV2RuntimeActivationPolicy="true"><supportedRuntime version="v4.0" sku=".NETFramework,Version=v4.8"/></startup></configuration>
'@ | Set-Content -LiteralPath "$portableDir\InverterView.exe.config" -Encoding UTF8
if (-not $SkipTests) {
    & $compiler /nologo /optimize+ /target:exe @references "/out:$buildDir\Tests.exe" $versionSource "$projectRoot\src\Core.cs" "$projectRoot\src\SettingsFile.cs" "$projectRoot\tests\Tests.cs"
    if ($LASTEXITCODE -ne 0) { throw 'Test compilation failed.' }
    & "$buildDir\Tests.exe" "$projectRoot\evidence\status-capture.hex"
    if ($LASTEXITCODE -ne 0) { throw 'Tests failed.' }
    & $compiler /nologo /optimize+ /target:exe @references "/out:$buildDir\UiTests.exe" @sharedSources "$projectRoot\tests\UiTests.cs"
    if ($LASTEXITCODE -ne 0) { throw 'UI test compilation failed.' }
    & "$buildDir\UiTests.exe"
    if ($LASTEXITCODE -ne 0) { throw 'UI tests failed.' }
}
foreach ($doc in @('README.md','LICENSE','CHANGELOG.md')) { if (Test-Path -LiteralPath "$projectRoot\$doc") { Copy-Item -LiteralPath "$projectRoot\$doc" -Destination $portableDir -Force } }
Write-Output "Built $portableDir\InverterView.exe"
