# Automatic GitHub builds and releases

Developer entry points: [ReactionTimer.sln](ReactionTimer.sln), [human/agent documentation map](docs/README.md), and [AGENTS.md](AGENTS.md). Source release archives include the solution and editor configurations.

Toolchain: .NET 10 SDK builds .NET 8 targets for the pinned DH host. CI also installs .NET 8 to run compatibility tests. See [runtime/toolchain details](docs/human/DEVELOPMENT.md).

## Set up once

1. Create a GitHub repository and extract this archive.
2. Upload **its contents to the repository root**, including the `.github` directory. The workflow must be at `.github/workflows/build.yml`, not under another `DDO-Reaction-Timer` folder. Do not upload this ZIP as a single repository file.
3. Commit/push to the repository's default branch. Open **Actions → Build, test and release**.
4. The first successful run publishes **v1.0.0** under **Releases**. Download `ReactionTimer-1.0.0-plugin.zip` and install it directly through DH. The separate source ZIP must never go inside DH's plugin folder.

GitHub Actions and GitHub-hosted runners must be enabled. No personal access token, manually installed SDK or extra secret is required; publication uses the repository's built-in token with `contents: write` in the release job. Organization or tag-protection policies may require an administrator to permit release creation.

## Triggers and outputs

Every branch push and pull request runs checks and produces downloadable Actions artifacts. Successful default-branch pushes and manual runs on that branch also publish a tagged release. Other branches and PRs do not publish or reserve release versions. GitHub receives pushes, not unpushed local commits: a push containing several commits builds the tip and considers all commit messages since the preceding release. Rapid pushes are serialized; GitHub may coalesce pending runs in favor of the newest push.

Release assets:

- `ReactionTimer-X.Y.Z-plugin.zip`: exactly the DLL, dependency manifest and plugin metadata, under `plugins/ReactionTimer/`, with the DLL first.
- `ReactionTimer-X.Y.Z-source.zip`: source, workflow, tests and instructions, entirely separate.
- `contracts.json`: observed behavior used for the next comparison.
- `version.json`: chosen version, reason, baseline tag and source commit.
- `SHA256SUMS.txt`: asset hashes.

The workflow does not commit version changes back into the repository or create a commit loop. It stamps the checked-out build and tags the original commit. Rerunning an already released commit reuses its version and leaves its published assets intact. An interrupted draft can be resumed.

## How versions are chosen

The first stable release is 1.0.0. Later successful builds compare their observed contracts with `contracts.json` from the previous reachable stable release. Older v0.x tags are ignored.

| Highest detected change | Result from 1.2.3 |
| --- | --- |
| Existing compatibility observation changed/removed, or a feature observation removed | 2.0.0 |
| New compatibility/feature observation, or changed feature observation | 1.3.0 |
| All observations unchanged | 1.2.4 |
| Any test fails | No release |

Compatibility observations currently cover persisted settings property types, reaction numeric values, countdown/pause behavior and selected detection rules. Feature observations cover HUD text, font acceptance/default, sound and warning defaults. See `tests/ContractSnapshot.cs`.

This deliberately does **not** turn failing tests into major releases. Failures indicate a regression or a test expectation that must be reviewed. For an intentional breaking change, update the implementation and the relevant tests together; once the new tests pass, the changed compatibility observation requests a major version.

Nor can these observations discover every semantic change in arbitrary code. Add a behavioral assertion and a contract observation when adding a capability. UI, native capture and other behavior outside this small contract suite need deliberate classification. Conventional commits provide a second signal that can raise, but never lower, the automatic bump:

```text
fix: correct timer color                     # patch if contracts unchanged
feat: add a new HUD mode                     # at least minor
feat!: replace the settings format           # major
```

Alternatively put `BREAKING CHANGE: explanation` in a commit body. All messages since the baseline release are considered; one breaking declaration wins over minor/patch changes. Documentation-only changes currently produce a patch release too, matching the requested default of a hotfix when no contract changed. Line counts are not used.

Do not rename/remove contract keys just to tidy tests: removing an observation is intentionally conservative and requests a major version. Keep the contract schema at 1 unless you also implement an explicit migration for the comparator. Do not delete release contract assets: missing baselines stop automatic release rather than guess a version.

## Build and test stages

1. Python version-policy tests, including a temporary Git history that verifies 1.0.0 → 1.0.1 → 1.1.0 → 2.0.0 and same-commit reruns.
2. The 44 C# timer/settings/presentation checks, then generation of actual contract observations. The Python suite also tests SDK hash rejection and extraction failure handling.
3. Version selection against the preceding stable release and commit declarations.
4. Download the official DH installer; verify its pinned SHA-256, administratively extract it, and verify SDK 4.2.1.0 by hash and assembly version. The SDK is used only to build and is not redistributed.
5. Build on Windows with the .NET 10 SDK targeting .NET 8 and warnings treated as errors. Validate Debug outputs, then build Release and run the logic checks again. Normal builds produce the installer automatically: Release leaves only the ZIP in the plugin output directory; Debug keeps loose runtime files and PDB alongside the ZIP.
6. A Windows smoke executable loads the plugin from the Release ZIP, checks its platform declaration and version, and exercises the control panel in a synthetic host with a wider toolbar. It checks button bounds and minimum width; it does not attach to DDO or emulate LS.
7. Validate the installer archive's exact three entries and metadata version; produce source and hashes.
8. In parallel, an Ubuntu job installs msitools, extracts the same hash-pinned SDK, cross-compiles Debug and Release, checks both output layouts and a repeated Release build, and runs the 44 portable C# and 15 Python checks. It cannot execute the Windows UI harness.
9. Upload the Windows release artifacts. Only after both Windows and Linux jobs succeed, the default-branch release job publishes a draft with all assets, then makes it public.

The public installer URL may change in place. In that case the build intentionally fails at the hash check. Verify the new upstream installer/SDK and update `ci/sdk-lock.json`; never remove the hash verification just to make the build pass. Release tags and the current default branch should retain their ancestry; avoid rewriting already released history.

## Local commands

On Windows, with .NET 10 SDK, Python 3 and Dungeon Helper installed:

```powershell
python -m unittest discover -s ci -p test_*.py -v
dotnet run --project tests/ReactionTimer.Tests.csproj -c Release -- --contracts dist/contracts.json
.\build.ps1 -Version 1.0.0
# Nonstandard DH installation:
.\build.ps1 -DungeonHelperDir 'D:\Apps\Dungeon Helper' -Version 1.0.0
```

`build.ps1` stamps AssemblyInfo and plugin metadata in the local working copy; CI uses a disposable checkout. The runtime plugin version and diagnostics read the assembly version. The project/package version is passed through MSBuild.

## Validation delivered with this repository

On 2026-09-27, SDK 10.0.401 built the solution on Windows and Ubuntu 22.04 WSL against SDK 4.2.1.0. Debug/Release output checks passed, as did 44 C# checks and 15 Python checks on both operating systems. Visual Studio MSBuild and the Windows synthetic layout harness passed; the harness loaded the Release ZIP. Both SDK acquisition scripts downloaded and extracted the pinned official installer successfully. Workflow linting with actionlint 1.7.12 and source-archive validation passed locally. The GitHub-hosted run and publication remain unverified because this local source folder has no Git checkout or remote. Live DDO/LS behavior cannot be inferred from these tests.

Linux local setup and commands: [development guide](docs/human/DEVELOPMENT.md#linux-cross-compilation). Runtime support remains Windows-only. These build fixes do not change behavioral contracts: expected version impact is patch after an existing release, or the initial 1.0.0 baseline otherwise.

References: [GitHub .NET builds](https://docs.github.com/en/actions/tutorials/build-and-test-code/net), [workflow syntax and permissions](https://docs.github.com/en/actions/reference/workflows-and-actions/workflow-syntax), [workflow triggers](https://docs.github.com/en/actions/how-tos/write-workflows/choose-when-workflows-run/trigger-a-workflow).
