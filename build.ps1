param([string]$Configuration = "Release", [string]$ActPath = "C:\Program Files (x86)\Advanced Combat Tracker\Advanced Combat Tracker.exe")
$ErrorActionPreference = "Stop"
if (-not (Test-Path -LiteralPath $ActPath)) { throw "Advanced Combat Tracker.exe was not found. Pass -ActPath." }
dotnet build (Join-Path $PSScriptRoot "PvPChronicle.sln") -c $Configuration -p:ActPath="$ActPath" --configfile (Join-Path $PSScriptRoot "NuGet.Config")
if ($LASTEXITCODE -ne 0) { throw "Build failed." }
$testExe = Join-Path $PSScriptRoot "tests\PvPChronicle.Tests\bin\$Configuration\net48\PvPChronicle.Tests.exe"
& $testExe
if ($LASTEXITCODE -ne 0) { throw "Tests failed." }
$dll = Join-Path $PSScriptRoot "src\PvPChronicle\bin\$Configuration\net48\PvPChronicle.dll"
$updater = Join-Path $PSScriptRoot "src\PvPChronicle.Updater\bin\$Configuration\net48\PvPChronicle.Updater.exe"
Copy-Item -LiteralPath $updater -Destination (Join-Path (Split-Path -Parent $dll) "PvPChronicle.Updater.exe") -Force
$version = [Reflection.AssemblyName]::GetAssemblyName($dll).Version.ToString(3)
$dist = Join-Path $PSScriptRoot "dist"; $stage = Join-Path $dist "package"
New-Item -ItemType Directory -Force -Path $stage | Out-Null
Copy-Item -LiteralPath $dll -Destination (Join-Path $stage "PvPChronicle.dll") -Force
Copy-Item -LiteralPath $updater -Destination (Join-Path $stage "PvPChronicle.Updater.exe") -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot "README.md") -Destination (Join-Path $stage "README.md") -Force
$zipName = "PvPChronicle-v$version.zip"; $zip = Join-Path $dist $zipName
Compress-Archive -Path (Join-Path $stage "*") -DestinationPath $zip -Force
$hash = (Get-FileHash -Algorithm SHA256 -LiteralPath $zip).Hash.ToLowerInvariant()
Set-Content -LiteralPath (Join-Path $dist "PvPChronicle-v$version.sha256") -Value "$hash  $zipName" -Encoding ascii
& (Join-Path $PSScriptRoot 'verify-release.ps1') -ZipPath $zip -Version $version
Write-Host "Built $dll"; Write-Host "Release asset $zip"
