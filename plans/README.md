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
