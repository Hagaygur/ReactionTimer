# Agent research guidance

Human counterpart and verified reference table: [References](../human/REFERENCES.md).

## Primary entry points

- Dungeon Helper: https://dungeonhelper.com/ and https://beta.dungeonhelper.com/
- DH 5 SDK 5.3.0 documentation: http://sdk.dungeonhelper.com/5.3.0/
- NuGet `VoK.Sdk` (pinned `4.2.1.0` via DH 4 MSI and `5.3.0.0` via NuGet flatcontainer): `ci/sdk-lock.json`
- Windows capture affinity & layered drawing: `SetWindowDisplayAffinity`, `UpdateLayeredWindow`, Extended Window Styles (Microsoft Learn)
- Academic Game HCI / Accessibility references: Myers (1985), Edworthy et al. (1991, 2006), Weiser & Brown (1997), Matthews et al. (2004, 2007), Sweetser & Wyeth (2005), Okabe & Ito (2008), Desurvire & Wiberg (2009), Risko & Gilbert (2016), Blascheck et al. (2019), W3C WCAG 2.2, Material Design 3 Dark Tonal Elevation.

## Evidence rules

Inspect the pinned `VoK.Sdk.dll` binaries (`4.2.1.0` and `5.3.0.0`) rather than assuming unpinned API versions. Keep downloaded SDKs, private keys, and local logs outside git and source archives.
