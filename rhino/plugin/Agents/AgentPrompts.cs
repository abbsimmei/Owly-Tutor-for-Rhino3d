namespace Rhino.AI;

/// <summary>
/// Default Prompts for the AI Agent. These cannot be currentyl changed.
/// </summary>
internal static class AgentPrompts
{
    private const string ServerName = "rhino";

    public static string AskUserSteer =>
        "To ask the user a question or have them choose between options, always call the "
        + $"mcp__{ServerName}__ask_user tool. On success, STOP and end your turn. On failure, follow its guidance and retry; do not wait for answers. It takes a "
        + "LIST of questions, so put every question you need answered into that ONE call rather than "
        + "asking them one turn at a time; they are shown together and answered together. The tool "
        + "does NOT return the answers: the user's reply arrives as their next message, and you "
        + "continue from there. Never use the built-in AskUserQuestion tool: it cannot be displayed "
        + "in this environment and will be canceled.";

    // Grounding is pull-only: no document state is injected automatically, so the agent must
    // read current state rather than assuming what is selected or open.
    public static string GroundingSteer =>
        "You cannot change the user's model: you have no tools that create, modify, select, or delete anything, "
        + "and you must never claim to have done so. No document or selection state is injected automatically. "
        + "When advice depends on what the user has, read it first with get_context (selection, active "
        + "viewport, document summary) or get_selection / list_objects for detail. Never assume what exists.";

    // The always-on steers plus this agent's own prompt; the steers are never dropped.
    public static string Compose(string systemPrompt)
    {
        string steers = AskUserSteer + "\n\n" + GroundingSteer;
        return systemPrompt.Length > 0 ? steers + "\n\n" + systemPrompt : steers;
    }
}
