# Alchemist Reaction Timer

A Dungeon Helper plugin for **Dungeons & Dragons Online (DDO)** Alchemist Reaction Spikes. Runs inside Dungeon Helper with no RTSS or external timer executable, supporting both **Dungeon Helper 4** (`VoK.Sdk 4.2.1.0`, `.NET 8 Windows`) and **Dungeon Helper 5 Beta** (`VoK.Sdk 5.3.0.0`, `.NET 10 x64` in-game `ImGuiNET` overlay).

## Developer documentation

Open **ReactionTimer.sln** in Visual Studio or VS Code. Start with the [documentation map](docs/README.md), [IDE setup](docs/human/DEVELOPMENT.md), [GITHUB.md](GITHUB.md) for CI/signing, or [AGENTS.md](AGENTS.md) for AI coding assistants.

## DDO Alchemist mechanics

Alchemists pair three Spell Primers (**Crimsonite**, **Gildleaf**, **Ceruleite**) into persistent Reactions; transitioning into a different Reaction grants a temporary **12-second Reaction Spike**:

| Reaction Spike | Primers | Primary Role & Buff Focus | CVD Glyph & Tag | Next-Primer Hint |
| --- | --- | --- | --- | --- |
| **Pyrite** | Crimsonite + Gildleaf | DPS burst: elemental/poison Spellpower, Spell Crit, SP cost reduction | `▲ PYR` (Amber-Gold) | Cast **Ceruleite** |
| **Orchidium** | Crimsonite + Ceruleite | Survival burst: PRR + MRR / Elemental Resistance | `◆ ORC` (Arcane Violet) | Cast **Gildleaf** |
| **Verdanite** | Gildleaf + Ceruleite | Healing & CC burst: Positive/Negative Spellpower + Transmutation/Conjuration DCs | `✚ VRD` (Alchemy Emerald) | Cast **Crimsonite** |

- **Base-stance next-primer hint:** Tracks your persistent base Reaction (`duration < 0` or `> 30s`) alongside the 12s spike so when a spike expires, the HUD shows which primer breaks your stance to re-spike.
- **Town & shrine pause suppression:** In taverns/airships (`InTown`) or at rest shrines (`IsPaused`), `SuppressAlertsInTown` suppresses `RE-SPIKE` flashing and expiry sound and shows calm `TOWN  PAUSED` / `TOWN  RESTING`.

## Overlay & plugin features

- **Analog depletion bar & calm-to-urgent lifecycle:** Beneath the countdown (`PYRITE  8.4`), an optional 6px smooth depletion bar (`ShowDepletionBar`) marks `WarningSeconds` so remaining spike time can be judged peripherally without reading digits. Approaching `WarningSeconds` transitions smoothly to warning colors; expiry shows **RE-SPIKE** with bounded `<= 2 Hz` flashing for `RespikeAlertSeconds` (default `5`, `0–60`, `0` disables).
- **3 shared DDO × Material Design 3 themes (`UiTheme`):** Available on both DH 4 and DH 5 (`Theme` setting, or `/alch theme` in DH 5):
  - `EberronGold` (*Eberron Obsidian & Gold*, default): `#111318`/`#1A1D24`/`#222630`/`#2C313E`, outline `#4A5368`, primary `#E2B75B`, secondary `#56CCF2`.
  - `CannithArcane` (*House Cannith Arcane Forge*): `#0E141D`/`#151E2B`/`#1D293A`/`#27364B`, outline `#455D7A`, primary `#56CCF2`, secondary `#D4A359`.
  - `FeywildTwilight` (*Feywild Eldritch Twilight*): `#15111E`/`#1E182B`/`#282039`/`#332949`, outline `#584875`, primary `#CDA4FF`, secondary `#6EE7B7`.
- **DH 4 overlay (`net8.0-windows`):** Independent top-level, click-through GDI+ DIB window with `WDA_EXCLUDEFROMCAPTURE` (for Lossless Scaling) and `IsWineRuntime` (`ntdll!wine_get_version`) Wine/Proton fallbacks.
- **DH 5 Beta overlay (`net10.0`, `x64`, `ImGuiNET`):** Renders directly inside DDO's DX10/11 frame (no separate HWND or Lossless Scaling duplication), offering **Compact Pill + Depletion Bar** (`CompactPill`) or **Single-Arc Radial Donut** (`RadialDonut`) via `HudMode`, an Astral Blue Spell Points (`SP`) micro-bar (`ShowSpellPointsBar`), interactive 20s drag/corner-resize/wheel-font/Ctrl+wheel-opacity, `/alch` chat commands, and optional **`InvokeHotbar` Quick Re-Spike** (disabled by default until `QuickRespikeEnabled` and non-zero `QuickRespikeBar`/`QuickRespikeSlot` are configured).
- **Exact font-size input:** Integer pixels `10–160` (`FontSizePixels`, default `28`). Ordinary appearance changes do not reset active timers.

## Install or update

Choose the release archive matching your Dungeon Helper version (**never** install `ReactionTimer-X.Y.Z-source.zip` into Dungeon Helper):

### Option A: Dungeon Helper 4 (`4.2.1.0`)

1. Close DDO. In Dungeon Helper 4, choose **Settings → Add plugin from zip file** and select **`ReactionTimer-X.Y.Z-dh4-plugin.zip`**.
2. If prompted because `ReactionTimer` already exists, choose **Yes** to overwrite.
3. Fully exit DH from its tray icon, restart it, then launch DDO.
4. Click the amber stopwatch icon. Only `plugins/ReactionTimer/VoK.ReactionTimer.{dll,deps.json,metadata}` are in this ZIP.

If DH 4 refuses the update or a file is locked, fully close DDO and DH, extract the ZIP, and copy the contents of `plugins/ReactionTimer` into `%APPDATA%\Dungeon Helper\plugins\ReactionTimer\` while preserving your settings and logs.

### Option B: Dungeon Helper 5 Beta (`5.3.0.0`, `x64`)

1. Close DDO. In Dungeon Helper 5 Beta, open **Plugins → Install from zip** and select **`ReactionTimer-X.Y.Z-dh5-plugin.zip`**.
2. The archive is Authenticode- and manifest-signed for plugin **`Alch Reaction Timer`** (`fc0b3f68-e30d-4d1f-b2ac-4e5d62bb1725`, community entitlement by `Warned`) with all 8 canonical files at archive root.
3. Launch DDO in **DX10 or DX11** (`x64`). Settings are saved under `%LocalAppData%\DungeonHelperData\plugins\Alch Reaction Timer\reaction-timer.json`.

## Configure, play & DH 5 `/alch` commands

- **DH 4:** Open the stopwatch control panel (`Settings / effects`) to set `FontSizePixels`, `WarningSeconds`, `RespikeAlertSeconds`, `Theme`, `SuppressAlertsInTown`, `ShowDepletionBar`, or DID overrides. Use **Preview** (12s countdown + RE-SPIKE) and **Move (20s)** (click-through restores automatically after 20s).
- **DH 5 Beta:** Click the toolbar icon to toggle the Material-tabbed config window (`HUD | Spikes | Quick-Cast | Diagnostics`), or use in-game `/alch` chat commands:
  - `/alch status` — prints current spike, persistent base stance/next primer, and Spell Points.
  - `/alch warn <0-10>` — sets `WarningSeconds`.
  - `/alch hud [on|off|pill|donut]` — toggles HUD visibility or switches between `CompactPill` and `RadialDonut`.
  - `/alch town [on|off]` — toggles `SuppressAlertsInTown`.
  - `/alch theme <eberron|cannith|feywild>` — switches `Theme` live across `EberronGold`, `CannithArcane`, and `FeywildTwilight`.
  - `/alch respike` — triggers the configured `InvokeHotbar` Quick Re-Spike slot (refuses safely while `QuickRespikeEnabled` is off or bar/slot are `0`).
  - `/alch reset` — resets `HudX` (`0.5`), `HudY` (`0.72`), `FontSizePixels` (`28`), and `HudOpacity` (`0.90`).

### Settings reference (`reaction-timer.json`)

| Property | Type | Default | Range / Notes |
| --- | --- | --- | --- |
| `WarningSeconds` | `double` | `2` | `0–10` seconds before expiry for warning color |
| `SoundOnExpiry` | `bool` | `false` | Optional chime on expiry (suppressed in town/shrine pause when `SuppressAlertsInTown` is true) |
| `RespikeAlertSeconds` | `int` | `5` | `0–60` seconds for enlarged RE-SPIKE alert (`0` disables) |
| `FontSizePixels` | `int` | `28` | `10–160` exact integer pixels |
| `HudEnabled` | `bool` | `true` | Enables/disables the gameplay HUD (`Preview` and `Move` still show temporarily) |
| `HudX` | `double` | `0.5` | `0–1` normalized horizontal anchor |
| `HudY` | `double` | `0.72` | `0–1` normalized vertical anchor |
| `SuppressAlertsInTown` | `bool` | `true` | Suppresses `RE-SPIKE` flash/sound in taverns/airships and paused shrines |
| `ShowDepletionBar` | `bool` | `true` | Draws the 6px peripheral analog depletion bar + warning tick mark |
| `Theme` | `UiTheme` | `EberronGold` | `EberronGold`, `CannithArcane`, or `FeywildTwilight` |
| `HudMode` | `HudDisplayMode` | `CompactPill` | `CompactPill` or `RadialDonut` (DH 5 ImGui HUD layout) |
| `HudOpacity` | `double` | `0.90` | `0.25–1.00` DH 5 HUD backdrop/card opacity |
| `ShowSpellPointsBar` | `bool` | `true` | Shows Astral Blue Spell Points micro-bar under DH 5 HUD |
| `QuickRespikeEnabled` | `bool` | `false` | Gates DH 5 `InvokeHotbar` Quick Re-Spike (off by default for safety) |
| `QuickRespikeBar` | `int` | `0` | `0–20` hotbar index (`0` = unset) |
| `QuickRespikeSlot` | `int` | `0` | `0–10` slot index (`0` = unset) |
| `EffectMappings` | `Dictionary<int, Reaction>` | `{}` | Explicit effect DID overrides (`Pyrite`, `Orchidium`, `Verdanite`) |

## If a real spike is not recognized

Automatic detection requires `"spike"` plus `"reaction"`, `"Pyrite"`, `"Orchidium"`, or `"Verdanite"` in the effect name; permanent and long-lived effects (`> 30s`) are excluded from spike tracking (persistent reactions are tracked separately for next-primer hints).

In **Settings / effects**, trigger a spike, click **Refresh**, select the temporary effect, choose its reaction, click **Track selected**, then **Save**. **Export diagnostics** writes effect names, IDs, durations, and settings to `reaction-timer-effects.json` in the plugin data directory. The plugin makes no network requests and sends no game inputs unless you explicitly trigger DH 5 Quick Re-Spike.

## Lossless Scaling & platform status

- **Windows 11:** Verified locally with .NET 10 SDK compilation, C# behavioral checks, DH 4 `WindowsSmoke`, DH 5 `Dh5Smoke`, and real DH 5.0.858 `PluginTrustGate` / `InstallFromZip` verification. On DH 4 with Lossless Scaling, confirm `Capture exclusion: ON` and test Preview in WGC and DXGI with the configuration panel closed (Windows accepting `WDA_EXCLUDEFROMCAPTURE` `0x11` is not proof that every LS backend honors it). On DH 5 Beta, ImGui renders inside DDO's DX10/11 frame with no separate HWND.
- **Linux / Wine / Proton:** Solution builds (`Debug` + `Release`), portable C# checks, Python policy/SDK/signing checks, and `Dh5Smoke` cross-compile and execute on Ubuntu in GitHub Actions CI. `IsWineRuntime` (`ntdll!wine_get_version`) guards DH 4 capture exclusion (`WDA_EXCLUDEFROMCAPTURE`) and foreground checks on Wine/Proton; live Linux/Wine DDO gameplay has not been tested.

## Implementation, research basis & rebuilding

Thin SDK callbacks enqueue work onto non-blocking engines (`TimerEngine` on DH 4 with 10 Hz worker + STA GDI+ DIB `HudWindow`; async `Dh5TimerEngine` on DH 5 using `InTownAsync()`, `GetCurrentCharacterAsync()`, `IActiveEffect.Expiration`, `IReadOnlyList<IProperty>`, and `ImGuiNET` dynamic font sizing).

### Design & academic HCI references

- **Glanceable analog progress & prospective memory offloading:** Myers (1985, [CHI '85](https://doi.org/10.1145/317456.317459)); Risko & Gilbert (2016, [TiCS](https://doi.org/10.1016/j.tics.2016.07.002)); Blascheck et al. (2019, [IEEE TVCG](https://doi.org/10.1109/TVCG.2018.2865142) — single-arc donut `<220 ms` and horizontal bar `<300 ms`, avoiding multi-ring radial gauges).
- **Calm peripheral displays & bounded alarm urgency:** Weiser & Brown (1997, [Springer](https://doi.org/10.1007/978-1-4612-0685-9_6)); Matthews et al. (2004, [UIST '04](https://doi.org/10.1145/1029632.1029676); 2007, [HCI](https://doi.org/10.1080/07370020701307997)); Edworthy, Loxley & Dennis (1991, [Human Factors](https://doi.org/10.1177/001872089103300206)); Edworthy & Hellier (2006, [BJA](https://doi.org/10.1093/bja/ael114)); Sweetser & Wyeth (2005, [GameFlow](https://doi.org/10.1145/1077246.1077253)); Desurvire & Wiberg (2009, [PLAY](https://doi.org/10.1007/978-3-642-02774-1_60)).
- **Colorblind-safe encoding & Material Dark hierarchy:** Okabe & Ito (2008, [CUD](https://jfly.uni-koeln.de/color/)); W3C WCAG 2.2 ([2.3.1 Three Flashes `<=3 Hz`](https://www.w3.org/WAI/WCAG22/Understanding/three-flashes-or-below-threshold.html), [2.5.8 Target Size](https://www.w3.org/WAI/WCAG22/Understanding/target-size-minimum.html)); Google Material Design 3 ([Dark Tonal Elevation](https://m3.material.io/styles/elevation/overview)).

### Rebuilding from source

Extract `ReactionTimer-X.Y.Z-source.zip` and run from the repository root (with .NET 10 SDK, Python 3, OpenSSL/SignTool, and pinned SDKs):

```powershell
./ci/Get-Sdk.ps1
./build.ps1 -DungeonHelperDir $env:DH_SDK_DIR -Dh5SdkDir $env:DH5_SDK_DIR -Version 1.1.0 -AllowUnsigned
# Signed DH 5 bundle (supply your author key path via -KeyFile or DH_AUTHOR_KEY_FILE/DH_AUTHOR_KEY_B64):
./build.ps1 -DungeonHelperDir $env:DH_SDK_DIR -Dh5SdkDir $env:DH5_SDK_DIR -Version 1.1.0 -KeyFile C:\path\to\author.key
```

## Release history

- **`v1.1.0`** — Dual **Dungeon Helper 4** (`ReactionTimer-1.1.0-dh4-plugin.zip`, `net8.0-windows`, `VoK.Sdk 4.2.1.0`) + **Dungeon Helper 5 Beta** (`ReactionTimer-1.1.0-dh5-plugin.zip`, `net10.0` `x64`, `VoK.Sdk 5.3.0.0`, community-entitlement & Authenticode signed, in-frame `ImGuiNET` Compact Pill / Radial Donut HUD, SP micro-bar, `/alch` commands, optional `InvokeHotbar` Quick Re-Spike); 3 shared DDO × Material Design 3 themes (`EberronGold`, `CannithArcane`, `FeywildTwilight`); `SuppressAlertsInTown`, `ShowDepletionBar`, base-stance next-primer hints, `IsWineRuntime` Wine/Proton fallbacks, `Dh5Smoke`, and orphan-tag CI fix (`--tag-floor`).
- **`v1.0.1`** *(git tag only; no GitHub Release assets published)* — README documentation update.
- **`v1.0.0`** — Initial stable DH 4 release with independent transparent click-through HUD, exact `10–160px` integer font input, `WDA_EXCLUDEFROMCAPTURE`, and effect DID overrides.

References: [SetWindowDisplayAffinity](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setwindowdisplayaffinity), [extended window styles](https://learn.microsoft.com/en-us/windows/win32/winmsg/extended-window-styles), [Dungeon Helper](https://dungeonhelper.com/), [DH 5 SDK 5.3.0](http://sdk.dungeonhelper.com/5.3.0/).

Unofficial community plugin; not an official Dungeon Helper or Standing Stone Games release.
