# Agent task context

Human counterpart: [Background](../human/BACKGROUND.md). Repository instructions: [AGENTS.md](../../AGENTS.md).

## Required outcome

Maintain a DH-only, compact, transparent DDO Alchemist Reaction Spike HUD across **DH 4 (`4.2.1.0`)** and **DH 5 Beta (`5.3.0.0`)** without RTSS or separate timer executables. Preserve exact pixel font settings (`10–160`), `ContractSnapshot.cs` `compatibility.*` stability, zero-twitch tabular countdown layout, runtime-only installation ZIPs, and strict private-key hygiene (`*.key` / `*.pfx` never committed or packaged). Note the support cutoff: tested on both DH 4 and DH 5 Beta up through `v1.1.1`, with later versions after `v1.1.1` tested on DH 5 only.

## Historical evidence to retain

- Earlier SDK 4.3.4 dependency failed against DH 4 (`4.2.1.0`). Pin both DH 4 (`4.2.1.0`) and DH 5 (`5.3.0.0`) in `ci/sdk-lock.json` by SHA-256.
- DH 4 panel vanished under WGC and duplicated under DXGI -> independent click-through HWND with `WDA_EXCLUDEFROMCAPTURE`. DH 5 Beta renders in-frame via `ImGuiNET` inside DX10/11 (`Microsoft.NETCore.App 10.0.0` only; no WinForms/WPF).
- Single universal plugin ZIP is empirically impossible across DH 4 vs DH 5 Beta (`IDdoPlugin`/`IPluginUI` removed in `VoK.Sdk 5.3.0`, `IActiveEffect.Expiration`, `IReadOnlyList<IProperty>`, archive-root layout + mandatory `PluginTrustGate`). Ship dual runtime ZIPs (`*-dh4-plugin.zip` and signed `*-dh5-plugin.zip`).
- Orphan git tag `v1.0.1` has no GitHub Release assets -> `build.yml` + `ci/version.py --tag-floor` resolves the latest released `contracts.json` (`v1.0.0`) while bumping above the highest merged tag (`v1.0.1` -> `1.1.0`).
- Update evidence ledgers strictly from executed commands with exact counts and environments; never claim compilation or smoke tests prove live DDO/LS/Wine gameplay.
