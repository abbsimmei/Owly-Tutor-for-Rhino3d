namespace Rhino.AI;

// Plan 030: when Owly asks the student whether the explanations fit. Rhino-free, so Server.Tests can link it.
internal static class CheckInSchedule
{
    public const int First = 10;
    public const int Every = 30;

    // After the 10th answer in total, then every 30 answers: 10, 40, 70, ...
    public static bool IsDue(int answers) => answers >= First && (answers - First) % Every == 0;

    // Show at most one check-in per conversation. A due (or still pending) check-in that cannot be shown
    // because one was already shown in this conversation waits, as Pending, for the next conversation.
    public static (bool Show, bool Pending) Decide(int answers, bool pending, bool shownInConversation)
    {
        if (!IsDue(answers) && !pending)
            return (false, pending);
        return shownInConversation ? (false, true) : (true, false);
    }

    // Only a turn that ended normally and produced answer text is an answer.
    public static bool ShouldCount(bool completedNormally, bool hasAnswerText) => completedNormally && hasAnswerText;
}
