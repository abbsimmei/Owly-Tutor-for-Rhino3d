using NUnit.Framework;
using Rhino.AI.UI;

namespace Rhino.AI.Server.Tests;

// Synthetic data only: invented command and option names.
[TestFixture]
public class OptionMergeTests
{
    private static CommandOptionValue V(string n, string d = "") => new(n, d);

    private static readonly CommandOptions Help = new(new[]
    {
        new CommandOption("Sides", "info", Array.Empty<CommandOptionValue>(), "Demo sides."),
        new CommandOption("Shape", "choice", new[] { V("Round", "Demo round."), V("Square", "Demo square.") }, "Demo shape."),
        new CommandOption("Solid", "toggle", new[] { V("No", "n"), V("Yes", "y") }, "Demo solid."),
        new CommandOption("Edge", "info", Array.Empty<CommandOptionValue>(), "Demo edge."),
        new CommandOption("Both", "choice", new[] { V("One"), V("Two") }, "Demo both."),
    });

    private static CommandOption Find(CommandOptions o, string name) => o.Options.Single(x => x.Name == name);

    [Test]
    public void A_null_or_empty_probe_returns_help_by_reference()
    {
        Assert.That(OptionMerge.Merge(Help, null), Is.SameAs(Help));
        Assert.That(OptionMerge.Merge(Help, Array.Empty<ProbeOption>()), Is.SameAs(Help));
    }

    [Test]
    public void A_number_becomes_a_number_with_the_current_value_and_the_help_description()
    {
        var m = OptionMerge.Merge(Help, new[] { new ProbeOption("Sides", "8") });
        var o = Find(m, "Sides");
        Assert.That(o.Kind, Is.EqualTo("number"));
        Assert.That(o.Current, Is.EqualTo("8"));
        Assert.That(o.FirstPrompt, Is.True);
        Assert.That(o.Description, Is.EqualTo("Demo sides."));
        Assert.That(o.Values, Is.Empty);
    }

    [TestCase("-2")]
    [TestCase("2.5")]
    [TestCase("-0.25")]
    public void Signed_and_decimal_numbers_count(string v)
    {
        Assert.That(Find(OptionMerge.Merge(Help, new[] { new ProbeOption("Sides", v) }), "Sides").Kind, Is.EqualTo("number"));
    }

    [Test]
    public void A_bare_action_is_info_without_a_current_value()
    {
        var o = Find(OptionMerge.Merge(Help, new[] { new ProbeOption("Edge", null) }), "Edge");
        Assert.That(o.Kind, Is.EqualTo("info"));
        Assert.That(o.Current, Is.Null);
        Assert.That(o.FirstPrompt, Is.True);
        Assert.That(o.Description, Is.EqualTo("Demo edge."));
    }

    [Test]
    public void A_yes_no_toggle_uses_the_help_values_when_help_has_a_toggle()
    {
        var o = Find(OptionMerge.Merge(Help, new[] { new ProbeOption("Solid", "yes") }), "Solid");
        Assert.That(o.Kind, Is.EqualTo("toggle"));
        Assert.That(o.Current, Is.EqualTo("Yes"));
        Assert.That(o.Values.Select(x => x.Name), Is.EqualTo(new[] { "No", "Yes" }));
    }

    [Test]
    public void A_toggle_the_help_page_lacks_gets_yes_no_or_on_off()
    {
        var yn = Find(OptionMerge.Merge(Help, new[] { new ProbeOption("Fresh", "NO") }), "Fresh");
        Assert.That(yn.Kind, Is.EqualTo("toggle"));
        Assert.That(yn.Current, Is.EqualTo("No"));
        Assert.That(yn.Values.Select(x => x.Name), Is.EqualTo(new[] { "Yes", "No" }));
        Assert.That(yn.Values.Select(x => x.Description), Has.All.EqualTo(""));

        var oo = Find(OptionMerge.Merge(Help, new[] { new ProbeOption("Fresh", "on") }), "Fresh");
        Assert.That(oo.Current, Is.EqualTo("On"));
        Assert.That(oo.Values.Select(x => x.Name), Is.EqualTo(new[] { "On", "Off" }));
    }

    [Test]
    public void A_word_matching_a_help_choice_is_a_choice_in_the_help_spelling()
    {
        var o = Find(OptionMerge.Merge(Help, new[] { new ProbeOption("Shape", "round") }), "Shape");
        Assert.That(o.Kind, Is.EqualTo("choice"));
        Assert.That(o.Current, Is.EqualTo("Round"));
        Assert.That(o.Values.Select(x => x.Name), Is.EqualTo(new[] { "Round", "Square" }));
        Assert.That(o.Description, Is.EqualTo("Demo shape."));
    }

    [Test]
    public void A_word_without_a_matching_help_choice_is_read_only_info()
    {
        // The help page has the option but not this value.
        var a = Find(OptionMerge.Merge(Help, new[] { new ProbeOption("Shape", "Hexagon") }), "Shape");
        Assert.That(a.Kind, Is.EqualTo("info"));
        Assert.That(a.Current, Is.EqualTo("Hexagon"));
        // The help page does not have the option at all.
        var b = Find(OptionMerge.Merge(Help, new[] { new ProbeOption("Mode", "Inscribed") }), "Mode");
        Assert.That(b.Kind, Is.EqualTo("info"));
        Assert.That(b.Current, Is.EqualTo("Inscribed"));
        Assert.That(b.Description, Is.EqualTo(""));
        Assert.That(b.FirstPrompt, Is.True);
    }

    [Test]
    public void Probe_options_come_first_in_probe_order_then_help_only_options_as_info()
    {
        var m = OptionMerge.Merge(Help, new[] { new ProbeOption("Solid", "No"), new ProbeOption("Sides", "5") });
        Assert.That(m.Options.Select(x => x.Name), Is.EqualTo(new[] { "Solid", "Sides", "Shape", "Edge", "Both" }));
        foreach (var name in new[] { "Shape", "Edge", "Both" })
        {
            var o = Find(m, name);
            Assert.That(o.Kind, Is.EqualTo("info"));
            Assert.That(o.Current, Is.Null);
            Assert.That(o.FirstPrompt, Is.Null);
        }
        Assert.That(Find(m, "Both").Values.Select(x => x.Name), Is.EqualTo(new[] { "One", "Two" }));
        Assert.That(Find(m, "Both").Description, Is.EqualTo("Demo both."));
    }

    [Test]
    public void The_probe_name_matches_help_case_insensitively()
    {
        var o = Find(OptionMerge.Merge(Help, new[] { new ProbeOption("sides", "3") }), "sides");
        Assert.That(o.Description, Is.EqualTo("Demo sides."));
        Assert.That(OptionMerge.Merge(Help, new[] { new ProbeOption("sides", "3") }).Options.Count, Is.EqualTo(5));
    }
}
