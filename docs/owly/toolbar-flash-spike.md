# Toolbar flash spike (plan 016, Part A, offline step only)

Question: can Owly flash or highlight a toolbar button on screen in Rhino 9 using only Rhino's **public** API (ADR-0007)?

**Verdict: CANDIDATE - NEEDS IN-RHINO CHECK.** A public route to the toolbar service exists, but only down to the level of a whole toolbar control. Nothing public gives the bounds of a single button. Part A step 2 (running it inside Rhino 9) has not been done, and Part B has not been started.

## How this was inspected

- Packages: `rhinocommon` 9.0.26132.12305-wip (main) and 9.0.26300.9401 (comparison), `lib/net8.0/Rhino.UI.dll`, `RhinoCommon.dll`, `Eto.dll`/`Eto.xml`.
- Tool: a throwaway console app (outside the repo) using `System.Reflection.MetadataLoadContext`. Metadata only, nothing from Rhino was loaded or run.
- Only public signatures are relied on. The public surface of the two packages is identical for everything below.

## The chain

| Link | Status | Public signature found |
|---|---|---|
| Obtain `IToolbarsService` | public (unverified at run time) | `public class Rhino.UI.Runtime.RhinoUiServiceLocator` with `public RhinoUiServiceLocator()` and the instance method `public T GetService<T>()`. It is a locator, so whether `GetService<IToolbarsService>()` returns a non-null service is unknown. |
| `IToolbarsService` members | public | `public interface Rhino.UI.Runtime.IToolbarsService { bool UseNewStuff(); IToolbar GetToolbar(Guid); }` |
| `GetToolbar` | public | `IToolbar GetToolbar(System.Guid)`. The Guid is presumably the toolbar id from the `.rui` file (`Owly.rui`); unverified. |
| `IToolbar` members | public | `public interface Rhino.UI.Runtime.IToolbar { Guid Id {get;} Guid FileId {get;} void DisposeControls(); Eto.Forms.Control GetControl(uint, bool); void PanelVisibilityChanged(bool, uint, Rhino.UI.ShowPanelReason); void PanelClosing(uint, bool); }`. The meaning of the `uint` (probably a document serial number) and the `bool` is undocumented. |
| `GetControl` | public | `Eto.Forms.Control GetControl(System.UInt32, System.Boolean)` |
| Bounds of the toolbar control | public | `Eto.Forms.Control` has public `Bounds`, `Location`, `Size`, `PointToScreen(PointF)`, `ParentWindow`, `VisualControls`. |
| Bounds of one **button** | **not found** | No public type or member exposes a button, a button index or a button rectangle. `Rhino.UI.Toolbar` (RhinoCommon) has only `Id`, `Name`, `BitmapSize`, `TabSize`. `Rhino.UI.ToolbarFile` / `ToolbarFileCollection` (public) give `GetToolbar(int)`, `GetGroup`, `ToolbarGroup.Visible/IsDocked`, but no controls and no items. If `GetControl` returns an Eto container whose `VisualControls` are the buttons, a button rectangle could be derived; if the toolbar is drawn natively inside one control, it cannot. |
| Public implementing type of `IToolbarsService` / `IToolbar` | not found | Not enumerated cleanly (the scan hit a missing `System.Drawing.Common` reference). The only public entry found is `RhinoUiServiceLocator`. |

Other public but unrelated types seen: `Rhino.UI.Runtime.PlatformServiceProvider.Service` (`IPlatformService`, window helpers such as `MainRhinoWindow`, `ToEto`), and `HostUtils`. `IPlatformService` was not inspected for toolbar members beyond the name filter, which found none.

Non-public members exist (for example `Rhino.UI.Internal.TabPanels.*` toolbar settings and `RuiExplorer` editors). They were not examined and must not be used.

## What a human must run to confirm (Rhino 9 debug session)

Throwaway command (not committed, not shipped). It only reads and prints. Run it in the "Rhino 9" launch configuration with the Owly toolbar visible (the toolbar GUID can be read from `rhino/plugin/Owly.rui`):

```csharp
using Rhino; using Rhino.Commands; using Rhino.UI.Runtime; using Eto.Forms;

public class OwlyFlashProbe : Command
{
    public override string EnglishName => "OwlyFlashProbe";
    protected override Result RunCommand(RhinoDoc doc, RunMode mode)
    {
        var svc = new RhinoUiServiceLocator().GetService<IToolbarsService>();
        RhinoApp.WriteLine("service: " + (svc == null ? "NULL" : svc.GetType().FullName));
        if (svc == null) return Result.Success;
        RhinoApp.WriteLine("UseNewStuff: " + svc.UseNewStuff());
        var tbId = System.Guid.Parse("<toolbar Guid from Owly.rui>");
        var tb = svc.GetToolbar(tbId);
        RhinoApp.WriteLine("toolbar: " + (tb == null ? "NULL" : tb.GetType().FullName));
        if (tb == null) return Result.Success;
        foreach (var serial in new uint[] { doc.RuntimeSerialNumber, 0 })
        {
            Control c = tb.GetControl(serial, false);
            RhinoApp.WriteLine($"control({serial}): " + (c == null ? "NULL"
                : $"{c.GetType().FullName} bounds={c.Bounds} screen={c.PointToScreen(new Eto.Drawing.PointF(0,0))} children={System.Linq.Enumerable.Count(c.VisualControls)}"));
        }
        return Result.Success;
    }
}
```

Report back: is the service null; is the toolbar null; is the control null; does the control's screen position match the visible toolbar; do its `VisualControls` correspond to individual buttons (print each child's type and `Bounds`). Also check that calling `GetControl` does not alter the toolbar (it may create a control, and `DisposeControls()` exists, so do not call it). If any of these is null, or no per-button rectangle is available, the result is NOT FEASIBLE and PRD.md gets the note from plan 016 Part A step 4. If a button rectangle is available, only then is Part B (a fade-out overlay on the Owly toolbar button) worth starting.

## Uncertain

- Whether the locator is wired to a live service in a normal Rhino 9 session, and whether `GetControl` returns a live control for a docked toolbar or creates a fresh detached one.
- Whether the toolbar's buttons exist as separate Eto controls.
- `UseNewStuff()` suggests the toolbar implementation is in transition, so behaviour may change between Rhino 9 builds.
