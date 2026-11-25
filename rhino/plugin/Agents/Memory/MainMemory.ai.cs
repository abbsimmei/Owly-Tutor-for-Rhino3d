namespace Rhino.AI;

internal sealed record MainMemoryItem(string Text, string Updated);

// The file shape: profile.json at the Owly root. Settable, with a parameterless constructor, because
// OwlyStore reads a file into a new one (a missing or corrupt file reads as off and empty).
internal sealed class MainMemoryFile
{
    public int Version { get; set; } = 1;
    public bool Enabled { get; set; }
    public List<MainMemoryItem> Items { get; set; } = [];
}

// Plan 028 / ADR-0009: main memory, a short list of what the student finds hard, shared by every project.
// Opt-in and off by default. The switch lives in the same file as the list, because the memory is shared
// by Rhino 8 and 9 while AISettings is per Rhino version. Turning it off clears the list in the same write,
// and Merge does nothing while it is off. Nothing here throws into Rhino. Rhino-free, so Server.Tests links it.
internal sealed class MainMemory
{
    public const string FileName = "profile.json";

    private static readonly object Gate = new();

    private readonly OwlyStore _store;

    public MainMemory(OwlyStore store) => _store = store;

    public bool Enabled
    {
        get
        {
            lock (Gate)
                return _store.ReadJson<MainMemoryFile>(FileName).Enabled;
        }
    }

    // Turning it off clears the items in the same write. True when the file holds the wanted state.
    public bool SetEnabled(bool enabled)
    {
        lock (Gate)
        {
            return _store.Update<MainMemoryFile>(FileName, file =>
            {
                file.Version = 1;
                file.Enabled = enabled;
                if (!enabled) file.Items = [];
                return true;
            });
        }
    }

    // Newest first. Empty while the memory is off.
    public IReadOnlyList<string> Items()
    {
        lock (Gate)
        {
            MainMemoryFile file = _store.ReadJson<MainMemoryFile>(FileName);
            return file.Enabled ? Valid(file.Items).Select(item => item.Text).ToList() : [];
        }
    }

    // Puts the fresh items in front of the stored ones (see NotesPrompt.MergeDifficulties). It re-reads the
    // file under the store lock, so a switch turned off meanwhile wins: then nothing is written and the
    // result is false. True when the list changed.
    public bool Merge(IReadOnlyList<string> fresh)
    {
        if (fresh.Count == 0) return false;
        lock (Gate)
        {
            return _store.Update<MainMemoryFile>(FileName, file =>
            {
                if (!file.Enabled) return false;
                IReadOnlyList<string> merged = NotesPrompt.MergeDifficulties(Valid(file.Items).Select(item => item.Text).ToList(), fresh);
                string updated = DateTimeOffset.UtcNow.ToString("O");
                file.Version = 1;
                file.Items = merged.Select(text => new MainMemoryItem(text, updated)).ToList();
                return true;
            });
        }
    }

    // Empties the list; the switch stays as it is.
    public bool Clear()
    {
        lock (Gate)
        {
            return _store.Update<MainMemoryFile>(FileName, file =>
            {
                if (file.Items.Count == 0) return false;
                file.Items = [];
                return true;
            });
        }
    }

    private static List<MainMemoryItem> Valid(IEnumerable<MainMemoryItem>? items) =>
        (items ?? []).Where(item => item is { Text.Length: > 0 }).ToList();
}
