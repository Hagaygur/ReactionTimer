using System;
using System.IO;
using VoK.Sdk;
using VoK.Sdk.Ddo;

namespace VoK.ReactionTimer;

[PluginId("fc0b3f68-e30d-4d1f-b2ac-4e5d62bb1725")]
[PluginName("Alch Reaction Timer")]
[PluginIcon("Icon.png")]
public sealed class Dh5Plugin : IPlugin
{
    public const string PluginGuid = "fc0b3f68-e30d-4d1f-b2ac-4e5d62bb1725";
    public const string PluginDisplayName = "Alch Reaction Timer";

    private Dh5TimerEngine? _engine;
    private Dh5ReactionTimerUi? _ui;

    // Icon comes from [PluginIcon("Icon.png")] embedded resource.
    public byte[] ToolbarImage => null!;

    // Must supply a real backing property: IPlugin's default ignores sets and always returns true.
    public bool WindowVisible { get; set; }

    internal Dh5TimerEngine? Engine => _engine;

    public void Initialize(IDdoGameDataProvider gameDataProvider, IDdoEventProvider eventProvider, string pluginFolder)
    {
        Dispose();
        if (string.IsNullOrWhiteSpace(pluginFolder))
        {
            pluginFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "DungeonHelperData",
                "plugins",
                PluginDisplayName);
        }

        _engine = new Dh5TimerEngine(gameDataProvider, eventProvider, pluginFolder);
        _ui = new Dh5ReactionTimerUi(_engine);
    }

    public void ClientFrameStep()
    {
        // Keep the DDO client frame step lightweight and non-blocking.
        _engine?.NoteFrameStep();
    }

    public void ImGui_Render()
    {
        _ui?.Render(this);
    }

    public bool ResetUi()
    {
        _ui?.ResetLayout();
        _engine?.ResetHudLayout();
        return true;
    }

    public void Dispose()
    {
        _ui = null;
        _engine?.Dispose();
        _engine = null;
    }
}
