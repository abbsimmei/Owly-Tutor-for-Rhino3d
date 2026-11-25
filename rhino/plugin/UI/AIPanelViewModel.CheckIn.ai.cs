using Rhino.Runtime;
using Rhino.UI;

namespace Rhino.AI.UI;

// Plan 030: the level check-in card. The host decides when (CheckIns), the panel shows the card, and the level
// changes only when the student clicks Switch (level.set). The AI cannot trigger any of this.
internal partial class AIPanelViewModel
{
    private void SubscribeCheckIns() => CheckIns.Due += OnCheckInDue;

    private void UnsubscribeCheckIns() => CheckIns.Due -= OnCheckInDue;

    // Only the panel that shows this conversation posts the card; the others leave Delivered alone.
    private void OnCheckInDue(object? sender, CheckInDueEventArgs e)
    {
        if (!View.Loaded || !ReferenceEquals(e.Conversation, ActiveConversation))
            return;
        Bridge.Post(new CheckInEvent(e.TurnId, AISettings.Level.ToString()));
        e.Delivered = true;
        e.Conversation.CheckInShown = true;
    }

    // The verdict is only a debug log line. It is never sent anywhere and never stored.
    private bool AnswerCheckIn(string verdict)
    {
        if (verdict is not ("good" or "easy" or "hard"))
            return false;
        HostUtils.LogDebugEvent($"[owly] level check-in answer: {verdict}\n");
        return true;
    }

    // Only an exact level name; anything else is ignored.
    private bool SetLevel(string name)
    {
        if (!Enum.TryParse(name, ignoreCase: false, out OwlyLevel level) || !Enum.IsDefined(typeof(OwlyLevel), level))
            return false;
        if (level == AISettings.Level)
            return false;
        AISettings.Level = level;
        AgentHost.RestartIdleAgents();
        string label = level switch
        {
            OwlyLevel.Intermediate => Localization.LocalizeString("Intermediate", 448),
            OwlyLevel.Advanced => Localization.LocalizeString("Advanced", 449),
            _ => Localization.LocalizeString("Beginner", 447),
        };
        Bridge.Post(new NoticeEvent("info", string.Format(Localization.LocalizeString("Owly now explains at the {0} level.", 459), label)));
        return true;
    }
}
