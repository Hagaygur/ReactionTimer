using System;
namespace VoK.ReactionTimer;

internal static class ReactionMatcher
{
    public static Reaction? Match(string name, double? duration)
    {
        // Permanent base reactions must never be mistaken for the 12-second spike.
        if (duration is <= 0 or > 30) return null;
        if (string.IsNullOrWhiteSpace(name)) return null;
        bool spike = name.Contains("spike", StringComparison.OrdinalIgnoreCase);
        if (!spike) return null;
        if (name.Contains("pyrite", StringComparison.OrdinalIgnoreCase)) return Reaction.Pyrite;
        if (name.Contains("orchidium", StringComparison.OrdinalIgnoreCase)) return Reaction.Orchidium;
        if (name.Contains("verdanite", StringComparison.OrdinalIgnoreCase)) return Reaction.Verdanite;
        return name.Contains("reaction", StringComparison.OrdinalIgnoreCase) ? Reaction.Any : null;
    }

    // Matches the persistent base Alchemist Reaction stance (duration <= 0 or > 30s or indefinite)
    // so expired spikes can cue which opposing primer breaks the active stance.
    public static Reaction? MatchBaseStance(string name, double? duration)
    {
        if (duration is > 0 and <= 30) return null;
        if (string.IsNullOrWhiteSpace(name)) return null;
        if (name.Contains("spike", StringComparison.OrdinalIgnoreCase)) return null;
        if (!name.Contains("reaction", StringComparison.OrdinalIgnoreCase)) return null;
        if (name.Contains("pyrite", StringComparison.OrdinalIgnoreCase)) return Reaction.Pyrite;
        if (name.Contains("orchidium", StringComparison.OrdinalIgnoreCase)) return Reaction.Orchidium;
        if (name.Contains("verdanite", StringComparison.OrdinalIgnoreCase)) return Reaction.Verdanite;
        return null;
    }

    // Opposing primer that breaks the current Alchemist Reaction stance to trigger a new 12s spike.
    public static string? NextPrimerFor(Reaction stance) => stance switch
    {
        Reaction.Pyrite => "Ceruleite",
        Reaction.Orchidium => "Gildleaf",
        Reaction.Verdanite => "Crimsonite",
        _ => null
    };
}
