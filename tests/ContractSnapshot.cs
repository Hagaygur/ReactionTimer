using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using VoK.ReactionTimer;

// These are observations of the implementation, emitted only after tests pass.
// Removing/changing compatibility observations requests major; adding them minor.
// Feature observations change/add => minor; removal => major. Add a feature
// observation and a behavioral assertion when implementing a new capability.
internal static class ContractSnapshot
{
    public static void Write(string path)
    {
        var compatibility = new SortedDictionary<string, object>();
        foreach (var property in typeof(Settings).GetProperties())
            compatibility["settings." + property.Name] = property.PropertyType.ToString();
        foreach (var reaction in Enum.GetValues<Reaction>())
            compatibility["reaction." + reaction] = (int)reaction;
        var settings = new Settings();
        var alertState = new HudAlertState();
        var expired = new TimerView(TimerMode.Expired, Reaction.Pyrite, 0, 12, false, 1, "");
        var alert = alertState.Update(expired, 0, settings.RespikeAlertSeconds);
        var active = new TimerView(TimerMode.Active, Reaction.Pyrite, 3.5, 12, false, 0, "");
        var state = new TimerState();
        state.Apply(1, 10, Reaction.Pyrite, 12, 12, false, false, 100, true);
        compatibility["countdown.after-4s"] = state.Read(104).Remaining!;
        state.Update(1, 6, 12, true, 106);
        compatibility["countdown.paused"] = state.Read(200).Remaining!;
        compatibility["detect.pyrite"] = ReactionMatcher.Match("Pyrite Reaction Spike", 12).ToString()!;
        compatibility["detect.permanent-excluded"] = ReactionMatcher.Match("Pyrite Reaction", -1) is null;
        var features = new SortedDictionary<string, object>
        {
            ["hud.active-text"] = HudPresentation.Text(active, false),
            ["hud.preview-text"] = HudPresentation.Text(active, true),
            ["hud.expired-text"] = HudPresentation.Text(active with { Mode = TimerMode.Expired }, false),
            ["font.default"] = settings.FontSizePixels,
            ["font.accepts-20"] = SettingsInput.TryFontSize("20", out _),
            ["font.accepts-30"] = SettingsInput.TryFontSize("30", out _),
            ["sound.default"] = settings.SoundOnExpiry,
            ["warning.default"] = settings.WarningSeconds,
            ["respike-alert.default-seconds"] = settings.RespikeAlertSeconds,
            ["respike-alert.font-size"] = alert.FontSize(settings.FontSizePixels),
            ["respike-alert.starts-black"] = alert.BlackBackground,
            ["respike-alert.black-off-at-half-second"] = !alertState.Update(expired, .5, settings.RespikeAlertSeconds).BlackBackground,
            ["respike-alert.ends-at-duration"] = !alertState.Update(expired, settings.RespikeAlertSeconds, settings.RespikeAlertSeconds).Enlarged
        };
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        File.WriteAllText(path, JsonSerializer.Serialize(new { schema = 1, compatibility, features },
            new JsonSerializerOptions { WriteIndented = true }));
    }
}
