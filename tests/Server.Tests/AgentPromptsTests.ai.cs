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
        Assert.That(AgentPrompts.Compose("", OwlyLevel.Beginner), Does.Contain(expected));
    }

    [Test]
    public void Compose_tells_model_about_command_buttons()
    {
        string prompt = AgentPrompts.Compose("", OwlyLevel.Beginner);
        Assert.That(prompt, Does.Contain("Name each command once, at the step where the student uses it, in the order they use it."));
        Assert.That(prompt, Does.Contain("Never say that you typed, ran, opened or highlighted anything in Rhino."));
        Assert.That(prompt, Does.Contain("Under each command the panel shows a Run button that the student can click to start it in Rhino; you never start it yourself, so never say that you ran it."));
    }

    [Test]
    public void Compose_allows_suggesting_option_values_with_a_command()
    {
        Assert.That(AgentPrompts.Compose("", OwlyLevel.Beginner), Does.Contain("you may write it with the command, like `_Pyramid NumSides=6` or `_Pyramid DirectionConstraint=Vertical`, but only options and values you are certain exist; the panel drops anything else and the student decides before clicking Run."));
    }

    // The CLI is launched through cmd.exe, whose command line limit is 8,191 characters; the other arguments need room.
    [Test]
    public void Compose_stays_under_the_command_line_budget()
    {
        TestContext.Out.WriteLine("Prompt length: " + AgentPrompts.Compose("", OwlyLevel.Beginner).Length);
        Assert.That(AgentPrompts.Compose("", OwlyLevel.Beginner).Length, Is.LessThan(6500));
    }

    [Test]
    public void Compose_appends_definition_prompt_after_steers()
    {
        string prompt = AgentPrompts.Compose("Course note.", OwlyLevel.Beginner);
        Assert.That(prompt, Does.EndWith("\n\nCourse note."));
        Assert.That(prompt, Does.StartWith(AgentPrompts.IdentitySteer));
    }

    [Test]
    public void Compose_does_not_end_with_newline()
    {
        Assert.That(AgentPrompts.Compose("", OwlyLevel.Beginner), Does.Not.EndWith("\n"));
    }

    [TestCase("Beginner")]
    [TestCase("Intermediate")]
    [TestCase("Advanced")]
    public void Compose_contains_only_the_chosen_level(string levelName)
    {
        OwlyLevel level = OwlyLevelExtensions.Parse(levelName);
        string prompt = AgentPrompts.Compose("", level);
        foreach (OwlyLevel each in Enum.GetValues<OwlyLevel>())
        {
            if (each == level) Assert.That(prompt, Does.Contain(AgentPrompts.LevelSteer(each)));
            else Assert.That(prompt, Does.Not.Contain(AgentPrompts.LevelSteer(each)));
        }
        Assert.That(prompt, Does.Contain($"The student chose the level {level}."));
        Assert.That(prompt.IndexOf(AgentPrompts.LevelSteer(level)), Is.GreaterThan(prompt.IndexOf(AgentPrompts.TeachingSteer)));
        Assert.That(prompt.IndexOf(AgentPrompts.LevelSteer(level)), Is.LessThan(prompt.IndexOf(AgentPrompts.CommandSteer)));
    }

    [TestCase("Beginner")]
    [TestCase("Intermediate")]
    [TestCase("Advanced")]
    public void Level_steer_and_composed_prompt_stay_within_budget(string levelName)
    {
        OwlyLevel level = OwlyLevelExtensions.Parse(levelName);
        TestContext.Out.WriteLine($"{level}: steer {AgentPrompts.LevelSteer(level).Length}, prompt {AgentPrompts.Compose("", level).Length}");
        Assert.That(AgentPrompts.LevelSteer(level).Length, Is.LessThanOrEqualTo(400));
        Assert.That(AgentPrompts.Compose("", level).Length, Is.LessThan(6500));
        Assert.That(AgentPrompts.LevelSteer(level), Does.EndWith("If the student asks for simpler or deeper, follow that for the answer."));
    }

    [Test]
    public void Teaching_steer_no_longer_assumes_a_beginner()
    {
        Assert.That(AgentPrompts.Compose("", OwlyLevel.Advanced), Does.Not.Contain("assume a beginner"));
        Assert.That(AgentPrompts.TeachingSteer, Does.Contain("pitch the answer at the student's level (below)"));
    }

    [TestCase("Beginner", "Beginner")]
    [TestCase("intermediate", "Intermediate")]
    [TestCase("ADVANCED", "Advanced")]
    [TestCase(" Advanced ", "Advanced")]
    [TestCase(null, "Beginner")]
    [TestCase("", "Beginner")]
    [TestCase("expert", "Beginner")]
    [TestCase("7", "Beginner")]
    public void Level_parse(string? text, string expected)
    {
        Assert.That(OwlyLevelExtensions.Parse(text).ToString(), Is.EqualTo(expected));
    }
}
