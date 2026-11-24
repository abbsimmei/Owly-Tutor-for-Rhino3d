using NUnit.Framework;
using Rhino.AI.UI;

namespace Rhino.AI.Server.Tests;

// Plan 021: which image does a command's toolbar button have? Everything here is synthetic (made-up GUIDs and
// shapes); nothing is copied from Rhino or McNeel files.
[TestFixture]
public class ToolbarIconTests
{
    private const string LeftId = "e0000000-0000-0000-0000-00000000000a";
    private const string RightId = "e0000000-0000-0000-0000-00000000000b";
    private const string InlineId = "e0000000-0000-0000-0000-00000000000c";
    private const string TextId = "e0000000-0000-0000-0000-00000000000d";

    private const string Rui = """
        <RhinoUI guid="11111111-0000-0000-0000-000000000000" default_language_id="1033">
          <tool_bars>
            <tool_bar guid="b0000000-0000-0000-0000-000000000001">
              <text><locale_1033>Tools</locale_1033></text>
              <tool_bar_item guid="c0000000-0000-0000-0000-000000000001">
                <left_macro_id>d0000000-0000-0000-0000-000000000001</left_macro_id>
                <right_macro_id>d0000000-0000-0000-0000-000000000002</right_macro_id>
              </tool_bar_item>
              <tool_bar_item guid="c0000000-0000-0000-0000-000000000002">
                <left_macro_id>d0000000-0000-0000-0000-000000000003</left_macro_id>
              </tool_bar_item>
              <tool_bar_item guid="c0000000-0000-0000-0000-000000000003">
                <left_macro_id>d0000000-0000-0000-0000-000000000004</left_macro_id>
              </tool_bar_item>
              <tool_bar_item guid="c0000000-0000-0000-0000-000000000004">
                <left_macro_id>d0000000-0000-0000-0000-000000000005</left_macro_id>
              </tool_bar_item>
            </tool_bar>
          </tool_bars>
          <macros>
            <macro_item guid="d0000000-0000-0000-0000-000000000001" bitmap_id="e0000000-0000-0000-0000-00000000000a"><script>! _Alpha</script></macro_item>
            <macro_item guid="d0000000-0000-0000-0000-000000000002" bitmap_id="e0000000-0000-0000-0000-00000000000b"><script>! _Alpha</script></macro_item>
            <macro_item guid="d0000000-0000-0000-0000-000000000003" bitmap_id="e0000000-0000-0000-0000-00000000000b"><script>! _Beta</script></macro_item>
            <macro_item guid="d0000000-0000-0000-0000-000000000004" bitmap_id="E0000000-0000-0000-0000-00000000000C"><script>! _Gamma</script></macro_item>
            <macro_item guid="d0000000-0000-0000-0000-000000000005"><script>! _Delta</script></macro_item>
          </macros>
          <icons>
            <icon guid="e0000000-0000-0000-0000-00000000000c">
              <light><svg xmlns="http://www.w3.org/2000/svg" width="16" height="16"><rect width="16" height="16" fill="#111111"/></svg></light>
              <dark><svg xmlns="http://www.w3.org/2000/svg" width="16" height="16"><rect width="16" height="16" fill="#eeeeee"/></svg></dark>
            </icon>
            <icon guid="e0000000-0000-0000-0000-00000000000d">
              <light><![CDATA[<svg width="8" height="8"/>]]></light>
              <dark>   </dark>
            </icon>
          </icons>
        </RhinoUI>
        """;

    private static ToolbarIndex Index() => ToolbarIndex.Parse(Rui, 1033, "Win", "sidebar");

    [Test]
    public void A_left_click_button_image_wins_over_a_right_click_one()
    {
        Assert.That(Index().BitmapIdFor("_Alpha"), Is.EqualTo(LeftId));
    }

    [Test]
    public void A_right_click_only_button_image_is_still_found()
    {
        Assert.That(Index().BitmapIdFor("Beta"), Is.EqualTo(RightId));
    }

    [Test]
    public void A_command_with_no_bitmap_id_or_no_button_has_none()
    {
        Assert.That(Index().BitmapIdFor("_Delta"), Is.Null);
        Assert.That(Index().BitmapIdFor("_Unknown"), Is.Null);
        Assert.That(Index().BitmapIdFor("_"), Is.Null);
    }

    [Test]
    public void An_inline_icon_keeps_its_svg_element_and_ignores_the_guid_case()
    {
        var icon = Index().InlineIcon(Index().BitmapIdFor("_Gamma")!);

        Assert.That(icon, Is.Not.Null);
        Assert.That(icon!.Value.Light, Does.StartWith("<svg").And.Contain("#111111"));
        Assert.That(icon.Value.Dark, Does.StartWith("<svg").And.Contain("#eeeeee"));
    }

    [Test]
    public void An_inline_icon_given_as_text_is_taken_and_a_blank_variant_is_null()
    {
        var icon = Index().InlineIcon(TextId);

        Assert.That(icon!.Value.Light, Is.EqualTo("""<svg width="8" height="8"/>"""));
        Assert.That(icon.Value.Dark, Is.Null);
    }

    [Test]
    public void An_unknown_inline_icon_is_null()
    {
        Assert.That(Index().InlineIcon(LeftId), Is.Null);
    }

    [Test]
    public void Icon_ids_are_parsed_and_malformed_lines_skipped()
    {
        var csv = "e0000000-0000-0000-0000-00000000000A,one.svg,one_dark.svg\r\n"
                  + "\n"
                  + "not-a-guid,two.svg,two_dark.svg\n"
                  + "e0000000-0000-0000-0000-00000000000b,only_two_columns.svg\n"
                  + "e0000000-0000-0000-0000-00000000000c, three.svg , three_dark.svg \n"
                  + "e0000000-0000-0000-0000-00000000000d,,blank.svg\n";

        var map = IconIds.Parse(csv);

        Assert.That(map, Has.Count.EqualTo(2));
        Assert.That(map[Guid.Parse(LeftId)], Is.EqualTo(("one.svg", "one_dark.svg")));
        Assert.That(map[Guid.Parse(InlineId)], Is.EqualTo(("three.svg", "three_dark.svg")));
    }

    [Test]
    public void Resource_names_try_the_svg_folder_first()
    {
        Assert.That(IconIds.ResourceNames("x.svg"),
            Is.EqualTo(new[] { "Rhino.UI.Resources.svg.x.svg", "Rhino.UI.Resources.x.svg" }));
    }

    [Test]
    public void Accept_keeps_only_small_svg_text()
    {
        Assert.That(IconIds.Accept(null), Is.Null);
        Assert.That(IconIds.Accept("  "), Is.Null);
        Assert.That(IconIds.Accept("<html/>"), Is.Null);
        Assert.That(IconIds.Accept("<svg/>" + new string(' ', IconIds.MaxSvgChars) + "x"), Is.Null);
        Assert.That(IconIds.Accept("  <svg/> \n"), Is.EqualTo("<svg/>"));
        Assert.That(IconIds.Accept("<?xml version=\"1.0\"?><svg/>"), Is.EqualTo("<?xml version=\"1.0\"?><svg/>"));
    }

    [Test]
    public void The_toolbar_icon_command_the_panel_sends_is_understood()
    {
        var command = PanelJson.Deserialize("""{"type":"toolbar.icon","names":["_Loft","_Box"]}""");

        Assert.That(command, Is.TypeOf<ToolbarIconCommand>());
        Assert.That(((ToolbarIconCommand)command!).Names, Is.EqualTo(new[] { "_Loft", "_Box" }));
    }
}
