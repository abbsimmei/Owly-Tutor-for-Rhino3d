namespace Rhino.AI.UI;

// Plan 021: the Rhino-free helpers for finding a toolbar button's image. The list of built-in icons ships inside
// Rhino as a CSV resource (guid, light file, dark file); ToolbarIcons reads it. Nothing from Rhino is stored here.
internal static class IconIds
{
    public const int MaxSvgChars = 64 * 1024;

    // Lines of "guid,light.svg,dark.svg". Malformed lines are skipped.
    public static Dictionary<Guid, (string Light, string Dark)> Parse(string csv)
    {
        var map = new Dictionary<Guid, (string Light, string Dark)>();
        foreach (var line in csv.Split('\n'))
        {
            var columns = line.Split(',');
            if (columns.Length != 3) continue;
            var light = columns[1].Trim();
            var dark = columns[2].Trim();
            if (!Guid.TryParse(columns[0].Trim(), out var id) || light.Length == 0 || dark.Length == 0) continue;
            map[id] = (light, dark);
        }
        return map;
    }

    // Resource names to try for one icon file, in order.
    public static IEnumerable<string> ResourceNames(string file)
    {
        yield return "Rhino.UI.Resources.svg." + file;
        yield return "Rhino.UI.Resources." + file;
    }

    // Only small, SVG-looking text goes to the panel.
    public static string? Accept(string? svg)
    {
        if (svg is null) return null;
        var text = svg.Trim();
        if (text.Length == 0 || text.Length > MaxSvgChars) return null;
        return text.StartsWith("<svg", StringComparison.Ordinal) || text.StartsWith("<?xml", StringComparison.Ordinal)
            ? text
            : null;
    }
}
