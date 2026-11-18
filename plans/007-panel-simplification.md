# 007: Simplify the chat panel for students

- **Written against:** `d78f9eb` on `rhino-9.x`. Apply it after plan 006.
- **Status:** TODO
- **Effort:** L
- **Risk:** Medium. It touches the TypeScript panel, its browser checks and a few C# strings. Only UI is removed: no agent behaviour or wire format changes.
- **Phase:** PRD Phase 2, bullet "Simplify the existing interface"

## Why

The panel still carries RhinoAI controls that do nothing in Owly, or that look like they act on the model. The advisor checked each one in the code:

- **Revert and Ask again.** Every finished answer shows them. They send `turn.undo` and `turn.retry`, but the C# host never registers those commands (`rhino/plugin/UI/PanelCommands.cs:5-22`, `PanelCommands.ai.cs:5-7`). `PanelBridge` logs a parse error and nothing happens. `rhino/plugin/UI/ConversationFeed.ai.cs:111` hard-codes `Undoable: true`, which is why Revert shows.
- **The @-mention menu.** It lets the student attach "Selection" or a layer to a question. The panel sends that `context`, but C#'s `PromptRequest(string Text, IReadOnlyList<PanelAttachment> Attachments)` (`PanelCommands.ai.cs:16`) has no such field, so the AI never sees it. The AI reads the selection itself through its read-only agent tools.
- **The first starter prompt** ("How do I loft between curves?") never shows. It needs the `pluginCommands` capability (`rhino/panel/src/ui/empty.ts:12`), and C# always sends `false`.
- **Tool cards** show the raw tool name, and expanding one shows raw JSON arguments and results. `ToolSummary.cs` has no phrase for `get_context`, so the title reads `getcontext`.
- **Never used.** C# never sends anything for the plan strip, tool chips or tool previews (including Grasshopper wire counts).
- **The setup-guide link** opens McNeel's RhinoAI site (`empty.ts:66`).
- **The `/agent` slash command** opens a one-item menu. **`/login`** is redundant: sign-in starts automatically when a signed-out student sends a message (`rhino/plugin/Agents/StreamJsonAgent.ai.cs:379-425`).

The product owner decided:
- Delete all dead UI rather than wire any of it up.
- Remove the @-menu.
- Make each tool card one plain line with no JSON.
- Remove `/agent` and `/login`.
- Point the setup link at the Claude Code install page.
- Reword legacy strings.
- **Keep** token counts.

## Repo conventions you must follow

- The panel is dependency-free TypeScript in `rhino/panel/src`.
- `rhino/panel/dist/panel.host.html` is committed and embedded in the plugin. After any change under `src`, rebuild it with `npm run build` and commit the result. Never hand-edit it.
- UI is built with `el(tag, props, ...children)` and `when(cond, render)` from `src/core/dom.ts`. Match the surrounding code when you delete pieces, and don't restyle anything.
- **User-facing text comes from C#.** `rhino/plugin/UI/PanelStrings.ai.cs` sends the strings in the `hello` event, and they win inside Rhino. The `EN` table in `src/i18n/strings.ts` is only the fallback for the dev server and tests. Keep both tables' keys and English text identical.
- **Localization IDs.** Each C# string has an ID: `Localization.LocalizeString("text", 68)`. When you reword a string, keep its ID. A brand-new string gets a new ID starting at **260** (259 is the highest in use at `d78f9eb`), counting up.
- A **new** C# file written by an AI agent is named `*.ai.cs`. This plan should not need a new C# file.

## Scope

**Edit (TypeScript):**
- `rhino/panel/src/ui/transcript.ts`
- `rhino/panel/src/ui/toolCard.ts`
- `rhino/panel/src/ui/composer.ts`
- `rhino/panel/src/ui/empty.ts`
- `rhino/panel/src/main.ts`
- `rhino/panel/src/state/ui.ts`
- `rhino/panel/src/protocol/events.ts`
- `rhino/panel/src/protocol/mockHost.ts`
- `rhino/panel/src/i18n/strings.ts`
- `rhino/panel/src/styles/panel.css` (only to delete rules for removed classes, and only if you can do it confidently)
- `rhino/panel/tools/verify.mjs`

**Delete:** `rhino/panel/src/ui/previews.ts`, once nothing imports it.

**Edit (C#):**
- `rhino/plugin/UI/ConversationFeed.ai.cs` (one value)
- `rhino/plugin/UI/ToolSummary.cs`
- `rhino/plugin/UI/PanelStrings.ai.cs`
- `rhino/plugin/UI/AIPanelViewModel.ai.cs` (notice strings only)

**Rebuild:** `rhino/panel/dist/panel.host.html`.

**Out of scope. Do not touch:**
- **The host-to-panel wire format.** Keep every C# record in `rhino/plugin/UI/PanelEvents.ai.cs` and `PanelCommands*.cs` as it is, including `PanelCapabilities`, `PanelToolCall.Chips`, `PanelTurn.Plan`, `PanelTurn.Context`, `ContextEvent` and `LoginCommand`. Keep the matching *host event* types in `events.ts` (`HostEvent`, `ToolCall`, `TurnSnapshot`, the `context` event) and the store code that applies them. Server.Tests compiles these C# files, and old saved conversations still carry the fields. You remove only UI and the *panel-to-host commands* that the host never handled.
- `rhino/panel/tests/host-events.json`. It is C# output, and it stays valid because the wire format doesn't change.
- **Attachments** (paperclip, paste, drop, thumbnails). These stay.
- Token counts in the turn footer and history. These stay.
- The `ask_user` question overlay, history drawer, review bar, notices, zoom, image block and busy words.
- The settings dialog (plan 008) and `AgentPrompts.cs` (plan 006).

## Steps

### 1. Baseline

```sh
cd rhino/panel
npm install
npm run typecheck && npm test && npm run build && npm run verify
```

Expected: typecheck clean, unit tests pass, and verify prints its checks with no failures. If no Chrome or Edge is found, verify prints a "skipping" line, which is fine. Note which verify checks exist. If the baseline already fails, STOP and report.

From the repo root, also run the C# gates in `plans/README.md`.

### 2. Turn footer and transcript (`src/ui/transcript.ts`)

- In `turnFooter` (around lines 112-162), delete the two `when(...)` blocks that render the **Ask again** button (`turn.retry`) and the **Revert** button (`turn.undo`). Keep the time, the token count, the spacer and the **Copy** button.
- Delete `planStrip` (around lines 58-86), its `PLAN_MARKER` table, and its call `planStrip(turn),` in `turnView`.
- Delete `contextChip` (lines 25-47) and the file-local `CONTEXT_ICON` (lines 15-23).
- In `turnView`, the chip row is currently shown when `turn.context.length > 0 || turn.attachments.length > 0`. Change it so the row shows only when `turn.attachments.length > 0` and renders only `attachmentChip`s.
- Remove the `ContextItem` and `PlanStep` imports if they become unused.

### 3. Tool cards (`src/ui/toolCard.ts`)

Rewrite `toolCard` so that it renders exactly one row: `div.tool.<status>` containing `div.tool-head`. That row holds the spinner while running, or the family icon (`iconForTool`) when settled, then `span.title` with `call().title`. Keep the duration span.

Delete:
- the `button.tool-toggle` wrapper (render the head children directly in the `div`)
- the `aria-expanded` attribute and the chevron
- the auto-expand-on-failure `bind`
- `chips`, `inlinePreview`, the `json` helper and the whole expanded `tool-body`
- the `span.wire` showing the raw tool name

A failed call keeps the `failed` class, so the existing CSS still colours it. Its title already says it failed (see step 7).

Remove imports that become unused (`highlight`, `prettyJson`, `preview`, `bind`). If `ctx.ui.isToolExpanded` / `toggleTool` in `src/state/ui.ts` lose all callers, delete them too. `npm run typecheck` will tell you.

Then delete `src/ui/previews.ts`. Verify with `git grep -n "previews.js" -- rhino/panel/src`, which should print nothing.

### 4. Composer (`src/ui/composer.ts`, `src/main.ts`, `src/state/ui.ts`)

- In `COMMANDS` (lines 20-27), delete the `agent` and `login` entries. Keep `/new`, `/history`, `/stop` and `/settings`.
- Delete everything for the @-mention menu:
  - the file-local `CONTEXT_ICON` (lines 29-37)
  - `contextMatches`
  - `syncMention` and every call to it (`onInput`, `onClick`)
  - `pickContext`
  - the context branch of `acceptHighlighted`
  - the `mention-menu` branch of `menu`
  - the `ui.pickedContext()` chip row
  - the **@** `icon-btn` (`title: composer.attachContext`)
- `menuOpen` should depend only on `commandMatches()`. If the `menu` renderer then only draws the command menu, simplify it to that.
- In `src/state/ui.ts`, delete `pickedContext`, `toggleContext`, `mentionQuery` and their reset in `clearComposer`, if nothing else uses them.
- In `src/main.ts` `submit` (around lines 35-45), delete `const context = [...ui.pickedContext.peek()];` and send `request: { text, attachments }`.
- In `src/protocol/events.ts`, remove `context` from the panel's **prompt request** type (the interface used by `{ type: 'prompt'; request: ... }`, around line 214). Do **not** remove `context` from `TurnSnapshot` (line 146) or the `context` host event (line 187).

### 5. Panel-to-host commands nobody handles (`src/protocol/events.ts`, `src/protocol/mockHost.ts`)

In the `PanelCommand` union (around lines 230-245), delete these variants:
- `{ type: 'turn.undo'; ... }`
- `{ type: 'turn.retry'; ... }`
- `{ type: 'context.refresh' }`
- `{ type: 'context.reveal'; ... }`
- `{ type: 'attachments.drop'; ... }`

At `d78f9eb` nothing sends `attachments.drop` or `context.refresh`. Drag-and-drop reads files in the panel and adds them as attachments directly. Confirm this with `git grep -n "attachments.drop\|context.refresh" -- rhino/panel/src`, which should match only the type lines in `events.ts`.

In `mockHost.ts`, delete the `case` arms for the removed commands (around lines 898-910). You may leave the mock's scripted turns that emit `turn.plan`, `preview` or `chips`: the store still accepts them, and nothing renders them now. Also set `undoable: false` in the mock's turn snapshots.

### 6. Empty state (`src/ui/empty.ts`)

- In `STARTERS`, delete `requires: 'pluginCommands'` from the first entry so all three starters show.
- Change the setup link `https://mcneel.github.io/RhinoAI/` to `https://code.claude.com/docs/en/setup`.
- If the `Capability` type and the `requires` filter become unused, delete them.

### 7. C# changes

**`rhino/plugin/UI/ConversationFeed.ai.cs:111`.** Change `Undoable: true,` to `Undoable: false,` and leave the record shape alone.

**`rhino/plugin/UI/ToolSummary.cs`.** `Phrase` currently returns `"{0} failed"` with the raw tool name for failures, plus four success phrases, and falls back to `toolName.Replace("_", "")`. Replace the body of `Phrase` so it uses this table. Keep the existing IDs where a row already has one, and use new IDs from 260 up for the rest. Keep `IsFailure`, `Parse` and `RemoveUnderscoreUnderscoreNaming` unchanged. Delete `Verb` if it becomes unused.

| Tool | Success phrase (ID) | Failure phrase (ID) |
|---|---|---|
| `get_selection` | "Checked your selection" (181) | "Couldn't check your selection" (new) |
| `get_context` | "Looked at your document" (new) | "Couldn't look at your document" (new) |
| `list_objects` | "Listed objects in your model" (183) | "Couldn't list objects" (new) |
| `get_commands` | "Looked up Rhino commands" (184) | "Couldn't look up Rhino commands" (new) |
| `ask_user` | "Asked you a question" (190) | "Couldn't show the question" (new) |
| any other | "Looked something up" (200) | "Something went wrong" (176) |

The fallback must no longer echo the raw tool name.

**`rhino/plugin/UI/PanelStrings.ai.cs` and `rhino/panel/src/i18n/strings.ts`.** Apply the same changes to both tables. Keep each C# ID.

| Key | New English text |
|---|---|
| `header.switchAgent` | `Owly` |
| `header.noAgent` | `Owly isn't set up` |
| `header.settings` | `Owly settings` |
| `empty.theAgent` | `Owly` |
| `empty.noAgent` | `Owly isn't set up yet` |
| `empty.noAgentBody` | `Owly uses Claude Code. Install it and sign in, then come back here.` |
| `empty.openSettings` | `Open Owly settings` |
| `empty.setupGuide` | `How to install Claude Code` |
| `review.resumeTitle` | `Continue this conversation` |
| `history.search` | `Search conversations…` |
| `agent.settings` | `Owly settings…` |
| `cmd.settingsHint` | `Open Owly settings` |
| `cmd.stopHint` | `Stop Owly's answer` |
| `unsupported.classic` | `Until then, you can still use Rhino's Help (F1) and the command line.` |

**Delete** these keys from both tables, after checking with `git grep` that no remaining `t('…')` uses them: `plan.head`, `turn.retry`, `turn.undoTitle`, `turn.undo`, `chip.revealNamed`, `tool.copyNamedSection`, `tool.arguments`, `tool.result`, `tool.noResult`, `preview.captured`, `preview.revealObject`, `preview.wireCount`, `preview.errorCount`, `preview.warningCount`, `composer.attachContext`, `cmd.agentHint`, `cmd.loginHint`. **Keep** `chip.removeNamed`, which attachment tiles still use. `src/i18n/strings.ts` probably derives `StringKey` from its table, so typecheck will flag any leftover use.

The TS fallback `composer.placeholderIdle` (`strings.ts` around line 99) still reads "…describe what you want built…". Set it to the C# text: `Ask Owly how to do something in Rhino…`.

**`rhino/plugin/UI/AIPanelViewModel.ai.cs` notices.** Reword these and keep their IDs:
- ID 4 (around line 221) and ID 8 (around line 314): `"Owly isn't set up. Install Claude Code and sign in, or open Owly settings."`
- ID 7 (around line 288): `"Can't continue this conversation: Claude Code is no longer available."` This string currently takes a `{0}` argument. Remove the `string.Format` wrapper together with the placeholder.
- ID 9 (around line 319): `"Owly couldn't start its connection to this document. Try reopening the panel."`

### 8. Browser checks (`tools/verify.mjs`)

These checks test removed UI. Update or delete them so the file only asserts what still exists:
- **"tool cards / json"** (around lines 146-157): delete the JSON-highlighting and table-preview checks. Replace them with one check that a settled tool card contains no `.tool-body`, no `.wire` and no `pre code`.
- **"composer affordances"** (around lines 263-282): keep the slash-menu check, which should now expect at least 4 commands (`/new`, `/history`, `/stop`, `/settings`). Delete the @-menu and context-chip checks.
- **"a new conversation shows the empty state"** (around line 314): it expects `starters === 3`. Keep it. The mock host reports `pluginCommands: false` (`mockHost.ts` around line 1057), so before this plan only two starters render and this check may already fail in your baseline. After step 6 it should pass. Note in your report whether it failed in the baseline.
- **"a failed tool call auto-expands its error"** (around line 380): delete it, since cards no longer expand.
- **The C# contract replay** (around lines 680-715): delete the chip checks (`chipsOnRunning`, `chipsOnSettled`) and **"the wire name shows only when it adds something"**. Keep **"the mcp__rhino__ prefix never reaches the panel"** and **"a namespaced tool gets its real phrase"**. Their expected titles come from `host-events.json` (for example `listed objects`), which is a recorded C# sample, so they don't change.
- The image-block scenario that uses a file named `plan.png` is unrelated. Leave it.

### 9. Build and verify

```sh
cd rhino/panel
npm run typecheck && npm test && npm run build && npm run verify
```

Then commit `dist/panel.host.html`. From the repo root, run the shared C# gates.

## Done criteria

```sh
cd rhino/panel && npm run typecheck && npm test && npm run build && npm run verify   # all clean
git grep -nE "turn\.undo|turn\.retry|context\.reveal|context\.refresh" -- rhino/panel/src rhino/panel/tools   # nothing
git grep -n "mcneel.github.io/RhinoAI" -- rhino/panel/src                         # nothing
git grep -n "pickedContext\|mentionQuery\|planStrip" -- rhino/panel/src          # nothing
git grep -n "previews" -- rhino/panel/src                                         # nothing
git grep -nE "'/agent'|'/login'" -- rhino/panel/src                                # nothing
git grep -n "Undoable: true" -- rhino/plugin                                      # nothing
git grep -niE "classic AI panel|Revert every document|Switch agent or model" -- rhino/plugin rhino/panel/src   # nothing
dotnet build rhino/plugin/RhinoAI.csproj -p:RhinoTarget=R9 -clp:NoSummary         # succeeded
dotnet build rhino/plugin/RhinoAI.csproj -p:RhinoTarget=R8 -clp:NoSummary         # succeeded
dotnet test tests/Server.Tests/Server.Tests.csproj                                # all pass
git diff --stat d78f9eb -- rhino/plugin/UI/PanelEvents.ai.cs rhino/plugin/UI/PanelCommands.cs rhino/plugin/UI/PanelCommands.ai.cs rhino/panel/tests/host-events.json   # empty
```

`dist/panel.host.html` is rebuilt and committed in the same commit as the `src` changes.

Manual check by the user in Rhino 9:
- No Revert or Ask again under answers. Copy still works.
- No @ button.
- Typing `/` lists New, History, Stop and Settings only.
- Ask "What have I selected?". The tool line reads "Checked your selection" or "Looked at your document", can't be expanded, and shows no raw name or JSON.
- The empty state shows three starters.
- With Claude Code not installed (or renamed temporarily), the no-agent screen's link opens the Claude Code install page.
- Sign out of Claude Code (`claude auth logout`) and send a message. A browser sign-in still starts.

## Tests

- No new unit tests are required. If `src/state/store.test.ts` or `src/state/tools.test.ts` reference removed UI-state helpers, update them to match.
- `verify.mjs` is the regression net for the UI. Step 8 keeps it meaningful.

## Maintenance

- Phase 3 (chat panel and welcome popup) builds on this simpler panel.
- Phase 4 adds copy and highlight buttons next to commands in answers. They will hook into `src/core/markdown.ts` inline code, not tool cards.
- If "Ask again" is wanted later, it needs a real `turn.retry` handler in C#, which is a protocol addition. Don't resurrect the old button without one.
- The C# side still sends `context` events and `Plan`/`Chips` fields that the panel ignores. A later cleanup can remove both sides together, together with `host-events.json`.

## STOP and report if

- Removing a TypeScript type forces a change to any C# file in `PanelEvents.ai.cs` / `PanelCommands*.cs`.
- The grep in step 5 shows something other than the `events.ts` type lines sending `attachments.drop` or `context.refresh`.
- A `verify.mjs` scenario fails for a reason unrelated to removed UI. Report the check name and output; don't delete it.
- `npm install` or `npm run build` changes `package.json` or `package-lock.json`. Don't commit those changes.
