using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Media;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace VoK.ReactionTimer;

internal sealed class HudWindow : Form
{
    private readonly TimerEngine _engine;
    private readonly ConcurrentQueue<HudCommand> _commands;
    private readonly Action<string> _status;
    private readonly System.Windows.Forms.Timer _tick = new() { Interval = 100 };
    private readonly HudSurface _surface = new();
    private readonly FontFamily _family = new("Segoe UI");
    private readonly Font _editFont = new("Segoe UI", 11, FontStyle.Regular, GraphicsUnit.Pixel);
    private Settings _settings = new();
    private int _revision = -1;
    private double _previewStart = double.NegativeInfinity, _editUntil, _nextRaise, _nextScreenCheck, _lastMove, _lastErrorLog = -100;
    private bool _editing, _movingProgrammatically, _positionDirty, _needsPosition = true;
    private string _captureStatus = "Capture exclusion not checked", _lastRenderKey = "";
    private Rectangle _screen;
    private IntPtr _foreground;
    private bool _foregroundAllowed;
    private long _lastAlarm = -1;
    private uint _gamePid;

    public HudWindow(TimerEngine engine, ConcurrentQueue<HudCommand> commands, Action<string> status)
    {
        _engine = engine; _commands = commands; _status = status;
        Text = "Alchemist Reaction Timer HUD"; FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false; TopMost = true; AutoScaleMode = AutoScaleMode.None;
        StartPosition = FormStartPosition.Manual;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint, true);
        try
        {
            NativeHud.GetWindowThreadProcessId(_engine.GameWindow, out _gamePid);
            _screen = Screen.FromHandle(_engine.GameWindow).Bounds;
            _ = Handle; // Real top-level HWND, independent of Dungeon Helper's hosted panel.
            _tick.Tick += (_, _) => TickHud(); _tick.Start();
        }
        catch { Dispose(); throw; }
    }
    protected override bool ShowWithoutActivation => true;
    protected override CreateParams CreateParams
    {
        get { var p = base.CreateParams; p.ExStyle |= NativeHud.Layered | NativeHud.NoActivate | NativeHud.ToolWindow | NativeHud.Transparent; return p; }
    }
    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        bool set = NativeHud.SetWindowDisplayAffinity(Handle, 0x11);
        int error = Marshal.GetLastWin32Error();
        bool verified = set && NativeHud.GetWindowDisplayAffinity(Handle, out var affinity) && affinity == 0x11;
        _captureStatus = verified ? "Capture exclusion: ON" : $"Capture exclusion FAILED (Windows error {error})";
        _status(_captureStatus); _engine.Report(_captureStatus);
    }
    protected override void OnPaint(PaintEventArgs e) { }
    protected override void OnPaintBackground(PaintEventArgs e) { }
    protected override void WndProc(ref Message m)
    {
        if (m.Msg == 0x84) { m.Result = _editing ? new IntPtr(2) : new IntPtr(-1); return; } // caption / transparent hit-test
        if (m.Msg == 0x21) { m.Result = new IntPtr(3); return; } // MA_NOACTIVATE
        base.WndProc(ref m);
    }
    protected override void OnLocationChanged(EventArgs e)
    {
        base.OnLocationChanged(e);
        if (_editing && !_movingProgrammatically) { _positionDirty = true; _lastMove = TimerEngine.Now; }
    }
    private void TickHud()
    {
        try
        {
            double now = TimerEngine.Now;
            while (_commands.TryDequeue(out var command))
            {
                if (command == HudCommand.Stop) { _tick.Stop(); Hide(); Application.ExitThread(); return; }
                if (command == HudCommand.Preview) _previewStart = now;
                if (command == HudCommand.Move) _editUntil = now + 20;
            }
            if (_positionDirty && now - _lastMove > .5)
            {
                _engine.SavePosition((Left - _screen.Left) / (double)Math.Max(1, _screen.Width - Width),
                    (Top - _screen.Top) / (double)Math.Max(1, _screen.Height - Height));
                _positionDirty = false;
            }
            int revision = _engine.SettingsRevision;
            if (revision != _revision)
            {
                _settings = _engine.GetSettings(); _revision = revision;
                _lastRenderKey = ""; _needsPosition = true;
            }
            bool editing = now < _editUntil;
            if (editing != _editing)
            {
                _editing = editing; _lastRenderKey = "";
                long style = NativeHud.GetWindowLongPtr(Handle, -20).ToInt64();
                style = editing ? style & ~NativeHud.Transparent : style | NativeHud.Transparent;
                NativeHud.SetWindowLongPtr(Handle, -20, new IntPtr(style));
            }
            if (now >= _nextScreenCheck)
            {
                var screen = Screen.FromHandle(_engine.GameWindow).Bounds;
                if (screen != _screen) { _screen = screen; _needsPosition = true; }
                _nextScreenCheck = now + 1;
            }
            bool demo = now - _previewStart < 15;
            var live = _engine.View();
            var view = demo ? new TimerView(now - _previewStart < 12 ? TimerMode.Active : TimerMode.Expired,
                Reaction.Pyrite, Math.Max(0, 12 - (now - _previewStart)), 12, false, 0, "Preview") : live;
            bool foreground = ForegroundAllowed();
            bool display = (_settings.HudEnabled || demo || editing) &&
                (demo || editing || (foreground && live.Mode != TimerMode.Disconnected));
            if (display && !demo && _lastAlarm >= 0 && live.Alarm > _lastAlarm &&
                _settings.SoundOnExpiry && live.Mode == TimerMode.Expired) SystemSounds.Exclamation.Play();
            _lastAlarm = live.Alarm;
            _status(_captureStatus + (editing ? " • drag timer now" : display ? " • HUD visible" : " • HUD hidden"));
            if (!display) { if (Visible) Hide(); return; }
            Render(view, demo, now);
            if (!Visible) { Show(); _nextRaise = 0; }
            // LS may raise its own window after scaling begins. Reassert Z-order
            // only while the game, LS or DH is active, without ever taking focus.
            if (now >= _nextRaise)
            {
                NativeHud.SetWindowPos(Handle, new IntPtr(-1), 0, 0, 0, 0, 0x13);
                _nextRaise = now + .5;
            }
        }
        catch (Exception e)
        {
            _status("HUD error: " + e.Message);
            if (TimerEngine.Now - _lastErrorLog > 15) { _engine.Report("HUD rendering error: " + e); _lastErrorLog = TimerEngine.Now; }
        }
    }
    private bool ForegroundAllowed()
    {
        var current = NativeHud.GetForegroundWindow();
        if (current == _foreground) return _foregroundAllowed;
        _foreground = current;
        NativeHud.GetWindowThreadProcessId(current, out uint pid);
        _foregroundAllowed = pid != 0 && (pid == _gamePid || pid == Environment.ProcessId);
        if (!_foregroundAllowed && pid != 0)
            try
            {
                using var process = Process.GetProcessById(checked((int)pid));
                _foregroundAllowed = process.ProcessName.Replace(" ", "").Equals("LosslessScaling", StringComparison.OrdinalIgnoreCase);
            }
            catch (ArgumentException) { }
            catch (System.ComponentModel.Win32Exception) { }
        return _foregroundAllowed;
    }
    private void Render(TimerView view, bool demo, double now)
    {
        string text = HudPresentation.Text(view, demo);
        bool urgent = view.Mode == TimerMode.Expired || (view.Mode == TimerMode.Active && view.Remaining <= _settings.WarningSeconds);
        Color color = urgent ? Color.FromArgb(255, 99, 108) : view.Reaction switch
        {
            Reaction.Pyrite => Color.FromArgb(255, 181, 67),
            Reaction.Orchidium => Color.FromArgb(189, 145, 255),
            Reaction.Verdanite => Color.FromArgb(103, 222, 159),
            _ => Color.FromArgb(180, 208, 230)
        };
        string key = $"{text}/{color.ToArgb()}/{_settings.FontSizePixels}/{(_editing ? Math.Ceiling(_editUntil - now) : 0)}";
        if (key == _lastRenderKey && !_needsPosition) return;
        using var path = new GraphicsPath();
        path.AddString(text, _family, (int)FontStyle.Bold, _settings.FontSizePixels, PointF.Empty, StringFormat.GenericTypographic);
        var bounds = path.GetBounds();
        using (var shift = new Matrix()) { shift.Translate(5 - bounds.X, 5 - bounds.Y); path.Transform(shift); }
        int width = Math.Max(_editing ? 230 : 1, (int)Math.Ceiling(bounds.Width) + 10);
        int height = (int)Math.Ceiling(bounds.Height) + 10 + (_editing ? 20 : 0);
        bool sizeChanged = _surface.Size.Width != width || _surface.Size.Height != height;
        _surface.Resize(width, height);
        _movingProgrammatically = true;
        try
        {
            ClientSize = _surface.Size;
            if (_needsPosition || (sizeChanged && !_editing)) Location = HudPresentation.Position(_screen, Size, _settings.HudX, _settings.HudY);
        }
        finally { _movingProgrammatically = false; }
        _needsPosition = false;
        var g = _surface.Graphics!;
        g.CompositingMode = CompositingMode.SourceCopy; g.Clear(Color.Transparent);
        g.CompositingMode = CompositingMode.SourceOver; g.SmoothingMode = SmoothingMode.AntiAlias;
        if (_editing)
        {
            using var plate = new SolidBrush(Color.FromArgb(160, 20, 24, 30)); g.FillRectangle(plate, 0, 0, width, height);
            using var hint = new SolidBrush(Color.White);
            g.DrawString($"DRAG TO MOVE • locks in {Math.Ceiling(_editUntil - now):0}s", _editFont, hint, 5, height - 18);
        }
        using var outline = new Pen(Color.FromArgb(210, 0, 0, 0), 2.5f) { LineJoin = LineJoin.Round };
        using var fill = new SolidBrush(color);
        g.DrawPath(outline, path); g.FillPath(fill, path); g.Flush();
        _surface.Present(Handle, Location);
        _lastRenderKey = key;
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing) { _tick.Stop(); _tick.Dispose(); _surface.Dispose(); _family.Dispose(); _editFont.Dispose(); }
        base.Dispose(disposing);
    }
}
