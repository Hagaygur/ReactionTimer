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
