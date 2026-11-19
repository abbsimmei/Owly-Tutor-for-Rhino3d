# Owly — Phase 1 investigation

Findings from inspecting the inherited McNeel RhinoAI codebase (PRD §6.1, Phase 1). Written against commit `ba4b4a0`, 2026-11-18. Decisions that came out of it are recorded in [../adr/](../adr/), and the work is split into plans in [../../plans/](../../plans/).

## 1. Architecture as inherited

```
Chat panel (TypeScript, rhino/panel → embedded dist/panel.host.html)
   │  WebView messages
   ▼
PanelBridge / AIPanelViewModel (rhino/plugin/UI)      ← C# host, serves the page from a localhost HttpListener
   │  AgentDispatch.PromptActive(doc, UserMessage)
   ▼
AgentDispatch → AgentHost → AgentRunner (rhino/plugin/Agents)
   │  spawns an external CLI per (document, agent)
   ▼
Claude Code CLI (`claude -p`, stream-json)   |   Codex CLI (`codex exec --json`)
   │  MCP over HTTP: http://localhost:{port}/agent
   ▼
McpServer → McpEndpoint → ToolRegistry (rhino/plugin/Server)  ← reflection over [McpServerTool] classes
   ▼
Tools (rhino/plugin/Tools) + Resources (rhino/plugin/src/Resources)
```

The parts are:

- **Plugin** (`rhino/plugin`, assembly `Owly.rhp`, net8.0, plus net48 on Windows). It loads at Rhino startup and registers the panel (`RhinoAIPlugin.cs:24`).
- **Panel**. This is a dependency-free TypeScript app. It is the live UI: `UI/AIPanel.cs` is just an Eto `WebView`. The two sides talk through a typed protocol, with panel commands in `UI/PanelCommands*.cs` and host events in `UI/PanelEvents.ai.cs`, mirrored in `rhino/panel/src/protocol/events.ts`. `rhino/panel/README.md` says the C# host is not written; that is out of date.
- **AI backend.** Each student needs their own CLI installed and signed in. The plugin bundles no AI and holds no API key on this path.
  - The system prompt is built by `AgentPrompts.Compose` (fixed steers plus a per-agent prompt the user can override). Claude receives it through `--append-system-prompt`.
  - Claude is launched with `--tools ""`, which removes the CLI's own Bash, Edit, Write and WebFetch tools, plus `--allowedTools mcp__rhino__*` and `--strict-mcp-config`.
  - Codex runs with a read-only sandbox and no shell tool.
- **MCP server.** Each document gets its own localhost listener with two routes (`McpServer.ai.cs:37-38`):
  - `/` is the external route. It is unfiltered and unauthenticated.
  - `/agent` is the in-panel route. It filters by `AISettings.DisabledTools` (empty by default) and adds `ask_user`.
- **Router** (`rhino/router`). A stdio MCP proxy for *external* clients such as Claude Desktop, Cursor and the cc-plugin. It can launch Rhino instances. The panel does not use it.
- **SDK** (`rhino/sdk`, R9 only). A second, direct-API agent framework (Anthropic, OpenAI, Gemini, local models). It is only used for settings pages behind the hidden `TestAgentic` toggle, and the panel never calls it.
- **ACP** (`rhino/acp`). The Agent Client Protocol library that the agent runner uses.

## 2. What the AI can do today

There are 42 plugin tools, and the panel agent can call all of them.

| Class | Tools | Conflict with PRD §4 |
|---|---|---|
| Executes | `run_command` (RhinoApp.RunScript), `run_python`, `run_csharp`, `manage_plugin_commands` | "Never execute Rhino commands", "never perform modelling operations". Arbitrary code means full machine access |
| Changes the document or view | `open_doc`, `save_doc`, `close_doc`, `set_layer_material`, `set_selection`, `set_camera`, `zoom_to_layer`, `zoom_to_object`, `get_viewport_image` (changes display mode and camera) | "Never create or modify the user's model" |
| Grasshopper | 24 `g1_*` / `g2_*` tools | Grasshopper is out of scope |
| Read-only | `get_commands`, `get_context`, `get_selection`, `list_objects` | Fine. Useful for contextual advice (PRD §5.2) |
| UI | `ask_user` | Fine. Useful for follow-up questions |
| Router control | `_router_spawn_listener`, `_router_close_listener`, `_router_quit_app` | These were meant for the router only, but are visible to every agent |

The `ReadOnly` and `Destructive` attribute flags are hints and nothing enforces them. The SDK's `PermissionSet` is only enforced in the dormant SDK path, and `AIPermissionsPage` is a stub.

The prompt steers always included (`AgentPrompts.cs`) are `AskUserSteer`, `GroundingSteer` ("before acting on existing geometry … moving, filleting, deleting") and `GrasshopperSteer` (build, solve, fix GH graphs). The last two push the AI towards acting.

## 3. Reuse, modify, remove

| Component | Verdict | Why |
|---|---|---|
| Agent pipeline (`AgentDispatch`, `AgentHost`, `AgentRunner`, `StreamJsonAgent`, `ClaudeStreamJsonParser`) | **Reuse** | Works; the CLI's built-in tools are already off |
| WebView panel and bridge | **Reuse**, then simplify (Phases 2-3) | Markdown, code blocks with copy buttons, and clipboard (`AIPanelViewModel.ai.cs:470-474`) already exist, which covers the base of "copy command" |
| `ConversationStore` (history) | **Removed** | The product owner chose no saved conversations ([ADR-0006](../adr/0006-no-saved-conversations.md)) |
| `ask_user` | **Reuse** | Follow-up questions, quizzes |
| Read-only tools | **Reuse** | Context for advice. `get_context` needs its GH parts removed |
| `command_help` resource (docs.mcneel.com fetch) | **Reuse, modify** in Phase 5 | Basis for PRD §5.1. It is a resource, and the Claude CLI with `--tools ""` probably can't read resources, so it should become a tool |
| Eto dialog pattern (`MCPConnectCommand.cs` ConnectDialog) | **Done** | ConnectDialog was deleted in plan 003; the welcome popup (plan 011) follows `AISettingsDialog` |
| `CommandInterceptor` (watches command history) | **Keep in mind** | Currently off. Could detect which command the student ran |
| Execute, mutate and GH tools; `ScriptProjects/`, `RhinoCode/` | **Remove** | [ADR-0002](../adr/0002-delete-tools-not-filter.md) |
| Router, `/` endpoint, MCP* commands, client-config installer, cc-plugin, connector, docs site | **Remove** | [ADR-0001](../adr/0001-in-panel-only.md) |
| Codex and Gemini agents, agent switching | **Remove** | [ADR-0003](../adr/0003-claude-cli-only.md) |
| `TurnUndoCheckpoint`, per-turn revert | **Remove** | Nothing changes the document any more |
| `rhino/sdk`, `TestAgentic` | **Leave dormant** | Not wired to the panel. Revisit if students need API-key access without a CLI |

## 4. Limitations and gaps for later phases

- **Toolbar highlighting (Phase 4).** Nothing in the codebase touches toolbars (`RhinoApp.ToolbarFiles` / RUI). This is entirely new work, and its feasibility on Rhino 8 and 9 for Windows and Mac still needs a spike.
- **Command-line highlighting and insertion (Phase 4).** There is no existing code. `RhinoApp.SetCommandPrompt` is only valid inside a running command. Pre-filling the command line without running anything needs investigation. The PRD forbids auto-executing, so the fallback is copy-to-clipboard plus a visual cue.
- **Welcome popup (Phase 3).** There is no first-run flag yet. It belongs in `AISettings` (PersistentSettings keyed by the plugin GUID). Done in plan 011: AISettings.WelcomeShown, WelcomeDialog, the OwlyWelcome command.
- **Owl icon (Phase 3).** There is no owl artwork. The panel icon comes from `art/Panel_*.ico`. Bug: both icon constants in `RhinoAIPlugin.cs:12-13` point at the dark icon.
- **Docs access (Phase 5).** Only command pages are fetched, English only, with HTML stripped by regex. There is no search.
- **Every student needs a Claude subscription** plus the Claude Code CLI. That is a real cost and setup barrier for a course. It is recorded in ADR-0003.
- **Coexistence with Rhino 9's built-in RhinoAI.** Only the plugin GUID and assembly name were changed. The panel GUID, the command names and the yak package still collide ([ADR-0004](../adr/0004-coexist-with-builtin-rhinoai.md)).
- **CI.** `build.yml` checks for `RhinoAI.rhp`, which is no longer produced, and its release job publishes to McNeel's yak feed.

## 5. Privacy review (PRD §7)

| Data | Where | Concern |
|---|---|---|
| Conversations: prompts, answers, tool arguments and results, token cost | In memory only, discarded when the conversation ends (plan 009) | Resolved by ADR-0006. Old builds' saved conversations are deleted on load |
| CLI transcripts | Claude's own store under the user's profile | Outside Owly's control; Claude Code deletes them after about 30 days by default. Must be disclosed (welcome popup, plan 011). Phase 5 option: --no-session-persistence (ADR-0006) |
| Content sent to the AI provider | Prompts, attachments (inline base64), tool results (object lists, selection), host OS and Rhino version, installed plugin list | Must be disclosed. Removing `get_viewport_image` stops viewport images being sent |
| User's own Claude settings and hooks | Loaded because `--setting-sources ""` isn't passed | A student's hooks could run inside Owly's agent. Fixed in plan 005 |
| Codex temp images, `auth.json` copy | `%TEMP%/rhinoai-*.png` (never deleted), `%APPDATA%/RhinoAI/Codex` | Removed along with Codex |
| Unauthenticated localhost `/` endpoint | `McpServer.ai.cs` | Any local process can call the tools. Removed (ADR-0001) |
| Telemetry | None found | — |

Unresolved: `PRIVACY.md` only describes the old Claude Desktop connector and needs rewriting for Owly. The GDPR basis (Anthropic as processor, student consent text) has not been decided.

## 6. Open questions for Phases 2-5

1. Can a command be placed on Rhino's command line without running it, on both Windows and Mac?
2. Which toolbar API allows a button to be highlighted or flashed, and does it work for Rhino 8 and 9?
3. Does the Claude CLI with `--tools ""` still expose MCP resource reads? If not, convert `command_help` to a tool.
4. Is a Claude subscription acceptable for students, or should Owly move to the SDK's direct-API path with a course-provided key?
