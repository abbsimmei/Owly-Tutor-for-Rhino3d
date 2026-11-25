using System.Diagnostics;

namespace Rhino.AI;

// ADR-0003 / plan 027: the flags that lock the Claude Code CLI down, shared by the chat process
// (ConfigureArguments) and the notes request (ProjectNotesWriter), so the two can't drift apart. Kept in its
// own file, apart from the rest of the parser, so Server.Tests can link it without Rhino.
internal sealed partial class ClaudeStreamJsonParser
{
    // Only the MCP servers named on the command line, no settings files, no built-in tools, no slash commands.
    internal static void AddLockdown(ProcessStartInfo psi)
    {
        psi.AddArgument("--strict-mcp-config");
        psi.AddArgument("--setting-sources");
        psi.AddArgument(string.Empty);
        // No built-in tools: the agent can only use the MCP tools it is given.
        psi.AddArgument("--tools");
        psi.AddArgument(string.Empty);
        psi.AddArgument("--disable-slash-commands");
    }
}
