# Agent build, test and release playbook

Human counterpart: [Operations](../human/OPERATIONS.md). Detailed CI reference: [GITHUB.md](../../GITHUB.md). IDE setup: [Development](DEVELOPMENT.md).

1. Read `AGENTS.md` and the guide matching the task. Inspect current code rather than treating historical docs as newer evidence.
2. Keep the change scoped. Do not update the SDK, recreate plugin identity, rewrite settings or merge source into the installer as an incidental improvement.
3. Run relevant pure tests; on Windows also build and use the smoke harness for layout/plugin-contract changes. Commands are in root `AGENTS.md`. Report unavailable checks explicitly.
4. Update both human and agent documentation when interfaces, behavior, build steps or evidence change.
5. Inspect version impact and archive contents. No failing test may be reinterpreted as a successful major release.

## CI file map

- `.github/workflows/build.yml`: branch/PR build, default-branch publication, permissions and serialization.
- `ci/Get-Sdk.ps1` + `ci/sdk-lock.json`: official MSI extraction and exact SDK verification; no SDK redistribution.
- `ci/get_sdk_linux.py`: Linux extraction with msitools and the same installer/SDK hash checks.
- `ci/PackagePlugin.targets`: normal-build installer creation; ZIP-only Release and loose files plus ZIP in Debug.
- `ci/check_build_output.py`: verify both output layouts and exact archive entries; run on Windows and Linux.
- `tests/ContractSnapshot.cs`: emits observed contracts only after behavioral checks pass.
- `ci/version.py` + `ci/test_version.py`: compatibility/feature comparisons, conventional commit escalation, initial version and reruns.
- `build.ps1`: stamp assembly/metadata, build with package version, run tests, runtime-only ZIP.
- `tests/WindowsSmoke/`: synthetic DH host; assembly identity and toolbar/button geometry checks. This does not emulate LS or live DDO.
- The smoke harness accepts a Release ZIP or Debug DLL. Release publication requires both the Windows build/smoke job and Linux cross-build/pure-test job to pass.
- `ci/package.py`: validate installer, package source/documentation/editor files, write hashes.
- `ci/release.py`: verify tag ownership, upload draft assets, publish; do not overwrite a completed public release.

## Version classification

First stable release: 1.0.0. Existing compatibility observation changes/removals and removed feature observations request major. Additions or changed feature observations request minor. Otherwise patch. `feat:` can raise to minor; `type!:` or `BREAKING CHANGE:` can raise to major. Tests must pass first. Intentionally changing an assertion requires a justified changed requirement, not a desire for green CI. Renaming a contract key counts as removal/addition and can trigger major.

Use the actual preceding reachable release asset as baseline. Preserve contract schema or write a migration. Do not delete/replace a released baseline to force a smaller bump. Version stamps are generated in disposable CI checkouts; local builds edit AssemblyInfo and metadata, so inspect working-tree changes before committing.

## Packaging and troubleshooting

Installer entries in order: `plugins/ReactionTimer/VoK.ReactionTimer.dll`, `.deps.json`, `.metadata`. No other entries. Source packages must include `AGENTS.md`, paired docs, solution, editor configuration and `.github`; exclude SDKs, logs, caches and build outputs.

For loading failure collect exact assembly version/log evidence. For UI clipping collect display scale, screenshot and version. For HUD visibility test Preview with LS off, then WGC and DXGI independently; record status and LS version. For recognition use local exported effect diagnostics and avoid invented DIDs. Preserve user settings and logs when replacing runtime files. Never assume turning Collector on/off can repair this timer.
