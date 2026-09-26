using System;
namespace VoK.ReactionTimer;

internal static class ReactionMatcher
{
    public static Reaction? Match(string name, double? duration)
    {
        // Permanent base reactions must never be mistaken for the 12-second spike.
        if (duration is <= 0 or > 30) return null;
        bool spike = name.Contains("spike", StringComparison.OrdinalIgnoreCase);
        if (!spike) return null;
        if (name.Contains("pyrite", StringComparison.OrdinalIgnoreCase)) return Reaction.Pyrite;
        if (name.Contains("orchidium", StringComparison.OrdinalIgnoreCase)) return Reaction.Orchidium;
        if (name.Contains("verdanite", StringComparison.OrdinalIgnoreCase)) return Reaction.Verdanite;
        return name.Contains("reaction", StringComparison.OrdinalIgnoreCase) ? Reaction.Any : null;
    }
}

