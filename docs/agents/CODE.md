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
| Re-spike alert | `HudAlertState` / `HudAlertFrame` in `src/HudPresentation.cs` | Monotonic, bounded expiry alert; separate preview/live state; no settings mutation |

Worker access to TimerState stays under the engine gate. Render paths consume immutable TimerView rather than calling the SDK. Preserve the difference between a fresh estimated event and an already-running effect with unknown remaining time. Do not replace monotonic countdowns with wall-clock subtraction each frame.

SDK snapshot enumeration must succeed before retaining/removing tracked IDs. Name resolution failures are tolerated and retried; numeric mappings still work. UI settings reads can take the engine lock, so do not describe every UI operation as lock-free.

Top-level HUD display affinity is requested and read back on handle creation. A successful readback only confirms the Windows setting. Test LS separately. Transparent hit testing changes temporarily for movement; no permanent opaque background or focus stealing. Draw only changed text/settings while visible, and keep finite native-resource ownership.

Teardown queues HUD Stop without blocking the DH UI. Treat disposal as asynchronous. Do not call WinForms controls from the worker thread or dispose host-owned SDK services. Preserve error throttling and log rotation.

Regression focus: pause/resume, snapshot versus reapply, logout, effect overlaps, 20/30 font saves, exact installer entries and toolbar width. New behavior should gain an assertion and, where appropriate, an observation in `tests/ContractSnapshot.cs`; see [Workflow](WORKFLOW.md).

## Re-spike visual alert

`RespikeAlertSeconds` defaults to 5; validate/copy/persist an integer in 0–60 (0 disables and cancels an active visual alert). On entry into Expired or a new alarm counter, the HUD captures a deadline, renders text at exactly twice the saved pixel size, and alternates an opaque black plate with transparency every 0.5 seconds. Text stays visible throughout. A repeated expired view or ordinary settings revision must not restart/extend the deadline. Other duration edits apply on the next expiry; reapplication, logout, waiting, pause and error cancel the alert. Do not change TimerState or the saved FontSizePixels to implement the visual effect.

Advance live alert state even while hidden or previewing. Preview has independent alert state and lasts 12 seconds plus at least the selected alert duration (minimum 3-second expired preview). Include the effective font and black-background phase in the render key; redraw only when those or existing inputs change. Restore normal size/transparency at the deadline. Keep click-through, capture exclusion and STA ownership unchanged; the temporary black plate is intentional user-requested behavior.

WindowsSmoke seeds only UI-facing engine state without an SDK worker. It checks duration save and minimum-size settings layout, then calls the actual HUD renderer and samples its native bitmap to check growth, black/transparent phases and restoration. This is synthetic rendering evidence, not a DDO/LS integration test.
