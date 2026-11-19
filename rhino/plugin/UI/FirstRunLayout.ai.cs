using System.IO;
using Rhino.UI;

namespace Rhino.AI;

// First-start placement (plan 013): Owly's panel becomes a tab next to Layers / Properties on the right,
// and Owly's toolbar is switched on (Rhino loads a plug-in's .rui hidden). Runs once per Rhino version.
// Owly only touches its own panel and toolbar, never Rhino's.
internal static class FirstRunLayout
{
    private const string ToolbarName = "Owly";

    public static void Apply()
    {
        try
        {
            AddPanelTab();
        }
        catch (Exception ex)
        {
            RhinoApp.WriteLine($"[owly] could not add the Owly panel tab: {ex.Message}");
        }

        try
        {
            ShowToolbar();
        }
        catch (Exception ex)
        {
            RhinoApp.WriteLine($"[owly] could not show the Owly toolbar: {ex.Message}");
        }
    }

    // Next to Layers if that panel is docked somewhere, else next to Properties. Not made the active tab:
    // the welcome popup's "Open Owly" does that, and the student's current tab stays in front.
    private static void AddPanelTab()
    {
        if (Panels.OpenPanelAsSibling(UI.AIPanel.PanelId, PanelIds.Layers, false))
            return;
        if (Panels.OpenPanelAsSibling(UI.AIPanel.PanelId, PanelIds.ObjectProperties, false))
            return;
        RhinoApp.WriteLine("[owly] Layers and Properties panels not found; the Owly panel opens with the Owly command.");
    }

    private static void ShowToolbar()
    {
        ToolbarFile? file = RhinoApp.ToolbarFiles.FindByName(ToolbarName, true);
        if (file is null)
        {
            // Rhino normally loads Owly.rui from next to Owly.rhp by itself; open it if it didn't.
            string folder = Path.GetDirectoryName(typeof(FirstRunLayout).Assembly.Location) ?? string.Empty;
            string path = Path.Combine(folder, "Owly.rui");
            if (File.Exists(path))
                file = RhinoApp.ToolbarFiles.Open(path);
        }

        if (file?.GetGroup(ToolbarName) is not { } group)
        {
            RhinoApp.WriteLine("[owly] Owly toolbar not found.");
            return;
        }

        group.Visible = true;
    }
}
