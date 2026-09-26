using System;
using System.Collections.Generic;
using System.Linq;

namespace VoK.ReactionTimer;

internal enum Reaction { Any, Pyrite, Orchidium, Verdanite }
internal enum TimerMode { Waiting, Active, Paused, Expired, Disconnected, Error }
internal sealed record TimerView(TimerMode Mode, Reaction Reaction, double? Remaining,
    double Duration, bool Estimated, long Alarm, string Detail);

// Pure state machine. All times are monotonic seconds, supplied by the caller.
// No SDK, UI, wall clock or threads: deterministic and independently testable.
internal sealed class TimerState
{
    private sealed record Entry(int Did, Reaction Reaction, double End, double? Frozen,
        double Duration, bool Paused, bool Estimated, long Order);
    private readonly Dictionary<int, Entry> _entries = new();
    private long _order, _alarm;
    private bool _seen, _wasActive;
    private Reaction _lastReaction;
    private bool _connected = true;

    public void Reset(bool connected)
    {
        _entries.Clear();
        _seen = _wasActive = false;
        _lastReaction = Reaction.Any;
        _connected = connected;
    }

    public void Apply(int id, int did, Reaction reaction, double? remaining, double duration,
        bool paused, bool estimated, double now, bool refresh)
    {
        if (!refresh && _entries.ContainsKey(id)) return;
        _connected = _seen = true;
        _entries[id] = new Entry(did, reaction, now + (remaining ?? 0), remaining,
            duration, paused, estimated, ++_order);
    }

    public bool Contains(int id) => _entries.ContainsKey(id);
    public void Remove(int id) => _entries.Remove(id);
    public void Retain(ISet<int> ids)
    {
        foreach (int id in _entries.Keys.Where(id => !ids.Contains(id)).ToArray()) _entries.Remove(id);
    }
    public void Update(int id, double remaining, double duration, bool paused, double now)
    {
        if (!_entries.TryGetValue(id, out var e)) return;
        // A duration update can refresh the same effect ID. Do not rely on ApplicationTimestamp.
        _entries[id] = e with { End = now + Math.Max(0, remaining), Frozen = Math.Max(0, remaining),
            Duration = duration > 0 ? duration : e.Duration, Paused = paused, Estimated = false,
            Order = ++_order };
    }

    public TimerView Read(double now)
    {
        if (!_connected) return new(TimerMode.Disconnected, Reaction.Any, null, 12, false, _alarm, "Log into a character");
        Entry? current = null;
        double? remaining = null;
        foreach (var e in _entries.Values)
        {
            double? r = e.Frozen is null ? null : e.Paused ? e.Frozen : Math.Max(0, e.End - now);
            if (r == 0) continue;
            if (current is null || e.Order > current.Order) { current = e; remaining = r; }
        }
        if (current is not null)
        {
            _lastReaction = current.Reaction;
            _wasActive = true;
            return new(current.Paused ? TimerMode.Paused : TimerMode.Active, current.Reaction,
                remaining, current.Duration, current.Estimated, _alarm,
                remaining is null ? "Active • timing unavailable" : current.Paused ? "Timer paused by game" : current.Estimated ? "Estimated countdown" : "Reaction Spike");
        }
        if (_wasActive) { ++_alarm; _wasActive = false; }
        return new(_seen ? TimerMode.Expired : TimerMode.Waiting, _lastReaction,
            _seen ? 0 : null, 12, false, _alarm, _seen ? "Cast to trigger your next spike" : "Waiting for a Reaction Spike");
    }
}
