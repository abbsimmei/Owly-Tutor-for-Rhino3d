using System.IO;

namespace Rhino.AI;

// What the panel's strip shows for one saved conversation.
internal sealed record ConversationSummary(string SessionId, string Title, DateTimeOffset StartedAt);

// Plan 026 / ADR-0009: saved conversations, one JSON file each at
// projects/<projectId>/conversations/<sessionId>.json under the Owly folder, through OwlyStore (so
// every write is atomic and under the shared lock). Ids become file names, so they are checked first:
// a session id must be a GUID, a project id "unsorted" or the 32 hex characters ProjectRegistry mints.
// Anything else is refused and nothing touches the disk. Nothing here throws into Rhino. Rhino-free,
// so Server.Tests links it.
internal sealed class ConversationStore
{
    public const int MaxTitleLength = Conversation.MaxTitleLength;

    // Save and Discard take this, so a save that is already running can't write a conversation back
    // after the student deleted it.
    private static readonly object SaveGate = new();

    private readonly OwlyStore _store;

    public ConversationStore(OwlyStore store) => _store = store;

    // Newest first. Corrupt files, files with no turns and files whose id doesn't match their name are skipped.
    public IReadOnlyList<ConversationSummary> List(string? projectId)
    {
        if (!IsProjectId(projectId)) return [];
        List<ConversationSummary> found = [];
        foreach (string name in _store.ListJson(FolderOf(projectId!)))
        {
            ConversationDto dto = _store.ReadJson<ConversationDto>(name);
            if (!Guid.TryParse(dto.SessionId, out Guid id) || dto.Turns is not { Count: > 0 } turns) continue;
            if (!string.Equals(Path.GetFileNameWithoutExtension(name), id.ToString("D"), StringComparison.OrdinalIgnoreCase)) continue;

            string title = string.IsNullOrWhiteSpace(dto.Title) ? Conversation.TitleFrom(turns[0].Prompt, "Conversation") : dto.Title;
            found.Add(new ConversationSummary(id.ToString("D"), title, dto.StartedAt));
        }
        return found.OrderByDescending(c => c.StartedAt).ToList();
    }

    public ConversationDto? Load(string? projectId, string? sessionId)
    {
        if (NameOf(projectId, sessionId) is not { } name) return null;
        ConversationDto dto = _store.ReadJson<ConversationDto>(name);
        return Guid.TryParse(dto.SessionId, out _) && dto.Turns is { Count: > 0 } ? dto : null;
    }

    // Writes the conversation under its current session id and project, then removes the file it was
    // last saved as when that differs (a project move, an adopted session id). A conversation with no
    // turns, or a discarded one, is not saved. True when a file was written.
    public bool Save(Conversation convo)
    {
        lock (SaveGate)
        {
            if (convo.Discarded) return false;
            ConversationDto dto = Snapshot(convo);
            if (dto.Turns.Count == 0) return false;

            string projectId = convo.ProjectId ?? ProjectRegistry.UnsortedId;
            if (NameOf(projectId, dto.SessionId) is not { } name) return false;

            dto.ProjectId = projectId;
            if (!_store.WriteJson(name, dto)) return false;

            if (convo.SavedAs is { } old && (old.SessionId != convo.AgentSessionId || old.ProjectId != projectId))
                if (NameOf(old.ProjectId, old.SessionId.ToString("D")) is { } oldName)
                    _store.Delete(oldName);
            convo.SavedAs = (convo.AgentSessionId, projectId);
            return true;
        }
    }

    // Trimmed, 1-80 characters, no control characters; null when the title can't be used.
    public static string? CleanTitle(string? title)
    {
        string trimmed = (title ?? string.Empty).Trim();
        if (trimmed.Length is 0 or > MaxTitleLength) return null;
        return trimmed.Any(char.IsControl) ? null : trimmed;
    }

    public bool Rename(string? projectId, string? sessionId, string? title)
    {
        if (NameOf(projectId, sessionId) is not { } name || CleanTitle(title) is not { } clean) return false;
        lock (SaveGate)
        {
            return _store.Update<ConversationDto>(name, dto =>
            {
                if (!string.Equals(dto.SessionId, sessionId, StringComparison.OrdinalIgnoreCase)) return false;
                dto.Title = clean;
                return true;
            });
        }
    }

    public bool Delete(string? projectId, string? sessionId)
    {
        if (NameOf(projectId, sessionId) is not { } name) return false;
        lock (SaveGate)
            return _store.Delete(name);
    }

    // Everything saved for a project. Unsorted is never deleted this way.
    public bool DeleteProject(string? projectId)
    {
        if (!IsProjectId(projectId) || projectId == ProjectRegistry.UnsortedId) return false;
        lock (SaveGate)
            return _store.DeleteFolder(ProjectFolder(projectId!));
    }

    // The student deleted this open conversation (or its project, or all memory): nothing saves it again.
    public void Discard(Conversation convo)
    {
        lock (SaveGate)
        {
            convo.Discarded = true;
            if (convo.SavedAs is { } saved && NameOf(saved.ProjectId, saved.SessionId.ToString("D")) is { } name)
                _store.Delete(name);
        }
    }

    public static bool IsSessionId(string? id) => id is { Length: > 0 } && Guid.TryParse(id, out _);

    // "unsorted", or the 32 lowercase hex characters of Guid.ToString("N").
    public static bool IsProjectId(string? id)
    {
        if (id is null) return false;
        if (id == ProjectRegistry.UnsortedId) return true;
        return id.Length == 32 && id.All(c => c is (>= '0' and <= '9') or (>= 'a' and <= 'f'));
    }

    private static string ProjectFolder(string projectId) => Path.Combine("projects", projectId);

    private static string FolderOf(string projectId) => Path.Combine(ProjectFolder(projectId), "conversations");

    private static string? NameOf(string? projectId, string? sessionId)
    {
        if (!IsProjectId(projectId) || !IsSessionId(sessionId)) return null;
        return Path.Combine(FolderOf(projectId!), Guid.Parse(sessionId!).ToString("D") + ".json");
    }

    private static ConversationDto Snapshot(Conversation convo)
    {
        List<TurnDto> turns = [];
        foreach (Turn turn in convo.Turns)
        {
            List<TurnEventDto> events = [];
            foreach (TurnEvent ev in turn.Events)
                events.Add(new TurnEventDto(ev.Kind, ev.Text, ev.At, ev.Args, ev.Result, ev.Id, ev.Failed, ev.Done));
            turns.Add(new TurnDto(turn.Prompt, turn.StartedAt, turn.CompletedAt, events, turn.Usage, turn.Attachments));
        }

        // The lifecycle lines (sign-in notes and the like) are about that moment, not the conversation.
        return new ConversationDto(convo.AgentSessionId.ToString("D"), convo.AgentName, convo.DocTitle, convo.StartedAt, [], turns)
        {
            Title = convo.Title,
        };
    }
}
