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

internal readonly record struct HudAlertFrame(bool Enlarged, bool BlackBackground)
{
    public int FontSize(int normalPixels) => Enlarged ? normalPixels * 2 : normalPixels;
}

// HUD-owned state with monotonic inputs. Repeated expired views and appearance
// edits cannot extend an alert; a new expiry gets its own captured duration.
internal sealed class HudAlertState
{
    private TimerMode _mode = TimerMode.Waiting;
    private long _alarm = -1;
    private double _start, _until;

    public HudAlertFrame Update(TimerView view, double now, int durationSeconds)
    {
        if (view.Mode == TimerMode.Expired && (_mode != TimerMode.Expired || view.Alarm != _alarm))
        {
            _start = now;
            _until = now + Math.Clamp(durationSeconds, 0, 60);
        }
        if (view.Mode != TimerMode.Expired || durationSeconds == 0) _until = now;
        _mode = view.Mode;
        _alarm = view.Alarm;
        bool enlarged = view.Mode == TimerMode.Expired && now < _until;
        return new(enlarged, enlarged && (long)Math.Floor((now - _start) * 2) % 2 == 0);
    }
}
