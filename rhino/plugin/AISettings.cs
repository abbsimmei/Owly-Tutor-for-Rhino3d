using System.Collections.Generic;
using System.Text.Json;
using Rhino.PlugIns;

namespace Rhino.AI;

internal static class AISettings
{

    private static Guid PluginId { get; } = new("1f0762a7-df2f-4d11-8ff3-22ec1a40391c");

    private static PersistentSettings Settings =>
        PlugIn.Find(PluginId) is PlugIn plugin
            ? plugin.Settings
            : throw new InvalidOperationException("RhinoAI plugin is not loaded; AISettings is unavailable.");

    // Name of the agent the registry prefers when resolving the active one.
    public static string DefaultAgentName
    {
        get => Settings.GetString(nameof(DefaultAgentName), "claude");
        set => Settings.SetString(nameof(DefaultAgentName), value);
    }

    // The panel's text zoom as the percentage the user sees; the ladder itself lives in the panel.
    public static int ZoomLevel
    {
        get => Settings.GetInteger(nameof(ZoomLevel), 100);
        set => Settings.SetInteger(nameof(ZoomLevel), value);
    }

    public static bool AutoLoadMCP
    {
        get => Settings.GetBool(nameof(AutoLoadMCP), false);
        set => Settings.SetBool(nameof(AutoLoadMCP), value);
    }

    // Owly has one agent and no on/off switch (plan 008); a disabled-agents value from an older build is ignored.
    public static bool IsEnabled(AgentDefinition def) => def.Enabled;

    public static string AgentModel(string name) => Settings.GetString(AgentKey(name, "Model"), string.Empty);

    public static void SetAgentModel(string name, string model) => Settings.SetString(AgentKey(name, "Model"), model);

    public static string EffectiveModel(AgentDefinition def) =>
        AgentModel(def.Name) is { Length: > 0 } model ? model : def.DefaultModel;

    // Students cannot change Owly's prompt (ADR-0005): a value saved by an older build is ignored.
    public static string EffectivePrompt(AgentDefinition def) => def.DefaultPrompt;

    private static string AgentKey(string name, string field) => $"Agent_{name.ToLowerInvariant()}_{field}";

    // Overrides the username-derived name of the plugin manage_plugin_commands writes to.
    public static string? ScriptPluginName
    {
        get
        {
            string stored = Settings.GetString(nameof(ScriptPluginName), string.Empty);
            return string.IsNullOrWhiteSpace(stored) ? null : stored;
        }
        set => Settings.SetString(nameof(ScriptPluginName), value ?? string.Empty);
    }

    // Owly keeps no saved conversations (ADR-0006). Older builds stored them under this child node;
    // delete it once so nothing from those builds stays on the student's computer.
    public static void DeleteLegacyConversations()
    {
        const string legacyNode = "Conversations";
        if (Settings.TryGetChild(legacyNode, out _))
            Settings.DeleteChild(legacyNode);
    }

    public static int StartingPort
    {
        get => Settings.GetInteger(nameof(StartingPort), 10500);
        set => Settings.SetInteger(nameof(StartingPort), value);
    }

    public const int MinPort = 1;
    public const int MaxPort = 65535;


    public static string AcpCommand
    {
        get => Settings.GetString(nameof(AcpCommand), "npx");
        set => Settings.SetString(nameof(AcpCommand), value);
    }

    public static string AcpPackage
    {
        get => Settings.GetString(nameof(AcpPackage), "@agentclientprotocol/claude-agent-acp");
        set => Settings.SetString(nameof(AcpPackage), value);
    }

    public static string ClaudeCommandFileName
    {
        get => Settings.GetString(nameof(ClaudeCommandFileName), "claude");
        set => Settings.SetString(nameof(ClaudeCommandFileName), value);
    }

    public static TimeSpan AgentStartupTimeout
    {
        get => TimeSpan.FromMilliseconds(Settings.GetInteger(nameof(AgentStartupTimeout), 60_000));
        set => Settings.SetInteger(nameof(AgentStartupTimeout), (int)value.TotalMilliseconds);
    }


    public static TimeSpan ScriptCleanupDelay
    {
        get => TimeSpan.FromMilliseconds(Settings.GetInteger(nameof(ScriptCleanupDelay), 15_000));
        set => Settings.SetInteger(nameof(ScriptCleanupDelay), (int)value.TotalMilliseconds);
    }

    public static TimeSpan CommandHelpHttpTimeout
    {
        get => TimeSpan.FromMilliseconds(Settings.GetInteger(nameof(CommandHelpHttpTimeout), 2_000));
        set => Settings.SetInteger(nameof(CommandHelpHttpTimeout), (int)value.TotalMilliseconds);
    }

    public static TimeSpan SlotCloseTempDeleteDelay
    {
        get => TimeSpan.FromMilliseconds(Settings.GetInteger(nameof(SlotCloseTempDeleteDelay), 1_000));
        set => Settings.SetInteger(nameof(SlotCloseTempDeleteDelay), (int)value.TotalMilliseconds);
    }

    public static TimeSpan RouterExitDelay
    {
        get => TimeSpan.FromMilliseconds(Settings.GetInteger(nameof(RouterExitDelay), 200));
        set => Settings.SetInteger(nameof(RouterExitDelay), (int)value.TotalMilliseconds);
    }

    public static int MaxCommandResults
    {
        get => Settings.GetInteger(nameof(MaxCommandResults), 200);
        set => Settings.SetInteger(nameof(MaxCommandResults), value);
    }

    public static int DefaultObjectListLimit
    {
        get => Settings.GetInteger(nameof(DefaultObjectListLimit), 1000);
        set => Settings.SetInteger(nameof(DefaultObjectListLimit), value);
    }
}
