# External references, SDKs & HCI research

## Platform & SDK references

| Resource | Where to read | Role |
| --- | --- | --- |
| Dungeon Helper 4 & FAQ | [dungeonhelper.com](https://dungeonhelper.com/) | Host requirements & troubleshooting |
| Dungeon Helper 5 Beta | [beta.dungeonhelper.com](https://beta.dungeonhelper.com/) | DH 5 architecture, `Install from zip`, trust model |
| DH 5 SDK `5.3.0` docs | [sdk.dungeonhelper.com/5.3.0](http://sdk.dungeonhelper.com/5.3.0/) | `IPlugin`, `IDdoGameDataProvider`, `IActiveEffect.Expiration`, `InvokeHotbar`, `ImGuiNET` |
| Windows Capture Exclusion | [SetWindowDisplayAffinity](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setwindowdisplayaffinity) | `WDA_EXCLUDEFROMCAPTURE` (`0x11`) on DH 4 |
| Layered HUD Drawing | [UpdateLayeredWindow](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-updatelayeredwindow) | Premultiplied-alpha DIB rendering on DH 4 |

## Verified academic HCI & accessibility sources

1. **Myers, B. A. (1985).** *The Importance of Percent-Done Progress Indicators for Computer-Human Interfaces.* CHI '85. https://doi.org/10.1145/317456.317459
2. **Edworthy, J., Loxley, S., & Dennis, I. (1991).** *Improving Auditory Warning Design.* Human Factors, 33(2), 205–231. https://doi.org/10.1177/001872089103300206
3. **Weiser, M., & Brown, J. S. (1997).** *The Coming Age of Calm Technology.* Springer. https://doi.org/10.1007/978-1-4612-0685-9_6
4. **Matthews, T., Dey, A. K., Mankoff, J., Carter, S., & Rattenbury, T. (2004).** *A Toolkit for Managing User Attention in Peripheral Displays.* UIST '04. https://doi.org/10.1145/1029632.1029676
5. **Sweetser, P., & Wyeth, P. (2005).** *GameFlow: a model for evaluating player enjoyment in games.* ACM CIE. https://doi.org/10.1145/1077246.1077253
6. **Edworthy, J., & Hellier, E. (2006).** *Alarms and human behaviour: implications for medical alarms.* British Journal of Anaesthesia, 97(1), 12–17. https://doi.org/10.1093/bja/ael114
7. **Matthews, T., Rattenbury, T., & Carter, S. (2007).** *Defining, Designing, and Evaluating Peripheral Displays.* HCI, 22(1–2), 221–261. https://doi.org/10.1080/07370020701307997
8. **Okabe, M., & Ito, K. (2008).** *Color Universal Design (CUD).* https://jfly.uni-koeln.de/color/
9. **Desurvire, H., & Wiberg, C. (2009).** *Game Usability Heuristics (PLAY).* LNCS 5621, Springer. https://doi.org/10.1007/978-3-642-02774-1_60
10. **Risko, E. F., & Gilbert, S. J. (2016).** *Cognitive Offloading.* Trends in Cognitive Sciences, 20(9), 676–688. https://doi.org/10.1016/j.tics.2016.07.002
11. **Blascheck, T., Besançon, L., Bezerianos, A., Lee, B., & Isenberg, P. (2019).** *Glanceable Visualization.* IEEE TVCG. https://doi.org/10.1109/TVCG.2018.2865142
12. **W3C (2023).** *WCAG 2.2* ([2.3.1 Three Flashes `<=3 Hz`](https://www.w3.org/WAI/WCAG22/Understanding/three-flashes-or-below-threshold.html), [2.5.8 Target Size](https://www.w3.org/WAI/WCAG22/Understanding/target-size-minimum.html)) & **Google Material Design 3** ([Dark Tonal Elevation](https://m3.material.io/styles/elevation/overview)).

Agent counterpart: [Research guidance](../agents/REFERENCES.md).
