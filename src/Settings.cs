using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace VoK.ReactionTimer;

internal sealed class Settings
{
    public double WarningSeconds { get; set; } = 2;
    public bool SoundOnExpiry { get; set; }
    public int RespikeAlertSeconds { get; set; } = 5;
    public int FontSizePixels { get; set; } = 28;
    public bool HudEnabled { get; set; } = true;
    public double HudX { get; set; } = .5;
    public double HudY { get; set; } = .72;
    public Dictionary<int, Reaction> EffectMappings { get; set; } = new();
    public Settings Copy() => new() { WarningSeconds = WarningSeconds, SoundOnExpiry = SoundOnExpiry,
        RespikeAlertSeconds = RespikeAlertSeconds, FontSizePixels = FontSizePixels, HudEnabled = HudEnabled, HudX = HudX, HudY = HudY, EffectMappings = new(EffectMappings) };
    public void Validate()
    {
        if (!double.IsFinite(WarningSeconds)) WarningSeconds = 2;
        WarningSeconds = Math.Clamp(WarningSeconds, 0, 10);
        FontSizePixels = Math.Clamp(FontSizePixels, 10, 160);
        RespikeAlertSeconds = Math.Clamp(RespikeAlertSeconds, 0, 60);
        HudX = double.IsFinite(HudX) ? Math.Clamp(HudX, 0, 1) : .5;
        HudY = double.IsFinite(HudY) ? Math.Clamp(HudY, 0, 1) : .72;
        EffectMappings ??= new();
        foreach (var value in EffectMappings.Values)
            if (!Enum.IsDefined(value)) throw new InvalidDataException("Invalid reaction mapping");
    }
}

internal sealed class SettingsStore
{
    public string DirectoryPath { get; }
    public string ConfigPath => Path.Combine(DirectoryPath, "reaction-timer.json");
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true,
        PropertyNameCaseInsensitive = true, Converters = { new JsonStringEnumConverter() } };
    public SettingsStore(string directory) { DirectoryPath = directory; Directory.CreateDirectory(directory); }
    public Settings Load()
    {
        if (!File.Exists(ConfigPath)) { var defaults = new Settings(); Save(defaults); return defaults; }
        var settings = JsonSerializer.Deserialize<Settings>(File.ReadAllText(ConfigPath), Json) ?? new();
        settings.Validate(); return settings;
    }
    public void Save(Settings settings)
    {
        settings.Validate();
        string temporary = ConfigPath + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(settings, Json));
        File.Move(temporary, ConfigPath, overwrite: true);
    }
}

internal static class SettingsInput
{
    public static bool TryFontSize(string text, out int value) =>
        int.TryParse(text.Trim(), System.Globalization.NumberStyles.Integer,
            System.Globalization.CultureInfo.InvariantCulture, out value) && value >= 10 && value <= 160;
}
