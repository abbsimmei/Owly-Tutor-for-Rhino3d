using NUnit.Framework;

namespace Rhino.AI.Server.Tests;

// Plan 026: the saved transcript handed back to Claude Code when its own session file is gone.
[TestFixture]
public class ResumeContextTests
{
    private static TurnDto Turn(string prompt, string answer, bool withTool = false)
    {
        List<TurnEventDto> events = [];
        if (withTool)
        {
            events.Add(new TurnEventDto(TurnEventKind.ToolUse, "get_commands", DateTimeOffset.UnixEpoch, "{\"secret\":\"args\"}", "TOOL-RESULT", "c1", false, true));
        }
        events.Add(new TurnEventDto(TurnEventKind.AssistantText, answer.Substring(0, answer.Length / 2), DateTimeOffset.UnixEpoch, "", ""));
        events.Add(new TurnEventDto(TurnEventKind.AssistantText, answer.Substring(answer.Length / 2), DateTimeOffset.UnixEpoch, "", ""));
        return new TurnDto(prompt, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch, events);
    }

    [Test]
    public void No_turns_give_no_context()
    {
        Assert.That(ResumeContext.Build([]), Is.Empty);
    }

    [Test]
    public void The_context_is_framed_and_joins_the_answer_chunks()
    {
        string text = ResumeContext.Build([Turn("How do I loft?", "Use _Loft."), Turn("And a sweep?", "Use _Sweep1.")]);

        Assert.That(text, Is.EqualTo(
            "Earlier in this conversation (for context only):\n" +
            "Student: How do I loft?\nOwly: Use _Loft.\n" +
            "Student: And a sweep?\nOwly: Use _Sweep1.\n" +
            "End of the earlier conversation. The student's new message follows."));
    }

    [Test]
    public void Tool_calls_and_results_are_left_out()
    {
        string text = ResumeContext.Build([Turn("Q", "Answer.", withTool: true)]);

        Assert.That(text, Does.Not.Contain("TOOL-RESULT").And.Not.Contain("secret").And.Not.Contain("get_commands"));
        Assert.That(text, Does.Contain("Owly: Answer."));
    }

    [Test]
    public void A_turn_without_an_answer_keeps_only_the_question()
    {
        string text = ResumeContext.Build([new TurnDto("Q", DateTimeOffset.UnixEpoch, null, [])]);

        Assert.That(text, Does.Contain("Student: Q\n"));
        Assert.That(text, Does.Not.Contain("Owly:"));
    }

    [Test]
    public void The_total_never_exceeds_the_cap_and_the_newest_whole_turns_are_kept()
    {
        List<TurnDto> turns = [];
        for (int i = 0; i < 30; i++)
            turns.Add(Turn($"question {i:00}", new string((char)('a' + i % 26), 300)));

        string text = ResumeContext.Build(turns);

        Assert.That(text.Length, Is.LessThanOrEqualTo(ResumeContext.DefaultCap));
        Assert.That(text, Does.Contain("question 29"));
        Assert.That(text, Does.Not.Contain("question 00"));
        // Whole turns only: the oldest kept turn still starts with its question.
        string firstKept = text.Split('\n')[1];
        Assert.That(firstKept, Does.StartWith("Student: question "));
    }

    [Test]
    public void A_smaller_cap_keeps_fewer_turns()
    {
        TurnDto[] turns = [Turn("one", "aaaa"), Turn("two", "bbbb"), Turn("three", "cccc")];

        string text = ResumeContext.Build(turns, cap: 140);

        Assert.That(text.Length, Is.LessThanOrEqualTo(140));
        Assert.That(text, Does.Contain("three"));
        Assert.That(text, Does.Not.Contain("Student: one"));
    }

    [Test]
    public void A_single_oversize_turn_keeps_its_tail()
    {
        string answer = new string('a', 5000) + "THE-END";

        string text = ResumeContext.Build([Turn("Q", answer)]);

        Assert.That(text.Length, Is.LessThanOrEqualTo(ResumeContext.DefaultCap));
        Assert.That(text, Does.Contain("THE-END\n"));
        Assert.That(text, Does.StartWith("Earlier in this conversation (for context only):\n"));
        Assert.That(text, Does.EndWith("The student's new message follows."));
    }

    [Test]
    public void The_tail_is_cut_on_a_character_boundary()
    {
        string answer = string.Concat(Enumerable.Repeat("\U0001F989", 3000));

        string text = ResumeContext.Build([Turn("Q", answer)], cap: 301);

        Assert.That(text.Length, Is.LessThanOrEqualTo(301));
        Assert.That(text.Any(char.IsLowSurrogate) && !text.Any(char.IsHighSurrogate), Is.False);
        for (int i = 0; i < text.Length; i++)
            if (char.IsLowSurrogate(text[i]))
                Assert.That(i > 0 && char.IsHighSurrogate(text[i - 1]), Is.True, $"orphan low surrogate at {i}");
    }

    [Test]
    public void A_cap_too_small_for_the_framing_gives_nothing()
    {
        Assert.That(ResumeContext.Build([Turn("Q", "A")], cap: 20), Is.Empty);
    }
}
