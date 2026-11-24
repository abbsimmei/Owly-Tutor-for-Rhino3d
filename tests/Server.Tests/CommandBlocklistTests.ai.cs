using NUnit.Framework;
using Rhino.AI.UI;

namespace Rhino.AI.Server.Tests;

[TestFixture]
public class CommandBlocklistTests
{
    [TestCase("_Delete")]
    [TestCase("_Purge")]
    [TestCase("_Save")]
    [TestCase("_SaveAs")]
    [TestCase("_Open")]
    [TestCase("_Exit")]
    [TestCase("_Undo")]
    [TestCase("_Redo")]
    [TestCase("_Options")]
    [TestCase("_DocumentProperties")]
    [TestCase("_PlugInManager")]
    [TestCase("_Grasshopper")]
    [TestCase("_Owly")]
    [TestCase("_OwlyWelcome")]
    [TestCase("_-RunPythonScript")]
    [TestCase("_IncrementalSave")]
    [TestCase("_ReadCommandFile")]
    [TestCase("_MacroEditor")]
    [TestCase("_delete")]
    public void Dangerous_commands_are_blocked(string name)
    {
        Assert.That(CommandBlocklist.IsBlocked(name), Is.True, name);
    }

    [TestCase("_Pyramid")]
    [TestCase("_Loft")]
    [TestCase("_-Loft")]
    [TestCase("_Box")]
    public void Modelling_commands_are_allowed(string name)
    {
        Assert.That(CommandBlocklist.IsBlocked(name), Is.False, name);
    }

    [Test]
    public void Bare_strips_one_underscore_then_one_hyphen()
    {
        Assert.That(CommandBlocklist.Bare("_-Loft"), Is.EqualTo("Loft"));
        Assert.That(CommandBlocklist.Bare("_Loft"), Is.EqualTo("Loft"));
    }

    [TestCase("Loft")]
    [TestCase("_Loft x")]
    public void An_invalid_shape_is_classified_before_Rhino_is_asked(string name)
    {
        var verdict = CommandBlocklist.Classify(name, _ => throw new InvalidOperationException(), _ => throw new InvalidOperationException());
        Assert.That(verdict, Is.EqualTo(RunVerdict.Invalid));
    }

    [Test]
    public void Classify_orders_unknown_alias_blocked_ok()
    {
        Assert.That(CommandBlocklist.Classify("_Nonsense", _ => false, _ => false), Is.EqualTo(RunVerdict.Unknown));
        Assert.That(CommandBlocklist.Classify("_Loft", _ => true, _ => true), Is.EqualTo(RunVerdict.Blocked));
        Assert.That(CommandBlocklist.Classify("_Delete", _ => true, _ => false), Is.EqualTo(RunVerdict.Blocked));
        Assert.That(CommandBlocklist.Classify("_Loft", _ => true, _ => false), Is.EqualTo(RunVerdict.Ok));
    }

    [Test]
    public void The_command_checked_event_has_the_json_the_panel_fixture_pins()
    {
        var json = PanelJson.Serialize(new CommandCheckedEvent("_Pyramid", "ok"));
        Assert.That(json, Is.EqualTo("""{"type":"command.checked","name":"_Pyramid","run":"ok"}"""));
    }

    [Test]
    public void The_run_and_check_commands_the_panel_sends_are_understood()
    {
        var run = (RunCommandCommand)PanelJson.Deserialize("""{"type":"command.run","name":"_Pyramid"}""")!;
        Assert.That(run.Name, Is.EqualTo("_Pyramid"));

        var check = (CheckCommandsCommand)PanelJson.Deserialize("""{"type":"command.check","names":["_Pyramid","_Cone"]}""")!;
        Assert.That(check.Names, Is.EqualTo(new[] { "_Pyramid", "_Cone" }));
    }
}
