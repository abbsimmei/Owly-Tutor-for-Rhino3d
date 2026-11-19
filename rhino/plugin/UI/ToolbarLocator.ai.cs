using System.Diagnostics;
using System.IO;
using Rhino.Runtime;
using Rhino.UI;

namespace Rhino.AI.UI;

// Plan 015 / ADR-0007: where does a command live on the open toolbars? Reads the loaded .rui files as saved on
// disk (read-only, shared). Never shows, hides or changes a toolbar.
internal static class ToolbarLocator
{
    private const int MaxPlaces = 5;

    private static readonly Dictionary<string, (DateTime Stamp, int Language, ToolbarIndex Index)> Cache =
        new(StringComparer.OrdinalIgnoreCase);

    // UI thread only: RhinoApp.ToolbarFiles is read here.
    public static IReadOnlyList<PanelToolbarPlace> Find(string command)
    {
        var result = new List<PanelToolbarPlace>();
        var stopwatch = Stopwatch.StartNew();
        int files = 0, parsed = 0;
        try
        {
            var language = Localization.CurrentLanguageId;
            foreach (ToolbarFile file in RhinoApp.ToolbarFiles)
            {
                var path = file.Path;
                if (string.IsNullOrEmpty(path) || !File.Exists(path)) continue;
                files++;

                ToolbarIndex? index = IndexFor(path, language, ref parsed);
                if (index is null) continue;

                foreach (var place in index.Find(command))
                {
                    var panelPlace = new PanelToolbarPlace(place.Group, place.Tab, place.Path, place.Button, place.Click, IsHidden(file, place));
                    if (result.Any(r => (r.Group, r.Tab, r.Path, r.Button, r.Click) ==
                                        (panelPlace.Group, panelPlace.Tab, panelPlace.Path, panelPlace.Button, panelPlace.Click)))
                        continue;
                    result.Add(panelPlace);
                }
            }
        }
        catch (Exception ex)
        {
            HostUtils.LogDebugEvent($"[owly] toolbar lookup failed: {ex.Message}\n");
        }

        stopwatch.Stop();
        HostUtils.LogDebugEvent($"[owly] toolbar lookup took {stopwatch.ElapsedMilliseconds} ms ({files} files, {parsed} parsed)\n");
        return result.Take(MaxPlaces).ToList();
    }

    private static ToolbarIndex? IndexFor(string path, int language, ref int parsed)
    {
        try
        {
            var stamp = File.GetLastWriteTimeUtc(path);
            if (Cache.TryGetValue(path, out var cached) && cached.Stamp == stamp && cached.Language == language)
                return cached.Index;

            string xml;
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var reader = new StreamReader(stream))
            {
                xml = reader.ReadToEnd();
            }

            var index = ToolbarIndex.Parse(xml, language, OperatingSystem.IsMacOS() ? "Mac" : "Win", PanelStrings.SidebarWord());
            Cache[path] = (stamp, language, index);
            parsed++;
            return index;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    // Read only: the group's Visible flag is looked at, never set.
    private static bool IsHidden(ToolbarFile file, ToolbarPlace place)
    {
        if (place.GroupId == Guid.Empty) return false;

        ToolbarGroup? group = null;
        for (int i = 0; i < file.GroupCount; i++)
        {
            var candidate = file.GetGroup(i);
            if (candidate is not null && candidate.Id == place.GroupId) { group = candidate; break; }
        }
        if (group is null)
        {
            for (int i = 0; i < file.GroupCount; i++)
            {
                var candidate = file.GetGroup(i);
                if (candidate is not null && string.Equals(candidate.Name, place.Group, StringComparison.Ordinal)) { group = candidate; break; }
            }
        }

        return group is { Visible: false };
    }
}
