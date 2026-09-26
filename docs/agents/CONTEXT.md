# Agent task context

Human counterpart: [Background](../human/BACKGROUND.md). Repository instructions: [AGENTS.md](../../AGENTS.md).

## Required outcome

Maintain a DH-only, compact, transparent DDO Alchemist Reaction Spike HUD that can coexist with Lossless Scaling. Preserve exact pixel font settings, efficient drawing/state handling, movable click-through display and runtime-only installation packaging. Do not add RTSS or a separate runtime executable as a routine refactor.

## Historical evidence to retain

- Earlier SDK 4.3.4 dependency failed to load against the user's DH installation. Rebuild against the verified 4.2.1 assembly solved loading by manual replacement.
- The ordinary DH panel vanished under WGC and duplicated under DXGI. The current independent HWND is an intentional response, not accidental complexity.
- Old percentage size input caused unacceptable changes; 20/30 pixel persistence now has regression checks. Only the old 60 minimum explains the 30→60 report; do not claim all original symptoms were reproduced.
- User confirmed that removing all non-plugin files from the ZIP made installation work. Keep exactly the three runtime entries even if DH's first-entry parser appears to accept more.
- User reported the independent HUD seemed to work well. A supplied screenshot showed the control panel cutting off toolbar icons and part of the Settings button. The fix measures toolbar width and replaces fixed button-row heights; live confirmation of that correction remains pending.
- The 1.0.0 repository adds CI and future semantic versions; it does not mean a public 1.0.0 GitHub release has already been published.

## Evidence ledger at handoff

Executed on 2026-09-27: SDK 10.0.401 solution builds on Windows and Ubuntu 22.04 WSL against SDK 4.2.1.0; 44 pure C# and 15 Python checks on both; Visual Studio MSBuild; Debug/Release archive/output checks; Windows synthetic layout smoke loading the 1.0.0 Release ZIP; both pinned installer acquisition scripts; actionlint 1.7.12 workflow validation; source-archive validation. Not executed here: GitHub-hosted Actions and publication, full live DDO/LS matrix. Update this ledger only from actual results, with environment and version.

No live canonical Alchemist DIDs were established. Use names plus explicit user mappings; do not invent IDs. Do not conflate DH Crowd Source Collector with the local game-data provider. Keep runtime telemetry absent unless a future task explicitly designs it.
