# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

File-naming rule for AI-authored C#: a new C# file written by an AI agent is named `*.ai.cs`. Editing an existing file does not rename it. (There is no `AGENTS.md` in this repo.)

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
- **The Claude Code CLI is the only AI backend** (ADR-0003). It is launched with `--tools ""`, `--setting-sources ""`, `--strict-mcp-config`, `--system-prompt` (Owly's prompt replaces Claude Code's default, ADR-0005) and only the `rhino` MCP server (`Agents/Conversations/ClaudeStreamJsonParser.ai.cs`). Keep all of that.
- **Students can't edit Owly's prompt, tools or on/off state** (ADR-0005, plan 008). The settings dialog offers the model and "Delete all Owly memory" (plan 025, which empties `%AppData%\Owly` after a confirmation). Plans 028 and 029 add the main-memory switch and the level; each updates this sentence. After changing `AgentPrompts.cs`, run the manual checklist in [docs/owly/prompt-checks.md](docs/owly/prompt-checks.md). Commands in answers are inline code with a leading underscore, such as `_Loft`; Phase 4's copy buttons will rely on that format.
- **Owly is used only from its in-Rhino panel** (ADR-0001). There is no router and no external MCP endpoint, only `/agent` on localhost.
- Make small, targeted changes. Reuse the working systems and don't rewrite them. Ask the user before making a decision that changes the architecture or the product's behaviour.
- **Projects and conversations are saved locally** (ADR-0009, plans 025-026). Students name projects, and `%AppData%\Owly` (`Agents/Memory/`, through `OwlyStore`, shared by Rhino 8 and 9) holds `projects.json` (which .3dm paths belong to which project) and `projects/<projectId>/conversations/<sessionId>.json`, one file per conversation (`ConversationStore`; a conversation with no project is filed under `unsorted`). Nothing is written into the .3dm. A conversation is saved after every turn and when it ends (`AgentHost.SafeDispose`). Reopening a file starts an empty conversation; the saved ones are the owls in the strip on the panel's right edge (`conversationStrip.ts`), and the open conversation always belongs to the panel's current project. Opening one continues it with the CLI's `--resume <session id>`; if Claude Code no longer has that session (about 30 days), the CLI rejects it, the session id is rotated and the student's message is retried once as a fresh session, with the capped saved transcript (`ResumeContext`) at the start of that stdin message. Never put that context, notes or anything long on the command line (cmd.exe's 8,191 limit). The CLI always starts in `%AppData%\Owly\cli` (`OwlyMemory.CliDirectory`), because Claude Code finds a session only from the folder it was started in; "Delete all Owly memory" keeps that folder. Stop still relies on `--resume` and `StreamJsonAgent.HasEverStarted`. Deleting a conversation, a project or all memory marks the open conversation `Discarded`, so nothing saves it again. Plans 027-028 add project notes and the main memory. Claude Code's own copy of each session is disclosed in the welcome popup.
- **Panel actions run only on a student's click** (ADR-0007, ADR-0008). A command chip copies the command and focuses Rhino. The toolbar icon opens the "where to find" popover. A click on **Run** in a command's card starts a non-blocked command (blocklist and checks in `UI/CommandBlocklist.ai.cs`; the host re-checks every Run, refuses aliases, unknown names and a busy Rhino), and Rhino then waits for the student's picks. Owly never types into the command line, never supplies picks, and never shows, hides or changes a toolbar. The AI cannot trigger any of this. The one click-less action is the option probe (`UI/CommandOptionProbe.ai.cs`, ADR-0008): from Idle, on Rhino 9 and later, under six guards (not in a command, nothing selected, not blocked, the help page lists options, Rhino 9+, English UI), it starts a command with `_X _Cancel` and reads the first prompt's options and current values. It is the only file that contains `_Cancel`, it runs nothing else, and the AI cannot trigger it.
- **The owl art in `art/owly*.png` belongs to the team** (Git LFS). Never create, redraw, re-encode or resize it. Load it only through `UI/OwlyArt.ai.cs` (C#) or `src/ui/owl.ts` (panel). Every path must keep the PNG's alpha, so transparent re-exports can be swapped in as plain files. Never give the panel's `.owl` element a background, filter, blend mode or rounded clipping; `npm run verify` checks this. `rhino/plugin/Owly.rui` embeds its own copy of `owly-small*.png` (plan 012 explains how to re-embed it).
- Only user-visible identifiers say Owly: the panel GUID, the `Owly` and `OwlyWelcome` commands, the `Owly.rui` toolbar, the yak package and the `Owly.rhp` assembly (ADR-0004). The csproj file name, the `Rhino.AI` namespace and class names like `RhinoAIPlugin` deliberately keep RhinoAI. If you add a command, give it an `Owly` prefix so it can't clash with the RhinoAI plugin built into Rhino 9.

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
  - `Agents/`: starts the Claude Code CLI as a child process (`StreamJsonAgent`) and drives it through ACP (`AgentRunner`). `Agents/AgentPrompts.cs` holds Owly's complete system prompt (ADR-0005). That file is where Owly's identity and teaching rules belong.
  - `Server/` + `Tools/`: the in-process MCP server. Tools are classes marked `[McpServerToolType]`/`[McpServerTool]` that `ToolRegistry` finds by reflection; there is no central list.
  - `src/Resources/`: MCP resources. `command_help` fetches pages from docs.mcneel.com. The Claude CLI probably can't read resources, so it may need to become a tool later. Its `FetchHtml` also feeds the options card (plan 023): `UI/CommandOptionsSource` → `CommandOptionsParser` (help page), merged by `OptionMerge` with `CommandOptionProbe` + `ProbeLineParser` (plan 024), written by `RunMacro`.
  - `UI/`: the docked WebView panel. `PanelBridge` and the `PanelEvents`/`PanelCommands` records carry the protocol between C# and TypeScript. `PanelStrings.ai.cs` holds the panel's user-facing text. It also holds `OwlyArt.ai.cs` (the only C# loader of the owl art), `WelcomeDialog.ai.cs` (the welcome popup; the `OwlyWelcome` command and "About Owly" in settings reopen it) and `FirstRunLayout.ai.cs`.
  - First start: `RhinoAIPlugin.OnFirstIdle` runs once-per-Rhino-version steps, each guarded by an `AISettings` flag that is set before the step runs:
    - delete conversations saved by older builds
    - `FirstRunLayout`: add the Owly panel as a tab next to Layers or Properties, and switch on the Owly toolbar, which Rhino loads hidden
    - show the welcome popup (`WelcomeShown`)

    Put new start-up steps before the welcome block, which can return early.
  - `Owly.rui`: a one-button toolbar (left click `_Owly`, right click `_OwlyWelcome`), hand-written and copied next to every `Owly.rhp`. Rhino auto-loads a `.rui` named like the plugin. Keep its GUIDs.
  - The C# panel records (`PanelEvents.ai.cs`, `PanelCommands*.cs`) still carry fields the panel no longer shows, such as plan, chips and context. They are kept on purpose because Server.Tests compiles them. Remove both sides together or not at all.
  - `AISettings.cs`: settings stored in Rhino's PersistentSettings (per Rhino version), keyed by the plugin GUID. That GUID is written by hand in both this file and `Properties/AssemblyInfo.cs`, and the two must match.
- **`rhino/panel/`**: the chat UI as a dependency-free TypeScript web app. `src/protocol/events.ts` defines the protocol between panel and host.
- **`rhino/acp/`**: the Agent Client Protocol library that the agent runner uses. `src/Generated/` is codegen output.
- **`rhino/sdk/`**: an R9-only, direct-API agent SDK. It is not connected to the panel and is kept on purpose (ADR-0003); don't build on it without a new ADR.
