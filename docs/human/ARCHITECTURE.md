# Code architecture

## Dual-target layout

Shared pure C# domain logic (`TimerState.cs`, `ReactionMatcher.cs`, `Settings.cs`, `HudPresentation.cs`, `UiTheme.cs`) lives in `src/` and is linked directly into `src-dh5/` via `<Compile Link="Shared/..." />` and into `tests/ReactionTimer.Tests.csproj` with zero duplicated state-machine logic.

| Component | DH 4 (`src/`, `net8.0-windows`) | DH 5 Beta (`src-dh5/`, `net10.0` `x64`) |
| --- | --- | --- |
| Plugin entry | `ReactionTimerPlugin.cs` (`IDdoPlugin`, `IPluginUI`) | `Dh5Plugin.cs` (`VoK.Sdk.IPlugin`, `[PluginId]`, `[PluginIcon("Icon.png")]`) |
| Engine | `TimerEngine` (10 Hz sync worker, `InTown()`, `GetActiveEffects()`) | `Dh5TimerEngine` (non-reentrant `TickAsync()`, `InTownAsync()`, `GetCurrentCharacterAsync()`, `IActiveEffect.Expiration`, `IReadOnlyList<IProperty>`, `InvokeHotbar`, `/alch` commands) |
| UI & HUD | `ReactionTimerUi.cs` + `SettingsDialog.cs` + STA GDI+ DIB `HudWindow.cs` / `NativeHud.cs` | `Dh5ReactionTimerUi.cs` (`ImGuiNET` in-frame DX10/11 rendering, Compact Pill or Single-Arc Radial Donut, SP micro-bar, 20s interactive drag/resize/wheel) |

## State, time & DDO Alchemist behavior

Effect instance IDs identify active applications; definition IDs (DIDs) identify the kind of effect and key saved `EffectMappings` overrides. Automatic spike recognition requires `"spike"` plus `"reaction"`, `"Pyrite"`, `"Orchidium"`, or `"Verdanite"` with `duration <= 30s`. Persistent base Reactions (`duration < 0` or `> 30s`) are tracked separately via `ReactionMatcher.MatchBaseStance` to display the next-primer hint when a spike expires (`Pyrite -> Ceruleite`, `Orchidium -> Gildleaf`, `Verdanite -> Crimsonite`).

When `SuppressAlertsInTown` is enabled, `InTown`/`InTownAsync()` or `effect.IsPaused` suppresses `RE-SPIKE` flashing and expiry audio, showing calm `TOWN  PAUSED` / `TOWN  RESTING` instead.

## Threading, rendering & persistence

- **DH 4:** `HudController` owns a dedicated WinForms STA message loop, reading immutable `TimerView` snapshots via `Volatile`. `HudWindow` reuses a premultiplied-alpha DIB surface, caches per-pixel-size `TabularMetrics` (integer `LineHeight`, fixed digit/dot/tilde cell widths, constant `>= 2` integer columns) and commits `UpdateLayeredWindow` (`Present`) before syncing `ClientSize`/`Location`, renders an optional 6px analog depletion bar (`ShowDepletionBar`) with a `WarningSeconds` tick mark, applies `IsWineRuntime` (`ntdll!wine_get_version`) guards for `SetWindowDisplayAffinity`/`GetForegroundWindow`, and preserves `(1,1)` corner pixel contracts during `RE-SPIKE`.
- **DH 5:** `Dh5Plugin.ImGui_Render()` draws inside DDO's DX10/11 frame using Dear ImGui 1.92 dynamic font sizing (`PushFont`/`AddText`) and cached `TabularMetrics` across both `CompactPill` and `RadialDonut` (`0 px` jitter across `12.0 -> 0.0`, estimated `~`, and `>= 100s`), rendering the always-on HUD every frame and the 4-tab Material Dark config window (`HUD | Spikes | Quick-Cast | Diagnostics`) when `WindowVisible` is true.
- **Persistence:** `SettingsStore` writes `reaction-timer.json` atomically via `.tmp` + `File.Move(..., overwrite: true)` in the DH-supplied plugin folder (fallback `%APPDATA%\Dungeon Helper\plugins\ReactionTimer` on DH 4, `%LocalAppData%\DungeonHelperData\plugins\Alch Reaction Timer` on DH 5). Defaults: `FontSizePixels = 28`, `WarningSeconds = 2`, `RespikeAlertSeconds = 5`, `SuppressAlertsInTown = true`, `ShowDepletionBar = true`, `Theme = UiTheme.EberronGold`, `HudMode = CompactPill`, `HudOpacity = 0.90`, `ShowSpellPointsBar = true`, `QuickRespikeEnabled = false`, `QuickRespikeBar = 0`, `QuickRespikeSlot = 0`.

Agent counterpart: [Code map and invariants](../agents/CODE.md).
