using System;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Windows.Forms;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        string pluginPath = Path.GetFullPath(args[0]);
        string sdkPath = Path.Combine(Path.GetFullPath(args[1]), "VoK.Sdk.dll");
        AssemblyLoadContext.Default.Resolving += (_, name) => name.Name == "VoK.Sdk"
            ? AssemblyLoadContext.Default.LoadFromAssemblyPath(sdkPath) : null;
        var pluginAssembly = LoadPlugin(pluginPath);
        if (pluginAssembly.GetCustomAttribute<System.Runtime.Versioning.SupportedOSPlatformAttribute>()?.PlatformName != "windows7.0")
            throw new Exception("Plugin must declare its net8.0-windows platform contract");
        var sdk = Assembly.LoadFrom(sdkPath);
        var pluginType = pluginAssembly.GetTypes().Single(t => t.IsPublic && !t.IsAbstract &&
            sdk.GetType("VoK.Sdk.Ddo.IDdoPlugin")!.IsAssignableFrom(t));
        var plugin = Activator.CreateInstance(pluginType)!;
        var ui = pluginType.GetMethod("GetPluginUI")!.Invoke(plugin, null)!;
        var contract = sdk.GetType("VoK.Sdk.Plugins.IPluginUI")!;
        using var host = new Form { Width = 400, Height = 400 };
        var strip = new Panel { Width = 720, Height = 45 };
        strip.Controls.Add(new PictureBox { Name = (string)pluginType.GetProperty("Name")!.GetValue(plugin)!, Left = 680, Width = 36 });
        host.Controls.Add(strip); host.Show();
        using var form = (Form)contract.GetProperty("UserInterfaceForm")!.GetValue(ui)!;
        form.Show(); Application.DoEvents(); form.PerformLayout();
        if (form.Width < strip.Width) throw new Exception("Panel would clip host toolbar");
        CheckButtons(form);
        if ((Version)pluginType.GetProperty("Version")!.GetValue(plugin)! != pluginAssembly.GetName().Version)
            throw new Exception("Runtime version disagrees with assembly");
        Console.WriteLine("PASS Windows plugin loading, toolbar-width layout, button bounds and version consistency");
        CheckRespikeUi(pluginAssembly);
    }
    private static void CheckRespikeUi(Assembly assembly)
    {
        const BindingFlags fields = BindingFlags.Instance | BindingFlags.NonPublic;
        var engineType = assembly.GetType("VoK.ReactionTimer.TimerEngine")!;
        // Seed only the UI-facing state; no SDK provider, worker or game is started.
        var engine = System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(engineType);
        void Set(string name, object value) => engineType.GetField(name, fields)!.SetValue(engine, value);
        string folder = Path.Combine(Path.GetTempPath(), "reaction-hud-smoke-" + Guid.NewGuid());
        Directory.CreateDirectory(folder);
        try
        {
            var settings = Activator.CreateInstance(assembly.GetType("VoK.ReactionTimer.Settings")!)!;
            settings.GetType().GetProperty("FontSizePixels")!.SetValue(settings, 30);
            Set("_gate", new object()); Set("_settings", settings);
            Set("_effects", Activator.CreateInstance(engineType.GetField("_effects", fields)!.FieldType)!);
            Set("_store", Activator.CreateInstance(assembly.GetType("VoK.ReactionTimer.SettingsStore")!, folder)!);
            using var dialog = (Form)Activator.CreateInstance(assembly.GetType("VoK.ReactionTimer.SettingsDialog")!, engine)!;
            dialog.Show(); dialog.Size = dialog.MinimumSize; Application.DoEvents(); dialog.PerformLayout();
            var input = (NumericUpDown)dialog.Controls.Find("RespikeAlertSeconds", true).Single();
            if (!input.Visible || !input.Parent!.ClientRectangle.Contains(input.Bounds) || input.Value != 5)
                throw new Exception("Respike duration control is clipped or has the wrong default");
            CheckButtons(dialog);
            input.Value = 17;
            Button FindSave(Control parent) => parent.Controls.Cast<Control>()
                .SelectMany(Descendants).OfType<Button>().Single(button => button.Text == "Save");
            FindSave(dialog).PerformClick();
            using (var saved = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(folder, "reaction-timer.json"))))
                if (saved.RootElement.GetProperty("RespikeAlertSeconds").GetInt32() != 17 ||
                    saved.RootElement.GetProperty("FontSizePixels").GetInt32() != 30)
                    throw new Exception("Settings UI did not preserve duration and exact font pixels");

            var hudType = assembly.GetType("VoK.ReactionTimer.HudWindow")!;
            var queue = Activator.CreateInstance(typeof(System.Collections.Concurrent.ConcurrentQueue<>)
                .MakeGenericType(assembly.GetType("VoK.ReactionTimer.HudCommand")!))!;
            using var hud = (Form)Activator.CreateInstance(hudType, engine, queue, new Action<string>(_ => { }))!;
            ((System.Windows.Forms.Timer)hudType.GetField("_tick", fields)!.GetValue(hud)!).Stop();
            var view = Activator.CreateInstance(assembly.GetType("VoK.ReactionTimer.TimerView")!,
                Enum.Parse(assembly.GetType("VoK.ReactionTimer.TimerMode")!, "Expired"),
                Enum.Parse(assembly.GetType("VoK.ReactionTimer.Reaction")!, "Pyrite"), 0d, 12d, false, 1L, "")!;
            var frameType = assembly.GetType("VoK.ReactionTimer.HudAlertFrame")!;
            void Render(bool enlarged, bool black) => hudType.GetMethod("Render", fields)!.Invoke(hud,
                new[] { view, false, 0d, Activator.CreateInstance(frameType, enlarged, black)! });
            Color Corner()
            {
                var surface = hudType.GetField("_surface", fields)!.GetValue(hud)!;
                // Sample inside the plate, outside the text, avoiding its antialiased outer edge.
                return ((Bitmap)surface.GetType().GetField("_image", fields)!.GetValue(surface)!).GetPixel(1, 1);
            }
            Render(false, false); int normalHeight = hud.ClientSize.Height;
            Render(true, true); var enlargedSize = hud.ClientSize;
            if (hud.ClientSize.Height <= normalHeight * 1.5 || Corner().ToArgb() != Color.Black.ToArgb())
                throw new Exception($"Expiry HUD did not grow and draw opaque black: height {normalHeight} -> {hud.ClientSize.Height}, corner {Corner()}");
            Render(true, false);
            if (hud.ClientSize != enlargedSize || Corner().A != 0)
                throw new Exception("Flash off must restore transparency without shrinking the text");
            Render(false, false);
            if (hud.ClientSize.Height != normalHeight || Corner().A != 0)
                throw new Exception("Alert end must restore normal size and transparency");
            Console.WriteLine("PASS respike settings save/layout and native HUD size/black-background/transparency transitions");
        }
        finally { Directory.Delete(folder, true); }
    }
    private static System.Collections.Generic.IEnumerable<Control> Descendants(Control root)
    {
        yield return root;
        foreach (Control child in root.Controls)
            foreach (var descendant in Descendants(child)) yield return descendant;
    }
    private static Assembly LoadPlugin(string path)
    {
        if (!path.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)) return Assembly.LoadFrom(path);
        using var archive = ZipFile.OpenRead(path);
        var expected = new[] { "dll", "deps.json", "metadata" }
            .Select(extension => "plugins/ReactionTimer/VoK.ReactionTimer." + extension);
        if (!archive.Entries.Select(entry => entry.FullName).SequenceEqual(expected))
            throw new Exception("Installer must contain exactly three runtime entries, DLL first");
        using var source = archive.Entries[0].Open();
        using var assembly = new MemoryStream();
        source.CopyTo(assembly);
        assembly.Position = 0;
        return AssemblyLoadContext.Default.LoadFromStream(assembly);
    }
    private static void CheckButtons(Control root)
    {
        foreach (Control control in root.Controls)
        {
            if (control is Button && (!root.ClientRectangle.Contains(control.Bounds) ||
                control.Height < control.GetPreferredSize(Size.Empty).Height))
                throw new Exception("Clipped button: " + control.Text);
            CheckButtons(control);
        }
    }
}
