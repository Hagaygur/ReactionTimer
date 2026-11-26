# Background and requirements

The timer tracks DDO Alchemist Reaction Spikes (**Pyrite**, **Orchidium**, **Verdanite**) and tells the player when a spike is expiring or which Spell Primer (**Crimsonite**, **Gildleaf**, **Ceruleite**) breaks their current stance to re-spike.

## Why dual DH 4 + DH 5 Beta builds exist

| Observed boundary | Resulting design decision |
| --- | --- |
| DH 4 uses `.NET 8 Windows` + WinForms (`IDdoPlugin`/`IPluginUI`) and `plugins/ReactionTimer/` ZIPs | Keep `src/` (`net8.0-windows`) targeting pinned `VoK.Sdk 4.2.1.0`, packaged as `*-dh4-plugin.zip` |
| DH 5 Beta injects `.NET 10 x64` in-process into DX10/11 DDO (`VoK.Sdk.IPlugin` + `ImGuiNET`), drops WinForms, and enforces `PluginTrustGate` | Add `src-dh5/` (`net10.0`, `x64`) sharing portable core logic (`src/*.cs`) packaged & signed at archive root as `*-dh5-plugin.zip` |
| Orphan `v1.0.1` tag lacked release assets and broke CI baseline download | Scan released tags descending for `contracts.json` while bumping above `--tag-floor` (`v1.1.0`) |
| Bare countdown digits force foveal saccades; towns/shrines triggered false `RE-SPIKE` flashes | Add analog 6px depletion bar (`ShowDepletionBar`), `SuppressAlertsInTown`, base-stance next-primer hints, and 3 CVD-safe M3 themes (`UiTheme`) |

## Evidence scope

Automated verification covers compilation with `TreatWarningsAsErrors=true`, portable C# behavioral/contract checks, Python policy/SDK/signing/README sync tests, DH 4 `WindowsSmoke`, DH 5 `Dh5Smoke`, `PluginTrustGate` / `DungeonHelper.App` `InstallFromZip` checks, and Ubuntu cross-compilation/smoke checks in GitHub Actions CI. Live DDO gameplay on Windows, Lossless Scaling WGC/DXGI matrices, and live Linux Wine/Proton gameplay remain separate user integration checks.

See [Operations](OPERATIONS.md) for verification commands and [References](REFERENCES.md) for academic and SDK sources. Agent counterpart: [Context](../agents/CONTEXT.md).
