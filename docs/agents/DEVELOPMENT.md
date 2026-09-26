# Agent IDE and development instructions

Human counterpart: [IDE setup](../human/DEVELOPMENT.md).

- `ReactionTimer.sln` contains plugin, pure console tests and WindowsSmoke; keep project paths/GUID configurations aligned when adding projects.
- `.vsconfig` declares the Visual Studio managed desktop workload; the pure-check debug profile builds only the test project.
- `global.json` selects stable .NET 10 SDKs. Runtime targets remain .NET 8 to match DH. CI installs SDKs 8 and 10 so .NET 8 tests can run; global.json selects SDK 10. Do not retarget to net10.0 until the DH host supports it.
- The plugin is a DH-loaded DLL. Never invent a direct executable launch configuration for it. Use the tests/harness or attach to the actual hosting process.
- SDK resolution uses `DungeonHelperDir`, otherwise `%APPDATA%\Dungeon Helper`. For a nonstandard install set the environment before opening the IDE or pass MSBuild `-p:DungeonHelperDir=...`. Smoke-test arguments must reference the same directory.
- The tests are console executables with failing process exits, not an adapter-backed Test Explorer suite. Preserve the documented `dotnet run` checks unless intentionally changing the test framework.
- `.vscode/tasks.json` builds Debug, runs C#/Python checks and calls the packaging script through `pwsh`. `.vscode/launch.json` debugs tests, debugs the Windows harness or attaches via process selection. Do not auto-launch DDO, auto-install plugins or deploy over locked DLLs.
- Local debugging can place a matching PDB beside the installed plugin after closing DH; never add the PDB to the release installer. Debugger/host privileges must match when attaching.
- Keep `.editorconfig`, `.gitattributes` and `.gitignore` project-scoped. Do not change machine-wide editor policy.
- Linux can cross-compile the complete solution and run the pure C#/Python tests. Both Windows projects require `EnableWindowsTargeting`; the Windows smoke harness can compile there but must execute on Windows.
- After changing packaging, verify source ZIP inclusion of the solution, `.vscode`, `.github`, root configuration files, `AGENTS.md` and paired docs. Runtime ZIP remains exactly the three plugin files.

The preparation environment has not opened Visual Studio or exercised debugger attachment. Distinguish syntax/project-structure validation from an actual IDE session.

Windows solution builds now pass with SDK 10.0.401 and Visual Studio MSBuild. Visual Studio UI/debugger attachment remains unverified.

## .NET 10 build tools and DH runtime compatibility

The build SDK is now .NET 10; all three projects still target .NET 8. The pinned DH installer contains a runtimeconfig with `tfm: net8.0` and bundled Core/Desktop runtimes 8.0.14. Installing .NET 10 on a PC does not upgrade that bundled host. Retargeting the plugin to `net10.0-windows` requires a verified newer DH host and a separate compatibility migration.

CI installs SDK 8 for its test runtimes and SDK 10 for building. `global.json` selects 10.0.100 or a newer stable 10.0 feature band; build.ps1 checks the selected major. Existing package/output paths stay net8.0. This preserves tests against the runtime family DH actually uses. Runtime behavior and the initial 1.0.0 release baseline are unchanged.

SDK 10.0.401 compilation and the synthetic Windows layout smoke test have executed locally. Actual GitHub-hosted execution and live DDO/LS testing remain separate verification steps. See Microsoft's [SDK/Visual Studio compatibility guidance](https://learn.microsoft.com/en-us/dotnet/core/porting/versioning-sdk-msbuild-vs).

## Platform metadata and build outputs

`src/AssemblyInfo.cs` explicitly declares `SupportedOSPlatform("windows7.0")`, matching the default `net8.0-windows` platform contract. Keep it while `GenerateAssemblyInfo` is false; otherwise CA1416 treats every WinForms call as reachable on all platforms. Do not suppress CA1416 or weaken warnings-as-errors.

`ci/PackagePlugin.targets` runs after ordinary plugin builds, including Visual Studio builds. Release retains only `ReactionTimer-<Version>-plugin.zip` in `src/bin/Release/net8.0-windows/`; Debug retains the DLL, deps.json, metadata and PDB alongside that ZIP. Intermediate build files still belong under obj. Test executables retain ordinary outputs. The ZIP always contains exactly the three runtime entries, DLL first; PDBs stay outside it. Cleanup deletes only known plugin outputs and old plugin ZIPs, never arbitrary files. Visual Studio's fast up-to-date shortcut is disabled for the plugin so packaging runs on each build.

The smoke harness accepts a Release ZIP or a Debug DLL. `build.ps1` runs the Release build/tests and copies the already-generated ZIP to dist. Linux setup and commands are in the human counterpart. CI validates both configuration layouts and a repeated Release build; release publication depends on Windows and Linux jobs.
