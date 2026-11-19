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

        // Welcome popup, once per computer and Rhino version (plan 011). The flag is set before
        // showing, so a crash inside the dialog can never make it pop up on every start.
        try
        {
            if (RhinoApp.IsRunningHeadless || AISettings.WelcomeShown)
                return;

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
