# Agent code map and invariants

Human counterpart: [Architecture](../human/ARCHITECTURE.md). All paths below are repository-relative.

| Change | Inspect first | Preserve |
| --- | --- | --- |
| Lifecycle / DH 4 SDK events | `src/ReactionTimerPlugin.cs` | Stable ID/key (`f8d154e8-...`), short callbacks, queue cap, inert callbacks after disposal |
| Lifecycle / DH 5 SDK (`IPlugin`) | `src-dh5/Dh5Plugin.cs`, `src-dh5/Dh5TimerEngine.cs` | `[PluginId("fc0b3f68-...")]`, `[PluginName("Alch Reaction Timer")]`, real `WindowVisible` backing property (default `false`), non-blocking `TickAsync()` |
| Timing / pause / overlap / town | `src/TimerState.cs`, `tests/Program.cs` | Monotonic inputs, snapshot no-restart, same-ID refresh, single expiry alarm, `BaseStance`/`InTown` init-only compatibility |
| Detection & next-primer hints | `src/ReactionMatcher.cs` | Instance ID differs from DID; explicit mappings precede auto-filter; `MatchBaseStance` (`<0` or `>30s`) + `NextPrimerFor` |
| Settings & 3 DDO × M3 themes | `src/Settings.cs`, `src/UiTheme.cs`, `src/SettingsDialog.cs` | Exact 10–160px input; atomic writes; `UiTheme` (`EberronGold`, `CannithArcane`, `FeywildTwilight`); `QuickRespikeEnabled=false`/`0,0` default |
| DH 4 panel & GDI+ HUD | `src/ReactionTimerUi.cs`, `src/HudController.cs`, `src/HudWindow.cs`, `src/NativeHud.cs` | Toolbar width (`360px`/`FitToolbarWidth`), STA ownership, `IsWineRuntime` fallback, `(1,1)` corner pixel contract on `Expired`, `HudPresentation.Text` strings, `TabularMetrics` fixed-width cells & atomic `Present` before `ClientSize`/`Location` sync |
| DH 5 ImGui HUD & tabs | `src-dh5/Dh5ReactionTimerUi.cs` | Zero WinForms/System.Media refs; matched `Begin`/`End` and `Push`/`Pop` across all 3 `UiTheme` palettes; `CompactPill` & `RadialDonut` zero-jitter `TabularMetrics`; `InvokeHotbar` gated by user config |

Worker access to `TimerState` stays under the engine gate. Render paths consume immutable `TimerView` rather than calling the SDK. On DH 5, `InTownAsync()` and `GetCurrentCharacterAsync()` run on `Dh5TimerEngine`'s async background loop — NEVER call `.Result` or `.GetAwaiter().GetResult()` inside `ClientFrameStep()`, `ImGui_Render()`, or under locks.

Top-level DH 4 HUD display affinity (`WDA_EXCLUDEFROMCAPTURE`) is requested and read back on handle creation on native Windows (`IsWineRuntime == false`), and skipped gracefully on Wine/Proton. Do not infer LS compatibility from Windows API acceptance alone.
