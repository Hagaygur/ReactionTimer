# Automatic GitHub builds and releases

Developer entry points: [ReactionTimer.sln](ReactionTimer.sln), [human/agent documentation map](docs/README.md), and [AGENTS.md](AGENTS.md). Source release archives include the solution, both `src/` and `src-dh5/`, tests, CI scripts, and editor configurations.

Toolchain: .NET 10 SDK builds both `.NET 8 Windows` (`src/`) against pinned `VoK.Sdk 4.2.1.0` and `.NET 10 x64` (`src-dh5/`) against pinned `VoK.Sdk 5.3.0.0`. CI also installs .NET 8 to run compatibility tests. See [runtime/toolchain details](docs/human/DEVELOPMENT.md).

## Set up once

1. Upload repository contents at the repository root, including `.github/workflows/build.yml` and the public community entitlement files in `ci/`.
2. Configure the repository Actions secret **`DH_AUTHOR_KEY_B64`** (base64-encoded PKCS#8 author private key matching `ci/Alch Reaction Timer.entitlement` `authorKeyFingerprint`). Never commit `*.key` or `*.pfx` to the repository.
3. Push to `master`. Open **Actions → Build, test and release**.
4. Successful releases publish under **Releases** with dual runtime installers: `ReactionTimer-X.Y.Z-dh4-plugin.zip` (DH 4) and signed `ReactionTimer-X.Y.Z-dh5-plugin.zip` (DH 5 Beta), plus `ReactionTimer-X.Y.Z-source.zip` (never install the source ZIP in DH).

Fork pull requests without `DH_AUTHOR_KEY_B64` still build and pass `Dh5Smoke` in `--allow-unsigned` mode, while publish/release builds require `DH_AUTHOR_KEY_B64` and fail if unsigned.

## Triggers and outputs

Every branch push and pull request runs checks and produces downloadable Actions artifacts. Successful default-branch pushes and manual runs publish a tagged release only when plugin C# source (`src/**/*.cs` or `src-dh5/**/*.cs`) differs from the last reachable released baseline. Additions, edits, renames, and deletions count. Documentation, tests, CI/build scripts, project configuration, and metadata alone do not trigger publication or a version bump.

Release assets:

- `ReactionTimer-X.Y.Z-dh4-plugin.zip`: DH 4 archive (`plugins/ReactionTimer/VoK.ReactionTimer.{dll,deps.json,metadata}`, DLL first).
- `ReactionTimer-X.Y.Z-dh5-plugin.zip`: DH 5 Beta signed archive (8 root entries: `Alch Reaction Timer.entitlement`, `.entitlement.sig`, `.manifest`, `.manifest.sig`, `.pem`, `VoK.ReactionTimer.dll`, `.deps.json`, `plugin.metadata`).
- `ReactionTimer-X.Y.Z-source.zip`: source, workflow, tests, and documentation (never install in DH).
- `contracts.json`: observed behavior used for the next comparison.
- `version.json`: chosen version, reason, baseline tag, tag floor, and source commit.
- `SHA256SUMS.txt`: SHA-256 hashes covering all 5 release artifacts.

## How versions are chosen

The workflow scans merged `vX.Y.Z` tags descending to find the latest tag that actually owns a published `contracts.json` release asset (`--base`), while passing the highest merged tag as `--tag-floor` so orphan git tags (such as `v1.0.1`) cannot break baseline download or cause a version collision (`v1.0.0` baseline + `v1.0.1` floor + minor change -> `1.1.0`).

| Highest detected change | Result from `1.2.3` |
| --- | --- |
| Existing compatibility observation changed/removed, or feature removed | `2.0.0` |
| New compatibility/feature observation, or changed feature observation | `1.3.0` |
| All observations unchanged | `1.2.4` |
| Any test fails | No release |

Conventional commits (`feat:`, `type!:`, `BREAKING CHANGE:`) touching `src/**/*.cs` or `src-dh5/**/*.cs` can escalate, but never lower, the bump.

## Build and test stages

1. Python unit tests (`ci/test_version.py`, `ci/test_get_sdk_linux.py`) verify contract classification, `--tag-floor` orphan-tag handling, dual SDK hash pinning, source-allowlist coverage, zero secret leakage, `--allow-unsigned` gating, and `README.md` settings documentation coverage.
2. `tests/ReactionTimer.Tests.csproj` runs behavioral checks and emits `dist/contracts.json`.
3. `ci/version.py` selects the version using `--base` and `--tag-floor`.
4. `ci/Get-Sdk.ps1` (Windows) and `ci/get_sdk_linux.py` (Ubuntu with `msitools`) verify and extract both `VoK.Sdk 4.2.1.0` and `VoK.Sdk 5.3.0.0` by SHA-256 from `ci/sdk-lock.json`.
5. Windows builds `ReactionTimer.sln` in `Debug` and `Release`, runs `ci/sign_dh5.py` (ephemeral self-signed `.pfx` + `signtool.exe /fd SHA256` + RSA-SHA256 `.manifest.sig`), runs `tests/WindowsSmoke` on `*-dh4-plugin.zip`, runs `tests/Dh5Smoke` (`--require-signed` when signed) on `*-dh5-plugin.zip`, and runs `ci/package.py`.
6. In parallel, Ubuntu cross-compiles `ReactionTimer.sln` (`Debug`, `Release`, repeated `Release`), runs `ReactionTimer.Tests`, signs/packages the DH 5 bundle with `osslsigncode` + `openssl` when `DH_AUTHOR_KEY_B64` is available (else `--allow-unsigned`), and runs `tests/Dh5Smoke`.
7. After both jobs succeed and `publish == true`, the `release` job runs `ci/release.py` to publish the tagged GitHub Release.

## Local commands

```powershell
python -m unittest discover -s ci -p test_*.py -v
dotnet run --project tests/ReactionTimer.Tests.csproj -c Release -- --contracts dist/contracts.json
./ci/Get-Sdk.ps1
.\build.ps1 -DungeonHelperDir $env:DH_SDK_DIR -Dh5SdkDir $env:DH5_SDK_DIR -Version 1.1.1 -AllowUnsigned
```
