using Rhino.PlugIns;
using Rhino.Runtime;
using Rhino.UI;

namespace Rhino.AI;

public class RhinoAIPlugin : PlugIn
{
    // private CommandInterceptorHost? CommandInterceptors { get; set; }

    protected override LoadReturnCode OnLoad(ref string errorMessage)
    {
        if (!RhinoApp.IsRunningHeadless)
            Panels.RegisterPanel(this, typeof(UI.AIPanel), Localization.LocalizeString("Owly", 1),
                typeof(RhinoAIPlugin).Assembly, OwlyArt.ResourceName(OwlyArtSize.Small, OwlyArt.DarkTheme), PanelType.PerDoc);

#if R9
        AIInstances.Agents.LoadAgentsFromJson();
#endif

        // CommandInterceptors = new CommandInterceptorHost();
        RhinoAIHost.RegisterDocumentWatcher();


        RhinoApp.Idle += OnFirstIdle;
        return base.OnLoad(ref errorMessage);
    }

    // One-shot work that needs the plugin fully loaded (AISettings resolves it with PlugIn.Find).
    private void OnFirstIdle(object? sender, EventArgs e)
    {
        RhinoApp.Idle -= OnFirstIdle;
        try
        {
            AISettings.DeleteLegacyConversations();
        }
        catch (Exception ex)
        {
            RhinoApp.WriteLine($"[owly] could not clear old conversations: {ex.Message}");
        }

        // Panel tab and toolbar, once per Rhino version (plan 013). Before the welcome popup, so its
        // "Open Owly" selects the tab that is already in place. Flag first, so a failure can't repeat.
        try
        {
            if (!RhinoApp.IsRunningHeadless && !AISettings.FirstRunLayoutDone)
            {
                AISettings.FirstRunLayoutDone = true;
                FirstRunLayout.Apply();
            }
        }
        catch (Exception ex)
        {
            RhinoApp.WriteLine($"[owly] could not place Owly on first start: {ex.Message}");
        }

        // Plan 027: notes of conversations that ended while Rhino was quitting (or whose request failed) are
        // written now, in the background. Before the welcome block, which can return early.
        try
        {
            ProjectNotesWriter.DrainPending();
        }
        catch (Exception ex)
        {
            RhinoApp.WriteLine($"[owly] could not queue the project notes: {ex.Message}");
        }

        // Welcome popup, once per computer and Rhino version, and once more whenever WelcomeDialog.CurrentVersion
        // goes up (plans 011, 029). The flag and version are set before showing, so a crash inside the dialog
        // can never make it pop up on every start.
        try
        {
            if (RhinoApp.IsRunningHeadless || AISettings.WelcomeVersion >= WelcomeDialog.CurrentVersion)
                return;

            AISettings.WelcomeVersion = WelcomeDialog.CurrentVersion;
            AISettings.WelcomeShown = true;
        }
        catch (Exception ex)
        {
            RhinoApp.WriteLine($"[owly] could not read the welcome flag: {ex.Message}");
            return;
        }

        try
        {
            WelcomeDialog.ShowFor(RhinoEtoApp.MainWindow);
        }
        catch (Exception ex)
        {
            RhinoApp.WriteLine($"[owly] could not show the welcome popup: {ex.Message}");
        }
    }

    // Adds the "AI" settings page to the Rhino Options dialog. Called each time Options is opened, so a
    // fresh page (and panel) is built per open and its state reflects the current settings.
    protected override void OptionsDialogPages(List<OptionsDialogPage> pages)
    {
        pages.Add(new AIOptionsPage());
    }

    protected override void OnShutdown()
    {
        // CommandInterceptors?.Dispose();
        AgentHost.Shutdown();
    }

    public override PlugInLoadTime LoadTime => PlugInLoadTime.AtStartup;

}
