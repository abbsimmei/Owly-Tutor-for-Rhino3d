namespace Rhino.AI;

internal sealed class CheckInDueEventArgs(Conversation conversation, string turnId) : EventArgs
{
    public Conversation Conversation { get; } = conversation;
    public string TurnId { get; } = turnId;

    // Set by the panel that shows the card. When nobody sets it, the check-in stays pending for a later conversation.
    public bool Delivered { get; set; }
}

// Plan 030: counts the answers Owly gives and says when to ask the student whether the explanations fit.
// The only caller of AnswerCompleted is AgentRunner.PromptAsync, so a reopened saved conversation (which never
// goes through PromptAsync) is never counted. Nothing here changes the level: that happens only on the student's
// click, through the panel's level.set.
internal static class CheckIns
{
    public static event EventHandler<CheckInDueEventArgs>? Due;

    // UI thread only (it reads and writes PersistentSettings).
    public static void AnswerCompleted(Conversation convo, string turnId)
    {
        int answers = AISettings.AnswersGiven + 1;
        AISettings.AnswersGiven = answers;

        (bool show, bool pending) = CheckInSchedule.Decide(answers, AISettings.CheckInPending, convo.CheckInShown);
        if (show)
        {
            CheckInDueEventArgs args = new(convo, turnId);
            try
            {
                Due?.Invoke(null, args);
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine($"[owly] check-in failed: {ex.Message}");
            }
            // No panel showed it: keep it for the next conversation rather than losing it.
            if (!args.Delivered)
                pending = true;
        }
        AISettings.CheckInPending = pending;
    }
}
