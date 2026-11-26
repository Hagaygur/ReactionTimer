# Agent build, test and release playbook

Human counterpart: [Operations](../human/OPERATIONS.md). Detailed CI reference: [GITHUB.md](../../GITHUB.md).

## CI file map

- `.github/workflows/build.yml`: Ubuntu + Windows parallel validation, `--base` + `--tag-floor` version selection, release publication.
- `ci/sdk-lock.json`, `ci/Get-Sdk.ps1`, `ci/get_sdk_linux.py`: dual SHA-256 verification for DH 4 (`4.2.1.0`) and DH 5 (`5.3.0.0`).
- `ci/PackagePlugin.targets`, `ci/check_build_output.py`: DH 4 runtime-only ZIP + DH 5 build output checks.
- `ci/sign_dh5.py`: ephemeral temp-only DH 5 Authenticode (`signtool` / `osslsigncode`) + `.pem` + `.manifest(.sig)` + `.entitlement(.sig)` packager (`--allow-unsigned` only for untrusted fork PRs; publish builds fail if unsigned).
- `ci/Alch Reaction Timer.entitlement`, `ci/Alch Reaction Timer.entitlement.sig`: public DH 5 community entitlement pair (private `.key`/`.pfx` never enter git).
- `tests/ContractSnapshot.cs`: contract observations after `ReactionTimer.Tests` passes.
- `tests/WindowsSmoke/`: DH 4 ZIP, `SupportedOSPlatform`, toolbar geometry, `SettingsDialog` (`Theme`, `SuppressAlertsInTown`, `ShowDepletionBar`, `RespikeAlertSeconds`), and `HudWindow.Render` bitmap + 11-tick `0 px` zero-twitch checks.
- `tests/Dh5Smoke/`: cross-platform `net10.0` `x64` DH 5 archive layout, cryptographic trust verification (`--require-signed`), 4-rule DH 5 security constraint scanner (`VerifyDh5SecurityConstraints`), `CompactPill`/`RadialDonut` zero-jitter layout checks (`VerifyZeroJitterHudLayout`), PE `TypeRef`/`MemberRef` resolution, zero WinForms/System.Media refs, 36x36 PNG icon, and mocked `IPlugin` lifecycle.
- `ci/version.py`, `ci/test_version.py`, `ci/package.py`, `ci/release.py`: SemVer classification (`src/**/*.cs` + `src-dh5/**/*.cs`), `--tag-floor` orphan-tag handling, source allowlist coverage, secret-leak guard, `README.md` settings sync check, and GitHub Release publication.
