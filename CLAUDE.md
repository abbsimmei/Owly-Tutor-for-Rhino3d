# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

File-naming rules for AI-authored C# (`*.ai.cs`) are in @AGENTS.md. Follow them.

## Project: Owly

Owly is a fork of McNeel's RhinoAI. It is being turned into a **Rhino tutor for architecture students**. [PRD.md](PRD.md) is the source of truth. Its core rule: **Owly only helps.** It never creates or changes geometry, never runs Rhino commands for the user, and never invents commands. Instead it recommends tools and commands, explains them, highlights the matching toolbar button or the command line, and lets the user copy commands. Grasshopper is out of scope.

What this means for code changes:
- Enforce the PRD restrictions in two places: in the prompts, and by removing or blocking tools. Most of the inherited MCP tools in `rhino/plugin/Tools/` change the model, and GH1/GH2 is all Grasshopper. These tools are what needs removing or restricting.
- Make small, targeted changes. Reuse the working systems and don't rewrite them. Ask the user before making a decision that changes the architecture or the product's behaviour.
- Many identifiers still say `RhinoAI` (the csproj, namespaces, the yak package). Rename them only on purpose.

## Commands

The .NET 8 SDK is required. In Release builds, warnings are errors.

```sh
dotnet build rhino/plugin/RhinoAI.csproj -p:RhinoTarget=R9   # plugin (R8 also supported)
dotnet build rhino/router/Router.csproj
dotnet test tests/Router.Tests/Router.Tests.csproj
dotnet test tests/Server.Tests/Server.Tests.csproj
dotnet test tests/Server.Tests/Server.Tests.csproj --filter "FullyQualifiedName~SomeTest"  # single test
```

`tests/Integration.Tests` needs a running Rhino, and CI does not run it. In VS Code, **Run and Debug** builds the plugin, launches Rhino and starts the MCP server. Tasks for packing and installing the yak package are in [.vscode/tasks.json](.vscode/tasks.json).

Panel (TypeScript, in `rhino/panel/`):
```sh
npm install && npm run build   # writes dist/panel.host.html, which is embedded in the plugin
npm run dev                    # dev server on :5173 against a mock host (no Rhino needed)
npm test                       # unit tests; npm run typecheck; npm run verify (browser checks)
```
Rebuild the panel before building the plugin if `rhino/panel/src` has changed.

## Architecture

- **`rhino/plugin/`**: the Rhino plugin (`.rhp`, net8.0 and net48 on Windows).
  - `Agents/`: starts an external agent CLI (Claude Code, Codex, Gemini) as a child process and talks to it over stream-JSON or ACP. Agents are defined in `Agents/Definitions.json`. `Agents/AgentPrompts.cs` holds the steer text that is always prepended to the system prompt. That file is where Owly's identity and restrictions belong.
  - `Server/` + `Tools/` + `Resources/`: Rhino's in-process MCP server. Tools are attribute-annotated classes that `ToolRegistry` discovers. This is the toolset the agent sees.
  - `UI/`: the docked panel. `PanelBridge` and `PanelEvents`/`PanelCommands` host the web panel in a WebView and pass events between C# and TypeScript.
- **`rhino/panel/`**: the chat UI as a dependency-free TypeScript web app. `src/protocol/events.ts` defines the wire protocol between panel and host.
- **`rhino/router/`**: a stdio MCP router (`rhino-mcp-router`). The agent CLI connects to the router, and the router proxies to one or more Rhino instances, launching them if needed.
- **`rhino/sdk/`**, **`rhino/acp/`**: an R9-only agent and model SDK, and the Agent Client Protocol library. `acp/src/Generated/` is codegen output.
- `cc-plugin/`, `connector/`, `docs/` and the `.github/workflows` are inherited RhinoAI distribution. They are not core to Owly.
