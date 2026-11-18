using Rhino.Commands;

namespace Rhino.AI.Tools;

[McpServerToolType]
internal static class GetCommandsTool
{
    private const int MaxResults = 200;

    [McpServerTool("get_commands", "List Rhino Commands", true, false)]
    [Description("List Rhino command names (English) from all registered plugins, including ones not yet loaded. Use it to verify a command exists before recommending it to the user; you cannot run commands. Use filter to narrow the list.")]
    public static IToolResult GetCommands(
        RhinoDoc _,
        [Description("Substring filter (case-insensitive). Strongly recommended — unfiltered results can exceed 1000 commands.")] string? filter = null)
    {
        string? trimmed = filter?.TrimStart('_', '-');
        List<string> commands = FindMatching(trimmed);

        if (commands.Count == 0)
            return Failure(ToolError.RH_Command_NotFound, string.IsNullOrEmpty(filter)
                ? "No commands found."
                : $"No commands found matching '{filter}'.");

        if (commands.Count <= MaxResults)
            return Success(ContentBlock.CreateText($"# {commands.Count} commands\n" + string.Join("\n", commands)));

        string head = string.Join("\n", commands.Take(MaxResults));
        return Success(ContentBlock.CreateText($"# {commands.Count} commands (showing first {MaxResults}; refine filter to narrow)\n{head}"));
    }

    private static List<string> FindMatching(string? filter)
    {
        IEnumerable<string> commands = Command.GetCommandNames(true, false).OrderBy(n => n, StringComparer.OrdinalIgnoreCase);
        if (String.IsNullOrEmpty(filter)) return commands.ToList();

        return commands.Where(n => n.Contains(filter, StringComparison.OrdinalIgnoreCase)).ToList();
    }

}
