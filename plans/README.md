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

## Phase 4: Rhino Integration

Written by an advisor pass (`/improve` + `/grill-with-docs`) against commit `6571b16` on `rhino-9.x`. New decision: [ADR-0007](../docs/adr/0007-panel-ui-actions-on-student-click.md) (panel actions touch Rhino's UI only on a student's click, never the model). Glossary updated: **Command option**, **Tool highlight**, **Location card**, **Command copy**, **Command-line highlight**.

Facts that shaped Phase 4 (RhinoCommon 8.19 and 9.0):
- No public API flashes a toolbar button, gives its screen position or lists its macros. Only the `.rui` XML on disk can be read.
- No API returns the command line's bounds.
- No API lists a command's options without running it.

```
014 command chips ──► 015 toolbar location card ──► 017 prompt + PRD notes ──► 016 R9 flash spike (may end "not feasible")
```

The order is linear:
- 015 extends 014's chip.
- 017 tells the model about both buttons.
- 016 hooks into 015's handler and is last because it may produce no code.

| # | Plan | Status | Depends on |
|---|---|---|---|
| 014 | [Command chips: click a command to copy it](014-command-chips.md) | DONE: `50204fe`, merged into `rhino-9.x`. Advisor review approved. R9 and R8 build with 0 errors, Server.Tests 158 (148 + 10), panel typecheck clean, 42/42 tests, verify all passed, rebuilt `panel.host.html` is reproducible. IDs 277-279. Approved deviations: the switch-arm variable is named `chip` (`command` clashes with the parameter, CS0136), and the C# regex ends in `\z`, not `$` (.NET `$` accepts a trailing newline, so the plan's own `"_Loft\n"` test failed). The manual Rhino check is still open, in particular whether the focus call behaves | 013 |
| 015 | [Toolbar location card](015-toolbar-location-card.md) | DONE: `980aa7f`, merged into `rhino-9.x`. Advisor review approved. R9 and R8 build with 0 warnings, Server.Tests 176 (158 + 18), panel typecheck clean, 46/46 tests, verify all passed, rebuild is reproducible. IDs 280-289. The real R9 `.rui` matched the plan's rules (structure recorded in the plan).<br>Approved deviations:<ul><li>the card list is keyed on name + places, so a newer answer rebuilds the card (a plan bug)</li><li>an extra verify check confirms a card exists before the replay clears it</li><li>the structure record was written to a side file and merged into the plan by the advisor</li><li>the `PanelStrings.SidebarWord()` accessor</li></ul>Open:<ul><li>step 2.4 and the whole manual Rhino check, including the first-lookup time and "hidden"</li><li>the R8 `.rui`, as R8 isn't installed</li><li>follow-up: `platforms` on `tool_bar` is not filtered (Mac only)</li><li>plan bug: the `NonPublic` done-criteria grep also hits `bin/` DLLs, so use `git grep`</li></ul> | 014 |
| 017 | [Tell Owly about the command buttons, and update the PRD](017-phase4-prompt-and-docs.md) | DONE: `926637a`, merged into `rhino-9.x`. Advisor review approved. R9 and R8 build with 0 warnings, Server.Tests 177 (176 + 1). The `CommandSteer` change is append-only, and the PRD diff is limited to the planned lines. Approved deviation: the criteria-4/5 notes sit after the sentence's full stop, keeping the markdown line break. Open: the prompt-checks run (rows 1-12, especially 1 and 12) in Rhino | 015 |
| 016 | [Spike: flash the toolbar button on Rhino 9](016-r9-toolbar-flash-spike.md) | DONE (not feasible). Part A offline: `a8969df`, merged into `rhino-9.x`, found a public locator (`Rhino.UI.Runtime.RhinoUiServiceLocator().GetService<IToolbarsService>()`). The in-Rhino 9 probe on 2026-11-19 printed `service: NULL`, so no public route reaches a toolbar control, let alone a button. Part B is not built. The PRD Phase 4 note and the spike doc are updated, and the probe was deleted (never committed). Owly's tool highlight is the location card (plan 015) | 015 (run after 017) | 015 (run after 017) |

Decisions the product owner made in the grilling session (each plan repeats the ones it needs):
- **Platforms.** Windows Rhino 8 and 9 are required. On Mac the features degrade safely and never crash.
- **Who acts.** Only the student's click. No new agent tool; the AI keeps its five read-only tools (ADR-0007).
- **Command chips.** Every `` `_Name` `` in an answer is a clickable chip with a copy symbol. One click copies it (with the underscore), shows a tick, focuses Rhino's window, and shows a hint to paste with Ctrl+V (⌘V on Mac) and press Enter. Other inline code stays plain. The host doesn't re-verify the command.
- **Command line.** Nothing is ever typed into it (no `SendKeystrokes`). "Command terminal highlight" = copy plus focus.
- **Toolbar.**
  - A second icon on each chip shows a text-only location card under the answer: group or tab, toolbar, button tooltip, left/right click, and "hidden" if the group is hidden. Otherwise it says "not on any open toolbar".
  - The `.rui` files are read as saved, with no images and no menu paths.
  - Toolbars are never shown, hidden or changed.
- **Flash.** An R9-only spike using public types only. If that isn't possible, it is recorded as not feasible and no code ships.
- **Command options** (PRD "configurable parameters") move to Phase 5, built on verified documentation.
- **Follow-up suggestions** move to Phase 5.
- **Prompt.** The model tells the student to click a command to copy it, and never claims it typed, ran or highlighted anything. A new prompt-checks row covers this.

## Phase 5 rework: commands & tools → memory → user level

Written by an advisor pass (`/grill-with-docs`) against commit `40f2c38` on `rhino-9.x`, after the product owner changed direction on 2026-11-24 (two mock-ups: an options card with a green Run button, and a strip of owl buttons for saved conversations).
- New decisions: [ADR-0008](../docs/adr/0008-run-on-student-click-and-option-probe.md) supersedes ADR-0007 (Run on click, guarded option probe), and [ADR-0009](../docs/adr/0009-saved-conversations-per-project.md) supersedes ADR-0006 (saved conversations per project).
- Glossary: **Options card**, **Run**, **Blocked command**, **Option probe**, **Button image**, **Location popover** (replaces Location card), **Project**, **Unsorted**, **Project notes**, **Main memory**, **Level**, **Level check-in**. **Conversation** is kept as the term; "chat" stays an avoid-word.
- PRD §3.2, §3.3, §4 and the new §3.4 carry notes.
- CLAUDE.md marks the rules being replaced.

The product owner set the priority: **commands and tools first, then memory, then user level.** Every plan is a separate deliverable. Dependencies are only where listed.

```
A  018 lookup lines   019 once/in order   020 popover   021 button images      (independent; build in this order)
   022 Run ──► 023 options card ──► 024 option probe spike (may end "not feasible")
B  025 projects ──► 026 saved conversations + owl strip ──► 027 project notes ──► 028 main memory
C  029 level at start ──► 030 level check-in
```

| # | Plan | Status | Depends on |
|---|---|---|---|
| 018 | [Remove the lookup lines](018-remove-lookup-lines.md) | DONE: `14bf2da`, merged into `rhino-9.x` (`1d0c1df`). Advisor review re-ran typecheck, 40/40 tests, verify (all passed) and the build (`panel.host.html` reproducible). C# untouched; the executor's R9 build and 177 Server.Tests passed. Approved deviations: all icons kept (host chip icons are typed `IconName`, so unused ones couldn't be proven); one README diagram line updated. Manual Rhino check still open | — |
| 019 | [Name each command once, in order](019-commands-once-in-order.md) | DONE: `d3337f7`, merged into `rhino-9.x` (`7f0ac7b`). Advisor review re-ran typecheck, 50/50 tests (+4), verify (all passed, +1 check), the build (`panel.host.html` reproducible) and the 8 AgentPrompts tests; the executor's R9 build and 177 Server.Tests passed. No deviations. `transcript.ts` merged with 018 cleanly; `panel.host.html` was rebuilt. Open: prompt-checks rows 1, 12, 13, 14 in Rhino | — |
| 020 | ["Where to find" popover](020-location-popover.md) | DONE: `cac2459` + `e7eb66d`, merged into `rhino-9.x`. An advisor addendum (in the plan) re-anchored the plan at `acc2756` and settled the gaps. Advisor review re-ran typecheck, 46/46 tests (45 before), verify (136 checks, all passed), the build (`panel.host.html` reproducible), R9 and R8 (0 warnings) and 212 Server.Tests. A zoom probe (0.67/1/1.5/2) found the popover spilling past `.stream` and rising over the header; fixed in `e7eb66d` and re-probed. Approved deviations: verify spells the old class as `'.location-' + 'card'` (the plan's grep gate covered `tools/`, a plan bug); Esc closes through the existing handler plus `closeLocation`; the title is reactive. Known edge: at 200% with a short visible transcript, the popover fits on neither side, so it clamps to the transcript top and partly covers the chip. Open: the manual Rhino check | — (best after 019) |
| 021 | [Real toolbar button images](021-real-button-images.md) | DONE: `d104884`, merged into `rhino-9.x`. Step 1 (the Rhino 9 resource check) passed on 9.1.26314.9001, and an advisor addendum at `b47b96b` settled the design. Advisor review re-ran typecheck, 50/50 tests (46 before), verify (141 checks, all passed), the build (`panel.host.html` reproducible), R9 and R8 (0 warnings) and 223 Server.Tests (212 before). Both copyright greps are clean and the test data is all synthetic. Approved deviations: the pinned JSON carries `+` (the host serialiser escapes `+` in base64); a `.location-lead` span keeps the popover head's layout. Nit: a single `toolbar.icon` request answers at most 20 names, and the rest keep the glyph. Open: the manual Rhino check (real icons, dark mode, the `default.rui` parse time on the first chip) and the R8 check (step 7) | — (the popover image needs 020) |
| 022 | [Run button](022-run-button.md) | DONE: `2af1d64`, merged into `rhino-9.x` (`40aef53`). An advisor addendum at `fb72651` re-anchored the plan:<ul><li>the host answers `command.check` per name instead of sending a `commands.blocked` list</li><li>cards wait until their block stops streaming</li><li>an unknown command gets the blocked note (wording confirmed by the product owner)</li></ul>The advisor review re-ran the R9 and R8 builds (0 warnings, 0 errors), Server.Tests 253/253 (223 before), typecheck, panel tests 53/53, the build (`panel.host.html` reproducible), verify (all passed, 7 new checks) and both done-criteria greps (empty). The prompt is 4,592 characters, under the 7,000 guard. Approved deviation: the dark theme gets an `--ok-text` token (`#0d2416`), because white on the dark `--ok` is about 1.9:1. Open: the manual Rhino check, including:<ul><li>that `IsCommand` and `IsAlias` with the bare English name work in a non-English Rhino</li><li>that the panel stays responsive while Rhino waits for picks</li><li>whether the `Save`/`Undo`/`Redo` substring rules block an ordinary command by accident</li></ul> | — (best after 019) |
| 023 | [Options card from help pages, with suggested values](023-options-card-from-help-pages.md) | DONE: `e8d605a` + `1664480` + `c79f568`, merged into `rhino-9.x` (`bc43ff5`). Step 1: the advisor checked the help-page structure (a missing page returns 403, two layouts, pages can document several commands), and the product owner tested the macro forms in Rhino 9. Product-owner decision: only choices and Yes/No toggles are editable; every other option is read-only until the probe (plan 024). Advisor review: ran the parser against 13 real pages saved outside the repo and found five rules missing. These were fixed in `1664480`, and an advisor-caused regression in Polygon `Mode` was fixed in `c79f568`. The re-check gives the right options for Pyramid, Fillet, Loft, Polygon, Circle, Offset and Pipe. The advisor re-ran the R9 and R8 builds (0 warnings), Server.Tests 296/296 (253 before), typecheck, panel tests 63/63 (53 before), the build (`panel.host.html` reproducible) and verify (all passed); the `SendKeystrokes` and `mc-main-content` greps are clean, and the fixtures are hand-written. Prompt length 4,839. IDs 330-336 and 340 (new privacy text). Approved deviations: the suggested-value verify check uses an injected `_Demo` command in `turn-0` (the plan's fallback), and plan 022's blocked-card verify check now joins all notes. Known gaps: some options have empty descriptions (Offset `Loose`/`Trim`); an editable option at a later prompt makes Rhino print "Unknown command" (harmless, and the card hint explains it); focus is lost after a click on the card. Open: the manual Rhino check | 022 |
| 024 | [Spike: silent option probe](024-option-probe-spike.md) | IN PROGRESS: Part A. An advisor addendum at `4ea69f1` specifies the throwaway `OwlyProbeTest` command (branch `owly/probe-spike`, never merged): the probe is armed on a delay and runs from Idle, and three cancel methods are compared. Waiting for the product owner's Rhino 9 run | 023 |
| 025 | [Projects](025-projects.md) | DONE: `0ce7d0a` + `eebe4f9`, merged into `rhino-9.x` (`aff49c4`). Advisor review re-ran the R9 and R8 builds (0 errors), 212/212 Server.Tests (+35), typecheck, 47/47 tests, verify (129 checks, all passed) and the build (`panel.host.html` reproducible). Fix requested and made: the privacy line repeated "sent to Anthropic through Claude Code" (a plan wording flaw). Executor decision to confirm with the product owner: Switch re-files only an unknown file (otherwise this session only), and Move always re-files. Open: the manual Rhino check (Untitled then Save, Save As suggestion, rename/delete, Delete all, R8+R9 together, welcome popup layout) | — |
| 026 | [Saved conversations and the owl strip](026-saved-conversations.md) | TODO | 025 |
| 027 | [Project notes](027-project-notes.md) | TODO | 026 |
| 028 | [Main memory (opt-in)](028-main-memory.md) | TODO | 027 |
| 029 | [Choose a level at the start](029-level-at-start.md) | TODO | — (adds the main-memory switch if 028 is done) |
| 030 | [Level check-in](030-level-check-in.md) | TODO | 029 |

Decisions the product owner made in the grilling session (each plan repeats the ones it needs):
- **Lookup lines.** All of them go. The thinking strip stays.
- **Commands.**
  - Each command is named once, in order: a prompt rule, plus only the first mention per answer becoming a chip.
  - The chip still copies.
  - One options card per step, under the step that names the command.
- **Run.**
  - It starts the command with the student's options, and the student does the picks.
  - It is refused while Rhino is in a command.
  - Blocked: script and plug-in runners, Grasshopper, Owly's commands, file and app commands, Delete and Purge, Undo and Redo, settings and layout. A blocked card has options but no Run.
  - The AI never triggers it.
- **Options.**
  - The silent probe is used if its Rhino 9 test passes, with four guards (idle, nothing selected, not blocked, help page lists options). A history line is an acceptable trace.
  - McNeel's help pages are the fallback and the complement.
  - Fields start blank, meaning keep Rhino's value.
  - Unknown choices are read-only with a hint.
  - Each row has an explain icon.
  - Owly may pre-fill only verified values.
- **Where to find:** a popover over the transcript.
- **Button images:** local Rhino install only, on chips and in the popover.
- **Projects.**
  - Named by the student, mapped by file path, stored in `%AppData%\Owly` and shared by R8 and R9.
  - Header menu.
  - A card for unknown or Untitled files, with a folder suggestion and Unsorted.
  - Kept until deleted, with Delete all.
- **Conversations.**
  - The owl strip with tooltips and right-click rename/delete.
  - Reopening a file starts an empty conversation.
  - Old conversations continue (`--resume`, else the saved transcript as context).
- **Notes.** A Haiku summary at the end of each conversation, injected into new conversations, viewable and deletable.
- **Main memory.** Opt-in, off by default, viewable and clearable.
- **Level.**
  - Beginner, Intermediate or Advanced, global, chosen in the welcome popup and changeable in settings.
  - The popup re-shows once to existing installs.
  - Check-in after answer 10 and then every 30, at most once per conversation. It offers a level change and changes nothing without a click.

Facts found during planning:
- **cmd.exe limit.** The CLI is launched through `cmd.exe`, which caps the command line at 8,191 characters, and the steers already take about 5.6k. So notes, main memory and resume context go in the first user message over stdin, never in `--system-prompt`.
- **Resume and working directory.** `--resume` only finds a session from its original working directory, so plan 026 fixes the CLI's working directory at `%AppData%\Owly\cli`.
- **`RunScript`** blocks while a command waits for picks. Post it with `AsyncInvoke`, never inside the WebView handler.
- **Built-in button images** are embedded resources of `Rhino.UI.dll`, mapped by the `.rui` `bitmap_id`. Never copy them into the repo.
- **Localisation ID ranges** per plan: 020: 290-299, 021: 300-309, 022: 310-329, 023: 330-349, 025: 360-389, 026: 390-409, 027: 410-429, 028: 430-439, 029: 440-449, 030: 450-459.
- **Expect merge friction** in `markdown.ts`, `AIPanelViewModel.ai.cs`, `AgentPrompts.cs`, the protocol mirrors (`events.ts`, `PanelEvents`/`PanelCommands`, `mockHost.ts`, `host-events.json`), `verify.mjs` and `WelcomeDialog.ai.cs`. Run the plans in table order.

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
- **A highlight agent tool the AI calls itself.** Rejected in ADR-0007. The AI would act on Rhino's UI, and the five-tool rule would break.
- **Typing the command into the command line (`SendKeystrokes`, no Enter).** Rejected by the product owner (ADR-0007). A space acts as Enter in Rhino, so any options could run the command.
- **Switching on a hidden toolbar to show the button.** Rejected: it changes the student's layout (ADR-0007, plans 012/013).
- **An end-of-answer "Commands" strip instead of inline chips.** Rejected by the product owner. Commands are clickable where they appear.
- **AI-proposed command options in an editable macro now.** Rejected for Phase 4: they can't be verified before the command runs. Moved to Phase 5 with verified docs.
- **Reflection into Rhino's internal toolbar types for the flash.** Rejected (plan 016): it is fragile across Rhino updates.
- **A team-verified allowlist of commands for Run.** Rejected by the product owner for the Phase 5 rework in favour of a blocklist (ADR-0008).
- **Reading options only after the student clicks Run.** Rejected in favour of the silent probe, if its test passes (ADR-0008).
- **Loading button images from docs.mcneel.com.** Rejected: local install only, with no web requests for images.
- **"Chat" as the product term.** Rejected: "conversation" stays.
- **Notes or main memory in `--system-prompt`.** Rejected: cmd.exe's 8,191-character limit (ADR-0009).
- **Project ID inside the .3dm, or folder/file as the project.** Rejected in ADR-0009.
