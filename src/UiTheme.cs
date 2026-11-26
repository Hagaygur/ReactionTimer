using System;

namespace VoK.ReactionTimer;

public enum UiTheme
{
    EberronGold = 0,
    CannithArcane = 1,
    FeywildTwilight = 2
}

internal readonly record struct ThemeRgb(byte R, byte G, byte B);

internal sealed record UiThemePalette(
    UiTheme Theme,
    string DisplayName,
    ThemeRgb SurfaceLowest,
    ThemeRgb SurfaceLow,
    ThemeRgb Surface,
    ThemeRgb SurfaceHigh,
    ThemeRgb Outline,
    ThemeRgb Primary,
    ThemeRgb Secondary,
    ThemeRgb OnSurface,
    ThemeRgb OnSurfaceVariant);

internal static class UiThemePalettes
{
    public static readonly UiThemePalette EberronGold = new(
        UiTheme.EberronGold,
        "Eberron Obsidian & Gold",
        SurfaceLowest: new(0x11, 0x13, 0x18),
        SurfaceLow: new(0x1A, 0x1D, 0x24),
        Surface: new(0x22, 0x26, 0x30),
        SurfaceHigh: new(0x2C, 0x31, 0x3E),
        Outline: new(0x4A, 0x53, 0x68),
        Primary: new(0xE2, 0xB7, 0x5B),
        Secondary: new(0x56, 0xCC, 0xF2),
        OnSurface: new(0xEC, 0xEE, 0xF4),
        OnSurfaceVariant: new(0xA9, 0xB1, 0xC3));

    public static readonly UiThemePalette CannithArcane = new(
        UiTheme.CannithArcane,
        "House Cannith Arcane Forge",
        SurfaceLowest: new(0x0E, 0x14, 0x1D),
        SurfaceLow: new(0x15, 0x1E, 0x2B),
        Surface: new(0x1D, 0x29, 0x3A),
        SurfaceHigh: new(0x27, 0x36, 0x4B),
        Outline: new(0x45, 0x5D, 0x7A),
        Primary: new(0x56, 0xCC, 0xF2),
        Secondary: new(0xD4, 0xA3, 0x59),
        OnSurface: new(0xEA, 0xF2, 0xFA),
        OnSurfaceVariant: new(0x9E, 0xB2, 0xC8));

    public static readonly UiThemePalette FeywildTwilight = new(
        UiTheme.FeywildTwilight,
        "Feywild Eldritch Twilight",
        SurfaceLowest: new(0x15, 0x11, 0x1E),
        SurfaceLow: new(0x1E, 0x18, 0x2B),
        Surface: new(0x28, 0x20, 0x39),
        SurfaceHigh: new(0x33, 0x29, 0x49),
        Outline: new(0x58, 0x48, 0x75),
        Primary: new(0xCD, 0xA4, 0xFF),
        Secondary: new(0x6E, 0xE7, 0xB7),
        OnSurface: new(0xF2, 0xEC, 0xF9),
        OnSurfaceVariant: new(0xB8, 0xAB, 0xC9));

    public static UiThemePalette Get(UiTheme theme) => theme switch
    {
        UiTheme.CannithArcane => CannithArcane,
        UiTheme.FeywildTwilight => FeywildTwilight,
        _ => EberronGold
    };

    public static string DisplayName(UiTheme theme) => Get(theme).DisplayName;

    public static bool TryParse(string? token, out UiTheme theme)
    {
        string value = (token ?? "").Trim().ToLowerInvariant();
        switch (value)
        {
            case "eberron":
            case "eberrongold":
            case "gold":
            case "0":
                theme = UiTheme.EberronGold;
                return true;
            case "cannith":
            case "cannitharcane":
            case "arcane":
            case "1":
                theme = UiTheme.CannithArcane;
                return true;
            case "feywild":
            case "feywildtwilight":
            case "twilight":
            case "fey":
            case "2":
                theme = UiTheme.FeywildTwilight;
                return true;
            default:
                theme = UiTheme.EberronGold;
                return false;
        }
    }
}
