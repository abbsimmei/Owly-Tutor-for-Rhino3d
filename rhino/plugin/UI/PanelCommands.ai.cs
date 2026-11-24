using System.Text.Json.Serialization;

namespace Rhino.AI.UI;

[JsonDerivedType(typeof(PromptCommand), "prompt")]
[JsonDerivedType(typeof(OpenImageCommand), "image.open")]
[JsonDerivedType(typeof(SaveImageCommand), "image.save")]
[JsonDerivedType(typeof(CopyCommandCommand), "command.copy")]
[JsonDerivedType(typeof(FindToolbarCommand), "toolbar.find")]
[JsonDerivedType(typeof(ToolbarIconCommand), "toolbar.icon")]
[JsonDerivedType(typeof(RunCommandCommand), "command.run")]
[JsonDerivedType(typeof(CheckCommandsCommand), "command.check")]
[JsonDerivedType(typeof(CommandOptionsCommand), "command.options")]
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

// Plan 021: which of these commands have a toolbar button image? Read-only. The answers are toolbar.icon.found.
internal sealed record ToolbarIconCommand(IReadOnlyList<string> Names) : PanelCommand;

// Plan 022 / ADR-0008: the student clicked Run on a command card. Starts the command in Rhino (RunScript, posted),
// after the host re-checks it. Only a click sends this; the AI cannot.
// Plan 023: Options is only what the student set (option name to value). The host rebuilds the macro from its own
// cached option data with RunMacro, so a name or value that is not in that data is dropped.
internal sealed record RunCommandCommand(string Name, IReadOnlyDictionary<string, string>? Options = null) : PanelCommand;

// Plan 022 / ADR-0008: may these commands get a Run button? Read-only. The answers are command.checked.
internal sealed record CheckCommandsCommand(IReadOnlyList<string> Names) : PanelCommand;

// Plan 023: what options do these commands have, from McNeel's help page? Read-only. The answers are command.options.found.
internal sealed record CommandOptionsCommand(IReadOnlyList<string> Names) : PanelCommand;

// Plan 025: projects. Choose makes a project current for this document (and remembers it for a file
// Owly didn't know yet); Create does the same for a new project; Move re-files this document.
internal sealed record ChooseProjectCommand(string Id) : PanelCommand;
internal sealed record CreateProjectCommand(string Name) : PanelCommand;
internal sealed record RenameProjectCommand(string Id, string Name) : PanelCommand;
internal sealed record DeleteProjectCommand(string Id) : PanelCommand;
internal sealed record MoveProjectCommand(string Id) : PanelCommand;

internal sealed record PromptRequest(string Text, IReadOnlyList<PanelAttachment> Attachments);
