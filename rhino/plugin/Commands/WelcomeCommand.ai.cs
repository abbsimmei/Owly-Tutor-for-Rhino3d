using RhinoCommand = Rhino.Commands.Command;
using Rhino.UI;

namespace Rhino.AI;

// Shows Owly's welcome popup again (plan 011). Doesn't change the first-run flag.
public class WelcomeCommand : RhinoCommand
{
    public override string EnglishName => LOC.COMMANDNAME("OwlyWelcome");

    protected override string CommandContextHelpUrl => DocsLinks.Homepage;

    protected override Commands.Result RunCommand(RhinoDoc doc, Commands.RunMode mode)
    {
        WelcomeDialog.ShowFor(RhinoEtoApp.MainWindow);
        return Commands.Result.Success;
    }
}
