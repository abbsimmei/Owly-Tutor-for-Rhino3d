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

    // Per-tool phrasing. Returns null to defer to the generic fallback (unknown tool or unreadable
    // payload). A leading failure verb is preferred over a misleading success phrase.
    private static string Phrase(string toolName, string argsJson, string resultJson, bool failed)
    {
        if (failed)
            return string.Format(Localization.LocalizeString("{0} failed", 176), Verb(toolName));

        return toolName switch
        {
            "get_selection" => Localization.LocalizeString("read selection", 181),
            "list_objects" => Localization.LocalizeString("listed objects", 183),
            "get_commands" => Localization.LocalizeString("listed commands", 184),
            "ask_user" => Localization.LocalizeString("asked a question", 190),

            null => Localization.LocalizeString("unknown tool", 200),

            _ => toolName.Replace("_", ""),

        };
    }

    // A short failure verb per tool family so "X failed" reads naturally.
    private static string Verb(string rawToolName)
    {
        string toolName = RemoveUnderscoreUnderscoreNaming(rawToolName);
        return toolName switch
        {
            _ => toolName,
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
