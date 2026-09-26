using System;
using System.Globalization;
using System.Drawing;
namespace VoK.ReactionTimer;
internal static class HudPresentation
{
    public static string Text(TimerView view, bool demo)
    {
        string label = demo ? "PREVIEW" : view.Reaction == Reaction.Any ? "SPIKE" : view.Reaction.ToString().ToUpperInvariant();
        string value = view.Mode switch
        {
            TimerMode.Active => view.Remaining.HasValue ? (view.Estimated ? "~" : "") + view.Remaining.Value.ToString("0.0", CultureInfo.InvariantCulture) : "ACTIVE",
            TimerMode.Paused => "PAUSED",
            TimerMode.Expired => "RE-SPIKE",
            TimerMode.Error => "NO DATA",
            TimerMode.Disconnected => "OFFLINE",
            _ => "READY"
        };
        return label + "  " + value;
    }
    public static Point Position(Rectangle screen, Size window, double x, double y) => new(
        screen.Left + (int)Math.Round(Math.Clamp(x, 0, 1) * Math.Max(0, screen.Width - window.Width)),
        screen.Top + (int)Math.Round(Math.Clamp(y, 0, 1) * Math.Max(0, screen.Height - window.Height)));
}
