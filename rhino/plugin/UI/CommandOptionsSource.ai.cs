using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using Rhino.AI.Resources;
using Rhino.Runtime;

namespace Rhino.AI.UI;

// Plan 023: option data for a command, fetched by the HOST (never the AI) from McNeel's help page and parsed.
// Cached per (command, Rhino major) for the session. A definite non-200 is cached as empty; a timeout or
// network exception is not, so a later card can retry.
internal static class CommandOptionsSource
{
    private static readonly ConcurrentDictionary<string, Task<CommandOptions>> Cache = new(StringComparer.Ordinal);

    private static string Key(string name, int major) => $"{CommandBlocklist.Bare(name).ToLowerInvariant()}|{major}";

    public static Task<CommandOptions> Get(string name)
    {
        try
        {
            if (!CommandName.IsValid(name)) return Task.FromResult(CommandOptions.Empty);
            if (!RhinoApp.IsInternetAccessAllowed) return Task.FromResult(CommandOptions.Empty);
            var bare = CommandBlocklist.Bare(name);
            if (!Rhino.Commands.Command.IsCommand(bare)) return Task.FromResult(CommandOptions.Empty);

            var major = RhinoApp.Version.Major;
            var key = Key(name, major);
            return Observe(key, Cache.GetOrAdd(key, _ => Load(bare, major)));
        }
        catch (Exception ex)
        {
            HostUtils.LogDebugEvent($"[owly] command options lookup failed for {name}: {ex.Message}\n");
            return Task.FromResult(CommandOptions.Empty);
        }
    }

    // A failed fetch (timeout, network) is not remembered: the entry is removed so a later card can retry.
    private static async Task<CommandOptions> Observe(string key, Task<CommandOptions> task)
    {
        try
        {
            return await task.ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            HostUtils.LogDebugEvent($"[owly] command help fetch failed for {key}: {ex.Message}\n");
            ((ICollection<KeyValuePair<string, Task<CommandOptions>>>)Cache).Remove(new KeyValuePair<string, Task<CommandOptions>>(key, task));
            return CommandOptions.Empty;
        }
    }

    // For Run: never fetches.
    public static bool TryCached(string name, out CommandOptions options)
    {
        options = CommandOptions.Empty;
        try
        {
            if (!Cache.TryGetValue(Key(name, RhinoApp.Version.Major), out var task)) return false;
            if (task.Status != TaskStatus.RanToCompletion) return false;
            options = task.Result;
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    // Faults on a timeout or network exception (Observe handles that); a definite non-200 parses to Empty.
    private static Task<CommandOptions> Load(string bare, int major)
    {
        // Off the UI thread, so the WebView never waits for the network.
        return Task.Run(async () =>
        {
            var html = await CommandHelpResource.FetchHtml(bare, major).ConfigureAwait(false);
            if (html == null && major > 8)
                html = await CommandHelpResource.FetchHtml(bare, 8).ConfigureAwait(false);
            return CommandOptionsParser.Parse(html);
        });
    }
}
