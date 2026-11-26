param(
    [ValidateSet('all', 'dh4', 'dh5')][string]$Target = 'all',
    [string]$Destination = ''
)
$ErrorActionPreference = 'Stop'
$lock = Get-Content "$PSScriptRoot/sdk-lock.json" -Raw | ConvertFrom-Json
if ([string]::IsNullOrWhiteSpace($Destination)) {
    $tempRoot = if ($env:RUNNER_TEMP) { $env:RUNNER_TEMP } else { [System.IO.Path]::GetTempPath() }
    $Destination = Join-Path $tempRoot 'reaction-timer-sdk'
}
New-Item -ItemType Directory -Force $Destination | Out-Null

function Export-GithubEnv([string]$Name, [string]$Value) {
    [System.Environment]::SetEnvironmentVariable($Name, $Value, 'Process')
    if ($env:GITHUB_ENV) {
        Add-Content -Path $env:GITHUB_ENV -Value "$Name=$Value" -Encoding utf8
    }
}

function Acquire-Dh4([string]$TargetDir) {
    $spec = $lock.dh4
    $work = Join-Path $Destination ('tmp-dh4-' + [System.Guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Force $work | Out-Null
    try {
        $msi = Join-Path $work 'DungeonHelper.msi'
        Invoke-WebRequest $spec.url -OutFile $msi
        if ((Get-FileHash $msi -Algorithm SHA256).Hash -ne $spec.installerSha256) {
            throw 'Official DH 4 installer changed. Verify the new installer and update ci/sdk-lock.json deliberately.'
        }
        $extract = Join-Path $work 'extracted'
        $process = Start-Process msiexec.exe -ArgumentList "/a `"$msi`" /qn TARGETDIR=`"$extract`"" -WindowStyle Hidden -Wait -PassThru
        if ($process.ExitCode -notin @(0, 3010)) { throw "MSI extraction failed: $($process.ExitCode)" }
        $sdk = Get-ChildItem $extract -Recurse -Filter VoK.Sdk.dll | Where-Object {
            (Get-FileHash $_.FullName -Algorithm SHA256).Hash -eq $spec.sdkSha256
        } | Select-Object -First 1
        if (-not $sdk) { throw 'Pinned DH 4 SDK DLL was not found in the installer.' }
        if ([Reflection.AssemblyName]::GetAssemblyName($sdk.FullName).Version.ToString() -ne $spec.sdkVersion) {
            throw 'Unexpected DH 4 SDK assembly version.'
        }
        New-Item -ItemType Directory -Force $TargetDir | Out-Null
        Copy-Item $sdk.FullName (Join-Path $TargetDir 'VoK.Sdk.dll') -Force
    } finally {
        if (Test-Path $work) { Remove-Item $work -Recurse -Force -ErrorAction SilentlyContinue }
    }
    Export-GithubEnv 'DH_SDK_DIR' $TargetDir
    Write-Host "Verified DH 4 SDK $($spec.sdkVersion): $TargetDir"
}

function Acquire-Dh5([string]$TargetDir) {
    $spec = $lock.dh5
    $work = Join-Path $Destination ('tmp-dh5-' + [System.Guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Force $work | Out-Null
    try {
        $nupkg = Join-Path $work 'vok.sdk.nupkg'
        Invoke-WebRequest $spec.url -OutFile $nupkg
        if ((Get-FileHash $nupkg -Algorithm SHA256).Hash -ne $spec.packageSha256) {
            throw 'Official DH 5 VoK.Sdk package changed. Verify the new package and update ci/sdk-lock.json deliberately.'
        }
        Add-Type -AssemblyName System.IO.Compression.FileSystem
        $stagedDll = Join-Path $work 'VoK.Sdk.dll'
        $archive = [System.IO.Compression.ZipFile]::OpenRead($nupkg)
        try {
            $entry = $archive.GetEntry($spec.entry)
            if (-not $entry) { throw "Pinned DH 5 SDK entry '$($spec.entry)' was not found in package." }
            $inStream = $entry.Open()
            $outStream = [System.IO.File]::Create($stagedDll)
            try { $inStream.CopyTo($outStream) } finally { $outStream.Dispose(); $inStream.Dispose() }
        } finally {
            $archive.Dispose()
        }
        if ((Get-FileHash $stagedDll -Algorithm SHA256).Hash -ne $spec.sdkSha256) {
            throw 'Pinned DH 5 SDK DLL hash mismatch.'
        }
        if ([Reflection.AssemblyName]::GetAssemblyName($stagedDll).Version.ToString() -ne $spec.sdkVersion) {
            throw 'Unexpected DH 5 SDK assembly version.'
        }
        New-Item -ItemType Directory -Force $TargetDir | Out-Null
        Copy-Item $stagedDll (Join-Path $TargetDir 'VoK.Sdk.dll') -Force
    } finally {
        if (Test-Path $work) { Remove-Item $work -Recurse -Force -ErrorAction SilentlyContinue }
    }
    Export-GithubEnv 'DH5_SDK_DIR' $TargetDir
    Write-Host "Verified DH 5 SDK $($spec.sdkVersion): $TargetDir"
}

if ($Target -eq 'all') {
    Acquire-Dh4 (Join-Path $Destination 'dh4')
    Acquire-Dh5 (Join-Path $Destination 'dh5')
} elseif ($Target -eq 'dh4') {
    Acquire-Dh4 $Destination
} else {
    Acquire-Dh5 $Destination
}
