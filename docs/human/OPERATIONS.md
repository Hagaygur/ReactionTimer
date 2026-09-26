# Build, release and troubleshooting

## Install and use

Install the release asset named `ReactionTimer-X.Y.Z-plugin.zip` directly through DH. It must contain only `VoK.ReactionTimer.dll`, `VoK.ReactionTimer.deps.json` and `VoK.ReactionTimer.metadata` under `plugins/ReactionTimer/`, DLL first. The source/repository ZIP is for development, never installation.

Close DDO before updating and fully restart DH afterward. If files are locked, close both applications and replace those three files manually in the existing plugin folder. Preserve settings and logs. Remove an old installed `source` subfolder with DH closed; do not leave duplicate plugin copies.

Use the stopwatch panel to set font size, preview and move the HUD. Move unlocks dragging for 20 seconds. Close the panel during play; the independent timer remains. Test a real reaction after testing the preview. If names cannot be resolved, trigger the temporary spike, refresh the effects list, select it, assign its reaction and save the DID override.

## Development and release

The complete setup and workflow explanation is in [GITHUB.md](../../GITHUB.md). Upload repository contents at the root, including `.github`. Windows builds and Linux cross-builds use the .NET 10 SDK targeting .NET 8 and the compatible DH SDK; Python 3 runs the version-policy and SDK verification checks. The source build script modifies version stamps in the working copy. Normal Release plugin builds leave only the installer ZIP in their output directory; Debug builds also retain the DLL, supporting files and PDB. See [development](DEVELOPMENT.md) for Linux commands and output paths.

```powershell
python -m unittest discover -s ci -p test_*.py -v
dotnet run --project tests/ReactionTimer.Tests.csproj -c Release -- --contracts dist/contracts.json
.\build.ps1 -DungeonHelperDir "$env:APPDATA\Dungeon Helper" -Version 1.0.0
```

The first successful stable release is 1.0.0. Publication requires changes to plugin C# files under src since the last stable release: additions, edits, renames and deletions count. Documentation, tests, CI/build scripts, project configuration and metadata alone still run checks and produce build artifacts, but do not publish or bump the version. Manual workflow runs follow the same rule. Comparing the full unreleased range preserves pending code changes after failed builds; fully reverted source changes do not qualify. Exact tagged-commit retries can resume interrupted drafts.

For eligible code changes, changed/removed compatibility observations or removed feature observations request major; additions or feature behavior changes request minor; otherwise patch. A failing test blocks release. `feat:` and breaking-change declarations in commits touching plugin code can raise the result; documentation-only messages cannot. Contracts cover selected behavior, not all possible semantic changes. Branch/PR builds produce artifacts; only successful eligible default-branch runs publish. Keep released `contracts.json` assets intact.

The CI SDK download is hash-pinned. If the official MSI changes, review and update the lock deliberately. Do not bypass the hash or substitute the newest SDK to make a build green.

## Diagnose by symptom

| Symptom | First checks |
| --- | --- |
| Installed but no plugin icon | DH log, requested SDK assembly version, duplicate DLLs, runtime-only archive contents |
| Already installed / update refuses | Choose overwrite if offered; otherwise close DH/DDO and replace runtime files manually |
| Toolbar or buttons clipped | Record Windows display scaling, installed plugin version and screenshot; check `FitToolbarWidth` and layout smoke test |
| HUD missing | Show timer / Preview, connection and foreground state, capture status; compare LS off versus on |
| Two HUD copies under LS | Confirm updated independent HUD, close configuration panel, record capture mode and affinity status |
| Wrong or absent countdown | Compare preview with live effect; export effect diagnostics and inspect DID, duration, pause/refresh events |
| Font changes after save | Reopen settings and inspect saved `FontSizePixels`; reproduce 20 and 30 independently |
| CI fails fetching SDK | Confirm the upstream installer and hashes; retain the compatible assembly requirement |
| CI cannot find baseline contract | Restore the release asset or resume the incomplete draft; do not reset version history to evade it |

## Windows integration check

With LS off, open Preview and check transparent text, exact font save/reopen, movement, panel close/reopen and toolbar visibility. Trigger real spikes, then exercise refresh, pause, early removal and logout. Enable LS and repeat independently for WGC and DXGI; record LS version, Windows version/display scale, capture-exclusion status and observed copy count. Switch to an unrelated application to check hiding. Stop DH and confirm the HUD closes.

Do not disable Crowd Source Collector to fix timer state or enable it to obtain effect data; it is not a timer dependency. Exported local diagnostics and DH logs are the relevant evidence. Human explanations of Collector are in [References](REFERENCES.md); agent counterpart: [Workflow](../agents/WORKFLOW.md).
