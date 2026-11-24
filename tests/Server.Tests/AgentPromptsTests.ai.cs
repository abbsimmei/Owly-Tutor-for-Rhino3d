using NUnit.Framework;

namespace Rhino.AI.Server.Tests;

[TestFixture]
public class AgentPromptsTests
{
    [TestCase("You are Owly")]
    [TestCase("mcp__rhino__get_commands")]
    [TestCase("`_Loft`")]
    [TestCase("mcp__rhino__ask_user")]
    [TestCase("Grasshopper")]
    public void Compose_contains_required_text(string expected)
    {
        Assert.That(AgentPrompts.Compose(""), Does.Contain(expected));
    }

    [Test]
    public void Compose_tells_model_about_command_buttons()
    {
        string prompt = AgentPrompts.Compose("");
        Assert.That(prompt, Does.Contain("Name each command once, at the step where the student uses it, in the order they use it."));
        Assert.That(prompt, Does.Contain("Never say that you typed, ran, opened or highlighted anything in Rhino."));
        Assert.That(prompt, Does.Contain("Under each command the panel shows a Run button that the student can click to start it in Rhino; you never start it yourself, so never say that you ran it."));
    }

    [Test]
    public void Compose_allows_suggesting_option_values_with_a_command()
    {
        Assert.That(AgentPrompts.Compose(""), Does.Contain("you may write it with the command, like `_Pyramid DirectionConstraint=Vertical`, but only options and values you are certain exist; the panel drops anything else and the student decides before clicking Run."));
    }

    // The CLI is launched through cmd.exe, whose command line limit is 8,191 characters; the other arguments need room.
    [Test]
    public void Compose_stays_under_the_command_line_budget()
    {
        TestContext.Out.WriteLine("Prompt length: " + AgentPrompts.Compose("").Length);
        Assert.That(AgentPrompts.Compose("").Length, Is.LessThan(7000));
    }

    [Test]
    public void Compose_appends_definition_prompt_after_steers()
    {
        string prompt = AgentPrompts.Compose("Course note.");
        Assert.That(prompt, Does.EndWith("\n\nCourse note."));
        Assert.That(prompt, Does.StartWith(AgentPrompts.IdentitySteer));
    }

    [Test]
    public void Compose_does_not_end_with_newline()
    {
        Assert.That(AgentPrompts.Compose(""), Does.Not.EndWith("\n"));
    }
}
