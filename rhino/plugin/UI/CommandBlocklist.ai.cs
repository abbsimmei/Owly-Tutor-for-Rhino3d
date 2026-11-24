namespace Rhino.AI.UI;

internal enum RunVerdict { Invalid, Unknown, Blocked, Ok }

// Plan 022 / ADR-0008: which commands the Run button may start. Rhino-free so Server.Tests can link it.
// The host is authoritative: the panel only asks (command.check) and Run checks again (command.run).
internal static class CommandBlocklist
{
    // Substring rules catch variants such as IncrementalSave, -RunPythonScript or a plug-in's script runner.
    private static readonly string[] Substrings =
    {
        "Script", "Python", "Grasshopper", "Owly", "Save", "Export", "Import", "Undo", "Redo", "Purge", "PlugIn", "Toolbar", "Workspace",
    };

    public static readonly HashSet<string> Names = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        // Script and plug-in runners that no substring catches.
        "ReadCommandFile", "MacroEditor", "Grasshopper", "RunPythonScript", "EditPythonScript",
        // Owly's own commands.
        "Owly", "OwlyWelcome", "OwlyPanel",
        // File and app.
        "Save", "SaveAs", "SaveSmall", "Open", "New", "Close", "Exit", "Import", "Export", "Insert", "Print", "Revert", "Worksession", "Recover",
        // Delete and Purge.
        "Delete", "Purge",
        // Undo and Redo.
        "Undo", "Redo", "UndoMultiple", "RedoMultiple", "ClearUndo",
        // Settings and layout.
        "Options", "DocumentProperties", "Units", "WindowLayout", "PackageManager", "PlugInManager", "Alias", "CommandAlias", "Toolbar", "Workspace",
    };

    // Strips one leading '_', then one leading '-'.
    public static string Bare(string name)
    {
        if (name.StartsWith("_", StringComparison.Ordinal)) name = name.Substring(1);
        if (name.StartsWith("-", StringComparison.Ordinal)) name = name.Substring(1);
        return name;
    }

    public static bool IsBlocked(string name)
    {
        string bare = Bare(name);
        if (Names.Contains(bare)) return true;
        foreach (string rule in Substrings)
            if (bare.IndexOf(rule, StringComparison.OrdinalIgnoreCase) >= 0) return true;
        return false;
    }

    // The order of checks, shared by command.check and command.run, so it is tested without Rhino.
    public static RunVerdict Classify(string? name, Func<string, bool> isCommand, Func<string, bool> isAlias)
    {
        if (!CommandName.IsValid(name)) return RunVerdict.Invalid;
        string bare = Bare(name!);
        if (!isCommand(bare)) return RunVerdict.Unknown;
        if (isAlias(bare) || IsBlocked(name!)) return RunVerdict.Blocked;
        return RunVerdict.Ok;
    }
}
