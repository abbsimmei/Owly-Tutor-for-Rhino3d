using NUnit.Framework;
using Rhino.AI.UI;

namespace Rhino.AI.Server.Tests;

[TestFixture]
public class CommandCopyTests
{
    [Test]
    public void The_command_copy_the_panel_sends_is_understood()
    {
        var command = PanelJson.Deserialize("""{"type":"command.copy","name":"_Loft"}""");

        Assert.That(command, Is.TypeOf<CopyCommandCommand>());
        Assert.That(((CopyCommandCommand)command!).Name, Is.EqualTo("_Loft"));
    }

    [TestCase("_Loft")]
    [TestCase("_-Loft")]
    [TestCase("_ExtrudeCrv")]
    public void A_command_name_is_accepted(string name)
    {
        Assert.That(CommandName.IsValid(name), Is.True);
    }

    [TestCase("Loft")]
    [TestCase("_Loft Enter")]
    [TestCase("_Loft\n")]
    [TestCase("__Loft")]
    [TestCase("")]
    [TestCase(null)]
    public void Anything_else_is_rejected(string? text)
    {
        Assert.That(CommandName.IsValid(text), Is.False);
    }
}
