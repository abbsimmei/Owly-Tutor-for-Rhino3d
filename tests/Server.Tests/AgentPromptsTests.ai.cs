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
