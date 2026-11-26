# Build, release, signing and troubleshooting

## Install and use

Download the runtime archive for your host (never install `ReactionTimer-X.Y.Z-source.zip`). Both Dungeon Helper 4 and Dungeon Helper 5 Beta are tested up through `v1.1.1`; later versions after `v1.1.1` are tested on Dungeon Helper 5 only:

- **DH 4 (`ReactionTimer-X.Y.Z-dh4-plugin.zip`):** install via **Settings → Add plugin from zip file**. Contains only `plugins/ReactionTimer/VoK.ReactionTimer.{dll,deps.json,metadata}`.
- **DH 5 Beta (`ReactionTimer-X.Y.Z-dh5-plugin.zip`):** install via **Plugins → Install from zip**. Signed for `Alch Reaction Timer` with all 8 root files (`VoK.ReactionTimer.dll`, `.deps.json`, `plugin.metadata`, `.pem`, `.manifest`, `.manifest.sig`, `.entitlement`, `.entitlement.sig`).

## Development and release

See [GITHUB.md](../../GITHUB.md) for the full CI pipeline. Both `src/**/*.cs` and `src-dh5/**/*.cs` qualify for releases; `build.yml` resolves the latest release carrying `contracts.json` (`--base`) while passing the highest merged tag as `--tag-floor` so orphan tags like `v1.0.1` do not block releases or collide.

Local commands on Windows:

```powershell
python -m unittest discover -s ci -p test_*.py -v
dotnet run --project tests/ReactionTimer.Tests.csproj -c Release -- --contracts dist/contracts.json
./ci/Get-Sdk.ps1
.\build.ps1 -DungeonHelperDir $env:DH_SDK_DIR -Dh5SdkDir $env:DH5_SDK_DIR -Version 1.1.1 -AllowUnsigned
```

## Diagnose by symptom

| Symptom | First checks |
| --- | --- |
| DH 5 `[NOT AUTHORIZED]` | Install `ReactionTimer-X.Y.Z-dh5-plugin.zip` (not unsigned/source ZIP), confirm `PluginId` `fc0b3f68-...` and all 8 root files |
| DH 4 plugin missing or update locked | Close DDO/DH and replace `plugins/ReactionTimer/` runtime files; verify `VoK.Sdk 4.2.1.0` |
| HUD missing or duplicated under LS (DH 4) | Check `Capture exclusion: ON` (or Wine fallback note), close settings panel, test WGC vs DXGI |
| `RE-SPIKE` flashing in town/shrine | Ensure `SuppressAlertsInTown` is `true` |
| CI fails comparing contracts | Ensure `build.yml` scans released tags for `contracts.json` and passes `--tag-floor` |

Agent counterpart: [Workflow](../agents/WORKFLOW.md).
