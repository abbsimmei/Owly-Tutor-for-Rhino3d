using Rhino.UI;

namespace Rhino.AI.UI;

// Authored host-side beside the card's title, because the panel holds no per-tool knowledge.
internal static class ToolChips
{
    public const string CancelId = "cancel";

    public static IReadOnlyList<PanelToolChip> None { get; } = [];

    private static IReadOnlyList<PanelToolChip> CancelOnly =>
        [new PanelToolChip(CancelId, Localization.LocalizeString("Cancel", 175), "stop", "danger")];

    public static IReadOnlyList<PanelToolChip> For(string toolName, bool isRunning) =>
        isRunning && IsCancellable(toolName) ? CancelOnly : None;

    // No remaining tool is cancellable from the chip row.
    private static bool IsCancellable(string toolName) => false;
}
