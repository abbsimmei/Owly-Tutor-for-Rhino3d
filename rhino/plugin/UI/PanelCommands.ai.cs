using System.Text.Json.Serialization;

namespace Rhino.AI.UI;

[JsonDerivedType(typeof(PromptCommand), "prompt")]
[JsonDerivedType(typeof(OpenImageCommand), "image.open")]
[JsonDerivedType(typeof(SaveImageCommand), "image.save")]
[JsonDerivedType(typeof(CopyCommandCommand), "command.copy")]
[JsonDerivedType(typeof(FindToolbarCommand), "toolbar.find")]
[JsonDerivedType(typeof(ChooseProjectCommand), "project.choose")]
[JsonDerivedType(typeof(CreateProjectCommand), "project.create")]
[JsonDerivedType(typeof(RenameProjectCommand), "project.rename")]
[JsonDerivedType(typeof(DeleteProjectCommand), "project.delete")]
[JsonDerivedType(typeof(MoveProjectCommand), "project.move")]
internal abstract partial record PanelCommand { }

internal sealed record PromptCommand(PromptRequest Request) : PanelCommand;

internal sealed record OpenImageCommand(string Id) : PanelCommand;

internal sealed record SaveImageCommand(string Id) : PanelCommand;

// Plan 014 / ADR-0007: copy a Rhino command name and focus Rhino. Never typed into the command line.
internal sealed record CopyCommandCommand(string Name) : PanelCommand;

// Plan 015 / ADR-0007: where is this command on the toolbars? Read-only.
internal sealed record FindToolbarCommand(string Name, string TurnId) : PanelCommand;

// Plan 025: projects. Choose makes a project current for this document (and remembers it for a file
// Owly didn't know yet); Create does the same for a new project; Move re-files this document.
internal sealed record ChooseProjectCommand(string Id) : PanelCommand;
internal sealed record CreateProjectCommand(string Name) : PanelCommand;
internal sealed record RenameProjectCommand(string Id, string Name) : PanelCommand;
internal sealed record DeleteProjectCommand(string Id) : PanelCommand;
internal sealed record MoveProjectCommand(string Id) : PanelCommand;

internal sealed record PromptRequest(string Text, IReadOnlyList<PanelAttachment> Attachments);
