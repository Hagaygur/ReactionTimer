# External references and SDK research

Checked 2026-09-27. External pages can change; the pinned binary and current code determine this project's actual compatibility.

| Resource | Where to read | How to use it |
| --- | --- | --- |
| DH installation and requirements | [Dungeon Helper home](https://dungeonhelper.com/) | Official installation and troubleshooting guidance |
| DH plugin framework | [FAQ](https://dungeonhelper.com/frequently-asked-questions/) | Host requirements and developer starting points |
| SDK documentation | [SDK root](https://sdk.dungeonhelper.com/) | Official documentation location identified by the SDK publisher; root timed out during this review |
| SDK package metadata | [VoK.Sdk on NuGet](https://www.nuget.org/packages/VoK.Sdk/) | Release metadata and publisher's documentation pointer; newest package is not automatically compatible |
| SDK code reference example | [4.3.4 provider file reference](https://sdk.dungeonhelper.com/4.3.4/IDdoGameDataProvider_8cs.html) | Indexed official reference, but a different version from our 4.2.1 target |
| Crowd Source Collector | [Official plugin page](https://dungeonhelper.com/plugins/crowd-source-collector/) | Collector purpose and documented data categories |
| Data handling | [DH privacy policy](https://dungeonhelper.com/privacy-policy/) | Collector opt-out, processing and sharing policy |
| Capture exclusion | [SetWindowDisplayAffinity](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setwindowdisplayaffinity) | Top-level-window requirements and exclusion modes |
| Alpha HUD drawing | [UpdateLayeredWindow](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-updatelayeredwindow) | Native layered-window rendering |
| Window flags | [Extended window styles](https://learn.microsoft.com/en-us/windows/win32/winmsg/extended-window-styles) | Layered, transparent and no-activate flags |
| Agent documentation | [AGENTS.md format](https://agents.md/) | Open Markdown entry point for coding-agent instructions |

## SDK fallback when the site is unavailable

Open the actual `VoK.Sdk.dll` in an IDE object browser or inspect its assembly metadata. If matching XML documentation is available, use it with that same version. Consult `ci/sdk-lock.json` for the binary expected by CI. The guessed `/4.2.1/` documentation URL could not be verified; do not treat it as a confirmed published documentation tree. A newer version's reference can suggest names, but validate signatures against 4.2.1 before using them.

Relevant symbols used by this project are `IDdoPlugin`, `IPluginUI`, `IDdoGameDataProvider`, `GetActiveEffects`, `GetGameWindowHandle`, `GetCurrentCharacterId`, `GetWeenieProperties`, `IActiveEffect.CalculateExpiration`, and the express/suppress/duration/login/logout events. Code in `ReactionTimerPlugin.cs` is the local integration map.

## Crowd Source Collector is separate

The official page describes an optional, bundled plugin with no UI. It lists server, quest/world-object, client-version and social-panel data categories. The policy describes anonymized cloud collection and opt-out by disabling that plugin. Read those primary pages for the full scope.

Our timer does not call Collector or its backend. This review did not verify a public Collector API, source repository or supported endpoint for retrieving effect IDs. Do not infer one from similarly named projects. For reaction debugging, use local SDK effect observations and this timer's diagnostics export. Agent counterpart: [Research guidance](../agents/REFERENCES.md).
