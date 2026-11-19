# Plans: Owly Phase 1 and removal of unnecessary functionality

Written by an advisor pass (`/improve` + `/grill-with-docs`) against commit `ba4b4a0` on `rhino-9.x`. Background: [docs/owly/phase1-investigation.md](../docs/owly/phase1-investigation.md). Decisions: [docs/adr/](../docs/adr/).

## Execution order

Plans must run in order; each builds on the previous one.

```
001 (docs) ──► 002 identity + CI baseline ──► 003 remove external MCP ──► 004 remove acting tools ──► 005 Claude-only + hardening + panel
```

| # | Plan | Status | Depends on |
|---|---|---|---|
| 001 | Phase 1 investigation, ADRs, glossary | DONE (written by the advisor) | — |
| 002 | [Identity and verification baseline](002-identity-and-ci-baseline.md) | DONE: `9ff86d6` on `owly/phase1-removal`. The Rhino 9 side-by-side check is still manual | 001 |
| 003 | [Remove the external MCP surface](003-remove-external-mcp-surface.md) | DONE: `c7d3f44`. Server.Tests 141 passed (30 router/config tests deleted) | 002 |
| 004 | [Remove execute/mutate/Grasshopper tools](004-remove-acting-tools.md) | DONE: `27e6bb7`. 5 agent tools remain. The leftover GH strings are handled in 005; the remaining `RunScript` is the `OwlyPanel` alias command, which the AI can't reach | 003 |
| 005 | [Claude-only agent, hardening, panel cleanup](005-claude-only-hardening-panel.md) | DONE: `01f1c00`. Panel tests 40/40. Manual Rhino checks still open | 004 |

Status values: TODO, IN PROGRESS, DONE, BLOCKED (add a note).

## Phase 2: Owly Foundation

Written by an advisor pass (`/improve` + `/grill-with-docs`) against commit `d78f9eb` on `rhino-9.x`. New decision: [ADR-0005](../docs/adr/0005-owly-owns-system-prompt.md) (Owly replaces Claude Code's system prompt).

The PRD's Phase 2 bullet "Restrict geometry generation and automated modelling" is already done by plans 003-005: five read-only agent tools, no built-in CLI tools, no user settings or hooks. Plans 006-008 cover the other three bullets.

```
006 Owly identity prompt ──► 007 panel simplification ──► 008 settings simplification
```

The order is linear. 007 and 008 depend on 006's prompt rules (the tool phrases, and keeping `get_commands` always available), and 007 and 008 edit neighbouring C# files.

| # | Plan | Status | Depends on |
|---|---|---|---|
| 006 | [Owly identity and system prompt](006-owly-identity-prompt.md) | DONE: `e8ae3e8`, merged into `rhino-9.x`. Advisor review approved. R9 and R8 build clean, Server.Tests 148 passed (141 + 7 new). The manual Rhino check (prompt-checks rows 1, 2, 5, 6) is still open | 005 |
| 007 | [Simplify the chat panel for students](007-panel-simplification.md) | DONE: `dcf7acd`, merged into `rhino-9.x`. Advisor review approved. Panel typecheck clean, 40/40 tests, verify all passed (the empty-state check failed before and now passes). C# builds clean, 148 tests. Approved deviations: verify checks for removed UI also deleted (unknown-call note, chip assertion, failed-card auto-expand), and the `previews` CSS section deleted. Manual Rhino check still open | 006 |
| 008 | [Simplify Owly's settings to a model choice](008-settings-simplification.md) | DONE: `fe2f3e7`, merged into `rhino-9.x`. Advisor review approved. R9 and R8 build clean, 148 tests. Approved deviation: the `IsEnabled` comment avoids the literal word `DisabledAgents` so the done-criteria grep stays clean. Manual Rhino check still open | 007 |

Decisions the product owner made in the grilling session (each plan repeats the ones it needs):
- **Prompt.** Answers mirror the student's language. Every command is checked with `get_commands` and written as `` `_Loft` ``. A "make it for me" request gets a one-sentence refusal, then a plain-text offer to teach. Grasshopper and other software are redirected to native Rhino. Answers end with a one-line plain-text offer of more detail, and the `ask_user` overlay is only for real choices. Reliability rules (PRD §8) are included now.
- **Model.** Sonnet is the default. Only Sonnet, Opus and Haiku are listed.
- **Panel.** Revert, Ask again, the plan strip, tool chips and previews, the @-mention menu, and `/agent` and `/login` are deleted. Tool cards become one plain line. Token counts stay.
- **Settings.** Only the model dropdown remains.

## Phase 3: Basic Interface

Written by an advisor pass (`/improve` + `/grill-with-docs`) against commit `17f9ef6` on `rhino-9.x`. New decision: [ADR-0006](../docs/adr/0006-no-saved-conversations.md) (no saved conversations). New glossary terms: **Conversation**, **Transcript**, **Welcome popup**.

The PRD's "Create the chat panel" and "message input" already exist. They get no plan, only a manual check in 010 that the panel docks on the right the first time it opens.

```
009 remove saved history ──► 010 owl icon everywhere ──► 011 welcome popup ──► 012 Owly toolbar
```

The order is linear. 010 edits the same panel files as 009. 011 needs 009's `OnFirstIdle` and 010's `OwlyArt`. 012's button runs 011's `OwlyWelcome` and uses 010's art.

| # | Plan | Status | Depends on |
|---|---|---|---|
| 009 | [Remove saved conversation history](009-remove-saved-history.md) | DONE: `a2b0ef7` on `owly/phase3-exec`. Advisor review approved. R9 and R8 build clean, Server.Tests 148, panel typecheck clean, 40/40 tests, verify all passed. Approved deviations: removed the now-unused `store` destructure in `app()`, and three extra stale comments reworded. Manual Rhino check still open | 008 |
| 010 | [Owly's owl icon everywhere](010-owly-icon-everywhere.md) | DONE: `970fa14` on `owly/phase3-exec`. Advisor review approved. R9 and R8 build with 0 warnings, Server.Tests 148, panel typecheck clean, 40/40 tests, verify all passed including the 4 owl checks. Art is byte-identical. Approved deviations: removed `icon` imports that became unused, and kept the `question` icon (still used by `state/tools.ts`). Known plan gap: the done-criteria reference grep also hits a commented-out line in out-of-scope `rhino/sdk/PlugIns/PlugInRegistry.cs:196`, which is harmless. The manual Rhino check is still open; the panel-tab icon (resource-id overload) and the Plug-in Manager icon (PNG) are unverified in Rhino | 009 |
| 011 | [Welcome popup](011-welcome-popup.md) | DONE: `5186e30` on `owly/phase3-exec`. Advisor review approved. R9 and R8 build with 0 warnings, Server.Tests 148. IDs 267-276 assigned; only ID 2 is shared, on purpose. Approved deviation: the headless check and flag read/write share one try/catch, which returns without setting the flag if the read throws, as the plan intended. Manual Rhino check still open | 010 |
| 012 | [A big Owly toolbar button](012-owly-toolbar.md) | DONE: `33758e6`. The plan was revised so the file is hand-written: its format is modelled on a Rhino-generated plug-in `.rui`, and the owl PNG is embedded byte for byte in an SVG wrapper. Advisor review approved. R9 and R8 build with 0 warnings, Server.Tests 148, `Owly.rui` sits next to every `Owly.rhp`. Plan check-command bugs (not code): the step-1 grep pattern and the step-3 `href` property access; the executor re-checked with working commands. The manual Rhino check is still open: whether the toolbar loads, the PNG-in-SVG renders and the text label shows. Fallbacks are in the plan. Product-owner test: the toolbar loaded **hidden** (Rhino ignored `visible`), which led to plan 013 | 011 |
| 013 | [Make Owly prominent right after install](013-prominent-first-run.md) | DONE: `5fcba00`. Advisor review approved; R9 and R8 build with 0 warnings, Server.Tests 148. On first start (once per Rhino version) Owly opens its panel as a tab next to Layers or Properties and switches its toolbar on; the toolbar is docked `right` in the `.rui`. Manual Rhino check still open, including whether Rhino honours the right-edge docking | 012 |

**Prerequisites the team owns:**
- Commit `art/owly.png`, `owly-dark.png`, `owly-small.png` and `owly-small-dark.png` (LFS) before 010.
- Build and commit `Owly.rui` before 012 Part B.
- Every plan has a manual Rhino check at the end.

Decisions the product owner made in the grilling session (each plan repeats the ones it needs):
- **History.** There are no saved conversations, only the live transcript. Delete the history code on both sides.
  - A conversation ends on New conversation, on closing the file (including File › Open/New on Windows), or on quitting Rhino. Closing the panel does not end it.
  - Old `Conversations` settings are wiped once on load.
  - New conversation has no confirmation; its tooltip says it clears the current one.
  - Claude Code's own `~/.claude/projects` copy stays (Stop + `--resume` needs it) and is disclosed.
- **Icon.** The team's PNGs are used as they are (currently opaque). Every icon path must keep alpha, so transparent re-exports are a plain file swap. The owl goes everywhere:
  - panel tab, Options page, plug-in and yak icons
  - settings and welcome windows
  - panel header, empty state, a per-answer head, the thinking strip, the question card
  - the toolbar
- **Welcome popup.**
  - Shown once after Rhino's first start, with Open Owly and Close.
  - The content includes an agreed privacy line.
  - It can be reopened with `OwlyWelcome` and with About Owly in settings.
- **Toolbar.** One button with the owl and the text "Owly", at the largest per-toolbar size (never the global setting), docked at the top.
- **Out of Phase 3.** Clickable follow-up suggestions are deferred to Phase 4/5.
- **PRD.md** §3.3 and the Phase 3 bullet are edited by plan 009 to say "transcript".

## Shared verification gates

Every plan must leave these passing. Run them from the repo root.

```sh
dotnet build rhino/plugin/RhinoAI.csproj -p:RhinoTarget=R9 -clp:NoSummary
dotnet build rhino/plugin/RhinoAI.csproj -p:RhinoTarget=R8 -clp:NoSummary   # on Windows this also builds net48
dotnet test tests/Server.Tests/Server.Tests.csproj
```

Expected: `Build succeeded` with 0 errors, and all tests passing. Debug builds allow warnings; do not add new ones.

## Repo conventions that every executor must follow

- **New C# files written by an AI agent are named `*.ai.cs`** (see `AGENTS.md`). Editing an existing file does not rename it.
- `tests/Server.Tests/Server.Tests.csproj` links plugin source files by explicit path (`<Compile Include="../../rhino/plugin/..."/>`). If you delete or rename a linked file, update that list too.
- The panel page `rhino/panel/dist/panel.host.html` is committed and embedded in the plugin. If you change anything under `rhino/panel/src`, rebuild it with `cd rhino/panel && npm install && npm run build` and commit the rebuilt file.
- Do not rename the `Rhino.AI` namespace, the `RhinoAI.csproj` file or classes such as `RhinoAIPlugin` (ADR-0004).

## Considered and rejected

- **Filtering tools in `ToolRegistry` instead of deleting them.** Rejected in ADR-0002.
- **Wiring the panel to `rhino/sdk` (direct API).** Rejected for now in ADR-0003.
- **A full namespace rename.** Rejected in ADR-0004.
- **Keeping Claude Code's default system prompt and appending Owly's rules.** Rejected in ADR-0005.
- **Wiring up "Ask again" and the @-mention context instead of deleting them.** Rejected by the product owner for Phase 2 (plan 007). Revisit only with a real C# handler.
- **A student- or teacher-editable prompt box.** Rejected in ADR-0005.
- **Keeping saved history with delete/expiry controls, or history kept only while Rhino is open.** Rejected in ADR-0006.
- **`--no-session-persistence` now.** Rejected for Phase 3 because Owly would forget the conversation after every Stop. It is a Phase 5 candidate, together with feeding the transcript back (ADR-0006).
- **Owly deleting Claude Code's transcript files.** Rejected: it would mean guessing the CLI's folder layout inside the student's profile.
- **Scripting the white/black background out of the owl PNGs.** Rejected: it machine-edits the team's art. The team will re-export transparent PNGs when they want them.
- **Changing the student's global toolbar button size.** Rejected: it changes all of Rhino. Only the Owly toolbar's own size is set.
- **One conversation shared across all open files.** Rejected: it means redesigning the per-document agent and listener (ADR-0006).
