using NUnit.Framework;
using Rhino.AI.UI;

namespace Rhino.AI.Server.Tests;

// Every fixture here is hand-written, with invented option names and descriptions. No McNeel help-page text is
// copied (the pages are copyrighted); the markup only mimics the structure the parser has to cope with.
[TestFixture]
public class CommandOptionsParserTests
{
    private static string Page(string section) =>
        "<html><body><div class=\"nav\"><h5>Nav</h5></div><div role=\"main\" id=\"mc-main-content\">" + section + "</div></body></html>";

    private static CommandOption Opt(CommandOptions o, string name) => o.Options.Single(x => x.Name == name);

    [Test]
    public void Layout_A_reads_a_choice_whose_values_sit_in_indent_level_2()
    {
        var html = Page("""
            <p class="Dialog_Box_Title">Command-line options</p>
            <h5>Shape</h5><p>Demo option description.</p>
            <div class="Indent_Level_2">
              <h5>Round</h5><p>Demo round value.</p>
              <h5>Square</h5><p>Demo square value.</p>
            </div>
            <h5>Sides</h5><p>Another demo description.</p>
            """);
        var o = CommandOptionsParser.Parse(html);
        Assert.That(o.Options.Select(x => x.Name), Is.EqualTo(new[] { "Shape", "Sides" }));
        var shape = Opt(o, "Shape");
        Assert.That(shape.Kind, Is.EqualTo("choice"));
        Assert.That(shape.Description, Is.EqualTo("Demo option description."));
        Assert.That(shape.Values.Select(v => v.Name), Is.EqualTo(new[] { "Round", "Square" }));
        Assert.That(shape.Values[1].Description, Is.EqualTo("Demo square value."));
        Assert.That(Opt(o, "Sides").Kind, Is.EqualTo("info"));
    }

    [Test]
    public void A_yes_no_option_is_a_toggle_and_on_off_too()
    {
        var html = Page("""
            <p class="Dialog_Box_Title">Command-line options</p>
            <h5>Filled</h5><p>Demo.</p>
            <div class="Indent_Level_2"><h5>No</h5><p>a</p><h5>Yes</h5><p>b</p></div>
            <h5>Glow</h5><p>Demo.</p>
            <div class="Indent_Level_2"><h5>On</h5><p>a</p><h5>Off</h5><p>b</p></div>
            """);
        var o = CommandOptionsParser.Parse(html);
        Assert.That(Opt(o, "Filled").Kind, Is.EqualTo("toggle"));
        Assert.That(Opt(o, "Glow").Kind, Is.EqualTo("toggle"));
    }

    [Test]
    public void A_single_value_keeps_the_kind_info()
    {
        var html = Page("""
            <p class="Dialog_Box_Title">Command-line options</p>
            <h5>Edge</h5><p>Demo.</p>
            <div class="Indent_Level_2"><h5>Only</h5><p>x</p></div>
            """);
        var edge = Opt(CommandOptionsParser.Parse(html), "Edge");
        Assert.That(edge.Kind, Is.EqualTo("info"));
        Assert.That(edge.Values.Count, Is.EqualTo(1));
    }

    [Test]
    public void Heading_text_ignores_anchors_and_new_icons_and_skips_non_identifiers()
    {
        var html = Page("""
            <p class="Dialog_Box_Title">Command-line options</p>
            <h5><a name="Corner"></a>Corner <img class="new_icon" src="x.png" /></h5><p>Demo.</p>
            <h5>(Default)</h5><p>Skipped.</p>
            <h5>Straight parts</h5><p>Skipped too.</p>
            <h5>Kept</h5><p>Demo kept.</p>
            """);
        var o = CommandOptionsParser.Parse(html);
        Assert.That(o.Options.Select(x => x.Name), Is.EqualTo(new[] { "Corner", "Kept" }));
    }

    [Test]
    public void An_anchor_in_the_heading_is_removed()
    {
        var html = Page("""
            <p class="Dialog_Box_Title">Command-line options</p>
            <h5><a name="Corner"></a>Corner</h5><p>Demo.</p>
            """);
        Assert.That(CommandOptionsParser.Parse(html).Options.Single().Name, Is.EqualTo("Corner"));
    }

    [Test]
    public void An_h4_tutorial_block_in_the_section_is_skipped()
    {
        var html = Page("""
            <p class="Dialog_Box_Title">Command-line options</p>
            <h5>Shape</h5><p>Demo.</p>
            <h4>Steps</h4>
            <p>Tutorial text, not an option.</p>
            <h5>Sides</h5><p>Demo sides.</p>
            """);
        var o = CommandOptionsParser.Parse(html);
        Assert.That(o.Options.Select(x => x.Name), Is.EqualTo(new[] { "Shape", "Sides" }));
        Assert.That(Opt(o, "Shape").Description, Is.EqualTo("Demo."));
    }

    [Test]
    public void Other_dialog_titles_are_ignored_and_end_the_section()
    {
        var html = Page("""
            <p class="Dialog_Box_Title">Demo Options</p>
            <h5>Dialogonly</h5><p>No.</p>
            <p class="Dialog_Box_Title">Command-line options</p>
            <h5>Shape</h5><p>Demo.</p>
            <p class="Dialog_Box_Title">Demo Options</p>
            <h5>Alsodialog</h5><p>No.</p>
            """);
        Assert.That(CommandOptionsParser.Parse(html).Options.Select(x => x.Name), Is.EqualTo(new[] { "Shape" }));
    }

    [Test]
    public void Two_command_line_sections_are_merged_and_duplicates_dropped()
    {
        var html = Page("""
            <p class="Dialog_Box_Title">Command-line options</p>
            <h5>Shape</h5><p>First.</p>
            <h2>See also</h2>
            <p class="Dialog_Box_Title">Command-line options</p>
            <h5>Sides</h5><p>Second.</p>
            <h5>Shape</h5><p>Duplicate.</p>
            """);
        var o = CommandOptionsParser.Parse(html);
        Assert.That(o.Options.Select(x => x.Name), Is.EqualTo(new[] { "Shape", "Sides" }));
        Assert.That(Opt(o, "Shape").Description, Is.EqualTo("First."));
    }

    [Test]
    public void Layout_B_reads_table_rows_with_h5_values_in_the_second_cell()
    {
        var html = Page("""
            <table>
              <tr><th colspan="2">Command-line options</th></tr>
              <tr><td><p>Sides</p></td><td>Demo number of sides.</td></tr>
              <tr><td><p>Mode</p></td><td>Demo mode.<h5>Inner</h5><p>Inner demo.</p><h5>Outer</h5><p>Outer demo.</p></td></tr>
            </table>
            <table><tr><th>Other</th></tr><tr><td><p>Ignored</p></td><td>x</td></tr></table>
            """);
        var o = CommandOptionsParser.Parse(html);
        Assert.That(o.Options.Select(x => x.Name), Is.EqualTo(new[] { "Sides", "Mode" }));
        Assert.That(Opt(o, "Sides").Description, Is.EqualTo("Demo number of sides."));
        Assert.That(Opt(o, "Sides").Kind, Is.EqualTo("info"));
        var mode = Opt(o, "Mode");
        Assert.That(mode.Kind, Is.EqualTo("choice"));
        Assert.That(mode.Description, Is.EqualTo("Demo mode."));
        Assert.That(mode.Values.Select(v => v.Name), Is.EqualTo(new[] { "Inner", "Outer" }));
    }

    [Test]
    public void Conditional_wrappers_are_transparent()
    {
        var html = Page("""
            <p class="Dialog_Box_Title">Command-line options</p>
            <div data-mc-conditions="V9.Only"><h5>Shape</h5><p>Demo.</p></div>
            <table><tr data-mc-conditions="V8.Win_Only"><td>x</td></tr></table>
            <div data-mc-conditions="V9.Only"><h5>Sides</h5><p>Demo.</p></div>
            """);
        Assert.That(CommandOptionsParser.Parse(html).Options.Select(x => x.Name), Is.EqualTo(new[] { "Shape", "Sides" }));
    }

    [Test]
    public void No_section_gives_empty()
    {
        var o = CommandOptionsParser.Parse(Page("<h5>Shape</h5><p>Not in a section.</p>"));
        Assert.That(o.Options, Is.Empty);
        Assert.That(CommandOptionsParser.Parse(null).Options, Is.Empty);
        Assert.That(CommandOptionsParser.Parse("").Options, Is.Empty);
        Assert.That(CommandOptionsParser.Parse("<Error><Code>AccessDenied</Code></Error>").Options, Is.Empty);
    }

    [Test]
    public void Malformed_or_truncated_html_never_throws()
    {
        var full = Page("""
            <p class="Dialog_Box_Title">Command-line options</p>
            <h5>Shape</h5><p>Demo.</p>
            <div class="Indent_Level_2"><h5>Round</h5><p>x</p><h5>Square</h5><p>y</p></div>
            <h5>Sides</h5><p>Demo.</p>
            """);
        for (var cut = 0; cut < full.Length; cut += 7)
            Assert.DoesNotThrow(() => CommandOptionsParser.Parse(full.Substring(0, cut)));
        Assert.DoesNotThrow(() => CommandOptionsParser.Parse("<<<>>> <h5 <p class= <div"));
        Assert.DoesNotThrow(() => CommandOptionsParser.Parse("<table><th colspan=\"2\">Command-line options<tr><td>"));
    }

    [Test]
    public void A_description_is_capped_at_600_characters()
    {
        var html = Page("<p class=\"Dialog_Box_Title\">Command-line options</p><h5>Shape</h5><p>" + new string('a', 2000) + "</p>");
        Assert.That(CommandOptionsParser.Parse(html).Options.Single().Description.Length, Is.EqualTo(600));
    }

    [Test]
    public void The_description_collapses_whitespace_and_decodes_entities()
    {
        var html = Page("<p class=\"Dialog_Box_Title\">Command-line options</p><h5>Shape</h5><p>One&#160;two <br />\n   three &amp; four.</p>");
        Assert.That(CommandOptionsParser.Parse(html).Options.Single().Description, Is.EqualTo("One two three & four."));
    }

    [Test]
    public void The_result_is_capped_at_30_options()
    {
        var sb = new System.Text.StringBuilder("<p class=\"Dialog_Box_Title\">Command-line options</p>");
        for (var i = 0; i < 50; i++) sb.Append($"<h5>Opt{i}</h5><p>d</p>");
        Assert.That(CommandOptionsParser.Parse(Page(sb.ToString())).Options.Count, Is.EqualTo(30));
    }

    [Test]
    public void Only_the_first_h1_block_counts_in_both_layouts()
    {
        var html = Page("""
            <h1>Demo</h1>
            <p class="Dialog_Box_Title">Command-line options</p>
            <h5>Shape</h5><p>Demo.</p>
            <table><tr><th colspan="2">Command-line options</th></tr><tr><td><p>Sides</p></td><td>Demo sides.</td></tr></table>
            <h1><a name="Other"></a>OtherDemo</h1>
            <p class="Dialog_Box_Title">Command-line options</p>
            <h5>Extra</h5><p>Second command.</p>
            <table><tr><th colspan="2">Command-line options</th></tr><tr><td><p>Density</p></td><td>Second command.</td></tr></table>
            """);
        Assert.That(CommandOptionsParser.Parse(html).Options.Select(x => x.Name), Is.EqualTo(new[] { "Sides", "Shape" }));
    }

    [Test]
    public void An_indent_block_with_a_nested_indent_block_holds_sub_options_not_values()
    {
        var html = Page("""
            <p class="Dialog_Box_Title">Command-line options</p>
            <h5>Chain</h5><p>Demo chain.</p>
            <div class="Indent_Level_2">
              <h5>Continuity</h5><p>Sub-option.</p>
              <h5>Direction</h5><p>Sub-option.</p>
              <div class="Indent_Level_2"><h5>Forward</h5><p>x</p><h5>Backward</h5><p>y</p></div>
            </div>
            <h5>Shape</h5><p>Demo.</p>
            <div class="Indent_Level_2"><h5>Round</h5><p>x</p><h5>Square</h5><p>y</p></div>
            """);
        var o = CommandOptionsParser.Parse(html);
        Assert.That(o.Options.Select(x => x.Name), Is.EqualTo(new[] { "Chain", "Shape" }));
        Assert.That(Opt(o, "Chain").Kind, Is.EqualTo("info"));
        Assert.That(Opt(o, "Chain").Values, Is.Empty);
        Assert.That(Opt(o, "Shape").Kind, Is.EqualTo("choice"));
    }

    [Test]
    public void Table_cell_values_only_count_before_a_tutorial()
    {
        var html = Page("""
            <table>
              <tr><th colspan="2">Command-line options</th></tr>
              <tr><td><p>Mode</p></td><td><h5>Inner</h5><p>Inner demo.</p><div class="tutorialblock"><h4>Inner steps</h4><h5>Bogus</h5><p>Steps.</p></div><h5>Outer</h5><p>Outer demo.</p><div class="tutorialblock"><h4>Outer steps</h4><p>Steps.</p></div></td></tr>
              <tr><td><p>Tangent</p></td><td>Demo tangent text.
                <div class="tutorialblock"><h4>Tangent steps</h4><p>Steps.</p></div>
                <h5>FromFirst</h5><p>Sub.</p><h5>Radius</h5><p>Sub.</p></td></tr>
              <tr><td><p>Edge</p></td><td>Demo edge text.<div class="tutorialblock"><h4>Edge steps</h4><h5>Flip</h5><p>Inside a tutorial.</p></div></td></tr>
            </table>
            """);
        var o = CommandOptionsParser.Parse(html);
        Assert.That(Opt(o, "Mode").Kind, Is.EqualTo("choice"));
        Assert.That(Opt(o, "Mode").Values.Select(v => v.Name), Is.EqualTo(new[] { "Inner", "Outer" }));
        Assert.That(Opt(o, "Tangent").Kind, Is.EqualTo("info"));
        Assert.That(Opt(o, "Tangent").Values, Is.Empty);
        Assert.That(Opt(o, "Edge").Values, Is.Empty);
    }

    [Test]
    public void A_table_description_stops_at_tutorial_text_or_a_heading()
    {
        var html = Page("""
            <table>
              <tr><th colspan="2">Command-line options</th></tr>
              <tr><td><p>Edge</p></td><td>Demo edge text. <h4>Edge steps</h4><p>Pick something.</p></td></tr>
              <tr><td><p>Star</p></td><td>Demo star text. <div class="tutorialblock"><p>Tutorial.</p></div></td></tr>
              <tr><td><p>Mode</p></td><td>Demo mode text. <h5>Inner</h5><p>x</p></td></tr>
            </table>
            """);
        var o = CommandOptionsParser.Parse(html);
        Assert.That(Opt(o, "Edge").Description, Is.EqualTo("Demo edge text."));
        Assert.That(Opt(o, "Star").Description, Is.EqualTo("Demo star text."));
        Assert.That(Opt(o, "Mode").Description, Is.EqualTo("Demo mode text."));
    }

    [Test]
    public void Yes_no_on_off_are_never_option_names()
    {
        var html = Page("""
            <p class="Dialog_Box_Title">Command-line options</p>
            <h5>Trim</h5><p>Demo.</p>
            <table><tr><td><h5 style="text-align: center;">Yes</h5></td><td><h5 style="text-align: center;">No</h5></td></tr></table>
            <h5>ON</h5><p>x</p><h5>off</h5><p>y</p>
            <h5>Shape</h5><p>Demo.</p>
            """);
        Assert.That(CommandOptionsParser.Parse(html).Options.Select(x => x.Name), Is.EqualTo(new[] { "Trim", "Shape" }));
    }

    [Test]
    public void The_options_found_event_has_the_json_the_panel_fixture_pins()
    {
        var ev = new CommandOptionsFoundEvent("_Pyramid", new[]
        {
            new PanelCommandOption("Shape", "choice", new[] { new PanelCommandOptionValue("Round", "Demo round."), new PanelCommandOptionValue("Square", "Demo square.") }, "Demo shape option."),
        });
        Assert.That(PanelJson.Serialize(ev), Is.EqualTo(
            """{"type":"command.options.found","name":"_Pyramid","options":[{"name":"Shape","kind":"choice","values":[{"name":"Round","description":"Demo round."},{"name":"Square","description":"Demo square."}],"description":"Demo shape option."}]}"""));
    }

    [Test]
    public void A_merged_options_found_event_has_the_json_the_panel_fixture_pins()
    {
        var ev = new CommandOptionsFoundEvent("_Demo", new[]
        {
            new PanelCommandOption("Sides", "number", Array.Empty<PanelCommandOptionValue>(), "Demo sides.", "8", true),
            new PanelCommandOption("Shape", "info", Array.Empty<PanelCommandOptionValue>(), "Demo shape.", "Round", true),
        });
        Assert.That(PanelJson.Serialize(ev), Is.EqualTo(
            """{"type":"command.options.found","name":"_Demo","options":[{"name":"Sides","kind":"number","values":[],"description":"Demo sides.","current":"8","firstPrompt":true},{"name":"Shape","kind":"info","values":[],"description":"Demo shape.","current":"Round","firstPrompt":true}]}"""));
    }

    [Test]
    public void The_options_commands_the_panel_sends_are_understood()
    {
        var q = (CommandOptionsCommand)PanelJson.Deserialize("""{"type":"command.options","names":["_Pyramid","_Cone"]}""")!;
        Assert.That(q.Names, Is.EqualTo(new[] { "_Pyramid", "_Cone" }));

        var withOptions = (RunCommandCommand)PanelJson.Deserialize("""{"type":"command.run","name":"_Pyramid","options":{"Shape":"Round"}}""")!;
        Assert.That(withOptions.Name, Is.EqualTo("_Pyramid"));
        Assert.That(withOptions.Options, Is.Not.Null);
        Assert.That(withOptions.Options!["Shape"], Is.EqualTo("Round"));

        var without = (RunCommandCommand)PanelJson.Deserialize("""{"type":"command.run","name":"_Pyramid"}""")!;
        Assert.That(without.Options, Is.Null);
    }
}
