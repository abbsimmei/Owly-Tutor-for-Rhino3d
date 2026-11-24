# 018: Remove the lookup lines from the transcript

- **Written against:** `40f2c38` on `rhino-9.x`.
- **Status:** TODO
- **Effort:** S
- **Risk:** Low. Panel rendering only; the protocol and C# stay as they are.
- **Phase:** Phase 5 rework, commands & tools. No ADR needed.

## Why

The product owner wants Owly to stop telling the student that it is looking things up. Mock-up 1 shows the answer without the "Looked up Rhino commands" line. **All** tool-call lines go, not just the commands line:
- "Looked up Rhino commands"
- "Checked your selection"
- "Looked at your document"
- "Listed objects in your model"
- "Asked you a question"
- all the "Couldn't …" variants

The thinking strip (the owl status strip while Owly works) stays, so the student still sees that Owly is busy.

## Facts you need

- **The phrases** are written only in C#: `rhino/plugin/UI/ToolSummary.cs:36-59` (`Phrase`). They are sent as the `title` of `PanelToolCall` / `PanelToolPatch` (`rhino/plugin/UI/ConversationFeed.ai.cs:329,348`), as the `turn.tool` / `turn.tool.patch` events (`rhino/panel/src/protocol/events.ts`).
- **Drawing:** `rhino/panel/src/ui/transcript.ts`, in `block()`, `case 'tool': return toolCard(ctx, view.call);`. The card is built in `rhino/panel/src/ui/toolCard.ts`.
- **The thinking strip** is separate: `statusStrip` in `rhino/panel/src/ui/app.ts:16-60`, driven by `store.thinking()`.
- **verify checks** on tool cards are in `rhino/panel/tools/verify.mjs`, around `:650-663`:
  - "a tool call and its folded-in result render as one settled card" (`rendered.tools === 4`)
  - "a result the agent flagged as an error renders as a failed card"
  - "a namespaced tool gets its real phrase"
  - "the mcp__rhino__ prefix never reaches the panel"
- **Fixtures:** `rhino/panel/tests/host-events.json:9-14` and the scripted tools in `src/protocol/mockHost.ts`.

## Scope

- **Panel only.** `block()` returns nothing for `view.kind === 'tool'`. Tool calls still arrive and stay in the store, because other code may use them (questions, images, replay), but they are not drawn.
- Delete `ui/toolCard.ts` and its CSS if nothing else imports them. Keep `state/tools.ts` only if something still uses it. Otherwise delete it with `tools.test.ts`.
- **Do not** change `ToolSummary.cs`, `ConversationFeed.ai.cs` or the protocol records. Server.Tests compiles them, and removing both sides is a separate decision (see CLAUDE.md on panel records).

## Steps

1. In `transcript.ts`, make `case 'tool'` render nothing. Check that an empty turn still shows correctly while running: the thinking strip covers it.
2. Remove the dead code (`toolCard.ts`, CSS) and any now-unused imports and icons.
3. In `verify.mjs`:
   - Replace the tool-card checks with one check: "no tool-call line renders", meaning no element of the old card class appears after the C#-serialised fixture is replayed.
   - Keep the `mcp__` check.
4. Run `npm run typecheck && npm test && npm run verify && npm run build`, then commit `dist/panel.host.html`.

## Done criteria

- Searching the rebuilt `dist/panel.host.html` finds none of the `toolCard` class names.
- The shared gates pass (see `plans/README.md`).

## Manual Rhino check

Ask "what is selected?" and "how do I loft?". No lookup line appears. The thinking strip shows while Owly works, and the answer appears as before.
