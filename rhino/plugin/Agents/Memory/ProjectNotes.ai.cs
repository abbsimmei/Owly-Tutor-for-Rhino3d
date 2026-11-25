using System.IO;

namespace Rhino.AI;

internal sealed record ProjectNote(string Id, string Text, string Created, string FromSession);

// The file shape: projects/<projectId>/notes.json. Settable, with a parameterless constructor, because
// OwlyStore reads a file into a new one (a missing or corrupt file reads as an empty list).
internal sealed class ProjectNotesFile
{
    public int Version { get; set; } = 1;
    public List<ProjectNote> Items { get; set; } = [];
}

// Plan 027 / ADR-0009: the short notes Owly keeps per project (newest first, at most 15), written when a
// conversation ends and read back when a new one starts. Ids become folder names or are compared to
// stored ids, so they are checked first. Nothing here throws into Rhino. Rhino-free, so Server.Tests links it.
internal sealed class ProjectNotes
{
    public const int MaxNotes = 15;

    private static readonly object Gate = new();

    private readonly OwlyStore _store;

    public ProjectNotes(OwlyStore store) => _store = store;

    // Newest first.
    public IReadOnlyList<ProjectNote> List(string? projectId)
    {
        if (NameOf(projectId) is not { } name) return [];
        lock (Gate)
            return Valid(_store.ReadJson<ProjectNotesFile>(name).Items);
    }

    // Prepends the texts (in the order given) and keeps the newest 15. True when the notes are stored.
    public bool Add(string? projectId, string? fromSession, IReadOnlyList<string> texts)
    {
        if (NameOf(projectId) is not { } name || !ConversationStore.IsSessionId(fromSession)) return false;
        string session = Guid.Parse(fromSession!).ToString("D");
        string created = DateTimeOffset.UtcNow.ToString("O");
        List<ProjectNote> fresh = texts
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Select(t => new ProjectNote(Guid.NewGuid().ToString("N"), t, created, session))
            .ToList();
        if (fresh.Count == 0) return true;

        lock (Gate)
        {
            return _store.Update<ProjectNotesFile>(name, file =>
            {
                file.Version = 1;
                file.Items = [.. fresh, .. Valid(file.Items)];
                if (file.Items.Count > MaxNotes) file.Items = file.Items.Take(MaxNotes).ToList();
                return true;
            });
        }
    }

    public bool Delete(string? projectId, string? noteId)
    {
        if (NameOf(projectId) is not { } name || !IsNoteId(noteId)) return false;
        lock (Gate)
        {
            return _store.Update<ProjectNotesFile>(name, file =>
            {
                int before = file.Items.Count;
                file.Items = file.Items.Where(n => !string.Equals(n.Id, noteId, StringComparison.OrdinalIgnoreCase)).ToList();
                return file.Items.Count != before;
            });
        }
    }

    // A GUID written with Guid.ToString("N"): 32 hex characters.
    public static bool IsNoteId(string? id) =>
        id is { Length: 32 } && id.All(c => c is (>= '0' and <= '9') or (>= 'a' and <= 'f') or (>= 'A' and <= 'F'));

    private static List<ProjectNote> Valid(IEnumerable<ProjectNote>? items) =>
        (items ?? []).Where(n => n is { Text.Length: > 0, Id: not null }).ToList();

    private static string? NameOf(string? projectId) =>
        ConversationStore.IsProjectId(projectId) ? Path.Combine("projects", projectId!, "notes.json") : null;
}
