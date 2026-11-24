using Rhino.Runtime;
using Rhino.UI;

namespace Rhino.AI;

// Plan 025: the one store and project registry this Rhino uses, over %AppData%\Owly. Unlike
// OwlyStore and ProjectRegistry this is the Rhino side (logging, localised names), so Server.Tests
// does not link it. Changed fires on the UI thread after anything here changes the projects, so
// every open panel can show the new state.
internal static class OwlyMemory
{
    public static OwlyStore Store { get; } =
        new(OwlyStore.DefaultRoot, message => HostUtils.LogDebugEvent(message + "\n"));

    public static ProjectRegistry Projects { get; } =
        new(Store, Localization.LocalizeString("Unsorted", 360));

    public static event Action? Changed;

    public static void NotifyChanged() => Changed?.Invoke();

    // "Delete all Owly memory" in settings.
    public static bool DeleteAll()
    {
        bool deleted = Store.DeleteAll();
        NotifyChanged();
        return deleted;
    }
}
