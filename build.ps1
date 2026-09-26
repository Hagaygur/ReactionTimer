param(
    [string]$DungeonHelperDir = (Join-Path $env:APPDATA 'Dungeon Helper'),
    [ValidatePattern('^\d+\.\d+\.\d+$')][string]$Version = '1.0.0'
)
$ErrorActionPreference = 'Stop'
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw 'Install the .NET 10 SDK to rebuild (and .NET 8 runtime for tests). The prebuilt plugin does not need the SDK.'
}
$sdkVersion = dotnet --version
if ($LASTEXITCODE -ne 0 -or $sdkVersion -notmatch '^10\.') {
    throw 'This source tree requires a stable .NET 10 SDK selected by global.json.'
}
if (-not (Test-Path (Join-Path $DungeonHelperDir 'VoK.Sdk.dll'))) {
    throw 'VoK.Sdk.dll was not found. Pass -DungeonHelperDir with the Dungeon Helper installation folder.'
}
# Stamp every version-bearing file together; CI never commits generated stamps.
$assemblyInfo = Join-Path $PSScriptRoot 'src/AssemblyInfo.cs'
$content = Get-Content $assemblyInfo -Raw
$content = $content -replace 'AssemblyVersion\("[^"]+"\)', "AssemblyVersion(`"$Version.0`")"
$content = $content -replace 'AssemblyFileVersion\("[^"]+"\)', "AssemblyFileVersion(`"$Version.0`")"
Set-Content $assemblyInfo $content -Encoding utf8
$metadataPath = Join-Path $PSScriptRoot 'src/VoK.ReactionTimer.metadata'
$metadata = Get-Content $metadataPath -Raw | ConvertFrom-Json
$metadata.Version = "$Version.0"
$metadata | ConvertTo-Json | Set-Content $metadataPath -Encoding utf8
$project = Join-Path $PSScriptRoot 'src\ReactionTimer.csproj'
dotnet build $project -c Release "-p:DungeonHelperDir=$DungeonHelperDir" "-p:Version=$Version"
if ($LASTEXITCODE -ne 0) { throw 'Plugin build failed.' }
dotnet run --project (Join-Path $PSScriptRoot 'tests\ReactionTimer.Tests.csproj') -c Release
if ($LASTEXITCODE -ne 0) { throw 'Timer tests failed.' }
$output = Join-Path $PSScriptRoot 'src/bin/Release/net8.0-windows'
$dist = Join-Path $PSScriptRoot 'dist'
New-Item -ItemType Directory -Force $dist | Out-Null
$zip = Join-Path $dist "ReactionTimer-$Version-plugin.zip"
# Normal builds already create the exact runtime-only installer.
Copy-Item (Join-Path $output "ReactionTimer-$Version-plugin.zip") $zip -Force
Write-Host "Built and tested: $zip"
Write-Host 'Install it through Dungeon Helper Settings > Add plugin from zip file.'
