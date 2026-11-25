using System.Text;

namespace Rhino.AI;

// Stream chunk kinds; SessionStarted lives at conversation level, not inside a turn.
internal enum TurnEventKind
{
    AssistantText,
    ToolUse,
    Result,
    SessionStarted,
}

// Args/Result are empty for kinds that don't carry them; ToolUse holds the tool's input JSON in
// Args and (once the matching tool_result arrives) its output in Result so a chip can expand both.
// Id is the tool call id on a ToolUse event so its later result can be matched back to it; empty
// for every other kind.
// Done is the terminal update arriving, not the tool having produced output: some succeed silently.
internal sealed record TurnEvent(
    TurnEventKind Kind,
    string Text,
    DateTimeOffset At,
    string Args = "",
    string Result = "",
    string Id = "",
    bool Failed = false,
    bool Done = false);

// Mutated only while it is the current turn; Complete() freezes it permanently.
internal sealed class Turn
{
    private object Sync { get; }
    private List<TurnEvent> EventList { get; } = new();

    internal Turn(string prompt, IReadOnlyList<AttachmentInfo> attachments, object sync, DateTimeOffset? startedAt = null)
    {
        Prompt = prompt;
        Attachments = attachments;
        StartedAt = startedAt ?? DateTimeOffset.UtcNow;
        Sync = sync;
    }

    public string Prompt { get; }
    public IReadOnlyList<AttachmentInfo> Attachments { get; }
    public DateTimeOffset StartedAt { get; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public bool Completed { get { lock (Sync) return CompletedAt.HasValue; } }

    // Token/cost accounting from the turn's terminal event; TokenUsage.Empty until the agent
    // reports it (and stays Empty for agents that never do).
    public TokenUsage Usage { get { lock (Sync) return UsageValue; } }
    private TokenUsage UsageValue { get; set; } = TokenUsage.Empty;

    internal void SetUsage(TokenUsage usage) { lock (Sync) UsageValue = usage; }

    public IReadOnlyList<TurnEvent> Events { get { lock (Sync) return EventList.ToArray(); } }

    internal void Add(TurnEvent ev) { lock (Sync) EventList.Add(ev); }
    internal void Complete(DateTimeOffset? at = null) { lock (Sync) CompletedAt ??= at ?? DateTimeOffset.UtcNow; }

    // Fold a tool's output into its originating ToolUse event (matched by id, most-recent first) so
    // it surfaces in that chip's expander rather than as a stray bubble. A missing id is dropped,
    // not turned into a new event.
    internal void SetToolResult(string id, string result, bool failed)
    {
        if (id.Length == 0)
            return;
        lock (Sync)
        {
            for (int i = EventList.Count - 1; i >= 0; i--)
            {
                TurnEvent ev = EventList[i];
                if (ev.Kind == TurnEventKind.ToolUse && ev.Id == id)
                {
                    EventList[i] = ev with { Result = result, Failed = failed, Done = true };
                    return;
                }
            }
        }
    }
}

// One lock guards the whole graph (shared with each Turn): reader thread writes, PromptAsync
// prompts, UI reads. Lifecycle events sit outside turns: a "session started" can arrive
// before the first turn exists.
internal sealed class Conversation
{
    private object Sync { get; } = new();
    private List<Turn> TurnList { get; } = new();
    private List<TurnEvent> LifecycleList { get; } = new();
    private Turn? Current { get; set; }

    // Transient UI state, not transcript history: the panel renders these inline while posed
    // ask_user questions are unanswered. Deliberately kept out of Render() so a
    // half-asked question never appears in the transcript. Appended by the ask_user tool body; cleared by the
    // panel once the answer prompt is dispatched. A list because one call can pose several questions
    // and a later call adds to the set rather than replacing it.
    private List<PendingQuestion> PendingQuestionList { get; } = new();

    public Conversation(Guid agentSessionId, string agentName, string docTitle)
    {
        AgentSessionId = agentSessionId;
        AgentName = agentName;
        DocTitle = docTitle;
        StartedAt = DateTimeOffset.UtcNow;
    }

    // Rebuild a live conversation from a saved one (plan 026), so a reopened conversation shows its
    // turns and carries the saved session id (the CLI's --resume token). The original StartedAt is
    // kept, and the restored turns are marked complete. A malformed SessionId degrades to a fresh
    // GUID, so a corrupt file can never throw here. SavedAs is the file it was read from, so the next
    // save replaces that file instead of leaving a second one.
    public static Conversation Restore(ConversationDto dto)
    {
        bool validId = Guid.TryParse(dto.SessionId, out Guid parsed);
        Conversation convo = new(validId ? parsed : Guid.NewGuid(), dto.AgentName, dto.DocTitle)
        {
            StartedAt = dto.StartedAt,
            ProjectId = string.IsNullOrEmpty(dto.ProjectId) ? null : dto.ProjectId,
            Title = dto.Title ?? string.Empty,
        };
        if (validId)
            convo.SavedAs = (parsed, string.IsNullOrEmpty(dto.ProjectId) ? ProjectRegistry.UnsortedId : dto.ProjectId);

        foreach (TurnEventDto ev in dto.Lifecycle ?? [])
            convo.LifecycleList.Add(new TurnEvent(ev.Kind, ev.Text, ev.At, ev.Args, ev.Result, ev.Id, ev.Failed));

        foreach (TurnDto turnDto in dto.Turns ?? [])
        {
            Turn turn = new(turnDto.Prompt, turnDto.Attachments ?? [], convo.Sync, turnDto.StartedAt);
            foreach (TurnEventDto ev in turnDto.Events ?? [])
                turn.Add(new TurnEvent(ev.Kind, ev.Text, ev.At, ev.Args, ev.Result, ev.Id, ev.Failed, ev.Done));
            turn.SetUsage(turnDto.Usage);
            // Stamping now would date every restored turn to the restore, which is what bounds the search for images the agent wrote during it.
            turn.Complete(turnDto.CompletedAt ?? turnDto.StartedAt);
            convo.TurnList.Add(turn);
        }
        return convo;
    }

    // The CLI's own session id: passed as --session-id on the first spawn and as --resume when the CLI is
    // restarted after Stop or reopened from a saved conversation. Mutable because a rejected resume
    // makes the agent open a fresh session, and the saved id has to follow.
    public Guid AgentSessionId { get; private set; }
    public string AgentName { get; }
    public string DocTitle { get; }
    public DateTimeOffset StartedAt { get; private init; }

    // Plan 026: the project this conversation is saved under; null while the project is unknown, which
    // is saved as Unsorted. The panel keeps it equal to the panel's current project.
    public string? ProjectId { get; set; }

    // The first line of the first question, at most 80 characters. Set by BeginTurn while empty.
    public string Title { get; set; } = string.Empty;

    // Used for the title of a conversation that began with only attachments. The host sets it localised.
    public string TitleFallback { get; set; } = "Conversation";

    // Once set, every save is a no-op: the student deleted this conversation, its project or all of Owly's memory.
    public bool Discarded { get; set; }

    // Where ConversationStore last wrote this conversation. A save under a different session id or
    // project removes this old file, so a move or a rotated id never leaves a duplicate behind.
    public (Guid SessionId, string ProjectId)? SavedAs { get; set; }

    public const int MaxTitleLength = 80;

    // The first non-blank line, trimmed and cut at 80 characters on a character boundary.
    public static string TitleFrom(string prompt, string fallback)
    {
        foreach (string raw in (prompt ?? string.Empty).Split('\n'))
        {
            string line = raw.Trim();
            if (line.Length == 0) continue;
            if (line.Length > MaxTitleLength)
            {
                int cut = MaxTitleLength;
                if (char.IsHighSurrogate(line[cut - 1])) cut--;
                line = line.Substring(0, cut).TrimEnd();
            }
            return line;
        }
        return fallback;
    }

    // Raised after every mutation so a panel can re-render. Fired OUTSIDE the lock: handlers
    // marshal to the UI thread and read the graph, which would deadlock if we still held Sync.
    public event Action? Changed;

    // Live references, not a snapshot: the current turn may still be appending.
    public IReadOnlyList<Turn> Turns { get { lock (Sync) return TurnList.ToArray(); } }
    public IReadOnlyList<TurnEvent> Lifecycle { get { lock (Sync) return LifecycleList.ToArray(); } }

    public Turn BeginTurn(string prompt, IReadOnlyList<AttachmentInfo>? attachments = null)
    {
        Turn turn;
        lock (Sync)
        {
            turn = new(prompt, attachments ?? [], Sync);
            TurnList.Add(turn);
            if (Title.Length == 0)
                Title = TitleFrom(prompt, TitleFallback);
            Current = turn;
        }
        Changed?.Invoke();
        return turn;
    }

    public void Record(TurnEventKind kind, string text, string args = "", string result = "", string id = "")
    {
        lock (Sync)
            Current?.Add(new TurnEvent(kind, text, DateTimeOffset.UtcNow, args, result, id));
        Changed?.Invoke();
    }

    // Attach a completed tool's output to its originating ToolUse event (see Turn.SetToolResult).
    public void CompleteToolCall(string id, string result, bool failed = false)
    {
        lock (Sync)
            Current?.SetToolResult(id, result, failed);
        Changed?.Invoke();
    }

    // Record the current turn's token/cost accounting (from its terminal event). A no-op once the
    // terminal event already cleared Current (a late, mis-ordered usage drop is dropped, not stored
    // on the wrong turn). Empty usage is ignored so a turn the agent never accounted for stays Empty.
    public void RecordUsage(TokenUsage usage)
    {
        if (usage.IsEmpty)
            return;
        lock (Sync)
            Current?.SetUsage(usage);
        Changed?.Invoke();
    }

    // Sum of every turn's usage: the session total shown in the header. Costs add only when reported
    // (see TokenUsage.operator+), so a tokens-only session yields a null session cost.
    public TokenUsage SessionUsage
    {
        get
        {
            TokenUsage total = TokenUsage.Empty;
            lock (Sync)
                foreach (Turn turn in TurnList)
                    total += turn.Usage;
            return total;
        }
    }

    // Called when the agent had to open a session under a different id than the one it was given.
    internal void AdoptSessionId(Guid sessionId)
    {
        lock (Sync)
            AgentSessionId = sessionId;
    }

    public void NoteSessionStarted()
    {
        lock (Sync)
            LifecycleList.Add(new TurnEvent(TurnEventKind.SessionStarted, "session started", DateTimeOffset.UtcNow));
        Changed?.Invoke();
    }

    // A free-form lifecycle line, rendered like a session marker. Used to surface a fail-soft note
    // (e.g. the CLI exited and Owly asks the student to sign in).
    public void NoteSystem(string text)
    {
        lock (Sync)
            LifecycleList.Add(new TurnEvent(TurnEventKind.SessionStarted, text, DateTimeOffset.UtcNow));
        Changed?.Invoke();
    }

    public bool TryGetPendingQuestions(out IReadOnlyList<PendingQuestion> questions)
    {
        lock (Sync)
        {
            questions = PendingQuestionList.ToArray();
            return questions.Count > 0;
        }
    }

    // Appends: a second ask_user must not evict an earlier unanswered question.
    public void AddPendingQuestions(IReadOnlyList<PendingQuestion> questions)
    {
        lock (Sync)
            foreach (PendingQuestion question in questions)
                PendingQuestionList.Add(question);
        Changed?.Invoke();
    }

    // Removes exactly these instances, so a finished question clearing late can't wipe a newer one
    // posed after it (e.g. a superseding ask_user posed before the old answer landed).
    public void ClearPendingQuestions(IReadOnlyList<PendingQuestion> questions)
    {
        lock (Sync)
            foreach (PendingQuestion question in questions)
                for (int i = PendingQuestionList.Count - 1; i >= 0; i--)
                    if (ReferenceEquals(PendingQuestionList[i], question))
                        PendingQuestionList.RemoveAt(i);
        Changed?.Invoke();
    }

    public void CompleteTurn()
    {
        lock (Sync)
        {
            Current?.Complete();
            Current = null;   // stray late events after a terminal event are dropped, not mis-filed
        }
        Changed?.Invoke();
    }

    // Flatten to plain text; assistant chunks appended raw to rejoin the stream.
    public string Render()
    {
        StringBuilder sb = new();
        lock (Sync)
        {
            foreach (TurnEvent ev in LifecycleList)
                sb.AppendLine($"[{ev.Text}]");

            foreach (Turn turn in TurnList)
            {
                sb.AppendLine();
                sb.AppendLine($"> {turn.Prompt}");
                foreach (TurnEvent ev in turn.Events)
                {
                    switch (ev.Kind)
                    {
                        case TurnEventKind.ToolUse:
                            sb.AppendLine($"  ⚙ {ev.Text}");
                            break;
                        case TurnEventKind.Result:
                            sb.AppendLine();
                            sb.AppendLine(ev.Text);
                            break;
                        default:
                            sb.Append(ev.Text);
                            break;
                    }
                }
                sb.AppendLine();
            }
        }
        return sb.ToString();
    }
}
