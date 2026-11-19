using System.Text.Json.Serialization;

namespace Rhino.AI.UI;

[JsonDerivedType(typeof(PromptCommand), "prompt")]
[JsonDerivedType(typeof(OpenImageCommand), "image.open")]
[JsonDerivedType(typeof(SaveImageCommand), "image.save")]
[JsonDerivedType(typeof(CopyCommandCommand), "command.copy")]
internal abstract partial record PanelCommand { }

internal sealed record PromptCommand(PromptRequest Request) : PanelCommand;

internal sealed record OpenImageCommand(string Id) : PanelCommand;

internal sealed record SaveImageCommand(string Id) : PanelCommand;

// Plan 014 / ADR-0007: copy a Rhino command name and focus Rhino. Never typed into the command line.
internal sealed record CopyCommandCommand(string Name) : PanelCommand;

internal sealed record PromptRequest(string Text, IReadOnlyList<PanelAttachment> Attachments);
