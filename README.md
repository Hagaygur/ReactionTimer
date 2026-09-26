# Documentation map

The project has two documentation tracks. Human guides explain how and why the system works. Agent guides turn the same project knowledge into working instructions, file pointers and checks. Both describe the same code and must be maintained together.

| Topic | For humans | For AI coding agents |
| --- | --- | --- |
| Goals, constraints, development history and evidence | [Background](human/BACKGROUND.md) | [Context](agents/CONTEXT.md) |
| Code organization, state, threading and drawing | [Architecture](human/ARCHITECTURE.md) | [Code map](agents/CODE.md) |
| Visual Studio, VS Code and debugging | [Development](human/DEVELOPMENT.md) | [Development instructions](agents/DEVELOPMENT.md) |
| Installation, troubleshooting, testing and releases | [Operations](human/OPERATIONS.md) | [Workflow](agents/WORKFLOW.md) |
| Dungeon Helper, SDK, Crowd Source Collector and API sources | [References](human/REFERENCES.md) | [Research guidance](agents/REFERENCES.md) |

Start at [README.md](../README.md) to use the plugin, [GITHUB.md](../GITHUB.md) to set up CI, or [AGENTS.md](../AGENTS.md) to brief an agent. `AGENTS.md` is ordinary Markdown in a recognized open agent-instruction format; discovery varies by tool. If an assistant does not load it automatically, explicitly ask it to read the file and follow its linked guides.

Documentation baseline: **2026-09-27**, source version 1.0.0. This date records documentation preparation, not proof that a GitHub release or live integration test has run.

# Alchemist Reaction Timer 1.0.0

A Dungeon Helper plugin for DDO's Alchemist Reaction Spikes. Runs entirely inside Dungeon Helper; no external timer application or separate service. The installer ZIP contains only the three runtime plugin files. Full source and executable logic tests are delivered separately in DDO-Reaction-Timer-source.zip.

## Developer documentation

Open **ReactionTimer.sln** in Visual Studio, or the repository folder in VS Code. Start with the [documentation map](docs/README.md), [IDE setup](docs/human/DEVELOPMENT.md), or [AGENTS.md](AGENTS.md) for AI coding assistants. Human and agent guides cover matching topics, including DH and Crowd Source Collector references.

## Automatic builds

See [GITHUB.md](GITHUB.md) for the included GitHub Actions workflow: first stable release 1.0.0, contract-based semantic versions, Windows build checks and separate plugin/source release assets. Upload the repository contents, including `.github`, at your GitHub repository root.


## Overlay features

- **Independent transparent HUD:** the countdown now uses its own top-level, click-through window. Only outlined text is drawn during normal play: no title bar, panel background, progress bar or large empty container.
- **Lossless Scaling approach:** the HUD requests Windows `WDA_EXCLUDEFROMCAPTURE`, verifies the setting, and stays above the scaling window without activating itself. This is intended to prevent a captured, enlarged duplicate while retaining the original text above LS. It avoids relying on DH's hosted panel being present in WGC's captured image. Actual compatibility needs testing on your Windows/LS setup.
- **Exact font-size input:** replaces the old 60–180% setting with integer pixels, 10–160. Entering 20 or 30 saves that exact value. Invalid input shows an error instead of silently substituting a size. Old effect mappings are retained; the new font default is 28px.
- **Directly installable ZIP:** the first archive entry is `plugins/ReactionTimer/VoK.ReactionTimer.dll`. Only the three runtime files are included; there is no nested ZIP or source directory.
- Changing appearance no longer resets an active timer.

## Install or update

1. Close DDO. In Dungeon Helper, choose **Settings → Add plugin from zip file** and select **DDO-Reaction-Timer-DH-only.zip directly**.
2. If prompted because ReactionTimer already exists, choose **Yes** to overwrite.
3. Fully exit DH from its tray icon, restart it, then launch DDO.
4. Click the amber stopwatch icon. The plugin is version **1.0.0.0**.

If DH refuses the update or a file is locked, fully close DDO and DH first. Extract the ZIP and copy the contents of `plugins/ReactionTimer` into the existing `%APPDATA%\Dungeon Helper\plugins\ReactionTimer\` folder (or your actual installation path), replacing files. If an older package installed a `source` subfolder inside ReactionTimer, remove that subfolder while DH is closed. Keep existing settings and logs. Do not leave another copy of the plugin DLL in a second plugin folder.

The plugin is compiled against **VoK.Sdk 4.2.1.0**, matching the SDK from the public DH installer used to diagnose your earlier load failure. No SDK DLL or .NET runtime is redistributed. Do not replace DH's own SDK.

## Configure and play

Open the stopwatch control panel and click **Settings / effects** to set **Font size (px)**; try 20 or 30. Click **Preview** for a marked 12-second demonstration followed by RE-SPIKE. **Move (20s)** temporarily enables dragging and shows a small translucent drag plate. After 20 seconds, the plate disappears and the HUD becomes click-through again. Position is saved relative to the game monitor.

Close the DH control panel while playing: the HUD remains active. The control panel is only for configuration. **Hide timer / Show timer** toggles the HUD; Preview and Move temporarily show it even when hidden. The HUD normally appears while DDO, DH or Lossless Scaling has focus and hides on unrelated applications or logout. The default sound is off.

The timer shows Pyrite in orange, Orchidium in purple and Verdanite in green. It turns red near expiry and shows **RE-SPIKE** at expiry/removal. Paused effects show **PAUSED**. Unknown remaining time shows **ACTIVE**; estimated times are marked `~`. The timer handles effect refreshes, pauses, character changes and overlapping effects.

## Lossless Scaling check

With LS off, click Preview and confirm the transparent text is visible. Check the control-panel status for **Capture exclusion: ON**. Then test Preview with LS on, separately in WGC and DXGI, closing the control panel during each check. The intended result is one unscaled copy of the text.

Windows accepting the capture-exclusion setting does not prove LS honors it. If it still disappears or duplicates, provide the status text, LS version and capture mode, and whether the preview works with LS off. If status reports failure, include the Windows error number. The `0x11` exclusion mode requires Windows 10 version 2004 or newer. Other Dungeon Helper panels and toolbars are not changed and can still exhibit their original capture behavior.

## If a real spike is not recognized

Automatic detection requires “spike” plus “reaction”, “Pyrite”, “Orchidium” or “Verdanite” in the effect name; permanent and long-lived effects are excluded. Exact effect identifiers have not been captured from a live game for this build.

In **Settings / effects**, trigger a spike, click **Refresh**, select its temporary effect, choose its reaction, click **Track selected**, then **Save**. Recently seen effects remain listed for 60 seconds. Select the temporary spike, not the permanent reaction. **Clear override** restores automatic detection for that definition.

**Export diagnostics** writes effect names, IDs, durations and settings to `reaction-timer-effects.json` in the plugin data directory and shows its path. The plugin makes no network requests and sends no game inputs.

## Verification and limits

- Compiled with warnings treated as errors against SDK 4.2.1.0 and .NET 8 Windows Desktop references.
- **44 executable checks passed**, including timer state, detection filters, exact 20/30 font save/reopen, invalid input rejection, migration of old settings and monitor position calculations.
- Passed assembly type discovery, public plugin construction and UI contract creation using the actual SDK and Windows managed assemblies from DH's public installer.
- Installer ZIP checked for exactly the three runtime entries, valid CRCs and byte-identical DLL after extraction.
- SDK 10.0.401 solution builds pass on Windows and Ubuntu WSL, including Debug/Release packaging. Visual Studio MSBuild and the synthetic Windows toolbar/button layout smoke test pass. All 44 C# and 23 Python checks pass on both operating systems.

## Implementation and rebuilding

Thin SDK callbacks enqueue events. A non-overlapping worker updates timer state at 10 Hz and reconciles active effects every second. The HUD has a dedicated STA message loop, reads immutable snapshots and renders only changed text/settings while visible. It reuses a premultiplied-alpha DIB surface until dimensions change and disposes native drawing resources on shutdown. Settings use atomic replacement; position saves are debounced. SDK handlers become inert after teardown because this SDK has no handler unregistration API.

Extract the separate **DDO-Reaction-Timer-source.zip** and open the extracted repository root. On Windows with the .NET 10 SDK:

```powershell
.\build.ps1
# For a nonstandard installation:
.\build.ps1 -DungeonHelperDir 'D:\Apps\Dungeon Helper'
```

Normal Visual Studio and `dotnet build` builds create the installer with the DLL first. Release leaves only the ZIP in `src/bin/Release/net8.0-windows/`; Debug retains the DLL, deps.json, metadata and PDB alongside the ZIP in `src/bin/Debug/net8.0-windows/`. The script additionally runs the logic checks and copies the Release ZIP to `dist/ReactionTimer-1.0.0-plugin.zip`. It does not install automatically. See [Linux cross-compilation](docs/human/DEVELOPMENT.md#linux-cross-compilation) for Linux builds; the plugin still runs only on Windows.

References: [SetWindowDisplayAffinity](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setwindowdisplayaffinity), [extended window styles](https://learn.microsoft.com/en-us/windows/win32/winmsg/extended-window-styles), [Dungeon Helper](https://dungeonhelper.com/).

Unofficial community plugin; not an official Dungeon Helper or Standing Stone Games release.
