using System.IO;
using System.Xml;
using System.Xml.Linq;

namespace Rhino.AI.UI;

// One place a command appears on a toolbar (plan 015). All text is already localised.
// Path is the toolbar, or "Parent › Flyout" when the button sits on a flyout. Tab is "" when there isn't one.
internal sealed record ToolbarPlace(string Group, string Tab, string Path, string Button, string Click, Guid GroupId);

// A parsed .rui file. Parse once (it can be several MB), then Find per command.
// Pure and Rhino-free so it is unit-tested; ToolbarLocator feeds it Rhino's loaded files.
internal sealed class ToolbarIndex
{
    private const int MaxFlyoutLevels = 3;
    private const int MaxPlaces = 5;

    private sealed record GroupEntry(string Group, string Tab, Guid GroupId);

    private sealed class ToolbarInfo
    {
        public string Text = "";
        public List<GroupEntry> Groups { get; } = new();
        public List<string> Parents { get; } = new();
    }

    private sealed record Hit(string ToolbarId, string Button, string Click, string Command);

    private sealed record Where(string Group, string Tab, string Path, Guid GroupId);

    private readonly Dictionary<string, ToolbarInfo> _toolbars = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<Hit> _hits = new();

    private ToolbarIndex() { }

    // platform: "Win" or "Mac". sidebarWord is the localised word appended to a tab's name for its sidebar toolbar.
    public static ToolbarIndex Parse(string ruiXml, int languageId, string platform, string sidebarWord)
    {
        var index = new ToolbarIndex();
        try
        {
            XDocument doc;
            using (var reader = XmlReader.Create(new StringReader(ruiXml),
                       new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null }))
            {
                doc = XDocument.Load(reader);
            }
            index.Build(doc, languageId, platform, sidebarWord);
        }
        catch (XmlException)
        {
            return new ToolbarIndex();
        }
        return index;
    }

    private void Build(XDocument doc, int languageId, string platform, string sidebarWord)
    {
        var root = doc.Root;
        if (root is null) return;

        var macros = new Dictionary<string, XElement>(StringComparer.OrdinalIgnoreCase);
        foreach (var macro in root.Descendants("macro_item"))
        {
            var id = (string?)macro.Attribute("guid");
            if (id is null || !OnPlatform(macro, platform)) continue;
            macros[id] = macro;
        }

        foreach (var toolbar in root.Descendants("tool_bar"))
        {
            var id = (string?)toolbar.Attribute("guid");
            if (id is null) continue;
            Info(id).Text = Localised(toolbar, "text", languageId);
        }

        foreach (var group in root.Descendants("tool_bar_group"))
        {
            var groupText = Localised(group, "text", languageId);
            var groupId = Guid.TryParse((string?)group.Attribute("guid"), out var g) ? g : Guid.Empty;
            foreach (var item in group.Elements("tool_bar_group_item"))
            {
                var tab = Localised(item, "text", languageId);
                foreach (var tid in item.Elements("tool_bar_id"))
                {
                    var key = tid.Value.Trim();
                    if (key.Length > 0) Info(key).Groups.Add(new GroupEntry(groupText, tab, groupId));
                }
                foreach (var side in item.Elements("sidebar"))
                {
                    var key = side.Value.Trim();
                    var label = tab.Length > 0 ? $"{tab} {sidebarWord}" : sidebarWord;
                    if (key.Length > 0) Info(key).Groups.Add(new GroupEntry(groupText, label, groupId));
                }
            }
        }

        foreach (var toolbar in root.Descendants("tool_bar"))
        {
            var toolbarId = (string?)toolbar.Attribute("guid");
            if (toolbarId is null) continue;
            foreach (var item in toolbar.Elements("tool_bar_item"))
            {
                if (!OnPlatform(item, platform)) continue;
                foreach (var link in item.Elements("link"))
                {
                    var child = link.Value.Trim();
                    if (child.Length > 0) Info(child).Parents.Add(toolbarId);
                }

                foreach (var (side, click) in new[] { ("left_macro_id", "left"), ("right_macro_id", "right") })
                {
                    var macroId = item.Element(side)?.Value.Trim();
                    if (string.IsNullOrEmpty(macroId) || !macros.TryGetValue(macroId, out var macro)) continue;
                    var command = FirstCommand(macro.Element("script")?.Value ?? "");
                    if (command is null) continue;
                    var label = FirstNonEmpty(
                        Localised(macro, "tooltip", languageId),
                        Localised(macro, "button_text", languageId),
                        Localised(macro, "text", languageId),
                        Localised(item, "text", languageId));
                    _hits.Add(new Hit(toolbarId, label, click, command));
                }
            }
        }
    }

    private ToolbarInfo Info(string id)
    {
        if (!_toolbars.TryGetValue(id, out var info)) _toolbars[id] = info = new ToolbarInfo();
        return info;
    }

    public IReadOnlyList<ToolbarPlace> Find(string command)
    {
        var key = Key(command);
        if (key.Length == 0) return Array.Empty<ToolbarPlace>();

        var places = new List<ToolbarPlace>();
        foreach (var hit in _hits)
        {
            if (!string.Equals(hit.Command, key, StringComparison.OrdinalIgnoreCase)) continue;
            foreach (var where in Resolve(hit.ToolbarId))
                places.Add(new ToolbarPlace(where.Group, where.Tab, where.Path, hit.Button, hit.Click, where.GroupId));
        }

        return places
            .GroupBy(p => (p.Group, p.Tab, p.Path, p.Button, p.Click))
            .Select(g => g.First())
            .OrderBy(p => p.Click == "left" ? 0 : 1)
            .ThenBy(p => p.Tab.Length > 0 ? 0 : 1)
            .ThenBy(p => p.Path, StringComparer.Ordinal)
            .Take(MaxPlaces)
            .ToList();
    }

    private List<Where> Resolve(string toolbarId)
    {
        var found = new List<Where>();
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        Walk(toolbarId, TextOf(toolbarId), 0, visited, found);
        if (found.Count == 0) found.Add(new Where("", "", TextOf(toolbarId), Guid.Empty));
        return found;
    }

    private void Walk(string id, string path, int level, HashSet<string> visited, List<Where> found)
    {
        if (!visited.Add(id) || !_toolbars.TryGetValue(id, out var info)) return;
        if (info.Groups.Count > 0)
        {
            foreach (var entry in info.Groups) found.Add(new Where(entry.Group, entry.Tab, path, entry.GroupId));
            return;
        }
        if (level >= MaxFlyoutLevels) return;
        foreach (var parent in info.Parents)
            Walk(parent, $"{TextOf(parent)} › {path}", level + 1, visited, found);
    }

    private string TextOf(string id) => _toolbars.TryGetValue(id, out var info) ? info.Text : "";

    internal static string? FirstCommand(string script)
    {
        var text = script.Trim();
        while (text.Length > 0 && (text[0] == '!' || text[0] == '\'' || char.IsWhiteSpace(text[0]))) text = text[1..];
        var end = 0;
        while (end < text.Length && !char.IsWhiteSpace(text[end]) && text[end] != ';') end++;
        var token = Key(text[..end]);
        return token.Length == 0 ? null : token;
    }

    private static string Key(string command) => command.TrimStart('_', '-');

    private static bool OnPlatform(XElement element, string platform)
    {
        var platforms = (string?)element.Attribute("platforms");
        return platforms is null || platforms.Contains(platform, StringComparison.OrdinalIgnoreCase);
    }

    private static string Localised(XElement element, string childName, int languageId)
    {
        var child = element.Element(childName);
        if (child is null) return "";
        var text = child.Element($"locale_{languageId}") ?? child.Element("locale_1033")
            ?? child.Elements().FirstOrDefault(e => e.Name.LocalName.StartsWith("locale_", StringComparison.Ordinal));
        return text?.Value.Trim() ?? "";
    }

    private static string FirstNonEmpty(params string[] values) => values.FirstOrDefault(v => v.Length > 0) ?? "";
}
