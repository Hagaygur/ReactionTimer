using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VoK.Sdk;
using VoK.Sdk.Ddo;
using VoK.Sdk.Plugins;
using VoK.Sdk.Properties;

namespace VoK.ReactionTimer;

public sealed class ReactionTimerPlugin : IDdoPlugin
{
    public Guid PluginId => new("f8d154e8-a756-4d85-a178-12b9575d810c");
    public GameId Game => GameId.DDO;
    public string PluginKey => "alchemist-reaction-timer";
    public string Name => "Alchemist Reaction Timer";
    public string Description => "Large Reaction Spike countdown in Dungeon Helper";
    public string Author => "Community";
    public Version Version => typeof(ReactionTimerPlugin).Assembly.GetName().Version!;
    private TimerEngine? _engine;
    private ReactionTimerUi? _ui;
    private HudController? _hud;
    internal HudController? Hud => _hud;
    internal TimerEngine? Engine => _engine;
    public IPluginUI GetPluginUI() => _ui ??= new ReactionTimerUi(this);
    public void Initialize(IDdoGameDataProvider provider, string folder)
    {
        _hud?.Dispose();
        _engine?.Dispose();
        if (string.IsNullOrWhiteSpace(folder)) folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Dungeon Helper", "ReactionTimer");
        _engine = new TimerEngine(provider, folder);
        _hud = new HudController(_engine);
    }
    public void Terminate()
    {
        _hud?.Dispose(); _hud = null;
        _engine?.Dispose(); _engine = null;
        _ui?.Dispose(); _ui = null;
    }
}

internal sealed record EffectInfo(int Did, string Name, double? Duration, double LastSeen);
internal sealed record EffectMessage(int Kind, int Id = 0, int Did = 0, double? Duration = null,
    double Remaining = 0, bool Paused = false, double ReceivedAt = 0);

internal sealed class TimerEngine : IDisposable
{
    private readonly IDdoGameDataProvider _data;
    private readonly SettingsStore _store;
    private Settings _settings;
    private readonly TimerState _state = new();
    private readonly object _gate = new();
    private readonly ConcurrentQueue<EffectMessage> _events = new();
    private readonly Dictionary<int, (string Name, double RetryAt)> _names = new();
    private readonly Dictionary<int, EffectInfo> _effects = new();
    private readonly System.Threading.Timer _timer;
    private volatile bool _disposed;
    private double _nextScan, _lastErrorLog = -100;
    private TimerView _view = new(TimerMode.Waiting, Reaction.Any, null, 12, false, 0, "Waiting for effect data");
    private ulong? _character;
    internal static double Now => Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency;
    internal string Folder => _store.DirectoryPath;
    internal IntPtr GameWindow { get; }
    private int _settingsRevision;
    internal int SettingsRevision => Volatile.Read(ref _settingsRevision);

    public TimerEngine(IDdoGameDataProvider data, string folder)
    {
        _data = data; GameWindow = data.GetGameWindowHandle(); _store = new SettingsStore(folder);
        try { _settings = _store.Load(); }
        catch (Exception e) { _settings = new(); Log("Settings could not be loaded; defaults in use: " + e.Message); }
        // SDK 4.2.1 registrars do not expose unsubscription. Each handler becomes
        // a no-op on disposal; Dungeon Helper owns and destroys the per-client provider.
        var ev = data.EventProvider;
        ev.OnEffectExpress.AddHandler(e => Enqueue(e.EffectId.HasValue
            ? new(1, e.EffectId.Value, e.EffectDid, e.Duration, ReceivedAt: Now) : new(5)));
        ev.OnEffectSuppress.AddHandler(id => Enqueue(new(2, id)));
        ev.OnEffectUpdateDuration.AddHandler(e => Enqueue(new(3, e.EffectId,
            Duration: e.OriginalDuration, Remaining: e.RemainingSeconds, Paused: e.IsPaused, ReceivedAt: Now)));
        ev.OnLogin.AddHandler(_ => Enqueue(new(4)));
        ev.OnLogout.AddHandler(() => Enqueue(new(6)));
        _timer = new System.Threading.Timer(_ => Tick(), null, 0, 100);
        Log($"Started v{typeof(ReactionTimerPlugin).Assembly.GetName().Version}; independent transparent HUD; SDK 4.2.1.0.");
    }

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
            _store.Save(settings); _settings = settings.Copy();
            Interlocked.Increment(ref _settingsRevision);
            if (mappingsChanged) { _state.Reset(_character.HasValue); _nextScan = 0; }
        }
    }
    internal void SavePosition(double x, double y)
    {
        lock (_gate)
        {
            if (_disposed) return;
            var settings = _settings.Copy(); settings.HudX = x; settings.HudY = y;
            _store.Save(settings); _settings = settings; Interlocked.Increment(ref _settingsRevision);
        }
    }
    internal void Report(string message) { lock (_gate) Log(message); }
    internal EffectInfo[] GetEffects()
    {
        lock (_gate) return _effects.Values.OrderByDescending(x => x.LastSeen).ToArray();
    }
    // The UI reads an immutable snapshot and never waits for an SDK call.
    internal TimerView View() => Volatile.Read(ref _view);
    private void Tick()
    {
        if (_disposed || !Monitor.TryEnter(_gate)) return;
        try
        {
            if (_disposed) return;
            double now = Now;
            while (_events.TryDequeue(out var e))
            {
                if (e.Kind == 4 || e.Kind == 6)
                {
                    _state.Reset(e.Kind == 4); _effects.Clear(); _names.Clear(); _character = null;
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
                var reaction = Match(e.Did, name, e.Duration);
                if (reaction is null) continue;
                double duration = e.Duration is > 0 and <= 120 ? e.Duration.Value : 12;
                _state.Apply(e.Id, e.Did, reaction.Value, Math.Max(0, duration - (now - e.ReceivedAt)),
                    duration, false, e.Duration is null or <= 0, now, refresh: true);
            }
            if (now >= _nextScan) { Scan(now); _nextScan = now + 1; }
            Volatile.Write(ref _view, _state.Read(now));
        }
        catch (Exception e)
        {
            Volatile.Write(ref _view, new(TimerMode.Error, Reaction.Any, null, 12, false,
                _view.Alarm, "Effect data unavailable • retrying"));
            if (Now - _lastErrorLog > 15) { Log("SDK read failed: " + e.Message); _lastErrorLog = Now; }
        }
        finally { Monitor.Exit(_gate); }
    }

    private void Scan(double now)
    {
        var character = _data.GetCurrentCharacterId();
        if (character != _character)
        {
            _state.Reset(character.HasValue && character != 0); _effects.Clear(); _names.Clear();
            _character = character;
        }
        if (!character.HasValue || character == 0) { _state.Reset(false); return; }
        var active = _data.GetActiveEffects();
        if (active is null) throw new InvalidOperationException("Active effects not available");
        // Snapshot before reconciliation. A failed enumeration must not remove tracked effects.
        var entries = active.Values.ToArray();
        var ids = new HashSet<int>();
        foreach (var effect in entries)
        {
            ids.Add(effect.EffectId);
            string name = NameFor(effect.EffectDid, now);
            Remember(effect.EffectDid, name, effect.Duration, now);
            if (_state.Contains(effect.EffectId)) continue;
            var reaction = Match(effect.EffectDid, name, effect.Duration);
            if (reaction is null) continue;
            double? remaining = null;
            var expiry = effect.CalculateExpiration();
            if (expiry.HasValue && expiry.Value != DateTime.MaxValue)
            {
                var wallNow = expiry.Value.Kind == DateTimeKind.Utc ? DateTime.UtcNow : DateTime.Now;
                remaining = Math.Max(0, (expiry.Value - wallNow).TotalSeconds);
            }
            // Never invent a new 12 seconds when attaching to an already-running buff.
            // A paused effect with no reliable remaining time is displayed as PAUSED.
            if (effect.IsPaused) remaining = null;
            _state.Apply(effect.EffectId, effect.EffectDid, reaction.Value, remaining,
                effect.Duration is > 0 ? effect.Duration.Value : 12, effect.IsPaused, false, now, false);
        }
        _state.Retain(ids);
        foreach (var did in _effects.Where(x => now - x.Value.LastSeen > 60).Select(x => x.Key).ToArray()) _effects.Remove(did);
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
            if (props?.Properties is not null)
                foreach (var pair in props.Properties)
                {
                    var p = pair.Value;
                    if (p is null || !(p.PropertyName ?? "").Contains("Name", StringComparison.OrdinalIgnoreCase)) continue;
                    if (p is IStringInfoProperty sip)
                    {
                        result = (sip.IsLiteral ? sip.Text : sip.GetStringEntry(_data.PropertyMaster)?.Value) ?? "";
                        if (string.IsNullOrWhiteSpace(result)) result = sip.GetText(_data.PropertyMaster, unchecked((uint)did), props) ?? "";
                    }
                    else if (p is IStringProperty sp) result = sp.StringValue ?? "";
                    if (!string.IsNullOrWhiteSpace(result)) break;
                }
        }
        catch { /* Names are optional; numeric effect mappings still work. Retry later. */ }
        if (_names.Count > 4096) _names.Clear();
        _names[did] = (result, string.IsNullOrWhiteSpace(result) ? now + 5 : double.PositiveInfinity);
        return result;
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
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true; _timer.Dispose();
            while (_events.TryDequeue(out _)) { }
            _state.Reset(false);
            Volatile.Write(ref _view, _state.Read(Now));
        }
    }
}
