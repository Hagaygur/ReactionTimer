using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Windows.Forms;
namespace VoK.ReactionTimer;

internal enum HudCommand { Preview, Move, Stop }
internal sealed class HudController : IDisposable
{
    private readonly ConcurrentQueue<HudCommand> _commands = new();
    private readonly Thread _thread;
    private readonly TimerEngine _engine;
    private int _disposed;
    private string _status = "Starting transparent timer…";
    internal string Status => Volatile.Read(ref _status);
    public HudController(TimerEngine engine)
    {
        _engine = engine;
        _thread = new Thread(Run) { IsBackground = true, Name = "Reaction Timer HUD" };
        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();
    }
    private void Run()
    {
        try
        {
            using var form = new HudWindow(_engine, _commands, s => Volatile.Write(ref _status, s));
            Application.Run();
        }
        catch (Exception e)
        {
            Volatile.Write(ref _status, "HUD error: " + e.Message);
            _engine.Report("HUD failed: " + e);
        }
    }
    internal void Preview() { if (_disposed == 0) _commands.Enqueue(HudCommand.Preview); }
    internal void Move() { if (_disposed == 0) _commands.Enqueue(HudCommand.Move); }
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        _commands.Enqueue(HudCommand.Stop);
        // Do not block Dungeon Helper's UI during teardown. The background STA
        // exits on its next 100ms tick and disposes all native resources.
    }
}
