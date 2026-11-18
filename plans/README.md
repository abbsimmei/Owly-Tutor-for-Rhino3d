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
