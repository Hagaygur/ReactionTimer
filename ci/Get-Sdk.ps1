$ErrorActionPreference = 'Stop'
$lock = Get-Content "$PSScriptRoot/sdk-lock.json" -Raw | ConvertFrom-Json
$root = Join-Path $env:RUNNER_TEMP 'reaction-timer-sdk'
New-Item -ItemType Directory -Force $root | Out-Null
$msi = Join-Path $root 'DungeonHelper.msi'
Invoke-WebRequest $lock.url -OutFile $msi
if ((Get-FileHash $msi -Algorithm SHA256).Hash -ne $lock.installerSha256) {
    throw 'Official installer changed. Verify the new installer and update ci/sdk-lock.json deliberately.'
}
$extract = Join-Path $root 'extracted'
$process = Start-Process msiexec.exe -ArgumentList "/a `"$msi`" /qn TARGETDIR=`"$extract`"" -WindowStyle Hidden -Wait -PassThru
if ($process.ExitCode -notin @(0, 3010)) { throw "MSI extraction failed: $($process.ExitCode)" }
$sdk = Get-ChildItem $extract -Recurse -Filter VoK.Sdk.dll | Where-Object {
    (Get-FileHash $_.FullName -Algorithm SHA256).Hash -eq $lock.sdkSha256
} | Select-Object -First 1
if (-not $sdk) { throw 'Pinned SDK DLL was not found in the installer.' }
if ([Reflection.AssemblyName]::GetAssemblyName($sdk.FullName).Version.ToString() -ne $lock.sdkVersion) {
    throw 'Unexpected SDK assembly version.'
}
"DH_SDK_DIR=$($sdk.DirectoryName)" >> $env:GITHUB_ENV
