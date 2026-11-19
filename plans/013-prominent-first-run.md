# 013: Make Owly prominent right after install

- **Written against:** `e076fc3` on `rhino-9.x` (plans 009-012 applied).
- **Status:** TODO
- **Effort:** S
- **Risk:** Low–Medium. It uses documented RhinoCommon APIs, but where Rhino puts toolbars and panels can only be checked by hand.
- **Phase:** PRD Phase 3, bullet "Implement Owly's icon" (PRD §3.3: "Accessible through an Owly owl icon … chat panel, preferably on the right-hand side")

## Why

After plan 012, Rhino loads `Owly.rui` but keeps the Owly toolbar **hidden**: the product owner had to switch it on by hand. Rhino ignored the file's `visible="True"`. The product owner wants Owly to be prominent **as soon as the plug-in is installed**, in two places:

1. **The Owly panel as a tab in the right-hand panel area**, next to Layers / Properties.
2. **The Owly toolbar switched on and docked on the right edge.**

Both happen **once**, on the first start after install, recorded per Rhino version. A student who later hides either one keeps that choice.

The RhinoCommon APIs exist in both RhinoCommon 8.19 and 9.0:
- `Rhino.UI.Panels.OpenPanelAsSibling(Guid panelId, Guid siblingPanelId, bool makeSelectedPanel)`: "in Windows Rhino this will look for a dock bar which contains the sibling panel and add this panel to that dock bar". On Mac it just calls `OpenPanel`.
- `Rhino.UI.PanelIds.Layers`, `Rhino.UI.PanelIds.ObjectProperties`
- `RhinoApp.ToolbarFiles.FindByName(string name, bool ignoreCase)` returns a `ToolbarFile` or null. `RhinoApp.ToolbarFiles.Open(string path)` opens one.
- `ToolbarFile.GetGroup(string name)` returns a `ToolbarGroup`. `ToolbarGroup.Visible` "Gets and sets a toolbar group's visibility".

## Repo conventions you must follow

- New C# files written by an AI agent are named `*.ai.cs`. `System` comes through `rhino/plugin/GlobalUsings.cs`.
- Settings booleans follow `AISettings.WelcomeShown` (`rhino/plugin/AISettings.cs`):
  ```csharp
    public static bool WelcomeShown
    {
        get => Settings.GetBool(nameof(WelcomeShown), false);
        set => Settings.SetBool(nameof(WelcomeShown), value);
    }
  ```
- One-shot start-up work lives in `RhinoAIPlugin.OnFirstIdle` (`rhino/plugin/RhinoAIPlugin.cs:30-65`). Each step has its own `try/catch` and logs with `RhinoApp.WriteLine("[owly] …")`. The welcome-popup block at the end can `return` early, so new steps go **before** it.
- Owly must not edit or hide Rhino's own toolbars or panels. It only adds its own.

## Scope

**Add:** `rhino/plugin/UI/FirstRunLayout.ai.cs`

**Edit:** `rhino/plugin/AISettings.cs`, `rhino/plugin/RhinoAIPlugin.cs`, `rhino/plugin/Owly.rui` (one attribute value)

**Out of scope:** the welcome dialog, the panel web code, the art, `plans/`, Rhino's own `default.rui`.

## Steps

### 1. Baseline

Run the shared gates from `plans/README.md` (R9 build, R8 build, Server.Tests). If they fail, STOP.

### 2. Settings flag (`rhino/plugin/AISettings.cs`)

Add this after `WelcomeShown`:
```csharp
    // True once Owly has placed its panel tab and toolbar on first start (plan 013). Per Rhino version,
    // like WelcomeShown; a student who later hides either keeps that choice.
    public static bool FirstRunLayoutDone
    {
        get => Settings.GetBool(nameof(FirstRunLayoutDone), false);
        set => Settings.SetBool(nameof(FirstRunLayoutDone), value);
    }
```

### 3. `rhino/plugin/UI/FirstRunLayout.ai.cs` (new)

```csharp
using System.IO;
using Rhino.UI;

namespace Rhino.AI;

// First-start placement (plan 013): Owly's panel becomes a tab next to Layers / Properties on the right,
// and Owly's toolbar is switched on (Rhino loads a plug-in's .rui hidden). Runs once per Rhino version.
// Owly only touches its own panel and toolbar, never Rhino's.
internal static class FirstRunLayout
{
    private const string ToolbarName = "Owly";

    public static void Apply()
    {
        try
        {
            AddPanelTab();
        }
        catch (Exception ex)
        {
            RhinoApp.WriteLine($"[owly] could not add the Owly panel tab: {ex.Message}");
        }

        try
        {
            ShowToolbar();
        }
        catch (Exception ex)
        {
            RhinoApp.WriteLine($"[owly] could not show the Owly toolbar: {ex.Message}");
        }
    }

    // Next to Layers if that panel is docked somewhere, else next to Properties. Not made the active tab:
    // the welcome popup's "Open Owly" does that, and the student's current tab stays in front.
    private static void AddPanelTab()
    {
        if (Panels.OpenPanelAsSibling(UI.AIPanel.PanelId, PanelIds.Layers, false))
            return;
        if (Panels.OpenPanelAsSibling(UI.AIPanel.PanelId, PanelIds.ObjectProperties, false))
            return;
        RhinoApp.WriteLine("[owly] Layers and Properties panels not found; the Owly panel opens with the Owly command.");
    }

    private static void ShowToolbar()
    {
        ToolbarFile? file = RhinoApp.ToolbarFiles.FindByName(ToolbarName, true);
        if (file is null)
        {
            // Rhino normally loads Owly.rui from next to Owly.rhp by itself; open it if it didn't.
            string folder = Path.GetDirectoryName(typeof(FirstRunLayout).Assembly.Location) ?? string.Empty;
            string path = Path.Combine(folder, "Owly.rui");
            if (File.Exists(path))
                file = RhinoApp.ToolbarFiles.Open(path);
        }

        if (file?.GetGroup(ToolbarName) is not { } group)
        {
            RhinoApp.WriteLine("[owly] Owly toolbar not found.");
            return;
        }

        group.Visible = true;
    }
}
```
Notes:
- If `PanelIds`, `ToolbarFile`, `ToolbarGroup` or `RhinoApp.ToolbarFiles` don't resolve, check the namespace in RhinoCommon. They should be `Rhino.UI.*` and `Rhino.RhinoApp.ToolbarFiles`. Fix only the `using`.
- If `UI.AIPanel.PanelId` doesn't resolve inside `namespace Rhino.AI`, use the same form `AIPanelCommand.cs` uses.
- If `OpenPanelAsSibling` returns `void` on one target, STOP and report.

### 4. Call it on first start (`rhino/plugin/RhinoAIPlugin.cs`)

In `OnFirstIdle`, insert this block **between** the `DeleteLegacyConversations` try/catch and the `// Welcome popup …` comment:
```csharp
        // Panel tab and toolbar, once per Rhino version (plan 013). Before the welcome popup, so its
        // "Open Owly" selects the tab that is already in place. Flag first, so a failure can't repeat.
        try
        {
            if (!RhinoApp.IsRunningHeadless && !AISettings.FirstRunLayoutDone)
            {
                AISettings.FirstRunLayoutDone = true;
                FirstRunLayout.Apply();
            }
        }
        catch (Exception ex)
        {
            RhinoApp.WriteLine($"[owly] could not place Owly on first start: {ex.Message}");
        }
```

### 5. Dock the toolbar on the right (`rhino/plugin/Owly.rui`)

Line 7 reads:
```xml
      <dock_bar_info dock_bar="False" docking="True" horz="False" visible="True" floating="False" dock_location="top" />
```
Change only `dock_location="top"` to `dock_location="right"`. Change nothing else in the file; keep the GUIDs and the embedded art. Then check:
```powershell
[xml](Get-Content -Raw rhino/plugin/Owly.rui) | Out-Null; "well-formed"
```

### 6. Gates

Run the shared gates again.

## Done criteria

```sh
dotnet build rhino/plugin/RhinoAI.csproj -p:RhinoTarget=R9 -clp:NoSummary   # succeeded, 0 errors, no new warnings
dotnet build rhino/plugin/RhinoAI.csproj -p:RhinoTarget=R8 -clp:NoSummary   # succeeded, 0 errors, no new warnings
dotnet test tests/Server.Tests/Server.Tests.csproj                          # all pass (148)
git grep -n "FirstRunLayout.Apply" -- rhino/plugin/RhinoAIPlugin.cs          # 1 hit
git grep -n "FirstRunLayoutDone" -- rhino/plugin                             # AISettings.cs + RhinoAIPlugin.cs
grep -c 'dock_location="right"' rhino/plugin/Owly.rui                       # 1
git diff --stat HEAD -- rhino/plugin/Owly.rui                               # 1 line changed (1 insertion, 1 deletion)
```
Plus the well-formed check from step 5.

## Manual check (by the user, Rhino 9 then Rhino 8)

To simulate a fresh install:
- quit Rhino
- in Owly's plug-in settings file under `%APPDATA%\McNeel\Rhinoceros\<version>\Plug-ins\` (the folder whose name contains `1f0762a7-df2f-4d11-8ff3-22ec1a40391c`), delete the `FirstRunLayoutDone` entry, and also `WelcomeShown` to see the whole first run
- hide the Owly toolbar and close the Owly panel tab

Then:
1. Start Rhino. The right-hand panel area has an **Owly tab with the owl**, next to Layers or Properties. The previously active tab stays in front.
2. The **Owly toolbar is visible**. Report where it is: docked on the right edge, or elsewhere. Rhino may keep a remembered position for a toolbar it has seen before. If it isn't on the right, drag it there once and tell the advisor. A truly fresh install is the real test, either on a second machine or after removing Rhino's copy of `Owly.rui` from `%APPDATA%\McNeel\Rhinoceros\<version>\UI\`.
3. The welcome popup appears. **Open Owly** selects the Owly tab.
4. Hide the toolbar and close the Owly tab, then restart Rhino. Neither comes back on its own, because the student's choice is kept.

## Tests

None automated. These are Rhino UI calls, not linked into Server.Tests.

## Maintenance

- Changing `FirstRunLayout` doesn't re-run it for existing users, because the flag is already set. To re-place Owly for everyone, rename the flag. Only do that with a good reason, since it overrides the students' choices once.
- If Rhino later honours `visible`/`dock_location` from the `.rui` on its own, `ShowToolbar` is harmless and can stay.

## STOP and report if

- An API named in step 3 doesn't exist or has a different signature on R8 or R9.
- The build adds a warning.
