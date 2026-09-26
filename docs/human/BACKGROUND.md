# Background and requirements

The timer tracks DDO Alchemist Reaction Spikes and indicates when another spike is needed. The user wanted a Dungeon Helper-only solution with efficient code, readable text, minimal screen obstruction and compatibility with Lossless Scaling. A configuration panel may be larger; the gameplay HUD should contain only compact outlined text.

The plugin uses DH's local game-data provider. It does not automate casting, modify the game buff bar, require RTSS, or depend on a network service. Its optional sound signals an expiry; it does not perform an action in the game.

## Why this architecture exists

| Observed problem | Resulting design decision |
| --- | --- |
| Initial assembly requested SDK 4.3.4, while the installed DH supplied 4.2.1 | Pin and verify the actual supported SDK; do not upgrade merely because NuGet is newer |
| DH hosted panel vanished with WGC and appeared twice with DXGI | Use an independent top-level HUD inside the DH process, request capture exclusion, and keep it above LS |
| Text size entered as 30 became 60; other edits also produced unexpected sizes | Replace percentage scaling with validated integer pixels; test save/reopen of 20 and 30 |
| Large opaque panel obstructed gameplay | Draw per-pixel-alpha text only; show a temporary drag plate during Move |
| ZIP installed correctly only after non-plugin files were removed | Enforce a three-file install archive; deliver source separately |
| Opening the panel clipped the DH toolbar and Settings button | Measure the toolbar before DH resizes its host and use automatically sized button rows |
| Future changes need repeatable builds and versioning | Add Windows CI, contract comparisons, tests and separate release assets |

The 30→60 observation was consistent with the earlier minimum of 60%; the precise cause of the reported 20→180 result was not independently reproduced. The replacement input path eliminates the old percentage control and has exact-value persistence checks.

## Evidence and remaining work

The user reported that the independent overlay seemed to work well after stripping non-runtime files from the ZIP. That is useful feedback, not a complete per-mode LS compatibility matrix. A screenshot confirmed panel/toolbar clipping. The subsequent layout correction has not yet been confirmed by another user report.

On 2026-09-27, the 1.0.0 plugin and solution built with SDK 10.0.401 on Windows and Ubuntu 22.04 WSL against SDK 4.2.1.0. Both systems passed 44 C# checks and 15 Python checks, plus Debug/Release output validation. Visual Studio MSBuild and the synthetic Windows toolbar/button layout harness passed; the latter loaded the Release ZIP. Both pinned installer acquisition scripts, workflow linting and source-archive validation also passed. The GitHub-hosted workflow has not run from this folder. Live reaction IDs, refresh timing and both LS capture modes remain integration checks on the target PC; the synthetic layout check is not a user confirmation inside DH.

See [Operations](OPERATIONS.md) for a reproducible verification sequence and [References](REFERENCES.md) for authoritative external sources. Agent counterpart: [Context](../agents/CONTEXT.md).
