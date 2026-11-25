using NUnit.Framework;

using Rhino.AI.UI;

namespace Rhino.AI.Server.Tests;

// Plan 030.
[TestFixture]
public class CheckInScheduleTests
{
    [TestCase(10)]
    [TestCase(40)]
    [TestCase(70)]
    public void It_is_due_at_the_tenth_answer_and_every_thirty_after(int answers) =>
        Assert.That(CheckInSchedule.IsDue(answers), Is.True);

    [TestCase(0)]
    [TestCase(9)]
    [TestCase(11)]
    [TestCase(39)]
    [TestCase(41)]
    public void It_is_not_due_at_other_counts(int answers) =>
        Assert.That(CheckInSchedule.IsDue(answers), Is.False);

    [Test]
    public void A_due_check_in_is_shown_when_none_was_shown_in_this_conversation() =>
        Assert.That(CheckInSchedule.Decide(10, pending: false, shownInConversation: false), Is.EqualTo((true, false)));

    [Test]
    public void A_due_check_in_waits_when_one_was_already_shown_in_this_conversation() =>
        Assert.That(CheckInSchedule.Decide(40, pending: false, shownInConversation: true), Is.EqualTo((false, true)));

    [Test]
    public void A_pending_check_in_is_shown_in_the_next_conversation_at_any_count() =>
        Assert.That(CheckInSchedule.Decide(12, pending: true, shownInConversation: false), Is.EqualTo((true, false)));

    [Test]
    public void A_pending_check_in_keeps_waiting_while_the_conversation_has_had_one() =>
        Assert.That(CheckInSchedule.Decide(12, pending: true, shownInConversation: true), Is.EqualTo((false, true)));

    [TestCase(false)]
    [TestCase(true)]
    public void Pending_is_unchanged_when_nothing_is_due_or_pending(bool shown) =>
        Assert.That(CheckInSchedule.Decide(11, pending: false, shownInConversation: shown), Is.EqualTo((false, false)));

    [TestCase(true, true, true)]
    [TestCase(true, false, false)]
    [TestCase(false, true, false)]
    [TestCase(false, false, false)]
    public void Only_a_normal_turn_with_answer_text_counts(bool normally, bool text, bool expected) =>
        Assert.That(CheckInSchedule.ShouldCount(normally, text), Is.EqualTo(expected));

    [Test]
    public void A_conversation_rebuilt_from_a_saved_one_has_not_had_a_check_in()
    {
        Conversation restored = Conversation.Restore(new ConversationDto(Guid.NewGuid().ToString(), "claude", "t", DateTimeOffset.UtcNow, [], []));
        Assert.That(restored.CheckInShown, Is.False);
    }

    // The panel fixture (rhino/panel/tests/host-events.json) pins this line.
    [Test]
    public void The_checkin_event_has_the_json_the_panel_fixture_pins() =>
        Assert.That(PanelJson.Serialize(new CheckInEvent("turn-0", "Intermediate")), Is.EqualTo("""{"type":"checkin","turnId":"turn-0","level":"Intermediate"}"""));

    [TestCase("""{"type":"checkin.answer","verdict":"easy"}""", typeof(CheckInAnswerCommand))]
    [TestCase("""{"type":"level.set","level":"Advanced"}""", typeof(SetLevelCommand))]
    public void The_check_in_commands_the_panel_sends_are_understood(string json, Type expected) =>
        Assert.That(PanelJson.Deserialize(json), Is.TypeOf(expected));
}
