using System.Text.Json;
using Rhino.UI;

namespace Rhino.AI;

/// <summary>
/// Makes verbose tool call names into something simpler
/// </summary>
internal static class ToolSummary
{
    /// <summary>
    /// Tools are named mcp__thing__tool which is verbose, this cleans up
    /// </summary>
    public static string RemoveUnderscoreUnderscoreNaming(string toolName)
    {
        const string prefix = "mcp__";
        if (!toolName.StartsWith(prefix, StringComparison.Ordinal))
            return toolName;

        int separator = toolName.IndexOf("__", prefix.Length, StringComparison.Ordinal);
        return separator < 0 ? toolName : toolName.Substring(separator + 2);
    }

    // result is empty while the call is still in flight; show the in-progress verb without a verdict.
    public static string Describe(string rawToolName, string argsJson, string resultJson, bool reportedFailure = false)
    {
        string toolName = RemoveUnderscoreUnderscoreNaming(rawToolName);
        bool failed = reportedFailure || (!string.IsNullOrWhiteSpace(resultJson) && IsFailure(resultJson));

        string phrase = Phrase(toolName, argsJson, resultJson, failed);
        return phrase;
    }

    // Per-tool phrasing for the panel's tool line. Every tool gets a plain sentence and an unknown
    // tool gets a generic one, so the raw tool name never reaches the student.
    private static string Phrase(string toolName, string argsJson, string resultJson, bool failed)
    {
        return toolName switch
        {
            "get_selection" => failed
                ? Localization.LocalizeString("Couldn't check your selection", 260)
                : Localization.LocalizeString("Checked your selection", 181),
            "get_context" => failed
                ? Localization.LocalizeString("Couldn't look at your document", 261)
                : Localization.LocalizeString("Looked at your document", 262),
            "list_objects" => failed
                ? Localization.LocalizeString("Couldn't list objects", 263)
                : Localization.LocalizeString("Listed objects in your model", 183),
            "get_commands" => failed
                ? Localization.LocalizeString("Couldn't look up Rhino commands", 264)
                : Localization.LocalizeString("Looked up Rhino commands", 184),
            "ask_user" => failed
                ? Localization.LocalizeString("Couldn't show the question", 265)
                : Localization.LocalizeString("Asked you a question", 190),

            _ => failed
                ? Localization.LocalizeString("Something went wrong", 176)
                : Localization.LocalizeString("Looked something up", 200),
        };
    }

    // Common shapes for a failed result: an { Ok: false } flag or a non-empty error/Error string. A
    // malformed or non-object payload counts as success here; the verdict only flips on a clear signal.
    internal static bool IsFailure(string resultJson)
    {
        if (Parse(resultJson) is not { } doc)
            return false;
        using (doc)
        {
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                return false;
            JsonElement root = doc.RootElement;

            if (root.TryGetProperty("Ok", out JsonElement ok) && ok.ValueKind == JsonValueKind.False)
                return true;
            if (root.TryGetProperty("ok", out JsonElement okLower) && okLower.ValueKind == JsonValueKind.False)
                return true;
            if (HasNonEmptyString(root, "error") || HasNonEmptyString(root, "Error"))
                return true;
            return false;
        }
    }

    private static bool HasNonEmptyString(JsonElement root, string name) =>
        root.TryGetProperty(name, out JsonElement el) && el.ValueKind == JsonValueKind.String && el.GetString() is { Length: > 0 };

    // null for absent or malformed JSON; callers own disposal of a returned document.
    private static JsonDocument? Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;
        try
        {
            return JsonDocument.Parse(json);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
