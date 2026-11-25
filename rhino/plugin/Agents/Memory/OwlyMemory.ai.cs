using System.IO;
using Rhino.Runtime;
using Rhino.UI;

namespace Rhino.AI;

// Plan 025: the one store and project registry this Rhino uses, over %AppData%\Owly. Unlike
// OwlyStore and ProjectRegistry this is the Rhino side (logging, localised names), so Server.Tests
// does not link it. Changed fires on the UI thread after anything here changes the projects, so
// every open panel can show the new state.
internal static class OwlyMemory
{
    public static OwlyStore Store { get; } =
        new(OwlyStore.DefaultRoot, message => HostUtils.LogDebugEvent(message + "\n"));

    public static ProjectRegistry Projects { get; } =
        new(Store, Localization.LocalizeString("Unsorted", 360));

    public static ConversationStore Conversations { get; } = new(Store);

    // Plan 027: the notes Owly keeps per project (Haiku writes them when a conversation ends).
    public static ProjectNotes Notes { get; } = new(Store);

    // Plan 028: main memory (opt-in; what the student finds hard, across projects).
    public static MainMemory Main { get; } = new(Store);

    public static event Action? Changed;

    public static void NotifyChanged() => Changed?.Invoke();

    // Plan 026: a conversation was saved, renamed, deleted or moved. Raised on the UI thread, so every
    // open panel can send its strip again.
    public static event Action? ConversationsChanged;

    public static void NotifyConversationsChanged() =>
        RhinoApp.InvokeOnUiThread(new Action(() => ConversationsChanged?.Invoke()));

    // Plan 027: notes were written. Raised on the UI thread, so every open panel can send its notes again.
    public static event Action? NotesChanged;

    public static void NotifyNotesChanged() =>
        RhinoApp.InvokeOnUiThread(new Action(() => NotesChanged?.Invoke()));

    // Plan 028: main memory gained items. Raised on the UI thread, so an open settings page can show them.
    public static event Action? MainChanged;

    public static void NotifyMainChanged() =>
        RhinoApp.InvokeOnUiThread(new Action(() => MainChanged?.Invoke()));

    // "Delete all Owly memory" ran. Every panel drops its open conversation without saving it again.
    public static event Action? Cleared;

    // The Claude Code CLI always starts here, whatever document is open, because Claude Code finds a
    // session only from the folder it was started in. Created when missing; if that fails, the CLI
    // gets Rhino's temp folder, which is fine for everything but resuming.
    public static string CliDirectory
    {
        get
        {
            string path = OwlyStore.CliDirectory(Store.Root);
            try
            {
                Directory.CreateDirectory(path);
                return path;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
            {
                HostUtils.LogDebugEvent($"[owly] could not create {path}: {ex.Message}\n");
                return Path.GetTempPath();
            }
        }
    }

    // "Delete all Owly memory" in settings.
    public static bool DeleteAll()
    {
        // First, so no open conversation can be saved again while (or after) the folder is emptied.
        Cleared?.Invoke();
        bool deleted = Store.DeleteAll();
        NotifyChanged();
        return deleted;
    }
}
