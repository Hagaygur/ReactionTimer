# Repository instructions for coding agents

This file applies to the entire repository. Read the task-specific documents below before changing the associated code. Explicit user instructions take precedence over these project guidelines. Human documentation starts at [docs/README.md](docs/README.md).

## Objective and current baseline

Build a small, transparent Alchemist Reaction Spike HUD for Dungeons & Dragons Online, running inside Dungeon Helper (DH), with no RTSS or separate timer executable. The HUD must remain usable with Lossless Scaling (LS). The stable release baseline is 1.0.0; GitHub Actions chooses later versions. Build with the .NET 10 SDK; the runtime target remains .NET 8 Windows and the pinned **VoK.Sdk 4.2.1.0**, not the newest NuGet package.

## Read by task

| Task | Required agent guide | Human counterpart |
| --- | --- | --- |
| Understand requirements and prior regressions | [Context](docs/agents/CONTEXT.md) | [Background](docs/human/BACKGROUND.md) |
| Change C# behavior, UI or native drawing | [Code map and invariants](docs/agents/CODE.md) | [Architecture](docs/human/ARCHITECTURE.md) |
| Configure solution, IDE tasks or debugging | [Development](docs/agents/DEVELOPMENT.md) | [IDE setup](docs/human/DEVELOPMENT.md) |
| Build, test, package, version, or troubleshoot | [Execution playbook](docs/agents/WORKFLOW.md) | [Operations](docs/human/OPERATIONS.md) |
| Research DH, SDK, Collector or Windows APIs | [Research guidance](docs/agents/REFERENCES.md) | [References](docs/human/REFERENCES.md) |

## Essential constraints

- Installer ZIP: exactly three runtime files under `plugins/ReactionTimer/`, DLL first. Never add documentation, source, tests or SDK DLLs to that ZIP. Put them in the separate source archive.
- Keep SDK callbacks short. TimerState is pure; SDK state is processed by TimerEngine. Native windows belong to the HUD STA thread.
- Keep the HUD separate from DH's hosted configuration panel. Retain transparency, click-through behavior and capture exclusion. Do not infer LS success from an accepted Windows API call.
- Font size is an exact integer pixel value. Saving 20 or 30 must preserve it. Ordinary appearance changes must not reset active effects.
- Keep the stable plugin ID/key and compatible saved settings unless an intentional migration is part of the task.
- The timer reads local SDK data. Crowd Source Collector is a separate optional DH component, not this plugin's event provider or a required cloud service. No Collector integration is currently implemented.
- A failing test blocks release; it does not justify bumping a version or weakening an assertion. Version contracts are observations of passing behavior, not a universal feature detector.
- Preserve the distinction between user reports, executed tests and untested Windows/LS behavior. Never label compilation as live integration verification.

## Commands from repository root

```powershell
python -m unittest discover -s ci -p test_*.py -v
dotnet run --project tests/ReactionTimer.Tests.csproj -c Release -- --contracts dist/contracts.json
.\build.ps1 -DungeonHelperDir "$env:APPDATA\Dungeon Helper" -Version 1.0.0
dotnet run --project tests/WindowsSmoke/WindowsSmoke.csproj -c Release -- src/bin/Release/net8.0-windows/ReactionTimer-1.0.0-plugin.zip "$env:APPDATA\Dungeon Helper"
```

Run checks relevant to the change. The solution cross-compiles on Linux with the compatible SDK; only smoke execution requires Windows. See the development guide for Linux SDK acquisition. Every plugin build creates the installer: Release leaves only the ZIP in its output directory, while Debug retains DLL, deps, metadata and PDB alongside the ZIP. `build.ps1` modifies assembly/metadata versions in the working tree; inspect these changes before committing.

## Documentation and completion

Update the human and agent counterpart when behavior, architecture, commands, limitations or verified evidence changes. Keep project facts in agreement; do not blindly copy a claimed test result into both. `ci/package.py` must include `docs/` and `AGENTS.md` in source packages while keeping plugin packages runtime-only. State what changed, what ran, what remains unverified, and the expected version impact in the handoff. Do not introduce environment-specific local paths, user logs or downloaded SDK binaries into the repository.
