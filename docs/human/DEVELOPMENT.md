# IDE and local development

Open **ReactionTimer.sln** in Visual Studio with .NET desktop development and a .NET 10 SDK. It contains the plugin, cross-platform executable checks and the Windows smoke harness, with Debug and Release configurations. The plugin is a class library hosted by DH, so it cannot be launched directly as a standalone program.

`global.json` selects stable .NET 10 SDKs (minimum 10.0.100, later 10.0 feature bands allowed). Use Visual Studio 2026 with a compatible installed .NET 10 SDK, or VS Code/CLI. Install the .NET 8 Desktop Runtime for local Windows smoke tests and the .NET 8 runtime for pure tests; installing SDK 8 alongside SDK 10 supplies these development runtimes. DH bundles its own runtime. `.vsconfig` lists the Visual Studio .NET desktop workload. `.editorconfig` supplies formatting suggestions; `.gitattributes` normalizes text files. `.gitignore` excludes build outputs and personal IDE settings.

## SDK reference

The plugin resolves `VoK.Sdk.dll` from `%APPDATA%\Dungeon Helper` by default. Verify its version is 4.2.1.0. For a different location, set the `DungeonHelperDir` environment variable before launching the IDE, or pass the property on the command line:

```powershell
$env:DungeonHelperDir = 'D:\Apps\Dungeon Helper'
dotnet build ReactionTimer.sln -c Debug
# Alternative explicit property:
dotnet build ReactionTimer.sln -c Debug '-p:DungeonHelperDir=D:\Apps\Dungeon Helper'
```

Do not copy the SDK into source control. The Windows smoke launch configuration has its own SDK-directory argument; adjust it for a nonstandard installation too.

## Visual Studio

Set **ReactionTimer.Tests** as the startup project to run or debug pure logic checks. These are console assertions, not an xUnit/MSTest adapter: use Run/Debug or the documented `dotnet run` command rather than expecting Test Explorer discovery.

For **WindowsSmoke**, set launch arguments to the Release installer ZIP (or Debug plugin DLL) and SDK directory. It opens a synthetic host and configuration panel to test layout; it does not launch DDO. The VS Code profile uses the Debug DLL.

For live debugging, close DH/DDO, build Debug, then deploy the three runtime files into the existing plugin folder. Copy the matching PDB alongside them only for local debugging. Start DH/DDO and attach the managed debugger to the DH process hosting the plugin. Match privileges if the host is elevated. Do not deploy over loaded DLLs, copy SDK dependencies, or include debug files in release installer ZIPs. The repository does not automate deployment or starting the game.

## VS Code

Open the repository folder. Suggested extensions are C# Dev Kit and PowerShell; their licensing and installation are managed by their publishers. The package task requires PowerShell 7 (`pwsh`).

Tasks: **Build solution (Debug)**, **Test timer and emit contracts**, **Test version policy**, and **Package plugin (Windows)**. Debug profiles run the timer checks, run the Windows layout harness, or attach to a selected managed process. Select DH deliberately for the attach profile. If its SDK path differs from the default, set `DungeonHelperDir` in the launching environment and adjust the smoke profile's final argument.

Linux can cross-compile the complete solution and run the pure C# project and Python checks. Running the plugin or Windows smoke harness still requires Windows. The pure-check debug profile builds only its own test project, so it does not require the DH SDK.

## What was added in this handoff

The solution, SDK selection, formatting, line-ending rules and VS Code configurations are new. Human and agent documentation tracks explain background, architecture, development, operations and references. Source packaging now includes these files while the plugin ZIP remains exactly three runtime files. No timer/HUD runtime behavior changed in this documentation/IDE handoff.

Agent counterpart: [Development instructions](../agents/DEVELOPMENT.md).

Validation note: Windows solution builds now pass with SDK 10.0.401 and Visual Studio MSBuild, and the synthetic layout harness passes. An interactive Visual Studio debugging session is still unverified.

## .NET 10 build tools and DH runtime compatibility

The build SDK is now .NET 10; all three projects still target .NET 8. The pinned DH installer contains a runtimeconfig with `tfm: net8.0` and bundled Core/Desktop runtimes 8.0.14. Installing .NET 10 on a PC does not upgrade that bundled host. Retargeting the plugin to `net10.0-windows` requires a verified newer DH host and a separate compatibility migration.

CI installs SDK 8 for its test runtimes and SDK 10 for building. `global.json` selects 10.0.100 or a newer stable 10.0 feature band; build.ps1 checks the selected major. Existing package/output paths stay net8.0. This preserves tests against the runtime family DH actually uses. Runtime behavior and the initial 1.0.0 release baseline are unchanged.

SDK 10.0.401 compilation and the synthetic Windows layout smoke test have executed locally. Actual GitHub-hosted execution and live DDO/LS testing remain separate verification steps. See Microsoft's [SDK/Visual Studio compatibility guidance](https://learn.microsoft.com/en-us/dotnet/core/porting/versioning-sdk-msbuild-vs).

## Build outputs

Normal Visual Studio and `dotnet build` plugin builds create the installer automatically:

| Configuration | Plugin output directory | Contents |
| --- | --- | --- |
| Release | `src/bin/Release/net8.0-windows/` | `ReactionTimer-<Version>-plugin.zip` only |
| Debug | `src/bin/Debug/net8.0-windows/` | Installer ZIP, plugin DLL, deps.json, metadata and PDB |

The installer contains exactly three runtime files under `plugins/ReactionTimer/`, DLL first. Debug symbols are only loose files. The test projects retain their normal executable outputs, and intermediate compiler files remain under obj. `build.ps1` additionally runs the logic checks and copies the Release ZIP to dist; GitHub creates its separate source archive there.

The flood of CA1416 diagnostics was caused by disabling generated assembly metadata without supplying the Windows platform declaration. The manual assembly metadata now supplies that declaration; warnings remain errors.

## Linux cross-compilation

Install .NET SDK 10, the .NET 8 runtime, Python 3, Git, and `msitools` (`sudo apt-get install msitools` on Ubuntu). Then run from the repository root:

```bash
python3 ci/get_sdk_linux.py --destination .build/dh-sdk
dotnet build ReactionTimer.sln -c Release "-p:DungeonHelperDir=$PWD/.build/dh-sdk"
python3 ci/check_build_output.py src/bin/Release/net8.0-windows Release
dotnet run --project tests/ReactionTimer.Tests.csproj -c Release --no-build
python3 -m unittest discover -s ci -p 'test_*.py' -v
```

Use `-c Debug` for the Debug outputs. The downloader verifies the same installer and SDK hashes used on Windows; the SDK is never included in either release archive. An existing verified SDK directory can also be passed directly. Both Windows projects opt into [Windows targeting on Linux](https://learn.microsoft.com/en-us/dotnet/core/tools/sdk-errors/netsdk1100). Cross-compilation produces a Windows plugin, not a Linux HUD.
