using System.IO;
using System.Reflection;
using Rhino.Runtime;

namespace Rhino.AI.UI;

// Plan 021 / ADR-0007: the image of a command's toolbar button, read from the student's own Rhino install only.
// Plug-in icons are inline in the .rui; built-in icons are embedded in Rhino.UI.dll. Any failure means "none".
// UI thread only (it reads the loaded toolbar files through ToolbarLocator).
internal static class ToolbarIcons
{
    private static readonly Dictionary<string, (string? Light, string? Dark)> Cache = new(StringComparer.OrdinalIgnoreCase);
    private static Dictionary<Guid, (string Light, string Dark)>? _builtIn;
    private static Assembly? _resources;

    public static (string? Light, string? Dark) TryGet(string command)
    {
        if (Cache.TryGetValue(command, out var cached)) return cached;
        (string? Light, string? Dark) found = (null, null);
        try
        {
            foreach (var index in ToolbarLocator.LoadedIndexes())
            {
                var id = index.BitmapIdFor(command);
                if (id is null) continue;

                string? light = null, dark = null;
                if (index.InlineIcon(id) is { } inline)
                    (light, dark) = inline;
                else if (Guid.TryParse(id, out var guid))
                    (light, dark) = BuiltIn(guid);

                light = IconIds.Accept(light);
                dark = IconIds.Accept(dark);
                if (light is null && dark is null) continue;
                found = (light ?? dark, dark ?? light);
                break;
            }
        }
        catch (Exception ex)
        {
            HostUtils.LogDebugEvent($"[owly] toolbar icon lookup failed: {ex.Message}\n");
            found = (null, null);
        }

        Cache[command] = found;
        return found;
    }

    private static (string? Light, string? Dark) BuiltIn(Guid id)
    {
        _resources ??= AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "Rhino.UI");
        if (_resources is null) return (null, null);

        _builtIn ??= IconIds.Parse(ReadResource(_resources, "Rhino.UI.Resources.icon_ids.txt") ?? "");
        return _builtIn.TryGetValue(id, out var files)
            ? (ReadSvg(_resources, files.Light), ReadSvg(_resources, files.Dark))
            : (null, null);
    }

    private static string? ReadSvg(Assembly assembly, string file)
    {
        foreach (var name in IconIds.ResourceNames(file))
            if (ReadResource(assembly, name) is { } text) return text;
        return null;
    }

    private static string? ReadResource(Assembly assembly, string name)
    {
        using var stream = assembly.GetManifestResourceStream(name);
        if (stream is null) return null;
        using var reader = new StreamReader(stream, System.Text.Encoding.UTF8);
        return reader.ReadToEnd();
    }
}
