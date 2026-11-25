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
        // Cheap exit before the lock: throwaway probe runners are disposed (and saved) on the UI thread.
        if (convo.Discarded || convo.Turns.Count == 0) return false;

        lock (SaveGate)
        {
            if (convo.Discarded) return false;
            ConversationDto dto = Snapshot(convo);
            if (dto.Turns.Count == 0) return false;

            string projectId = convo.ProjectId ?? ProjectRegistry.UnsortedId;
            if (NameOf(projectId, dto.SessionId) is not { } name) return false;

            dto.ProjectId = projectId;

            // The notes bookkeeping lives on disk only (plan 027): keep what the file has, or, when the save
            // moves the conversation, what the file it moves from has.
            string? oldName = null;
            if (convo.SavedAs is { } old && (old.SessionId != convo.AgentSessionId || old.ProjectId != projectId))
                oldName = NameOf(old.ProjectId, old.SessionId.ToString("D"));
            ConversationDto? moved = oldName is null ? null : _store.ReadJson<ConversationDto>(oldName);

            if (!_store.Replace<ConversationDto>(name, existing =>
            {
                ConversationDto source = existing.SessionId.Length == 0 && moved is { SessionId.Length: > 0 } ? moved : existing;
                dto.NotesPending = source.NotesPending;
                dto.NotesThroughTurn = source.NotesThroughTurn;
                return dto;
            })) return false;

            if (oldName is not null)
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

    // Plan 027: project notes bookkeeping. Each method validates its ids and runs under the store's lock,
    // so two Rhinos draining at start can't both summarize the same conversation.
    public bool MarkNotesPending(string? projectId, string? sessionId) =>
        ChangeNotes(projectId, sessionId, dto =>
        {
            dto.NotesPending = true;
            return true;
        });

    // Takes the conversation's notes job: when it is pending, clears the flag and hands the saved conversation
    // back. False when nothing is pending, or the conversation is gone (deleted, its project deleted, Delete all).
    public bool TryClaimNotes(string? projectId, string? sessionId, out ConversationDto dto)
    {
        ConversationDto? claimed = null;
        bool ok = ChangeNotes(projectId, sessionId, saved =>
        {
            if (!saved.NotesPending) return false;
            saved.NotesPending = false;
            claimed = saved;
            return true;
        });
        dto = claimed ?? new ConversationDto();
        return ok && claimed is not null;
    }

    // The notes now cover the first throughTurn turns. Whether more is pending is left as it is.
    public bool FinishNotes(string? projectId, string? sessionId, int throughTurn) =>
        ChangeNotes(projectId, sessionId, dto =>
        {
            dto.NotesThroughTurn = Math.Max(0, throughTurn);
            return true;
        });

    // A claimed job failed for a reason that may pass (no CLI, timeout): it is pending again.
    public bool ReleaseNotes(string? projectId, string? sessionId) => MarkNotesPending(projectId, sessionId);

    // Every saved conversation whose notes are still to be written, across all projects (Unsorted included).
    public IReadOnlyList<(string ProjectId, string SessionId)> ListPending()
    {
        List<(string, string)> pending = [];
        foreach (string folder in _store.ListFolders("projects"))
        {
            string projectId = Path.GetFileName(folder);
            if (!IsProjectId(projectId)) continue;
            foreach (string name in _store.ListJson(FolderOf(projectId)))
            {
                ConversationDto dto = _store.ReadJson<ConversationDto>(name);
                if (dto.NotesPending && Guid.TryParse(dto.SessionId, out Guid id)
                    && string.Equals(Path.GetFileNameWithoutExtension(name), id.ToString("D"), StringComparison.OrdinalIgnoreCase))
                    pending.Add((projectId, id.ToString("D")));
            }
        }
        return pending;
    }

    private bool ChangeNotes(string? projectId, string? sessionId, Func<ConversationDto, bool> change)
    {
        if (NameOf(projectId, sessionId) is not { } name) return false;
        lock (SaveGate)
        {
            return _store.Update<ConversationDto>(name, dto =>
                Guid.TryParse(dto.SessionId, out _) && dto.Turns is { Count: > 0 } && change(dto));
        }
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
