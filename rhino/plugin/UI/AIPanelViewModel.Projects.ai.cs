using Eto.Forms;
using Rhino.UI;

namespace Rhino.AI.UI;

// Plan 025 / ADR-0009: this document's project. The panel is per document, so the state lives here.
// Nothing is written into the document: the file's path is what the registry remembers.
internal partial class AIPanelViewModel
{
    private static ProjectRegistry Projects => OwlyMemory.Projects;

    // The current project for this session: resolved from the file's path, or chosen by the student.
    // Null while Owly doesn't know which project the file belongs to (pending).
    private string? ProjectId { get; set; }

    // The path ProjectId was resolved for, so a Save As to a new name is noticed.
    private string? ResolvedPath { get; set; }

    // An Untitled document's choice, filed under its path when the document is first saved.
    private string? UntitledChoice { get; set; }

    private string? DocumentPath => Document is { Path: { Length: > 0 } path } ? path : null;

    private void SubscribeProjects()
    {
        RhinoDoc.EndSaveDocument += OnDocumentSaved;
        OwlyMemory.Changed += SendProject;
    }

    private void UnsubscribeProjects()
    {
        RhinoDoc.EndSaveDocument -= OnDocumentSaved;
        OwlyMemory.Changed -= SendProject;
    }

    private void SendProject()
    {
        IReadOnlyList<OwlyProject> all = Projects.List();
        string? path = DocumentPath;

        // Anything deleted since (here, in settings or in the other Rhino) is forgotten.
        string? deletedProject = null;
        if (!all.Any(p => p.Id == ProjectId))
        {
            deletedProject = ProjectId;
            ProjectId = null;
        }
        if (!all.Any(p => p.Id == UntitledChoice)) UntitledChoice = null;

        if (ProjectId is null || !SamePath(path, ResolvedPath))
        {
            ProjectId = path is null ? UntitledChoice : Projects.Resolve(path);
            ResolvedPath = path;
        }

        // Plan 026: the open conversation of a deleted project is deleted with it, so it is never saved again.
        if (deletedProject is not null && ActiveConversation?.ProjectId == deletedProject)
            DiscardOpenConversation();
        SyncConversationProject();

        OwlyProject? current = all.FirstOrDefault(p => p.Id == ProjectId);
        bool pending = current is null;
        string? suggestion = pending && path is not null ? Projects.SuggestFor(path) : null;

        Bridge.Post(new ProjectEvent(
            current?.Id,
            current?.Name,
            pending,
            suggestion,
            all.Select(p => new PanelProject(p.Id, p.Name, p.Builtin)).ToList()));
        SendConversations();
    }

    // Switch project, or the project card's answer. A file Owly didn't know yet is filed under the
    // choice; a file that already has a project keeps it, and the switch lasts for this session.
    private bool ChooseProject(string id)
    {
        if (Projects.Find(id) is null) return false;

        if (DocumentPath is { } path)
        {
            if (Projects.Resolve(path) is null)
                Projects.Assign(path, id);
        }
        else
        {
            UntitledChoice = id;
        }

        ProjectId = id;
        ResolvedPath = DocumentPath;
        OwlyMemory.NotifyChanged();
        return true;
    }

    private bool CreateProject(string name)
    {
        if (ProjectRegistry.CleanName(name) is null)
        {
            Bridge.Post(new NoticeEvent("error", InvalidNameText));
            return false;
        }

        if (Projects.Create(name) is not { } project)
        {
            Bridge.Post(new NoticeEvent("error", Localization.LocalizeString("Owly couldn't save the project. Try again.", 362)));
            return false;
        }

        return ChooseProject(project.Id);
    }

    private bool RenameProject(string id, string name)
    {
        if (ProjectRegistry.CleanName(name) is null)
        {
            Bridge.Post(new NoticeEvent("error", InvalidNameText));
            return false;
        }

        if (!Projects.Rename(id, name)) return false;
        OwlyMemory.NotifyChanged();
        return true;
    }

    private bool DeleteProject(string id)
    {
        if (!Projects.Delete(id)) return false;

        // Notified first: every panel whose open conversation belongs to this project drops it (SendProject),
        // so nothing can save it into the folder that is removed next.
        OwlyMemory.NotifyChanged();
        Saved.DeleteProject(id);
        return true;
    }

    // "Move this file to another project": the file is filed under the new project.
    private bool MoveProject(string id)
    {
        if (Projects.Find(id) is null) return false;

        if (DocumentPath is { } path)
            Projects.Assign(path, id);
        else
            UntitledChoice = id;

        ProjectId = id;
        ResolvedPath = DocumentPath;
        OwlyMemory.NotifyChanged();
        return true;
    }

    private void OnDocumentSaved(object? sender, DocumentSaveEventArgs e)
    {
        if (e.DocumentSerialNumber != DocumentSerialNumber || e.ExportSelected) return;

        // Queued, so the document's new path is read once the save has finished.
        Application.Instance.AsyncInvoke(FileSaved);
    }

    private void FileSaved()
    {
        if (DocumentPath is not { } path || SamePath(path, ResolvedPath)) return;

        if (UntitledChoice is { } choice && Projects.Resolve(path) is null)
            Projects.Assign(path, choice);
        UntitledChoice = null;

        // A Save As to a new name is a file Owly doesn't know yet, unless the Untitled choice just filed it.
        ProjectId = Projects.Resolve(path);
        ResolvedPath = path;
        SendProject();
    }

    private static string InvalidNameText =>
        string.Format(Localization.LocalizeString("A project name needs 1 to {0} characters and no line breaks.", 361), ProjectRegistry.MaxNameLength);

    private static bool SamePath(string? a, string? b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
}
