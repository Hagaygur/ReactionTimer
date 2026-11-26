# IDE and local development

Open **ReactionTimer.sln** in Visual Studio 2026 or VS Code with a **.NET 10 SDK** and **.NET 8 Desktop Runtime**.

## SDK references

Both SDKs are hash-pinned in `ci/sdk-lock.json` and acquired into temp/cache directories without committing binaries:

```powershell
# Acquire verified DH 4 (4.2.1.0) and DH 5 (5.3.0.0) SDKs into temp and export DH_SDK_DIR / DH5_SDK_DIR:
./ci/Get-Sdk.ps1
dotnet build ReactionTimer.sln -c Debug "-p:DungeonHelperDir=$env:DH_SDK_DIR" "-p:Dh5SdkDir=$env:DH5_SDK_DIR"
```

## Build outputs

| Target | Configuration | Output directory | Contents |
| --- | --- | --- | --- |
| DH 4 (`src/`) | `Release` | `src/bin/Release/net8.0-windows/` | `ReactionTimer-<Version>-dh4-plugin.zip` only |
| DH 4 (`src/`) | `Debug` | `src/bin/Debug/net8.0-windows/` | DH 4 ZIP, DLL, `deps.json`, `metadata`, PDB |
| DH 5 (`src-dh5/`) | `Release` | `src-dh5/bin/x64/Release/net10.0/` | `VoK.ReactionTimer.dll`, `deps.json`, PDB (packaged to `dist/` by `ci/sign_dh5.py`) |

## Linux cross-compilation

Install .NET SDK 10, .NET 8 runtime, Python 3, `msitools`, and `osslsigncode`:

```bash
python3 ci/get_sdk_linux.py --destination .build/dh-sdk
dotnet build ReactionTimer.sln -c Release "-p:DungeonHelperDir=$DH_SDK_DIR" "-p:Dh5SdkDir=$DH5_SDK_DIR"
python3 ci/check_build_output.py src/bin/Release/net8.0-windows Release --target dh4
python3 ci/check_build_output.py src-dh5/bin/x64/Release/net10.0 Release --target dh5
dotnet run --project tests/ReactionTimer.Tests.csproj -c Release --no-build
python3 ci/sign_dh5.py --version 1.0.0 --allow-unsigned
dotnet run --project tests/Dh5Smoke/Dh5Smoke.csproj -c Release --no-build -- dist/ReactionTimer-1.0.0-dh5-plugin.zip "$DH5_SDK_DIR"
python3 -m unittest discover -s ci -p 'test_*.py' -v
```

Agent counterpart: [Development instructions](../agents/DEVELOPMENT.md).
