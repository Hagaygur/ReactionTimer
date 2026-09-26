# Agent code map and invariants

Human counterpart: [Architecture](../human/ARCHITECTURE.md). All paths below are repository-relative.

| Change | Inspect first | Preserve |
| --- | --- | --- |
| Lifecycle / SDK events | `src/ReactionTimerPlugin.cs` | Stable ID/key, short callbacks, queue cap, inert callbacks after disposal |
| Timing / pause / overlap | `src/TimerState.cs`, `tests/Program.cs` | Monotonic inputs, snapshot no-restart, same-ID refresh, single expiry alarm |
| Detection | `src/ReactionMatcher.cs`, engine `Match`/`NameFor` | Instance ID differs from DID; explicit mappings precede automatic filtering |
| Settings | `src/Settings.cs`, `src/SettingsDialog.cs` | Exact 10–160px input; atomic writes; old mappings survive; appearance does not reset timers |
| DH panel | `src/ReactionTimerUi.cs` | Toolbar width before host measurement; autosized button rows; close panel without stopping HUD |
| HUD behavior | `src/HudController.cs`, `src/HudWindow.cs` | Dedicated STA ownership; command queue; no activation; focus-aware visibility |
| Native resources | `src/NativeHud.cs` | Premultiplied alpha, restore GDI selection, dispose native handles, reuse surfaces |
| Text / coordinates | `src/HudPresentation.cs` | Pure functions; negative monitor origins; unknown timing remains explicit |

Worker access to TimerState stays under the engine gate. Render paths consume immutable TimerView rather than calling the SDK. Preserve the difference between a fresh estimated event and an already-running effect with unknown remaining time. Do not replace monotonic countdowns with wall-clock subtraction each frame.

SDK snapshot enumeration must succeed before retaining/removing tracked IDs. Name resolution failures are tolerated and retried; numeric mappings still work. UI settings reads can take the engine lock, so do not describe every UI operation as lock-free.

Top-level HUD display affinity is requested and read back on handle creation. A successful readback only confirms the Windows setting. Test LS separately. Transparent hit testing changes temporarily for movement; no permanent opaque background or focus stealing. Draw only changed text/settings while visible, and keep finite native-resource ownership.

Teardown queues HUD Stop without blocking the DH UI. Treat disposal as asynchronous. Do not call WinForms controls from the worker thread or dispose host-owned SDK services. Preserve error throttling and log rotation.

Regression focus: pause/resume, snapshot versus reapply, logout, effect overlaps, 20/30 font saves, exact installer entries and toolbar width. New behavior should gain an assertion and, where appropriate, an observation in `tests/ContractSnapshot.cs`; see [Workflow](WORKFLOW.md).
