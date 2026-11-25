using NUnit.Framework;
using Rhino.AI.UI;

namespace Rhino.AI.Server.Tests;

[TestFixture]
public class RunMacroTests
{
    private static CommandOption Choice(string name, params string[] values) =>
        new(name, "choice", values.Select(v => new CommandOptionValue(v, "")).ToList(), "");

    private static CommandOption Toggle(string name) =>
        new(name, "toggle", new[] { new CommandOptionValue("No", ""), new CommandOptionValue("Yes", "") }, "");

    private static readonly CommandOptions Data = new(new[]
    {
        Choice("Shape", "Round", "Square"),
        Toggle("Filled"),
        new CommandOption("Sides", "info", Array.Empty<CommandOptionValue>(), ""),
    });

    private static IReadOnlyDictionary<string, string> Changes(params (string, string)[] pairs) =>
        pairs.ToDictionary(p => p.Item1, p => p.Item2);

    [Test]
    public void No_changes_gives_just_the_name()
    {
        Assert.That(RunMacro.Build("_Demo", null, Data), Is.EqualTo("_Demo"));
        Assert.That(RunMacro.Build("_Demo", Changes(), Data), Is.EqualTo("_Demo"));
    }

    [Test]
    public void A_choice_uses_the_data_spelling()
    {
        Assert.That(RunMacro.Build("_Demo", Changes(("shape", "round")), Data), Is.EqualTo("_Demo _Shape=_Round"));
    }

    [Test]
    public void A_toggle_works()
    {
        Assert.That(RunMacro.Build("_Demo", Changes(("Filled", "yes")), Data), Is.EqualTo("_Demo _Filled=_Yes"));
    }

    [Test]
    public void Two_options_come_out_in_data_order()
    {
        Assert.That(RunMacro.Build("_Demo", Changes(("Filled", "No"), ("Shape", "Square")), Data), Is.EqualTo("_Demo _Shape=_Square _Filled=_No"));
    }

    [Test]
    public void An_info_option_is_dropped()
    {
        Assert.That(RunMacro.Build("_Demo", Changes(("Sides", "6")), Data), Is.EqualTo("_Demo"));
    }

    [TestCase("Round _Delete")]
    [TestCase("Round;_Exit")]
    [TestCase("\"Round\"")]
    [TestCase("!")]
    [TestCase("Round\n_Exit")]
    [TestCase("_Round")]
    [TestCase("Unlisted")]
    public void Injection_in_a_value_is_dropped(string value)
    {
        Assert.That(RunMacro.Build("_Demo", Changes(("Shape", value)), Data), Is.EqualTo("_Demo"));
    }

    [TestCase("Shape _Delete")]
    [TestCase("-Shape")]
    [TestCase("Bogus")]
    [TestCase("Shape;_Exit")]
    public void Injection_in_a_key_is_dropped(string key)
    {
        Assert.That(RunMacro.Build("_Demo", Changes((key, "Round")), Data), Is.EqualTo("_Demo"));
    }

    [Test]
    public void The_result_has_no_bang_semicolon_quote_newline_or_token_without_equals()
    {
        var macro = RunMacro.Build("_Demo", Changes(("Shape", "Round"), ("Filled", "Yes"), ("Shape ", "x"), ("x;y", "!")), Data);
        Assert.That(macro, Does.Not.Contain("!").And.Not.Contain(";").And.Not.Contain("\"").And.Not.Contain("\n"));
        var tokens = macro.Split(' ');
        Assert.That(tokens[0], Is.EqualTo("_Demo"));
        Assert.That(tokens.Skip(1), Has.All.Contain("="));
    }

    [TestCase("Demo")]
    [TestCase("_Demo _Delete")]
    [TestCase("_Demo;_Exit")]
    public void A_name_that_is_not_a_command_name_throws(string name)
    {
        Assert.Throws<ArgumentException>(() => RunMacro.Build(name, null, Data));
    }
    private static readonly CommandOptions NumData = new(new[]
    {
        new CommandOption("Sides", "number", Array.Empty<CommandOptionValue>(), "", "8", true),
        Toggle("Solid"),
        new CommandOption("Mode", "info", Array.Empty<CommandOptionValue>(), "", "Round", true),
    });

    [Test]
    public void A_number_is_written_as_two_tokens()
    {
        Assert.That(RunMacro.Build("_Demo", Changes(("Sides", "6")), NumData), Is.EqualTo("_Demo _Sides 6"));
    }

    [TestCase("2.5", "_Demo _Sides 2.5")]
    [TestCase("-3", "_Demo _Sides -3")]
    [TestCase("-0.25", "_Demo _Sides -0.25")]
    public void A_decimal_and_a_negative_number_work(string value, string expected)
    {
        Assert.That(RunMacro.Build("_Demo", Changes(("Sides", value)), NumData), Is.EqualTo(expected));
    }

    [Test]
    public void A_number_mixed_with_a_toggle_keeps_data_order()
    {
        Assert.That(RunMacro.Build("_Demo", Changes(("Solid", "No"), ("Sides", "6")), NumData), Is.EqualTo("_Demo _Sides 6 _Solid=_No"));
    }

    [TestCase("2,5")]
    [TestCase("1e5")]
    [TestCase("6 _Delete")]
    [TestCase("6;_Exit")]
    [TestCase("-")]
    [TestCase(".")]
    [TestCase("abc")]
    [TestCase("1234567890123456")]
    [TestCase("")]
    [TestCase("\u0663")]
    public void A_number_that_is_not_a_plain_number_is_dropped(string value)
    {
        Assert.That(RunMacro.Build("_Demo", Changes(("Sides", value)), NumData), Is.EqualTo("_Demo"));
    }

    [Test]
    public void A_number_change_for_an_info_option_is_dropped()
    {
        Assert.That(RunMacro.Build("_Demo", Changes(("Mode", "6")), NumData), Is.EqualTo("_Demo"));
    }

    [Test]
    public void Fifteen_characters_are_allowed()
    {
        Assert.That(RunMacro.Build("_Demo", Changes(("Sides", "123456789012345")), NumData), Is.EqualTo("_Demo _Sides 123456789012345"));
    }
}
