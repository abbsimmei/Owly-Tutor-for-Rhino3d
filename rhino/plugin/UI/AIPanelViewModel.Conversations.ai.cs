using Rhino.UI;

namespace Rhino.AI.UI;

// Plan 026 / ADR-0009: the saved conversations of this document's project, shown as the strip of owls.
// A conversation is saved after every turn and when it ends (AgentRunner, AgentHost). Here the panel
// asks to open, rename or delete one. The open conversation always belongs to the panel's current
// project: switching or moving the project moves it, and while the project is unknown it is filed
// under Unsorted.
internal partial class AIPanelViewModel
{
    private static ConversationStore Saved => OwlyMemory.Conversations;

    private void SubscribeConversations()
    {
        OwlyMemory.ConversationsChanged += SendConversations;
        OwlyMemory.Cleared += OnMemoryCleared;
    }

    private void UnsubscribeConversations()
    {
        OwlyMemory.ConversationsChanged -= SendConversations;
        OwlyMemory.Cleared -= OnMemoryCleared;
    }

    private void SendConversations()
    {
        string? project = ProjectId;
        Guid? open = ActiveConversation is { } convo ? convo.AgentSessionId : null;

        List<PanelConversationItem> items = [];
        if (project is not null)
        {
            foreach (ConversationSummary saved in Saved.List(project))
                items.Add(new PanelConversationItem(
                    saved.SessionId,
                    saved.Title,
                    saved.StartedAt.ToString("O"),
                    open is { } id && Guid.TryParse(saved.SessionId, out Guid savedId) && savedId == id));
        }

        Bridge.Post(new ConversationsEvent(project, items));
    }

    // Keeps the open conversation in the panel's current project. When that moves a conversation that
    // already has turns, the save moves its file too.
    private void SyncConversationProject()
    {
        if (ActiveConversation is not { } convo || convo.ProjectId == ProjectId) return;

        convo.ProjectId = ProjectId;
        if (convo.Turns.Count > 0 && Saved.Save(convo))
            OwlyMemory.NotifyConversationsChanged();
    }

    // The open conversation's project (or all of Owly's memory) was deleted: it must not be saved again.
    private void DiscardOpenConversation()
    {
        if (ActiveConversation is { } convo)
            Saved.Discard(convo);
        NewConversation();
    }

    private void OnMemoryCleared() => DiscardOpenConversation();

    // A saved conversation of this project, by id from the panel. Null when the id is not one.
    private ConversationSummary? FindSaved(string? sessionId)
    {
        if (ProjectId is not { } project || !ConversationStore.IsSessionId(sessionId)) return null;
        Guid wanted = Guid.Parse(sessionId!);
        return Saved.List(project).FirstOrDefault(c => Guid.TryParse(c.SessionId, out Guid id) && id == wanted);
    }

    private bool OpenInAnotherFile(string sessionId)
    {
        if (!AgentHost.IsLive(Guid.Parse(sessionId), DocumentSerialNumber)) return false;
        Bridge.Post(new NoticeEvent("info", Localization.LocalizeString("This conversation is open in another file.", 391)));
        return true;
    }

    private bool OpenConversation(string sessionId)
    {
        if (Document is not { } doc || ProjectId is not { } project || FindSaved(sessionId) is not { } saved) return false;

        Guid id = Guid.Parse(saved.SessionId);
        if (ActiveConversation?.AgentSessionId == id) return true;
        if (OpenInAnotherFile(saved.SessionId)) return false;

        // Replacing the pooled runner ends (and saves) the conversation that was open.
        if (Saved.Load(project, saved.SessionId) is not { } dto || !AgentHost.TryResume(doc, dto, out IAgentRunner resumed))
        {
            Bridge.Post(new NoticeEvent("error", Localization.LocalizeString("Owly couldn't open that conversation.", 392)));
            return false;
        }

        resumed.Conversation.ProjectId = project;
        ShowLive();
        SendConversations();
        return true;
    }

    private bool RenameConversation(string sessionId, string title)
    {
        if (ConversationStore.CleanTitle(title) is not { } clean)
        {
            Bridge.Post(new NoticeEvent("error", string.Format(Localization.LocalizeString("A conversation title needs 1 to {0} characters and no line breaks.", 393), ConversationStore.MaxTitleLength)));
            return false;
        }
        if (FindSaved(sessionId) is not { } saved || OpenInAnotherFile(saved.SessionId)) return false;

        // The open conversation keeps its title in memory, and its next save would write the old one back.
        if (ActiveConversation is { } convo && convo.AgentSessionId == Guid.Parse(saved.SessionId))
            convo.Title = clean;

        if (!Saved.Rename(ProjectId, saved.SessionId, clean)) return false;
        OwlyMemory.NotifyConversationsChanged();
        return true;
    }

    private bool DeleteConversation(string sessionId)
    {
        if (FindSaved(sessionId) is not { } saved || OpenInAnotherFile(saved.SessionId)) return false;

        if (ActiveConversation is { } convo && convo.AgentSessionId == Guid.Parse(saved.SessionId))
        {
            DiscardOpenConversation();
        }
        else if (!Saved.Delete(ProjectId, saved.SessionId))
        {
            return false;
        }

        OwlyMemory.NotifyConversationsChanged();
        return true;
    }
}
