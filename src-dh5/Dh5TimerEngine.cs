using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using VoK.Sdk;
using VoK.Sdk.Ddo;
using VoK.Sdk.Properties;

namespace VoK.ReactionTimer;

internal sealed record EffectInfo(int Did, string Name, double? Duration, double LastSeen);
internal sealed record EffectMessage(int Kind, int Id = 0, int Did = 0, double? Duration = null,
    double Remaining = 0, bool Paused = false, double ReceivedAt = 0);

internal sealed record Dh5HudSnapshot(
    TimerView View,
    int? SpellPointsCurrent,
    int? SpellPointsMax,
    ulong? CharacterId,
    bool InTown,
    int SettingsRevision);

internal sealed class Dh5TimerEngine : IDisposable
{
    private const uint MbIconExclamation = 0x00000030u;

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool MessageBeep(uint uType);

    private readonly IDdoGameDataProvider _data;
    private readonly IDdoEventProvider _eventsProvider;
    private readonly SettingsStore _store;
    private Settings _settings;
    private readonly TimerState _state = new();
    private readonly object _gate = new();
    private readonly ConcurrentQueue<EffectMessage> _events = new();
    private readonly Dictionary<int, (string Name, double RetryAt)> _names = new();
    private readonly Dictionary<int, EffectInfo> _effects = new();
    private readonly System.Threading.Timer _timer;
    private volatile bool _disposed;
    private int _tickRunning;
    private double _nextScan, _lastErrorLog = -100;
    private ulong? _character;
    private bool _inTown;
    private int? _spCurrent;
    private int? _spMax;
    private long _lastSoundAlarm = -1;
    private int _settingsRevision;
    private bool _chatRegistered;
    private long _frameSteps;
    private Dh5HudSnapshot _snapshot;

    internal static double Now => Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency;
    internal string Folder => _store.DirectoryPath;
    internal int SettingsRevision => Volatile.Read(ref _settingsRevision);
    internal long FrameSteps => Volatile.Read(ref _frameSteps);
    internal bool ChatCommandRegistered => _chatRegistered;

    public Dh5TimerEngine(IDdoGameDataProvider data, IDdoEventProvider eventsProvider, string folder)
    {
        _data = data;
        _eventsProvider = eventsProvider;
        _store = new SettingsStore(folder);
        try { _settings = _store.Load(); }
        catch (Exception e) { _settings = new(); Log("Settings could not be loaded; defaults in use: " + e.Message); }
        _state.SetTownState(false, _settings.SuppressAlertsInTown);
        _snapshot = new(
            new TimerView(TimerMode.Waiting, Reaction.Any, null, 12, false, 0, "Waiting for effect data"),
            null, null, null, false, 0);

        eventsProvider.OnEffectExpress.AddHandler(e => Enqueue(e.EffectId.HasValue
            ? new(1, e.EffectId.Value, e.EffectDid, e.Duration, ReceivedAt: Now) : new(5)));
        eventsProvider.OnEffectSuppress.AddHandler(id => Enqueue(new(2, id)));
        eventsProvider.OnEffectUpdateDuration.AddHandler(e => Enqueue(new(3, e.EffectId,
            Duration: e.OriginalDuration, Remaining: e.RemainingSeconds, Paused: e.IsPaused, ReceivedAt: Now)));
        eventsProvider.OnLogin.AddHandler(_ => Enqueue(new(4)));
        eventsProvider.OnLogout.AddHandler(() => Enqueue(new(6)));

        try
        {
            _chatRegistered = eventsProvider.OnChatCommand.AddHandler("alch", HandleChatCommandAsync);
        }
        catch (Exception e)
        {
            Log("Could not register /alch command: " + e.Message);
        }

        _timer = new System.Threading.Timer(_ => _ = TickAsync(), null, 0, 100);
        Log($"Started DH5 v{typeof(Dh5Plugin).Assembly.GetName().Version}; in-frame ImGui HUD; VoK.Sdk 5.3.0.0.");
    }

    internal void NoteFrameStep() => Interlocked.Increment(ref _frameSteps);

    private Task Enqueue(EffectMessage message)
    {
        if (!_disposed && _events.Count < 2048) _events.Enqueue(message);
        return Task.CompletedTask;
    }

    internal Settings GetSettings() { lock (_gate) return _settings.Copy(); }

    internal void SaveSettings(Settings settings)
    {
        lock (_gate)
        {
            if (_disposed) return;
            bool mappingsChanged = _settings.EffectMappings.Count != settings.EffectMappings.Count ||
                _settings.EffectMappings.Any(x => !settings.EffectMappings.TryGetValue(x.Key, out var r) || r != x.Value);
            _store.Save(settings);
            _settings = settings.Copy();
            _state.SetTownState(_inTown, _settings.SuppressAlertsInTown);
            int rev = Interlocked.Increment(ref _settingsRevision);
            if (mappingsChanged) { _state.Reset(_character.HasValue); _nextScan = 0; }
            Volatile.Write(ref _snapshot, _snapshot with { View = _state.Read(Now), SettingsRevision = rev });
        }
    }

    internal void SavePosition(double x, double y)
    {
        lock (_gate)
        {
            if (_disposed) return;
            var s = _settings.Copy();
            s.HudX = x;
            s.HudY = y;
            _store.Save(s);
            _settings = s;
            Interlocked.Increment(ref _settingsRevision);
        }
    }

    internal void ResetHudLayout()
    {
        lock (_gate)
        {
            if (_disposed) return;
            var s = _settings.Copy();
            s.HudX = 0.5;
            s.HudY = 0.72;
            s.FontSizePixels = 28;
            s.HudOpacity = 0.90;
            s.HudEnabled = true;
            _store.Save(s);
            _settings = s;
            Interlocked.Increment(ref _settingsRevision);
        }
    }

    internal EffectInfo[] GetEffects()
    {
        lock (_gate) return _effects.Values.OrderByDescending(x => x.LastSeen).ToArray();
    }

    internal Dh5HudSnapshot Snapshot() => Volatile.Read(ref _snapshot);

    internal bool TryInvokeQuickRespike(out string message)
    {
        Settings s = GetSettings();
        if (!s.QuickRespikeEnabled)
        {
            message = "Quick Re-Spike is disabled. Enable it in the Quick-Cast tab first.";
            return false;
        }
        if (s.QuickRespikeBar is < 1 or > 20 || s.QuickRespikeSlot is < 1 or > 10)
        {
            message = "Quick Re-Spike hotbar/slot not configured (set Bar 1–20 and Slot 1–10 in Quick-Cast tab).";
            return false;
        }
        uint bar = (uint)(s.QuickRespikeBar - 1);
        uint slot = (uint)(s.QuickRespikeSlot - 1);
        try
        {
            bool ok = _data.InvokeHotbar(bar, slot, null);
            message = ok
                ? $"Queued Quick Re-Spike (Bar {s.QuickRespikeBar}, Slot {s.QuickRespikeSlot})."
                : $"Game declined Quick Re-Spike (Bar {s.QuickRespikeBar}, Slot {s.QuickRespikeSlot}).";
            return ok;
        }
        catch (Exception e)
        {
            message = "Quick Re-Spike failed: " + e.Message;
            return false;
        }
    }

    internal static void PlayExpirySound()
    {
        if (!OperatingSystem.IsWindows()) return;
        try { _ = MessageBeep(MbIconExclamation); }
        catch { }
    }

    private Task HandleChatCommandAsync(IChatCommand command)
    {
        if (_disposed) return Task.CompletedTask;
        try
        {
            string rest = (command?.Rest ?? "").Trim();
            string[] parts = rest.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            string sub = parts.Length > 0 ? parts[0].ToLowerInvariant() : "help";
            switch (sub)
            {
                case "status":
                {
                    var snap = Snapshot();
                    var v = snap.View;
                    string stateText = HudPresentation.Text(v, false);
                    string stanceText = v.BaseStance != Reaction.Any
                        ? $" | Stance: {v.BaseStance} (next: {ReactionMatcher.NextPrimerFor(v.BaseStance)})"
                        : "";
                    string spText = snap.SpellPointsCurrent.HasValue && snap.SpellPointsMax is > 0
                        ? $" | SP: {snap.SpellPointsCurrent}/{snap.SpellPointsMax}"
                        : "";
                    PrintChat($"Status: {stateText}{stanceText}{spText}");
                    break;
                }
                case "warn":
                {
                    if (parts.Length < 2 || !double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double sec) || !double.IsFinite(sec))
                    {
                        PrintChat("Usage: /alch warn <0-10>");
                        break;
                    }
                    var s = GetSettings();
                    s.WarningSeconds = Math.Clamp(sec, 0, 10);
                    SaveSettings(s);
                    PrintChat($"Warning threshold set to {s.WarningSeconds:0.0}s.");
                    break;
                }
                case "hud":
                {
                    var s = GetSettings();
                    string arg = parts.Length > 1 ? parts[1].ToLowerInvariant() : "toggle";
                    if (arg == "on") s.HudEnabled = true;
                    else if (arg == "off") s.HudEnabled = false;
                    else if (arg == "pill") { s.HudEnabled = true; s.HudMode = HudDisplayMode.CompactPill; }
                    else if (arg == "donut") { s.HudEnabled = true; s.HudMode = HudDisplayMode.RadialDonut; }
                    else s.HudEnabled = !s.HudEnabled;
                    SaveSettings(s);
                    PrintChat($"HUD {(s.HudEnabled ? "enabled" : "hidden")} ({s.HudMode}).");
                    break;
                }
                case "town":
                {
                    var s = GetSettings();
                    string arg = parts.Length > 1 ? parts[1].ToLowerInvariant() : "toggle";
                    s.SuppressAlertsInTown = arg switch
                    {
                        "on" => true,
                        "off" => false,
                        _ => !s.SuppressAlertsInTown
                    };
                    SaveSettings(s);
                    PrintChat($"Town/shrine alert suppression: {(s.SuppressAlertsInTown ? "ON" : "OFF")}.");
                    break;
                }
                case "respike":
                {
                    TryInvokeQuickRespike(out string msg);
                    PrintChat(msg);
                    break;
                }
                case "theme":
                {
                    if (parts.Length < 2 || !UiThemePalettes.TryParse(parts[1], out var parsedTheme))
                    {
                        PrintChat("Usage: /alch theme <eberron|cannith|feywild>");
                        break;
                    }
                    var s = GetSettings();
                    s.Theme = parsedTheme;
                    SaveSettings(s);
                    PrintChat($"Theme set to {UiThemePalettes.DisplayName(parsedTheme)}.");
                    break;
                }
                case "reset":
                {
                    ResetHudLayout();
                    PrintChat("HUD position, font size (28px), and opacity (90%) reset to defaults.");
                    break;
                }
                default:
                    PrintChat("Commands: /alch status | /alch warn <sec> | /alch hud [on|off|pill|donut] | /alch town [on|off] | /alch theme <eberron|cannith|feywild> | /alch respike | /alch reset");
                    break;
            }
        }
        catch (Exception e)
        {
            Log("Chat command error: " + e.Message);
        }
        return Task.CompletedTask;
    }

    private void PrintChat(string message)
    {
        try
        {
            _data.Print($"<RGB=#E2B75B>Alch Reaction Timer:</RGB> {message}");
        }
        catch { }
    }

    // Non-reentrant async tick: awaits frame-marshaled SDK calls BEFORE taking _gate.
    internal async Task TickAsync()
    {
        if (_disposed || Interlocked.CompareExchange(ref _tickRunning, 1, 0) != 0) return;
        try
        {
            if (_disposed) return;
            double now = Now;
            bool shouldScan;
            lock (_gate) shouldScan = now >= _nextScan;

            ulong? character = null;
            bool? inTown = null;
            int? spCurrent = null;
            int? spMax = null;
            IActiveEffect[]? activeEntries = null;
            Exception? scanError = null;

            if (shouldScan)
            {
                try
                {
                    character = _data.GetCurrentCharacterId();
                    if (character.HasValue && character.Value != 0)
                    {
                        try { inTown = await _data.InTownAsync().ConfigureAwait(false); }
                        catch { /* Town detection is optional. */ }

                        try
                        {
                            var me = await _data.GetCurrentCharacterAsync().ConfigureAwait(false);
                            if (me is not null)
                            {
                                spCurrent = me.SpellPoints_Current;
                                spMax = me.SpellPoints_Max;
                            }
                        }
                        catch { /* SP telemetry is optional. */ }

                        var active = _data.GetActiveEffects();
                        if (active is null) throw new InvalidOperationException("Active effects not available");
                        activeEntries = active.Values.ToArray();
                    }
                }
                catch (Exception ex)
                {
                    scanError = ex;
                }
            }

            if (_disposed) return;

            bool playSound = false;
            lock (_gate)
            {
                if (_disposed) return;
                now = Now;
                while (_events.TryDequeue(out var e))
                {
                    if (e.Kind == 4 || e.Kind == 6)
                    {
                        _state.Reset(e.Kind == 4);
                        _effects.Clear();
                        _names.Clear();
                        _character = null;
                        _inTown = false;
                        _spCurrent = null;
                        _spMax = null;
                        _state.SetTownState(false, _settings.SuppressAlertsInTown);
                        _nextScan = e.Kind == 4 ? now + .25 : now + 1;
                        continue;
                    }
                    if (e.Kind == 5) { _nextScan = 0; continue; }
                    if (e.Kind == 2) { _state.Remove(e.Id); continue; }
                    if (e.Kind == 3)
                    {
                        double left = e.Paused ? e.Remaining : Math.Max(0, e.Remaining - (now - e.ReceivedAt));
                        _state.Update(e.Id, left, e.Duration ?? 12, e.Paused, now);
                        continue;
                    }
                    string name = NameFor(e.Did, now);
                    Remember(e.Did, name, e.Duration, now);
                    if (ReactionMatcher.MatchBaseStance(name, e.Duration) is { } baseStance)
                        _state.SetBaseStance(baseStance);
                    var reaction = Match(e.Did, name, e.Duration);
                    if (reaction is null) continue;
                    double duration = e.Duration is > 0 and <= 120 ? e.Duration.Value : 12;
                    _state.Apply(e.Id, e.Did, reaction.Value, Math.Max(0, duration - (now - e.ReceivedAt)),
                        duration, false, e.Duration is null or <= 0, now, refresh: true);
                }

                if (shouldScan)
                {
                    _nextScan = now + 1;
                    if (scanError is not null)
                    {
                        var errView = new TimerView(TimerMode.Error, Reaction.Any, null, 12, false,
                            _snapshot.View.Alarm, "Effect data unavailable • retrying");
                        Volatile.Write(ref _snapshot, _snapshot with { View = errView, SettingsRevision = _settingsRevision });
                        if (now - _lastErrorLog > 15) { Log("SDK read failed: " + scanError.Message); _lastErrorLog = now; }
                        return;
                    }

                    ReconcileScan(now, character, inTown, spCurrent, spMax, activeEntries);
                }

                TimerView view = _state.Read(now);
                bool townQuiet = view.InTown && _settings.SuppressAlertsInTown;
                if (!townQuiet && _lastSoundAlarm >= 0 && view.Alarm > _lastSoundAlarm &&
                    _settings.SoundOnExpiry && view.Mode == TimerMode.Expired)
                {
                    playSound = true;
                }
                _lastSoundAlarm = view.Alarm;
                Volatile.Write(ref _snapshot, new Dh5HudSnapshot(view, _spCurrent, _spMax, _character, _inTown, _settingsRevision));
            }

            if (playSound) PlayExpirySound();
        }
        catch (Exception e)
        {
            if (Now - _lastErrorLog > 15)
            {
                lock (_gate) Log("Tick failed: " + e.Message);
                _lastErrorLog = Now;
            }
        }
        finally
        {
            Interlocked.Exchange(ref _tickRunning, 0);
        }
    }

    private void ReconcileScan(double now, ulong? character, bool? inTown, int? spCurrent, int? spMax, IActiveEffect[]? entries)
    {
        if (character != _character)
        {
            _state.Reset(character.HasValue && character != 0);
            _effects.Clear();
            _names.Clear();
            _character = character;
            _inTown = false;
            _spCurrent = null;
            _spMax = null;
        }
        if (!character.HasValue || character == 0)
        {
            _state.Reset(false);
            return;
        }

        if (inTown.HasValue) _inTown = inTown.Value;
        _spCurrent = spCurrent;
        _spMax = spMax;
        _state.SetTownState(_inTown, _settings.SuppressAlertsInTown);

        if (entries is null) return;
        var ids = new HashSet<int>();
        Reaction? detectedBaseStance = null;
        foreach (var effect in entries)
        {
            ids.Add(effect.EffectId);
            string name = NameFor(effect.EffectDid, now);
            double? dur = effect.Duration.HasValue ? (double)effect.Duration.Value : null;
            Remember(effect.EffectDid, name, dur, now);
            if (ReactionMatcher.MatchBaseStance(name, dur) is { } stance)
                detectedBaseStance = stance;
            bool pausedChanged = false;
            if (_state.Contains(effect.EffectId))
            {
                pausedChanged = _state.IsPaused(effect.EffectId) != effect.IsPaused;
                if (!pausedChanged) continue;
            }
            var reaction = Match(effect.EffectDid, name, dur);
            if (reaction is null) continue;

            double? remaining = CalculateRemaining(effect);
            if (effect.IsPaused) remaining = null;
            _state.Apply(effect.EffectId, effect.EffectDid, reaction.Value, remaining,
                dur is > 0 ? dur.Value : 12, effect.IsPaused, false, now, refresh: pausedChanged);
        }
        _state.SetBaseStance(detectedBaseStance ?? Reaction.Any);
        _state.Retain(ids);
        foreach (var did in _effects.Where(x => now - x.Value.LastSeen > 60).Select(x => x.Key).ToArray())
            _effects.Remove(did);
    }

    // Drift-free mid-spike attach using DH 5's IActiveEffect.Expiration and Created properties.
    private static double? CalculateRemaining(IActiveEffect effect)
    {
        DateTime? expiry = effect.Expiration;
        if (expiry.HasValue && expiry.Value != DateTime.MaxValue)
        {
            var wallNow = expiry.Value.Kind == DateTimeKind.Utc ? DateTime.UtcNow : DateTime.Now;
            return Math.Max(0, (expiry.Value - wallNow).TotalSeconds);
        }
        if (effect.Duration is > 0 && effect.Created != default && effect.Created != DateTime.MinValue)
        {
            var wallNow = effect.Created.Kind == DateTimeKind.Utc ? DateTime.UtcNow : DateTime.Now;
            double elapsed = Math.Max(0, (wallNow - effect.Created).TotalSeconds);
            return Math.Max(0, effect.Duration.Value - elapsed);
        }
        return null;
    }

    private Reaction? Match(int did, string name, double? duration) =>
        _settings.EffectMappings.TryGetValue(did, out var mapped) ? mapped : ReactionMatcher.Match(name, duration);

    private void Remember(int did, string name, double? duration, double now) =>
        _effects[did] = new(did, name, duration, now);

    private string NameFor(int did, double now)
    {
        if (_names.TryGetValue(did, out var cache) && now < cache.RetryAt) return cache.Name;
        string result = "";
        try
        {
            var props = _data.GetWeenieProperties(unchecked((uint)did));
            IReadOnlyList<IProperty>? list = props?.Properties;
            if (list is not null)
            {
                foreach (var p in list)
                {
                    if (p is null || !(p.PropertyName ?? "").Contains("Name", StringComparison.OrdinalIgnoreCase)) continue;
                    if (p is IStringInfoProperty sip)
                    {
                        if (sip.IsLiteral && !string.IsNullOrWhiteSpace(sip.Text))
                        {
                            result = sip.Text;
                        }
                        else
                        {
                            result = sip.GetText(_data.PropertyMaster, unchecked((uint)did), props) ?? "";
                            if (string.IsNullOrWhiteSpace(result) && sip.Table.HasValue && sip.Key.HasValue && _data.PropertyMaster is not null)
                                result = _data.PropertyMaster.GetString(sip.Table.Value, sip.Key.Value) ?? "";
                        }
                    }
                    else if (p is IStringProperty sp)
                    {
                        result = sp.StringValue ?? "";
                    }
                    if (!string.IsNullOrWhiteSpace(result)) break;
                }
            }
        }
        catch { /* Names are optional; numeric effect mappings still work. Retry later. */ }
        if (_names.Count > 4096) _names.Clear();
        _names[did] = (result, string.IsNullOrWhiteSpace(result) ? now + 5 : double.PositiveInfinity);
        return result;
    }

    internal string ExportDiagnostics()
    {
        string path = Path.Combine(Folder, "reaction-timer-effects.json");
        var payload = new
        {
            PluginVersion = typeof(Dh5Plugin).Assembly.GetName().Version?.ToString() ?? "0.0.0.0",
            Host = "DH5",
            Snapshot = Snapshot(),
            Effects = GetEffects(),
            Settings = GetSettings()
        };
        File.WriteAllText(path, System.Text.Json.JsonSerializer.Serialize(payload,
            new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
        return path;
    }

    private void Log(string message)
    {
        try
        {
            string path = Path.Combine(Folder, "reaction-timer.log");
            if (File.Exists(path) && new FileInfo(path).Length > 256 * 1024)
                File.Move(path, path + ".previous", true);
            File.AppendAllText(path, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {message}{Environment.NewLine}");
        }
        catch { }
    }

    public void Dispose()
    {
        bool unregisterChat;
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            unregisterChat = _chatRegistered;
            _chatRegistered = false;
            _timer.Dispose();
            while (_events.TryDequeue(out _)) { }
            _state.Reset(false);
            Volatile.Write(ref _snapshot, _snapshot with { View = _state.Read(Now) });
        }
        if (unregisterChat)
        {
            try { _eventsProvider.OnChatCommand.RemoveHandler("alch"); }
            catch { }
        }
    }
}
