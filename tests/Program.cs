using System;
using System.Collections.Generic;
using System.IO;
using System.Drawing;
using VoK.ReactionTimer;

internal static class Program
{
    private static int _count;
    private static void Assert(bool condition, string name)
    {
        if (!condition) throw new Exception("FAILED: " + name);
        ++_count; Console.WriteLine("PASS " + name);
    }
    private static void Main(string[] args)
    {
        var s = new TimerState();
        Assert(s.Read(0).Mode == TimerMode.Waiting, "initial waiting state");
        s.Apply(1, 10, Reaction.Pyrite, 12, 12, false, false, 100, true);
        Assert(s.Read(104).Remaining == 8, "elapsed countdown");
        s.Apply(1, 10, Reaction.Pyrite, 12, 12, false, false, 104, false);
        Assert(s.Read(105).Remaining == 7, "repeated snapshot cannot restart a timer");
        s.Update(1, 6, 12, true, 106);
        Assert(s.Read(200).Mode == TimerMode.Paused && s.Read(200).Remaining == 6, "paused time does not elapse");
        s.Update(1, 6, 12, false, 200);
        Assert(s.Read(202).Remaining == 4, "resume from server remaining time");
        s.Update(1, 12, 12, false, 203);
        Assert(s.Read(204).Remaining == 11, "refresh with reused effect ID");
        long before = s.Read(214).Alarm;
        Assert(s.Read(215).Mode == TimerMode.Expired && s.Read(215).Alarm == before + 1, "expiration raises one alert");
        Assert(s.Read(300).Alarm == before + 1, "expired state does not repeat alert");
        s.Apply(1, 10, Reaction.Pyrite, 12, 12, false, false, 301, true);
        Assert(s.Read(302).Remaining == 11, "reapplication starts fresh");
        s.Remove(1);
        Assert(s.Read(303).Mode == TimerMode.Expired, "early game removal ends timer");
        s.Reset(true);
        s.Apply(2, 20, Reaction.Orchidium, 3, 12, false, false, 0, false);
        Assert(s.Read(1).Remaining == 2, "attach to partially elapsed effect");
        s.Reset(true);
        s.Apply(3, 30, Reaction.Verdanite, null, 12, false, false, 0, false);
        Assert(s.Read(100).Mode == TimerMode.Active && s.Read(100).Remaining is null, "unknown timing does not invent 12 seconds");
        s.Reset(false);
        Assert(s.Read(100).Mode == TimerMode.Disconnected, "logout clears old state");
        s.Reset(true);
        Assert(s.Read(100).Mode == TimerMode.Waiting, "new character starts waiting");
        s.Apply(1, 1, Reaction.Pyrite, 12, 12, false, false, 0, true);
        s.Apply(2, 2, Reaction.Orchidium, 12, 12, false, false, 1, true);
        Assert(s.Read(2).Reaction == Reaction.Orchidium, "newest overlapping spike displayed");
        s.Remove(2);
        Assert(s.Read(3).Reaction == Reaction.Pyrite, "remaining valid spike survives unrelated removal");
        s.Retain(new HashSet<int>());
        Assert(s.Read(4).Mode == TimerMode.Expired, "resync repairs a missed removal event");
        Assert(ReactionMatcher.Match("Pyrite Reaction Spike", 12) == Reaction.Pyrite, "named spike detection");
        Assert(ReactionMatcher.Match("Pyrite Reaction", -1) is null, "permanent base reaction excluded");
        Assert(ReactionMatcher.Match("Pyrite Reaction Spike", -1) is null, "permanent effect excluded despite name");
        Assert(ReactionMatcher.Match("Fire Shield", 12) is null, "unrelated 12-second effect excluded");
        Assert(ReactionMatcher.Match("Reaction Spike", 12) == Reaction.Any, "generic spike name supported");
        Assert(ReactionMatcher.Match("Orchidium Spike", 12) == Reaction.Orchidium, "Orchidium detection");
        Assert(ReactionMatcher.Match("Verdanite Spike", 12) == Reaction.Verdanite, "Verdanite detection");
        var settings = new Settings { WarningSeconds = double.NaN, FontSizePixels = 999, HudX = double.NaN, HudY = -1 };
        settings.Validate();
        Assert(settings.WarningSeconds == 2 && settings.FontSizePixels == 160 && settings.HudX == .5 && settings.HudY == 0, "invalid settings bounded");
        foreach (int requested in new[] { 10, 20, 30, 160 })
            Assert(SettingsInput.TryFontSize(requested.ToString(), out int parsed) && parsed == requested, $"font input {requested} stays exact");
        foreach (string invalid in new[] { "", "abc", "9", "161", "20.5", "999999999999" })
            Assert(!SettingsInput.TryFontSize(invalid, out _), "reject invalid font: " + invalid);
        string temp = Path.Combine(Path.GetTempPath(), "reaction-timer-tests-" + Guid.NewGuid());
        try
        {
            var store = new SettingsStore(temp);
            foreach (int pixels in new[] { 30, 20, 30, 20 })
            {
                var saved = store.Load(); saved.FontSizePixels = pixels; store.Save(saved);
                Assert(store.Load().FontSizePixels == pixels, $"save/reopen preserves {pixels}px");
            }
            File.WriteAllText(store.ConfigPath, "{\"TextScalePercent\":180,\"EffectMappings\":{\"42\":\"Pyrite\"}}");
            var migrated = store.Load();
            Assert(migrated.FontSizePixels == 28 && migrated.EffectMappings[42] == Reaction.Pyrite, "old settings retain mappings and use new pixel default");
        }
        finally { Directory.Delete(temp, true); }
        var monitor = new Rectangle(-1920, 0, 1920, 1080);
        Assert(HudPresentation.Position(monitor, new Size(200, 40), 0, 0) == new Point(-1920, 0), "negative monitor origin");
        Assert(HudPresentation.Position(monitor, new Size(200, 40), 1, 1) == new Point(-200, 1040), "HUD stays within monitor at bottom right");
        var active = new TimerView(TimerMode.Active, Reaction.Pyrite, 3.5, 12, false, 0, "");
        Assert(HudPresentation.Text(active, false) == "PYRITE  3.5", "compact countdown text");
        Assert(HudPresentation.Text(active, true) == "PREVIEW  3.5", "preview clearly marked");
        Console.WriteLine($"{_count} checks passed.");
        if (args.Length == 2 && args[0] == "--contracts") ContractSnapshot.Write(args[1]);
    }
}
