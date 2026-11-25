namespace Rhino.AI;

// Plan 026: the saved shapes of a conversation, written by ConversationStore as JSON files under the
// Owly folder. Dumb and behaviour-free: the mirror of the live Conversation/Turn/TurnEvent graph.
// Enums are stored by name (OwlyStore's serializer options), so reordering TurnEventKind can't
// corrupt saved files.
internal sealed record TurnEventDto(
    TurnEventKind Kind,
    string Text,
    DateTimeOffset At,
    string Args,
    string Result,
    string Id = "",
    bool Failed = false,
    bool Done = false);

internal sealed record TurnDto(
    string Prompt,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt,
    IReadOnlyList<TurnEventDto> Events,
    TokenUsage Usage = default,
    IReadOnlyList<AttachmentInfo>? Attachments = null);

// SessionId is the Claude Code session id (the --resume token). ProjectId is the folder it is saved
// in, "unsorted" when the student never chose a project. Title and ProjectId are settable so a rename
// can change a saved file in place. The parameterless constructor is what OwlyStore reads a file into;
// a file that is missing or corrupt reads as an empty one, which has no SessionId and is skipped.
internal sealed record ConversationDto(
    string SessionId,
    string AgentName,
    string DocTitle,
    DateTimeOffset StartedAt,
    IReadOnlyList<TurnEventDto> Lifecycle,
    IReadOnlyList<TurnDto> Turns)
{
    public ConversationDto() : this(string.Empty, string.Empty, string.Empty, default, [], [])
    {
    }

    public string ProjectId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;

    // Plan 027: project notes bookkeeping, kept on disk only. NotesPending means the conversation ended and
    // its notes were not written yet; NotesThroughTurn is how many turns the notes already cover. A live
    // conversation's save never changes them (ConversationStore.Save keeps what the file has).
    public bool NotesPending { get; set; }
    public int NotesThroughTurn { get; set; }
}
