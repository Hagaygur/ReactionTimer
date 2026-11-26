# Repository instructions for coding agents

This file applies to the entire repository. Read the task-specific documents below before changing the associated code. Explicit user instructions take precedence over these project guidelines. Human documentation starts at [docs/README.md](docs/README.md).

## Objective and current baseline

Maintain a compact, transparent DDO Alchemist Reaction Spike HUD running inside Dungeon Helper with no RTSS or separate timer executable, supporting both:

1. **Dungeon Helper 4 (`src/`, `net8.0-windows`)** against pinned **VoK.Sdk 4.2.1.0** (`plugins/ReactionTimer/` 3-file ZIP, independent click-through GDI+ DIB HUD with `WDA_EXCLUDEFROMCAPTURE` for Lossless Scaling and `IsWineRuntime` Wine/Proton fallbacks).
2. **Dungeon Helper 5 Beta (`src-dh5/`, `net10.0` `x64`)** against pinned **VoK.Sdk 5.3.0.0** (`Alch Reaction Timer`, signed archive-root 8-entry ZIP, in-game `ImGuiNET` overlay, zero `System.Windows.Forms`/`System.Media` refs).

Build with the .NET 10 SDK (`global.json`). Both projects share portable core logic (`TimerState.cs`, `ReactionMatcher.cs`, `Settings.cs`, `HudPresentation.cs`, `UiTheme.cs`) via `<Compile Link>` without duplicating business logic.

## Read by task

| Task | Required agent guide | Human counterpart |
| --- | --- | --- |
| Understand requirements and prior regressions | [Context](docs/agents/CONTEXT.md) | [Background](docs/human/BACKGROUND.md) |
| Change C# behavior, UI or native drawing | [Code map and invariants](docs/agents/CODE.md) | [Architecture](docs/human/ARCHITECTURE.md) |
| Configure solution, IDE tasks or debugging | [Development](docs/agents/DEVELOPMENT.md) | [IDE setup](docs/human/DEVELOPMENT.md) |
| Build, test, package, version, or troubleshoot | [Execution playbook](docs/agents/WORKFLOW.md) | [Operations](docs/human/OPERATIONS.md) |
| Research DH, SDK, Collector or Windows APIs | [Research guidance](docs/agents/REFERENCES.md) | [References](docs/human/REFERENCES.md) |

## Essential constraints

- **DH 4 installer ZIP (`ReactionTimer-<ver>-dh4-plugin.zip`):** exactly three runtime files under `plugins/ReactionTimer/`, DLL first. Never add documentation, source, tests, or SDK DLLs.
- **DH 5 installer ZIP (`ReactionTimer-<ver>-dh5-plugin.zip`):** archive-root canonical entries only (no `plugins/` prefix, no `./`, `\`, PDBs, SDK DLLs, or secrets). Signed releases contain exact 8 entries (`Alch Reaction Timer.entitlement`, `.entitlement.sig`, `.manifest`, `.manifest.sig`, `.pem`, `VoK.ReactionTimer.dll`, `.deps.json`, `plugin.metadata`).
- **Secret hygiene:** NEVER copy, commit, print, log, or package `*.key` or `*.pfx` files. Only the public entitlement pair (`ci/Alch Reaction Timer.entitlement` and `.sig`) belongs in git. `ci/sign_dh5.py` uses `tempfile.TemporaryDirectory()` cleaned up in `finally:`.
- **Non-blocking SDK usage:** Keep SDK callbacks short. `TimerState` is pure. On DH 4, native windows belong to the HUD STA thread. On DH 5, never call `.Result`/`.GetAwaiter().GetResult()` inside `ClientFrameStep()`, `ImGui_Render()`, or under locks; run `TickAsync()` asynchronously and override `WindowVisible` with a real backing property (default `false`).
- **HUD & contract preservation:** Keep `FontSizePixels` exact (`10–160`), `(1,1)` corner pixel behavior in `HudWindow.Render` on `Expired`, `HudPresentation.Text(...)` compatibility strings, and all existing `ContractSnapshot.cs` `compatibility.*` keys so SemVer stays `minor` unless intentionally breaking.
- **Quick-Cast safety:** Default `QuickRespikeEnabled = false`, `QuickRespikeBar = 0`, `QuickRespikeSlot = 0`. Never auto-cast without explicit user configuration.
- **Evidence accuracy:** Preserve the distinction between executed unit/smoke/`TrustProbe` tests and untested live DDO/Wine/LS gameplay. Never label compilation or synthetic smoke tests as live DDO integration verification.

## Commands from repository root

```powershell
python -m unittest discover -s ci -p test_*.py -v
dotnet run --project tests/ReactionTimer.Tests.csproj -c Release -- --contracts dist/contracts.json
./ci/Get-Sdk.ps1
.\build.ps1 -DungeonHelperDir $env:DH_SDK_DIR -Dh5SdkDir $env:DH5_SDK_DIR -Version 1.1.0 -AllowUnsigned
dotnet run --project tests/WindowsSmoke/WindowsSmoke.csproj -c Release -- "src/bin/Release/net8.0-windows/ReactionTimer-1.1.0-dh4-plugin.zip" "$env:DH_SDK_DIR"
dotnet run --project tests/Dh5Smoke/Dh5Smoke.csproj -c Release -- "dist/ReactionTimer-1.1.0-dh5-plugin.zip" "$env:DH5_SDK_DIR"
```

## Documentation and completion

Update both human and agent docs plus `README.md` (`Settings` table checked by `ci/test_version.py`) whenever settings, commands, build steps, or verified evidence change. Keep `ci/package.py` covering every tracked file in `git ls-files` while keeping runtime ZIPs strictly minimal.
