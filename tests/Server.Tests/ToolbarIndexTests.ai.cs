using NUnit.Framework;
using Rhino.AI.UI;

namespace Rhino.AI.Server.Tests;

// Plan 015: where a command lives on the toolbars. Fixtures are hand-written (never copied from McNeel's files).
[TestFixture]
public class ToolbarIndexTests
{
    private static string Load(string name) =>
        File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory, "Fixtures", name));

    private static IReadOnlyList<ToolbarPlace> Find(string fixture, string command, int language = 1033, string platform = "Win") =>
        ToolbarIndex.Parse(Load(fixture), language, platform, "sidebar").Find(command);

    private static (string, string, string, string, string) Shape(ToolbarPlace p) => (p.Group, p.Tab, p.Path, p.Button, p.Click);

    [Test]
    public void The_Owly_toolbar_is_found_by_its_left_click_command()
    {
        var places = Find("Owly.rui", "_Owly");

        Assert.That(places, Has.Count.EqualTo(1));
        Assert.That(Shape(places[0]), Is.EqualTo(("Owly", "Owly", "Owly", "Owly – your Rhino tutor", "left")));
    }

    [Test]
    public void The_right_click_command_is_found_and_marked_right()
    {
        var places = Find("Owly.rui", "_OwlyWelcome");

        Assert.That(places, Has.Count.EqualTo(1));
        Assert.That(places[0].Click, Is.EqualTo("right"));
        Assert.That(places[0].Button, Is.EqualTo("Owly welcome"));
    }

    [Test]
    public void A_command_that_is_not_on_the_toolbar_gives_nothing()
    {
        Assert.That(Find("Owly.rui", "_Loft"), Is.Empty);
    }

    [Test]
    public void Loft_is_found_on_the_tab_toolbar_and_not_on_a_mac_only_item_or_a_later_command()
    {
        var places = Find("toolbars-fixture.rui", "_Loft");

        Assert.That(places.Select(Shape), Is.EqualTo(new[]
        {
            ("Main", "Surface", "Surface Tools", "Loft", "left"),
            ("Main", "Surface", "Surface Tools", "Loft scripted", "right"),
        }));
    }

    [Test]
    public void On_a_mac_the_sidebar_item_counts_too()
    {
        var places = Find("toolbars-fixture.rui", "_Loft", platform: "Mac");

        Assert.That(places, Has.Count.EqualTo(3));
        Assert.That(places.Select(Shape), Does.Contain(("Main", "Surface sidebar", "Side", "Loft", "left")));
    }

    [Test]
    public void The_label_follows_the_ui_language()
    {
        Assert.That(Find("toolbars-fixture.rui", "_Loft", language: 1053)[0].Button, Is.EqualTo("Loft (sv)"));
    }

    [Test]
    public void An_unknown_language_falls_back_to_english()
    {
        Assert.That(Find("toolbars-fixture.rui", "_Loft", language: 9999)[0].Button, Is.EqualTo("Loft"));
    }

    [Test]
    public void A_macro_on_a_flyout_parent_item_belongs_to_the_parent_toolbar()
    {
        var places = Find("toolbars-fixture.rui", "_SelNone");

        Assert.That(places, Has.Count.EqualTo(1));
        Assert.That(places[0].Path, Is.EqualTo("Surface Tools"));
    }

    [Test]
    public void A_flyout_gets_a_path_and_a_loose_toolbar_gets_no_group_even_with_a_link_cycle()
    {
        var places = Find("toolbars-fixture.rui", "_Sweep2");

        Assert.That(places, Has.Count.EqualTo(2));
        Assert.That(Shape(places[0]).Item1, Is.EqualTo("Main"));
        Assert.That(places[0].Tab, Is.EqualTo("Surface"));
        Assert.That(places[0].Path, Is.EqualTo("Surface Tools › More Surfaces"));
        Assert.That(places[1].Group, Is.Empty);
        Assert.That(places[1].Tab, Is.Empty);
        Assert.That(places[1].Path, Is.EqualTo("Loose"));
        Assert.That(places[1].GroupId, Is.EqualTo(Guid.Empty));
    }

    [Test]
    public void Text_that_is_not_xml_gives_an_empty_index()
    {
        Assert.That(ToolbarIndex.Parse("<not xml", 1033, "Win", "sidebar").Find("_Loft"), Is.Empty);
    }

    [TestCase("! _Loft", "Loft")]
    [TestCase("'_-Loft", "Loft")]
    [TestCase("_Loft _Pause", "Loft")]
    [TestCase("  !  _Loft;", "Loft")]
    [TestCase("", null)]
    [TestCase("!", null)]
    public void The_first_command_of_a_script_is_found(string script, string? expected)
    {
        Assert.That(ToolbarIndex.FirstCommand(script), Is.EqualTo(expected));
    }

    [Test]
    public void The_toolbar_found_event_has_the_json_the_panel_fixture_pins()
    {
        var json = PanelJson.Serialize(new ToolbarFoundEvent("_Loft", "turn-0",
            new[] { new PanelToolbarPlace("Main", "Surface", "Surface Tools", "Loft", "left", false) }));

        Assert.That(json, Is.EqualTo("""{"type":"toolbar.found","name":"_Loft","turnId":"turn-0","places":[{"group":"Main","tab":"Surface","path":"Surface Tools","button":"Loft","click":"left","hidden":false}]}"""));
    }

    [Test]
    public void The_toolbar_find_command_the_panel_sends_is_understood()
    {
        var command = PanelJson.Deserialize("""{"type":"toolbar.find","name":"_Loft","turnId":"turn-0"}""");

        Assert.That(command, Is.TypeOf<FindToolbarCommand>());
        Assert.That(((FindToolbarCommand)command!).Name, Is.EqualTo("_Loft"));
        Assert.That(((FindToolbarCommand)command).TurnId, Is.EqualTo("turn-0"));
    }
}
