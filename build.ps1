param(
    [string]$DungeonHelperDir = (Join-Path $env:APPDATA 'Dungeon Helper'),
    [string]$Dh5SdkDir = '',
    [ValidatePattern('^\d+\.\d+\.\d+$')][string]$Version = '1.0.0',
    [string]$KeyFile = '',
    [switch]$AllowUnsigned
)
$ErrorActionPreference = 'Stop'
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw 'Install the .NET 10 SDK to rebuild (and .NET 8 runtime for tests). The prebuilt plugin does not need the SDK.'
}
$sdkVersion = dotnet --version
if ($LASTEXITCODE -ne 0 -or $sdkVersion -notmatch '^10\.') {
    throw 'This source tree requires a stable .NET 10 SDK selected by global.json.'
}
if ([string]::IsNullOrWhiteSpace($Dh5SdkDir)) {
    if (-not [string]::IsNullOrWhiteSpace($env:DH5_SDK_DIR)) {
        $Dh5SdkDir = $env:DH5_SDK_DIR
    } else {
        $Dh5SdkDir = Join-Path $PSScriptRoot '.sdk-cache\dh5'
    }
}
if (-not (Test-Path (Join-Path $DungeonHelperDir 'VoK.Sdk.dll'))) {
    throw 'DH 4 VoK.Sdk.dll was not found. Pass -DungeonHelperDir with the Dungeon Helper 4 installation folder.'
}
if (-not (Test-Path (Join-Path $Dh5SdkDir 'VoK.Sdk.dll'))) {
    throw "DH 5 VoK.Sdk.dll was not found at '$Dh5SdkDir'. Pass -Dh5SdkDir, set DH5_SDK_DIR, or run ./ci/Get-Sdk.ps1."
}

$utf8NoBom = [System.Text.UTF8Encoding]::new($false)
function Write-Utf8NoBom([string]$Path, [string]$Text) {
    [System.IO.File]::WriteAllText($Path, $Text, $utf8NoBom)
}

# Stamp every version-bearing file together; CI never commits generated stamps.
$assemblyInfo = Join-Path $PSScriptRoot 'src/AssemblyInfo.cs'
$content = [System.IO.File]::ReadAllText($assemblyInfo)
$content = $content -replace 'AssemblyVersion\("[^"]+"\)', "AssemblyVersion(`"$Version.0`")"
$content = $content -replace 'AssemblyFileVersion\("[^"]+"\)', "AssemblyFileVersion(`"$Version.0`")"
Write-Utf8NoBom $assemblyInfo $content

$metadataPath = Join-Path $PSScriptRoot 'src/VoK.ReactionTimer.metadata'
$metadata = Get-Content $metadataPath -Raw | ConvertFrom-Json
$metadata.Version = "$Version.0"
Write-Utf8NoBom $metadataPath (($metadata | ConvertTo-Json) + "`n")

foreach ($csprojRel in @('src/ReactionTimer.csproj', 'src-dh5/ReactionTimer.Dh5.csproj')) {
    $csprojPath = Join-Path $PSScriptRoot $csprojRel
    $projText = [System.IO.File]::ReadAllText($csprojPath)
    $projText = $projText -replace '<Version>[^<]+</Version>', "<Version>$Version</Version>"
    Write-Utf8NoBom $csprojPath $projText
}

$dh4Project = Join-Path $PSScriptRoot 'src\ReactionTimer.csproj'
dotnet build $dh4Project -c Release "-p:DungeonHelperDir=$DungeonHelperDir" "-p:Version=$Version"
if ($LASTEXITCODE -ne 0) { throw 'DH 4 plugin build failed.' }

$dh5Project = Join-Path $PSScriptRoot 'src-dh5\ReactionTimer.Dh5.csproj'
dotnet build $dh5Project -c Release "-p:Dh5SdkDir=$Dh5SdkDir" "-p:Version=$Version"
if ($LASTEXITCODE -ne 0) { throw 'DH 5 plugin build failed.' }

dotnet run --project (Join-Path $PSScriptRoot 'tests\ReactionTimer.Tests.csproj') -c Release
if ($LASTEXITCODE -ne 0) { throw 'Timer tests failed.' }

$dh4Out = Join-Path $PSScriptRoot 'src/bin/Release/net8.0-windows'
$dist = Join-Path $PSScriptRoot 'dist'
New-Item -ItemType Directory -Force $dist | Out-Null
$dh4Zip = Join-Path $dist "ReactionTimer-$Version-dh4-plugin.zip"
Copy-Item (Join-Path $dh4Out "ReactionTimer-$Version-dh4-plugin.zip") $dh4Zip -Force

$dh5Out = Join-Path $PSScriptRoot 'src-dh5/bin/x64/Release/net10.0'
$dh5Zip = Join-Path $dist "ReactionTimer-$Version-dh5-plugin.zip"
$signArgs = @(
    (Join-Path $PSScriptRoot 'ci/sign_dh5.py'),
    '--version', $Version,
    '--input-dir', $dh5Out,
    '--output', $dh5Zip
)
if (-not [string]::IsNullOrWhiteSpace($KeyFile)) {
    $signArgs += @('--key-file', $KeyFile)
}
if ($AllowUnsigned) {
    $signArgs += '--allow-unsigned'
}
python @signArgs
if ($LASTEXITCODE -ne 0) { throw 'DH 5 signing/packaging failed.' }

Write-Host "Built and tested DH 4: $dh4Zip"
Write-Host "Built and packaged DH 5: $dh5Zip"
Write-Host 'DH 4: Settings -> Add plugin from zip file | DH 5 Beta: Plugins -> Install from zip.'
