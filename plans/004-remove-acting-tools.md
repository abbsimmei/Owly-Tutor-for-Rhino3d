# 004: Remove execute, mutate and Grasshopper tools

- **Written against:** `ba4b4a0` (apply on top of plan 003)
- **Status:** TODO
- **Effort:** M
- **Risk:** Medium

## Why

Owly's core rule (PRD §4): never generate geometry, never modify the model, never execute Rhino commands, and Grasshopper is out of scope. Today the AI can call:

- `run_command` (RhinoApp.RunScript), `run_python`, `run_csharp`, `manage_plugin_commands` (arbitrary code);
- `open_doc`, `save_doc`, `close_doc`, `set_layer_material`, `set_selection`, `set_camera`, `zoom_to_layer`, `zoom_to_object`;
- `get_viewport_image`, which changes the display mode and camera;
- 24 Grasshopper tools.

ADR `docs/adr/0002-delete-tools-not-filter.md` decides to **delete** them, not filter them.

The only AI-visible tools after this plan are `get_commands`, `get_context`, `get_selection`, `list_objects` and `ask_user`.

Tools are found by reflection. Every class marked `[McpServerToolType]` with methods marked `[McpServerTool(...)]` is registered (`rhino/plugin/Server/ToolRegistry.ai.cs`), so deleting a file removes its tool.

## Scope

### Delete these files and folders

- Files under `rhino/plugin/Tools/`:
  - `RunCommandTool.cs`, `RunPythonTool.cs`, `RunCSharpTool.cs`, `CreateTool.cs`
  - `OpenDocTool.cs`, `SaveDocTool.cs`, `CloseDocTool.cs`
  - `SetLayerMaterialTool.cs`, `SetSelectionTool.cs`, `SetCameraTool.cs`
  - `ZoomToLayerTool.cs`, `ZoomToObjectTool.cs`, `GetViewportImageTool.cs`
  - `GrasshopperSummary.cs`, `Rewiring.ai.cs`, `Refusals.cs`
- `rhino/plugin/Tools/GH1/`, `rhino/plugin/Tools/GH2/`
- `rhino/plugin/Resources/GH1/`, `rhino/plugin/Resources/GH2/` (the `Resources/` folder, if it is then empty; **not** `rhino/plugin/src/Resources/`)
- `rhino/plugin/ScriptProjects/`, `rhino/plugin/RhinoCode/`

### Edit these

- `rhino/plugin/Tools/GetContextTool.ai.cs`
- `rhino/plugin/Server/ToolRegistry.ai.cs`
- `rhino/plugin/Tools/ToolResult.cs`
- `tests/Server.Tests/ToolResultFormatterTests.ai.cs`
- `rhino/plugin/RhinoAIPlugin.cs`
- `rhino/plugin/AISettings.cs`
- `rhino/plugin/UI/AISettingsPanel.ai.cs`
- `rhino/plugin/UI/AIPanelViewModel.ai.cs` (only the `PluginCommandsAvailable` capability)
- `rhino/plugin/UI/ToolSummary.cs`
- `rhino/plugin/UI/ToolChips.ai.cs`
- `rhino/plugin/Agents/AgentPrompts.cs`
- `rhino/plugin/RhinoAI.csproj`

## Out of scope

- The panel TypeScript (plan 005).
- `rhino/plugin/src/Resources/*` (`command_help`, `host_environment`, `installed_plugins`). Keep them.
- `ask_user` and its helpers (`AskUserTool.ai.cs`, `AskUserQuestion.ai.cs`, `AskUserValidation.ai.cs`, `QuestionSpec.cs`).
- `Coercions.cs` and `BackgroundThreadAttribute.cs`.

## Steps

1. **Delete** everything in the "Delete these" list.

2. **Find the fallout.** Run `dotnet build rhino/plugin/RhinoAI.csproj -p:RhinoTarget=R9 -clp:NoSummary`. Fix every compile error by removing code that referenced the deleted types, using only the files in "Edit these". If an error points at a file not on that list, STOP and report.

3. **`GetContextTool.ai.cs`.**
   - Remove the Grasshopper parts: the `using` lines for GH (around lines 5-6), the GH fields in the result record (around line 35), the GH summary calls (around lines 48-51) and the GH1/GH2 summary methods (around lines 132-171).
   - Update the tool's `[Description]` so it no longer mentions Grasshopper.
   - The tool must still return the selection, the active viewport and the document summary.

4. **`ToolRegistry.ai.cs`.** Delete the GH2 autostart: the code around lines 187-189 that checks for tool names containing `G2_`, and the helper around lines 250-262 that references `GH2_StartTool`.

5. **`ToolResult.cs`.**
   - Delete the `GH_*` members of the `ToolError` enum (around line 144) and their message cases (lines 114-122).
   - In `tests/Server.Tests/ToolResultFormatterTests.ai.cs:229`, replace `ToolError.GH_Document_NotFound` with another non-GH `ToolError` member that still exists, and change `guidance: "Start Grasshopper"` to `guidance: "Try again"`. The test checks that the error code survives; which code it uses doesn't matter.

6. **`RhinoAIPlugin.cs`.** Delete `ScriptProjects.ScriptProjectStartup.ReloadWhenIdle();` in `OnLoad` and `ScriptProjects.ScriptProjectStartup.DeleteRegistryCache();` in `OnShutdown`.

7. **`AISettings.cs`.** Delete `AutoLoadScriptPlugIn` (lines 51-55) and `DefaultViewportImageWidth`, `DefaultViewportImageHeight`, `MaxViewportImageWidth` and `MaxViewportImageHeight` (lines 185-198). Then delete their uses in `UI/AISettingsPanel.ai.cs`. The agent found `AutoLoadScriptPlugIn` used around lines 31, 142, 487 and 504. Remove the matching checkbox or row; don't leave an empty layout gap.

8. **`AIPanelViewModel.ai.cs`.** Around lines 487-500, the hello/capabilities payload computes `PluginCommandsAvailable` from the script-project code. Make it the constant `false` and delete the helper that computed it. Leave the `Grasshopper:` capability for plan 005.

9. **`ToolSummary.cs` and `ToolChips.ai.cs`.** Both are linked into Server.Tests, so they must keep compiling without RhinoCommon.
   - In `ToolSummary.cs`, delete the `switch` cases and strings for the removed tools (all `run_*`, `*_doc`, `set_*`, `zoom_*`, `get_viewport_image`, `manage_plugin_commands` and every `g1_*` / `g2_*`, around lines 60-70 and 135).
   - In `ToolChips.ai.cs`, `IsCancellable` is `toolName is "run_command"`. Change it to always return `false`, and keep `For`, `None` and `CancelId` so callers still compile.

10. **`AgentPrompts.cs`.**
    - Delete the `GrasshopperSteer` property.
    - Replace `GroundingSteer` with:
      ```csharp
      public static string GroundingSteer =>
          "You cannot change the user's model: you have no tools that create, modify, select, or delete anything, "
          + "and you must never claim to have done so. No document or selection state is injected automatically. "
          + "When advice depends on what the user has, read it first with get_context (selection, active "
          + "viewport, document summary) or get_selection / list_objects for detail. Never assume what exists.";
      ```
    - Change `Compose` to: `string steers = AskUserSteer + "\n\n" + GroundingSteer;`

11. **`RhinoAI.csproj`.**
    - Delete the `Grasshopper`, `Grasshopper2` and RhinoCode `PackageReference`s for both R8 and R9.
    - Delete `<Compile Remove="Tools/GH2/**;Resources/GH2/**">` (and any similar GH2 exclusion).
    - Do not touch the RhinoCommon references.
    - Then confirm that `dotnet build ... -p:RhinoTarget=R8` still succeeds, because R8 had its own GH and SDK-helper lines.

## Done criteria

- The shared gates pass (R9, R8, Server.Tests).
- `git grep -n "\[McpServerTool(" -- rhino/plugin` prints exactly 5 lines, for `get_commands`, `get_context`, `get_selection`, `list_objects` and `ask_user`.
- `git grep -nE "\bg[12]_|Grasshopper|ScriptProject|RhinoCode|RunScript" -- rhino/plugin ':!*.md'` prints nothing. If a line has to stay (for example a localized string id), list it in your report with the reason.
- `git grep -n "RunScript" -- rhino/plugin` prints nothing; the AI has no way left to run a command.

## Tests

`ToolResultFormatterTests` is updated in step 5. No new tests. Manual check by the user in Rhino: ask Owly to "draw a box for me". It should explain how to use `Box` instead, and the document's object count (`SelAll` → count) must not change.

## Maintenance

Any future tool must be read-only (ADR-0002). If one ever needs to change Rhino state, write a superseding ADR first.

## STOP and report if

- Deleting `ScriptProjects/` breaks something other than `RhinoAIPlugin.cs`, `AISettings.cs`, `AISettingsPanel.ai.cs` or `AIPanelViewModel.ai.cs`.
- The R8 build needs Grasshopper for a reason not covered above.
