# 008: Simplify Owly's settings to a model choice

- **Written against:** `d78f9eb` on `rhino-9.x`. Apply it after plans 006 and 007.
- **Status:** TODO
- **Effort:** M
- **Risk:** Low to Medium. This is mostly deletion in one Eto settings file, plus two small behaviour fixes in `AISettings.cs` and `McpEndpoint.ai.cs`.
- **Phase:** PRD Phase 2, bullet "Simplify the existing interface"

## Why

The settings dialog (Rhino Options → Owly, or the panel's gear button) still has RhinoAI's developer layout:

- An **agent grid** with Default/Agent columns and a right-click menu (Set Default, Restore Defaults). Owly has exactly one agent (ADR-0003).
- An **Enabled** checkbox that can switch Owly off with no obvious way back.
- A **Prompt:** box that appends free text to the AI's instructions. ADR-0005 removes it: students can't edit Owly's prompt.
- A **Tools** tab that can hide agent tools. Hiding `get_commands` would break plan 006's rule that every command must be checked with `get_commands`.
- A **Loading** tab ("Load MCP server at startup") with text that says "RhinoAI".

The product owner decided that students see **only the model dropdown**: Sonnet (the default), Opus and Haiku. The advisor added three follow-through fixes so the removed controls can't leave a stale setting in force:

1. Owly is always enabled. `AISettings.IsEnabled` ignores `DisabledAgents`.
2. All five read-only agent tools are always offered. `McpEndpoint` stops filtering by `DisabledTools`.
3. Saving no longer overwrites the auto-load setting when its tab isn't shown.

The read-only "Found at:" path list and the found/not-found label stay, to help troubleshoot a missing Claude Code install.

## Repo conventions you must follow

- **Localization IDs.** Each C# user string has an ID: `Localization.LocalizeString("text", 23)`. Keep the ID when you reword a string. A new string gets the next unused ID. At `d78f9eb` the highest is 259, and plan 007 uses IDs from 260 up. Find the current maximum with:
  ```sh
  git grep -hoE "LocalizeString\(\"[^\"]*\", [0-9]+\)" -- rhino/plugin | grep -oE "[0-9]+\)$" | tr -d ')' | sort -n | tail -1
  ```
- **Code under `#if R9 ... if (AIInstances.Preferences.UseSdk)`** belongs to the dormant SDK path (ADR-0003). Leave it alone.
- Don't rename `AISettingsPanel.ai.cs` or any class (ADR-0004).
- Debug builds allow warnings, but don't add new ones.

## Scope

**Edit:**
- `rhino/plugin/UI/AISettingsPanel.ai.cs` (most of the work)
- `rhino/plugin/UI/AISettingsDialog.cs` (title only)
- `rhino/plugin/AISettings.cs`
- `rhino/plugin/Server/McpEndpoint.ai.cs` (two small spots)

**Out of scope. Do not touch:**
- The `#if R9` / `UseSdk` branch in `AISettingsPanel.OnLoad`, and the pages it uses: `UI/Pages/*.cs`, `UI/Controls/*.cs`, `rhino/sdk/**`. That branch calls `LoadingTab()`, so keep `LoadingTab` and `AutoLoadMcpBox`.
- `AISettings.DefaultAgentName`, which `Agents/AgentHost.ai.cs:57` reads.
- `AISettings.AutoLoadMCP` and `AIAutoLoad.cs`.
- The `InPanelOnly` logic in `McpEndpoint.ai.cs`.
- Panel TypeScript (plan 007) and `AgentPrompts.cs` (plan 006).

## Steps

### 1. Baseline

Run the shared gates from `plans/README.md` (R9 build, R8 build, Server.Tests). If they fail before you change anything, STOP and report.

### 2. Always-on agent and tools

**`rhino/plugin/AISettings.cs`**

- Lines 51-52 currently read:
  ```csharp
  public static bool IsEnabled(AgentDefinition def) =>
      def.Enabled && !DisabledAgents.Contains(def.Name, StringComparer.OrdinalIgnoreCase);
  ```
  Change them to:
  ```csharp
  // Owly has one agent and no on/off switch (plan 008); a DisabledAgents value from an older build is ignored.
  public static bool IsEnabled(AgentDefinition def) => def.Enabled;
  ```
- Delete `SetAgentEnabled` (lines 54-60) and the `DisabledAgents` property (lines 31-36) once nothing calls them. The settings panel is the only caller, and step 3 removes that.
- Delete `AgentPrompt` and `SetAgentPrompt` (lines 66-68) once nothing calls them. Plan 006 already made `EffectivePrompt` stop using them.
- Delete the `DisabledTools` property (lines 24-29) after you finish the `McpEndpoint` change below.

**`rhino/plugin/Server/McpEndpoint.ai.cs`**

- In `HandleToolsList` (around lines 141-160), delete the `disabled` set and the `.Where(t => !disabled.Contains(t.Name))` filter. Keep `.Where(t => Filtered || !t.InPanelOnly)`.
- Around line 252, delete the whole `if (Filtered && Rhino.AI.AISettings.DisabledTools.Contains(...)) return new JsonRpcResponse { Error = ... };` block. Keep the `InPanelOnly` check above it.
- Don't change what `Filtered` means or where it's used otherwise.

Verify: `git grep -n "DisabledTools\|DisabledAgents" -- rhino` prints nothing once step 3 is also done.

### 3. The settings panel (`rhino/plugin/UI/AISettingsPanel.ai.cs`)

**Tabs.** In `OnLoad`, the non-SDK branch (lines 61-65) currently adds three tabs:
```csharp
            {
                tabs.Pages.Add(new TabPage { Text = Localization.LocalizeString("AI Agents", 17), Content = AgentsTab() });
                tabs.Pages.Add(new TabPage { Text = Localization.LocalizeString("Tools", 19), Content = ToolsTab() });
                tabs.Pages.Add(new TabPage { Text = Localization.LocalizeString("Loading", 20), Content = LoadingTab() });
            }
```
Replace them with one tab: `Text = Localization.LocalizeString("Owly", 2)` (ID 2 already means "Owly", see `UI/AIOptionsPage.cs:19`) and `Content = AgentsTab()`. Leave the SDK branch above it untouched.

**Agents tab.** Rewrite `AgentsTab()` (lines 158-228) so it returns only the property list for the single agent, without the grid:
- Rows: `NameHeader`, `AvailableLabel`, `LabeledColumn("Model:" (ID 23), ModelBox)`, `LabeledColumn("Found at:" (ID 24), SearchPathsBox)`, and a final `new TableRow { ScaleHeight = true }`.
- Delete the `EnabledBox` row and the `SystemPromptBox` row.
- Keep the `ModelBox.SelectedValueChanged` handler. Delete the `EnabledBox.CheckedChanged` and `SystemPromptBox.TextChanged` handlers.
- Delete the left `StackLayout` holding `AgentGrid`, and the two-cell layout around it. Return the `properties` table wrapped in a `TableLayout` with `Padding = new Padding(8)`.
- Call `LoadEditor()` before returning, as it does now.

**Selection without a grid.** Rewrite `TryGetSelected` (lines 349-358) to return the first row:
```csharp
    // Owly has exactly one agent (ADR-0003), so the editor always shows it.
    private bool TryGetSelected(out AgentRow row)
    {
        row = Rows.FirstOrDefault()!;
        return row is not null;
    }
```

**Delete** these members, and anything left with no caller:
- `AgentGrid`, `EnabledBox`, `SystemPromptBox`
- `BuildGridContextMenu`, `ResetSelected`, `SetSelectedDefault`, `ReloadGrid`
- the Tools tab and all its helpers: `ToolsTab`, `ToolLeaves`, `CategoryOrder`, `CategoryLabel`, `SyncGroupState`, `ScanTools`, `SafeGetTypes`, `ToolInfo`, `ToolNode`
- from `AgentRow`: `SystemPrompt`, `Enabled`, `IsDefault`, `DefaultImage`, `DefaultCheckIcon`, and the matching constructor parameters and `From(...)` arguments
- from `LoadEditor` and `EnableEditor`: the lines touching `EnabledBox` and `SystemPromptBox`
- `using` directives that become unused, such as `System.Reflection`

`SeedRows()` sets `IsDefault`. Simplify it to add one `AgentRow` per definition with no default logic.

**Commit.** In `TryCommit` (lines 95-141):
- Keep the two `foreach (... Pages)` loops. The SDK branch uses them.
- Replace the `foreach (AgentRow row in Rows)` body with only `AISettings.SetAgentModel(row.Name, row.Model);`.
- Delete the `DefaultAgentName` assignment (lines 120-121).
- Delete the whole `DisabledTools` block (lines 125-138).
- `AutoLoadMCP` must be written only when the Loading tab was actually built, which now happens only in the SDK branch. Add a field `private bool LoadingTabShown { get; set; }`, set it to `true` at the start of `LoadingTab()`, and write the setting as `if (LoadingTabShown) AISettings.AutoLoadMCP = AutoLoadMcpBox.Checked == true;`.
- End with `return true;`.

**Class comment.** Line 10 says `// Shared settings UI (AI Agents / MCP Servers / Tools).`. Change it to `// Shared settings UI: in Owly, just the model choice (plan 008).` and keep the rest of the comment.

**Loading tab text.** It is now SDK-only, but still fix the string at line 448. Change `"What RhinoAI starts when Rhino launches. Changes take effect the next time Rhino starts."` to `"What Owly starts when Rhino launches. Changes take effect the next time Rhino starts."` and keep ID 36.

### 4. Dialog title (`rhino/plugin/UI/AISettingsDialog.cs`)

Line 13 reads `Title = Localization.LocalizeString("AI Settings", 10);`. Change the text to `"Owly Settings"` and keep ID 10.

### 5. Build and test

From the repo root, run the shared gates.

## Done criteria

```sh
dotnet build rhino/plugin/RhinoAI.csproj -p:RhinoTarget=R9 -clp:NoSummary   # succeeded, 0 errors
dotnet build rhino/plugin/RhinoAI.csproj -p:RhinoTarget=R8 -clp:NoSummary   # succeeded, 0 errors
dotnet test tests/Server.Tests/Server.Tests.csproj                          # all pass
git grep -n "DisabledTools\|DisabledAgents\|SetAgentPrompt\|SystemPromptBox\|ToolsTab\|AgentGrid" -- rhino/plugin   # nothing
git grep -n "RhinoAI starts" -- rhino/plugin                                # nothing
git grep -n '"AI Settings"' -- rhino/plugin                                 # nothing
git diff --stat d78f9eb -- rhino/plugin/UI/Pages rhino/plugin/UI/Controls rhino/sdk   # empty
```

Manual check by the user in Rhino 9:
1. Open the panel's gear button. The dialog is titled "Owly Settings" and shows one tab, "Owly", with: the agent name, a found/not-found line, a Model dropdown, and the Found-at paths. There is no grid, Enabled box, Prompt box, Tools tab or Loading tab.
2. The Model dropdown lists "(default) - Sonnet (latest)", Sonnet, Opus and Haiku. Pick Opus, Save, ask a question, and confirm the answer comes back. Set it back to "(default)".
3. Rhino Options → Owly shows the same single page.
4. Ask "How do I loft?". The panel shows a "Looked up Rhino commands" tool line, so `get_commands` is offered to the AI.

## Tests

No new automated tests. The settings UI is Eto and isn't covered by Server.Tests. `McpEndpoint` isn't linked into Server.Tests either; check with `git grep -n McpEndpoint tests/Server.Tests/Server.Tests.csproj`. If it is linked, run the tests and fix any test that asserted `DisabledTools` filtering by deleting that assertion. Report it if you do.

## Maintenance

- If a course later needs extra settings (for example a retention period for chat history, from the privacy review), add them as rows in `AgentsTab`'s property table. Don't bring back the agent grid.
- A setting removed here leaves its old stored value in Rhino's PersistentSettings. That's harmless because nothing reads it now. Don't write migration code.

## STOP and report if

- The SDK branch (`#if R9 ... UseSdk`) turns out to call `ToolsTab`, `AgentsTab` or `AgentGrid`.
- `Rows` can be empty at runtime because `AgentRegistry.Instance.AllDefinitions` has no entries. In that case the editor shows "No agent selected" (ID 30); keep that `else` branch in `LoadEditor`. Report it only if the build or the manual check shows a crash.
- Removing `DisabledTools` from `McpEndpoint` breaks a Server.Tests test.
