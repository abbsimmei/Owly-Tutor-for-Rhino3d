# 005: Claude-only agent, hardening, panel cleanup

- **Written against:** `ba4b4a0` (apply on top of plan 004)
- **Status:** TODO
- **Effort:** M
- **Risk:** Medium (touches C# and the TypeScript panel)

## Why

ADR `docs/adr/0003-claude-cli-only.md`: Owly supports only the Claude Code CLI. Codex, Gemini and agent switching are removed. Two holes are also closed:

1. Claude is launched without `--setting-sources ""`, so a student's own `~/.claude` settings, hooks and CLAUDE.md load into Owly's agent.
2. User-configured "extra MCP servers" are merged in and pre-approved (`--allowedTools mcp__<server>__*`), which gets around the read-only toolset.

Finally, panel UI that only made sense for removed features goes: the agent switcher, Grasshopper icons and strings, per-turn undo, and the plugin-commands capability.

## Scope

### Delete these

- `rhino/plugin/Agents/Conversations/CodexStreamJsonParser.ai.cs`
- `rhino/plugin/Agents/CodexHome.ai.cs`
- `rhino/plugin/Agents/codex-config.toml`
- `rhino/plugin/Agents/GeminiConnection.ai.cs`
- `rhino/plugin/Agents/TurnUndoCheckpoint.ai.cs`

### Edit these

- C#:
  - `rhino/plugin/Agents/Definitions.json`
  - `rhino/plugin/Agents/AgentDefinition.cs`
  - `rhino/plugin/Agents/AgentHost.ai.cs`
  - `rhino/plugin/Agents/AgentDispatch.ai.cs`
  - `rhino/plugin/Agents/AgentRunner.ai.cs` (only if it passes extra MCP servers)
  - `rhino/plugin/Agents/Conversations/ClaudeStreamJsonParser.ai.cs`
  - `rhino/plugin/AISettings.cs`
  - `rhino/plugin/UI/AISettingsPanel.ai.cs`
  - `rhino/plugin/UI/AIPanelViewModel.ai.cs`
  - `rhino/plugin/UI/PanelStrings.ai.cs`
  - `rhino/plugin/RhinoAI.csproj`
- TypeScript:
  - `rhino/panel/src/ui/agentMenu.ts`
  - `rhino/panel/src/state/tools.ts`
  - `rhino/panel/src/state/tools.test.ts`
  - `rhino/panel/src/ui/icons.ts`
  - `rhino/panel/src/i18n/strings.ts`
  - `rhino/panel/src/protocol/mockHost.ts`
  - `rhino/panel/tests/host-events.json`
  - `rhino/panel/tools/verify.mjs`
  - `rhino/panel/README.md`
- `rhino/panel/dist/panel.host.html` (rebuilt, not hand-edited)

## Out of scope

- `rhino/sdk` and its Codex/Gemini models and harnesses. They stay dormant (ADR-0003).
- `rhino/acp`. Keep it.
- The full tutor UI and the Owly prompt (later plans).
- `ConversationStore` / history. Keep it.

## Steps

### A. Agents

1. **`Agents/Definitions.json`.** Delete the `codex` entry (and any `gemini` entry) so only `claude` remains. Keep the JSON valid.

2. **`AgentDefinition.cs`.** `GetRunner` currently reads:
   ```csharp
   "claude" => new AgentRunner(this, docTitle, (client, convo, cwd) => new StreamJsonAgent(this, client, convo, cwd, new ClaudeStreamJsonParser(this))),
   "codex" => ...CodexStreamJsonParser(this, CodexHome.Prepare())...,
   "gemini" => new AgentRunner(this, docTitle, (client, _, cwd) => GeminiConnection.Connect(this, client, cwd)),
   ```
   Delete the `codex` and `gemini` arms.

3. **`AgentHost.ai.cs`.** In `CreateResumed` (around lines 108-128), delete the `codex` and `gemini` cases.

4. **Delete** the five files in "Delete these". In `RhinoAI.csproj`, delete `<EmbeddedResource Include="Agents/codex-config.toml" ... />`.

5. **`AgentDispatch.ai.cs`.** Around lines 204-214, a `TurnUndoCheckpoint` wraps each turn in a Rhino undo record. Delete it and its using/dispose, because nothing can modify the document any more.

6. **Build** with R9, fix the remaining `Codex*` / `Gemini*` / `TurnUndoCheckpoint` references, and check: `git grep -niE "codex|gemini|TurnUndoCheckpoint" -- rhino/plugin` prints nothing.

### B. Hardening

7. **`ClaudeStreamJsonParser.ai.cs` `ConfigureArguments`.**
   - Delete the loop that merges `mcpServers` entries into `servers`, so only the `rhino` server remains.
   - Change `allowedTools` to the literal `"mcp__rhino__*"`.
   - Add two arguments right after `psi.AddArgument("--strict-mcp-config");`:
     ```csharp
     psi.AddArgument("--setting-sources");
     psi.AddArgument(string.Empty);
     ```
   - Change the stale comment `// The agent's cwd is the folder holding the user's .3dm, so Bash/Write/Edit point at it.` to `// No built-in tools: the agent can only use Owly's read-only MCP tools.`
   - Keep the `IReadOnlyList<string> mcpServers` parameter only if it is part of an interface; otherwise remove it and its callers' argument.

8. **`AISettings.cs`.** Delete `ExtraMcpServersJson` (lines 96-100). Fix the callers: the agent runner's resolution of extra servers, and the "MCP Servers" tab in `AISettingsPanel.ai.cs`. Delete that tab entirely, including its page class if it is defined only there.

8b. **`rhino/plugin/Tools/GetCommandsTool.cs:11`** (added after plan 004's review). The AI can see this `[Description]`, and it still says "Discover Rhino command names available to run_command … before calling run_command". That tool no longer exists. Replace it with:
   `"List Rhino command names (English) from all registered plugins, including ones not yet loaded. Use it to verify a command exists before recommending it to the user; you cannot run commands. Use filter to narrow the list."`
   Also in `rhino/plugin/Tools/GetContextTool.ai.cs`, change the class comment wording "so the agent can orient before acting" to "so the agent can orient before advising".
   Verify: `git grep -n "run_command" -- rhino/plugin` prints nothing.

### C. Panel (C# side)

9. **`AIPanelViewModel.ai.cs`.**
   - The `agent.select` handling (around lines 299-337) lets the user switch agents. Keep `agent.login`. Make `agent.select` a no-op that logs, so an old panel page doesn't crash.
   - In the capabilities payload (around line 500), set `Grasshopper: false` and `UndoTurn: false`. If these are record fields shared with `PanelEvents.ai.cs` (linked into Server.Tests), keep the fields and only change the values.

10. **`PanelStrings.ai.cs`.** Lines 17-26 hold the empty-state text and starter prompts ("It can read the document, run scripts, drive Grasshopper…", "Build a twisty tower in Grasshopper"). Line 105 holds the composer placeholder ("Ask, or describe what you want built…"). Replace them with neutral tutor text:
    - Empty state: `"Ask how to do something in Rhino. Owly explains which tools and commands to use. You do the modelling."`
    - Starter prompts: `"How do I loft between curves?"`, `"What's the difference between Join and Group?"`, `"How do I make a wall with openings?"`
    - Placeholder: `"Ask Owly how to do something in Rhino…"`
    - The busy verbs ("Grazing", "Ruminating") can stay. Final wording comes in Phase 3.

### D. Panel (TypeScript side)

11. Before editing, run `cd rhino/panel && npm install && npm test && npm run typecheck` once to get a baseline.

12. **Grasshopper:**
    - In `src/state/tools.ts`, delete the `g1_` / `g2_` mappings and the `grasshopper` family (around lines 8-12, 43, 53, 61-62). Update `src/state/tools.test.ts` to match.
    - In `src/ui/icons.ts:73-74`, delete the GH1/GH2 wordmark icons.
    - In `src/i18n/strings.ts:13,15`, delete the GH strings.
    - In `src/protocol/mockHost.ts`, `tests/host-events.json` and `tools/verify.mjs`, delete or replace any scenario that uses g1/g2 tools, `run_python` or `run_command`. Replace it with a `get_selection` call where a tool call is needed.
    - Keep the `grasshopper` capability field in `src/protocol/events.ts` if the C# side still sends it.

13. **Agent switcher.** In `src/ui/agentMenu.ts`, hide the agent selector when the host reports one agent or fewer. Keep sign-in.

14. **`rhino/panel/README.md`.** Replace the "Wiring it to Rhino" and "Not done" sections with one paragraph: the C# host exists (`rhino/plugin/UI/PanelBridge.cs`, `AIPanelViewModel*.cs`) and the panel is embedded as `dist/panel.host.html`.

15. **Rebuild:** `cd rhino/panel && npm run typecheck && npm test && npm run build`. Commit the regenerated `dist/panel.host.html`.

## Done criteria

- `cd rhino/panel && npm run typecheck && npm test` pass.
- The shared gates pass (R9, R8, Server.Tests).
- `git grep -niE "codex|gemini" -- rhino/plugin` prints nothing.
- `git grep -nE "\bg[12]_|run_command|run_python" -- rhino/panel/src rhino/panel/tests rhino/panel/tools` prints nothing.
- `git grep -n "ExtraMcpServers" -- rhino` prints nothing.
- `git grep -n -- '"--setting-sources"' rhino/plugin` prints one line.
- Manual check by the user in Rhino:
  - The panel shows no agent switcher.
  - A question gets an answer.
  - Add a test hook to `~/.claude/settings.json`, ask Owly something, and confirm the hook does not fire. Then remove the hook.

## Tests

Update `tools.test.ts` (step 12). No new C# tests.

## Maintenance

- Adding a second agent later means re-applying the same hardening (no built-in tools, `--setting-sources ""`, only the `rhino` MCP server) and writing an ADR that supersedes ADR-0003.
- Every panel change needs `dist/panel.host.html` rebuilt.

## STOP and report if

- `--setting-sources` with an empty value is rejected by the installed Claude CLI. Check with `claude --help`. Report the CLI version and the error instead of guessing at another flag.
- Removing `agent.select` breaks a type shared with `PanelEvents.ai.cs` / `PanelCommands*.cs` that Server.Tests compiles.
