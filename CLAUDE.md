# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

File-naming rules for AI-authored C# (`*.ai.cs`) are in @AGENTS.md. Follow them.

## Project: Owly

Owly is a fork of McNeel's RhinoAI that has been turned into a **Rhino tutor for architecture students**. [PRD.md](PRD.md) is the source of truth. Its core rule: **Owly only helps.** It never creates or changes geometry, never runs Rhino commands for the user, and never invents commands. Instead it recommends tools and commands, explains them, highlights the matching toolbar button or the command line, and lets the user copy commands. Grasshopper is out of scope.

Design records:
- [docs/owly/phase1-investigation.md](docs/owly/phase1-investigation.md): architecture, limitations and the privacy review.
- [docs/adr/](docs/adr/): decisions.
- [GLOSSARY.md](GLOSSARY.md): vocabulary.
- [plans/](plans/): implementation plans and their status.

Read the relevant ADR before changing something it covers. If a change overturns an ADR, write a new ADR that supersedes it.

Rules for code changes:
- **Every agent tool must be read-only** (ADR-0002). The AI can see exactly five tools: `get_commands`, `get_context`, `get_selection`, `list_objects` and `ask_user`. Code that executes, mutates or touches Grasshopper was deleted, not disabled. Don't bring it back.
- **The Claude Code CLI is the only AI backend** (ADR-0003). It is launched with `--tools ""`, `--setting-sources ""`, `--strict-mcp-config` and only the `rhino` MCP server (`Agents/Conversations/ClaudeStreamJsonParser.ai.cs`). Keep all of that.
- **Owly is used only from its in-Rhino panel** (ADR-0001). There is no router and no external MCP endpoint, only `/agent` on localhost.
- Make small, targeted changes. Reuse the working systems and don't rewrite them. Ask the user before making a decision that changes the architecture or the product's behaviour.
- Only user-visible identifiers say Owly: the panel GUID, the `Owly` command, the yak package and the `Owly.rhp` assembly (ADR-0004). The csproj file name, the `Rhino.AI` namespace and class names like `RhinoAIPlugin` deliberately keep RhinoAI. If you add a command, give it an `Owly` prefix so it can't clash with the RhinoAI plugin built into Rhino 9.

## Commands

The .NET 8 SDK is required. In Release builds, warnings are errors.

```sh
dotnet build rhino/plugin/RhinoAI.csproj -p:RhinoTarget=R9   # output: rhino/plugin/bin/R9-win/Debug/net8.0/Owly.rhp
dotnet build rhino/plugin/RhinoAI.csproj -p:RhinoTarget=R8   # on Windows this also builds net48
dotnet test tests/Server.Tests/Server.Tests.csproj
dotnet test tests/Server.Tests/Server.Tests.csproj --filter "FullyQualifiedName~SomeTest"  # single test
```

`tests/Server.Tests` links Rhino-free plugin source files by explicit path (`<Compile Include="../../rhino/plugin/..."/>`). If you delete or rename one of those files, update the list too. Nothing tests Owly end to end automatically, so behaviour changes need a manual check in Rhino (the "Rhino 9" / "Rhino 8" configurations in `.vscode/launch.json`).

Panel (TypeScript, in `rhino/panel/`):
```sh
npm install && npm run build   # writes dist/panel.host.html, which is committed and embedded in the plugin
npm run dev                    # dev server on :5173 against a mock host (no Rhino needed)
npm test                       # unit tests; npm run typecheck; npm run verify (browser checks)
```
After changing `rhino/panel/src`, rebuild and commit `dist/panel.host.html`. The panel has its own copy of the host's tool list and capabilities (`src/protocol/mockHost.ts`, `tests/host-events.json`), so keep it in sync with the C# side.

## Architecture

How a message travels: panel → `PanelBridge` → `AgentDispatch` → Claude Code CLI → `http://localhost:{port}/agent` (MCP) → read-only tools.

- **`rhino/plugin/`**: the Rhino plugin (`Owly.rhp`; net8.0, plus net48 on Windows).
  - `Agents/`: starts the Claude Code CLI as a child process (`StreamJsonAgent`) and drives it through ACP (`AgentRunner`). `Agents/AgentPrompts.cs` holds the steer text that is always prepended to the system prompt. That file is where Owly's identity and teaching rules belong.
  - `Server/` + `Tools/`: the in-process MCP server. Tools are classes marked `[McpServerToolType]`/`[McpServerTool]` that `ToolRegistry` finds by reflection; there is no central list.
  - `src/Resources/`: MCP resources. `command_help` fetches pages from docs.mcneel.com. The Claude CLI probably can't read resources, so Phase 5 should turn this into a tool.
  - `UI/`: the docked WebView panel. `PanelBridge` and the `PanelEvents`/`PanelCommands` records carry the protocol between C# and TypeScript. `PanelStrings.ai.cs` holds the panel's user-facing text.
  - `AISettings.cs`: settings stored in Rhino's PersistentSettings, keyed by the plugin GUID. That GUID is written by hand in both this file and `Properties/AssemblyInfo.cs`, and the two must match.
- **`rhino/panel/`**: the chat UI as a dependency-free TypeScript web app. `src/protocol/events.ts` defines the protocol between panel and host.
- **`rhino/acp/`**: the Agent Client Protocol library that the agent runner uses. `src/Generated/` is codegen output.
- **`rhino/sdk/`**: an R9-only, direct-API agent SDK. It is not connected to the panel and is kept on purpose (ADR-0003); don't build on it without a new ADR.
