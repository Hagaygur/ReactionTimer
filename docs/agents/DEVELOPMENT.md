# Agent IDE and development instructions

Human counterpart: [IDE setup](../human/DEVELOPMENT.md).

- `ReactionTimer.sln` contains `src/ReactionTimer.csproj` (`net8.0-windows`), `src-dh5/ReactionTimer.Dh5.csproj` (`net10.0`, `x64`), `tests/ReactionTimer.Tests.csproj` (`net8.0`), `tests/WindowsSmoke/WindowsSmoke.csproj` (`net8.0-windows`), and `tests/Dh5Smoke/Dh5Smoke.csproj` (`net10.0`, `x64`).
- `global.json` selects stable .NET 10 SDKs. CI installs both .NET 8 and .NET 10 runtimes.
- DH 4 SDK resolves via `DungeonHelperDir` (default `%APPDATA%\Dungeon Helper`). DH 5 SDK resolves via `Dh5SdkDir` / `DH5_SDK_DIR` (default `.sdk-cache/dh5`). Run `./ci/Get-Sdk.ps1` (Windows) or `python3 ci/get_sdk_linux.py` (Linux) to populate verified SDKs in temp/cache without committing them.
- `ci/PackagePlugin.targets` creates `ReactionTimer-<Version>-dh4-plugin.zip` after DH 4 builds (`Release` keeps only the ZIP; `Debug` retains loose runtime files + PDB).
- `ci/sign_dh5.py` creates `ReactionTimer-<Version>-dh5-plugin.zip` from `src-dh5/bin/x64/Release/net10.0` (signed via ephemeral PFX + `signtool.exe` on Windows or `osslsigncode` on Linux; `--allow-unsigned` only for local/fork-PR testing).
- Linux (`Ubuntu`) cross-compiles the entire solution and runs `ReactionTimer.Tests`, `Dh5Smoke`, and Python tests (`msitools` + `osslsigncode`). Only `WindowsSmoke` requires Windows.
