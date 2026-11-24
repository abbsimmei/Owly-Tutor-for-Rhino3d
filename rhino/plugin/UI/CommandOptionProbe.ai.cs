using System.Diagnostics;
using Rhino.Runtime;

namespace Rhino.AI.UI;

// Plan 024 / ADR-0008: the option probe. It starts a command with the cancel token (`_X _Cancel`) to read the first
// prompt's options and their current values off Rhino's command line. It is the only action Owly takes without a
// click, so it runs only under six guards, only on Rhino 9 and later, only on the UI thread outside a WebView handler,
// and the only script it ever runs is the validated chip name plus ` _Cancel`. The AI cannot trigger it.
// Plan 024 Part A (docs/owly/option-probe-spike.md): this form leaves no document change, no undo record, no
// selection change and does not take over "repeat last command". Other cancel forms do, so never change it.
internal static class CommandOptionProbe
{
    private const int MaxPrompts = 5;

    // The guards that never change for a session. True means a retry later is pointless.
    internal enum Guard { None, Version, Language, Blocked, NoHelpOptions, Busy, Selection }

    internal static Guard Check(RhinoDoc doc, string name, CommandOptions help)
    {
        try
        {
            if (RhinoApp.ExeVersion < 9) return Guard.Version;
            if ((Rhino.ApplicationSettings.AppearanceSettings.LanguageIdentifier & 0x3FF) != 0x09) return Guard.Language;
            if (CommandBlocklist.Classify(name, Rhino.Commands.Command.IsCommand, Rhino.ApplicationSettings.CommandAliasList.IsAlias) != RunVerdict.Ok) return Guard.Blocked;
            if (help.Options.Count == 0) return Guard.NoHelpOptions;
            if (Rhino.Commands.Command.InCommand()) return Guard.Busy;
            if (doc.Objects.GetSelectedObjects(false, false).Any()) return Guard.Selection;
            return Guard.None;
        }
        catch (Exception ex)
        {
            HostUtils.LogDebugEvent($"[owly] option probe guard check failed: {ex.Message}\n");
            return Guard.Blocked;
        }
    }

    // Guards that a later try can never fix.
    internal static bool IsPermanent(Guard g) => g is Guard.Version or Guard.Language or Guard.Blocked or Guard.NoHelpOptions;

    // Rhino 8 has no CommandPromptChanged event, and the version guard stops the probe there anyway; on R8 the prompts
    // list stays empty and only the captured strings are read.
    // Runs synchronously on the UI thread. The caller guarantees it is not inside a WebView message handler.
    internal static IReadOnlyList<ProbeOption>? TryProbe(RhinoDoc doc, string name, CommandOptions help, out Guard guard)
    {
        guard = Check(doc, name, help);
        if (guard != Guard.None) return null;
        if (!CommandName.IsValid(name)) { guard = Guard.Blocked; return null; }

        var prompts = new List<string>();
#if R9
        void OnPrompt(object? sender, EventArgs e)
        {
            try
            {
                var p = RhinoApp.CommandPrompt;
                if (!string.IsNullOrEmpty(p) && prompts.Count < MaxPrompts && !prompts.Contains(p)) prompts.Add(p);
            }
            catch (Exception) { }
        }
#endif

        var watch = Stopwatch.StartNew();
        string[]? captured = null;
        try
        {
#if R9
            RhinoApp.CommandPromptChanged += OnPrompt;
#endif
            RhinoApp.CommandWindowCaptureEnabled = true;
            RhinoApp.CapturedCommandWindowStrings(true);
            RhinoApp.RunScript(doc.RuntimeSerialNumber, name + " _Cancel", false);
            captured = RhinoApp.CapturedCommandWindowStrings(true);
        }
        catch (Exception ex)
        {
            HostUtils.LogDebugEvent($"[owly] option probe failed for {name}: {ex.Message}\n");
            return null;
        }
        finally
        {
#if R9
            try { RhinoApp.CommandPromptChanged -= OnPrompt; } catch (Exception) { }
#endif
            try { RhinoApp.CommandWindowCaptureEnabled = false; } catch (Exception) { }
        }

        try
        {
            IReadOnlyList<ProbeOption>? result = null;
            foreach (var p in prompts)
            {
                result = ProbeLineParser.Parse(p);
                if (result is not null) break;
            }
            if (result is null && captured is not null)
                foreach (var c in captured)
                {
                    result = ProbeLineParser.Parse(c);
                    if (result is not null) break;
                }
            HostUtils.LogDebugEvent($"[owly] option probe {name}: {(result is null ? "nothing read" : result.Count + " options")} in {watch.ElapsedMilliseconds} ms\n");
            return result;
        }
        catch (Exception ex)
        {
            HostUtils.LogDebugEvent($"[owly] option probe parse failed for {name}: {ex.Message}\n");
            return null;
        }
    }
}
