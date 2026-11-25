namespace Rhino.AI.UI;

// Plan 027 / ADR-0009: the notes of this document's project, which the panel shows (and lets the student
// delete from) in its project menu. The notes belong to the panel's current project, Unsorted included.
internal partial class AIPanelViewModel
{
    private static ProjectNotes SavedNotes => OwlyMemory.Notes;

    private void SendNotes()
    {
        string? project = ProjectId;
        List<PanelNote> items = [];
        if (project is not null)
            items = SavedNotes.List(project).Select(note => new PanelNote(note.Id, note.Text, note.Created)).ToList();

        Bridge.Post(new NotesEvent(project, items));
    }

    // Only a note of the current project, by a well-formed id; anything else does nothing.
    private bool DeleteNote(string id)
    {
        if (ProjectId is not { } project || !ProjectNotes.IsNoteId(id)) return false;
        bool deleted = SavedNotes.Delete(project, id);
        SendNotes();
        return deleted;
    }
}
