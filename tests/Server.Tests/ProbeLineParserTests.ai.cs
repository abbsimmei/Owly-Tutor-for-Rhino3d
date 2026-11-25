using NUnit.Framework;
using Rhino.AI.UI;

namespace Rhino.AI.Server.Tests;

// Synthetic lines only: invented command and option names.
[TestFixture]
public class ProbeLineParserTests
{
    private static string Show(IReadOnlyList<ProbeOption>? r) =>
        r is null ? "null" : string.Join("|", r.Select(o => o.Value is null ? o.Name : o.Name + "=" + o.Value));

    [Test]
    public void Reads_a_number_a_word_a_toggle_and_a_bare_action()
    {
        var r = ProbeLineParser.Parse("Center of demo shape ( Sides=8  Shape=Round  Edge  Solid=Yes )");
        Assert.That(Show(r), Is.EqualTo("Sides=8|Shape=Round|Edge|Solid=Yes"));
        Assert.That(r![2].Value, Is.Null);
    }

    [Test]
    public void A_trailing_colon_is_fine()
    {
        Assert.That(Show(ProbeLineParser.Parse("Pick a demo ( Sides=8  Edge ):")), Is.EqualTo("Sides=8|Edge"));
        Assert.That(Show(ProbeLineParser.Parse("  Pick a demo ( Sides=-2.5 ) :  ")), Is.EqualTo("Sides=-2.5"));
    }

    [TestCase("Pick a point")]
    [TestCase("Pick a point:")]
    [TestCase("Pick ( ):")]
    [TestCase("Pick (  )")]
    [TestCase("")]
    [TestCase("   ")]
    [TestCase("((( ))) ::: %%%")]
    [TestCase("Pick (Sides=8)")]
    public void No_options_gives_null(string line)
    {
        Assert.That(ProbeLineParser.Parse(line), Is.Null);
    }

    [Test]
    public void Null_gives_null()
    {
        Assert.That(ProbeLineParser.Parse(null), Is.Null);
    }

    [Test]
    public void A_non_ascii_name_is_dropped()
    {
        Assert.That(Show(ProbeLineParser.Parse("Pick ( Sides=8  Kantä=Yes  Ändern )")), Is.EqualTo("Sides=8"));
    }

    [Test]
    public void A_value_with_a_comma_drops_the_option()
    {
        Assert.That(Show(ProbeLineParser.Parse("Pick ( Radius=2,5  Sides=8 )")), Is.EqualTo("Sides=8"));
    }

    [Test]
    public void Single_space_separation_does_not_split_a_token()
    {
        Assert.That(Show(ProbeLineParser.Parse("Pick ( A  B C )")), Is.EqualTo("A"));
    }

    [Test]
    public void Duplicate_names_keep_the_first()
    {
        Assert.That(Show(ProbeLineParser.Parse("Pick ( Sides=8  sides=9  Edge )")), Is.EqualTo("Sides=8|Edge"));
    }

    [Test]
    public void A_value_with_symbols_drops_the_option()
    {
        Assert.That(Show(ProbeLineParser.Parse("Pick ( Sides=8;_Exit  Shape=\"Round\"  Edge )")), Is.EqualTo("Edge"));
    }
}
