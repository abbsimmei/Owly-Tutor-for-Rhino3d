# 018: Remove the lookup lines from the transcript

- **Written against:** `15b9a13` on `rhino-9.x`. Tightened by the advisor before execution.
- **Status:** TODO
- **Effort:** S
- **Risk:** Low. Panel rendering only; the protocol and C# stay as they are. Most of the work is re-anchoring verify checks that used tool cards as a progress signal.
- **Phase:** Phase 5 rework, commands & tools. No ADR needed.

## Why

The product owner wants Owly to stop telling the student that it is looking things up. Mock-up 1 shows the answer without the "Looked up Rhino commands" line. **All** tool-call lines go, not just the commands line:
- "Looked up Rhino commands"
- "Checked your selection"
- "Looked at your document"
- "Listed objects in your model"
- "Asked you a question"
- all the "Couldn't …" variants

The thinking strip (`.status-strip`, the owl status strip while Owly works) stays, so the student still sees that Owly is busy.

## Facts you need

All paths are relative to `rhino/panel/` unless they start with `rhino/plugin` or `tests/`.

- **The phrases** are written only in C# (`rhino/plugin/UI/ToolSummary.cs`) and arrive as `turn.tool` / `turn.tool.patch` events. The store keeps them as blocks with `view.kind === 'tool'`.
- **Drawing:** `src/ui/transcript.ts`:

  ```ts
  function block(ctx: PanelContext, turn: TurnView, view: BlockView): Child {
    switch (view.kind) {
      case 'text':
        return agentMessage(ctx, turn.id, view.text, () => { … });
      case 'tool':
        return toolCard(ctx, view.call);
      case 'image':
        return imageBlock(ctx, view.image);
      case 'notice':
        return el('div', { class: 'lifecycle', text: view.text });
    }
  }
  ```

  It imports `toolCard` from `./toolCard.js` (line 15).
- **The agent head** ("🦉 Owly" above an answer) currently shows as soon as **any** block exists (`transcript.ts` ~line 93):

  ```ts
  when(
    () => turn.blocks().length > 0,
    () => el('div', { class: 'agent-head' }, owl(ctx, 16), el('span', { text: () => t('turn.agentName') })),
  ),
  ```

  Once tool blocks are no longer drawn, a turn whose only blocks are tool calls would show an empty "Owly" head. **Change the condition** to "has at least one block whose `kind !== 'tool'`".
- **Code that becomes dead:**
  - `src/ui/toolCard.ts` is imported only by `transcript.ts`.
  - `src/state/tools.ts` is imported only by `toolCard.ts` and its own test `src/state/tools.test.ts`. Delete both files once `toolCard.ts` is gone. Before deleting, run `grep -rn "state/tools" src`; it must show only those two files.
  - Check `src/ui/icons.ts` for icon names that were used **only** by `state/tools.ts`/`toolCard.ts`. Remove them only if `grep -rn "'<name>'" src` shows no other user. If unsure, keep the icon.
- **CSS:** the `.tool`, `.tool-head`, `.tool-toggle`, `.tool-chips`, `.tool-chip` and `.tool-body` rules are in `src/styles/panel.css` starting near line 588. Delete them only after confirming with `grep -rn "tool-\|'tool'\|\"tool\"" src --include=*.ts` that no remaining TS file emits those classes. Don't touch other rules.
- **The thinking strip** is `statusStrip` in `src/ui/app.ts`, driven by `store.thinking()`. Don't change it.
- **Verify checks** in `tools/verify.mjs` that use `.tool` elements. Each must be rewritten as described:

  | Lines (approx.) | Check today | Rewrite to |
  |---|---|---|
  | 157-164 | "a settled tool card is one plain line" | **"no tool-call line renders"**: `document.querySelectorAll('.tool').length === 0` |
  | 200-207 (`mid.tools`) and 216-224 | "tool calls append as the turn proceeds", waiting for the `.tool` count to grow | Keep the intent (the turn makes progress) with another signal. Wait until the running turn's answer text grows, e.g. the text length of the last `.turn` increases, or the last `.turn` gains a text block. Keep the check name meaningful ("the running turn grows as it proceeds"). Also assert `.tool` count is 0. |
  | 440-444 | "answering closes the overlay and the turn continues" (`answered.tools >= 3`) | Keep `scrims === 0`. Replace `tools >= 3` with a signal that the turn continued after answering (the last `.turn`'s text grows, or it reaches `done` status, whichever the mock host produces). |
  | 630-663 | fixture replay: `rendered.tools === 4`, `toolOk`, `toolFailed`, `titles` checks ("a tool call and its folded-in result render as one settled card", "a result the agent flagged as an error renders as a failed card", "a namespaced tool gets its real phrase") | Replace the three with **one** check: "tool calls from the host draw no line" (`rendered.tools === 0`). **Keep** "the mcp__rhino__ prefix never reaches the panel". |
  | 711-724 | "a call the turn ended without a result stops without claiming success" (spinners, running, unknown, ok, failed, dashed) | Replace with "an ended turn with unfinished calls draws no tool line and no spinner" (`.tool` count 0 and `.spinner` count 0 within `.transcript`). The `dashed` `getComputedStyle` call would throw on `null`, so remove it. |
  | 795-811 | "re-announced turns and tool calls do not duplicate rows" | Drop `tools` from both snapshots and keep `turns` and `users`. Rename the check to "re-announced turns do not duplicate rows". |

  Line numbers are approximate. Find each block with `grep -n "querySelectorAll('.tool" tools/verify.mjs`. After your edits, that grep must show only `=== 0`-style assertions.
- **Fixtures:**
  - `tests/host-events.json` and `src/protocol/mockHost.ts` still send tool events. **Leave them.** They prove that tool events arrive and draw nothing.
- **Commands** (run from `rhino/panel`):

  ```sh
  npm install          # only if node_modules is missing
  npm run typecheck
  npm test
  npm run verify
  npm run build        # rewrites dist/panel.host.html
  ```

## Scope

**In scope:**
- `src/ui/transcript.ts`
- delete `src/ui/toolCard.ts`, `src/state/tools.ts`, `src/state/tools.test.ts`
- `.tool*` CSS in `src/styles/panel.css`
- now-unused icons in `src/ui/icons.ts`
- `tools/verify.mjs`
- the rebuilt `dist/panel.host.html`

**Out of scope (do not touch):**
- every C# file: `rhino/plugin/UI/ToolSummary.cs`, `rhino/plugin/UI/ConversationFeed.ai.cs`, `PanelEvents*`, `PanelCommands*`
- `src/protocol/events.ts`, `src/state/store.ts` (tool blocks stay in the store)
- `tests/host-events.json`, `src/protocol/mockHost.ts`
- the question overlay (`src/ui/question.ts`)
- the thinking strip
- `rhino/panel/README.md`, except one line if it describes tool cards (check with `grep -n -i "tool card" README.md`)

## Steps

1. **Record the baseline.** Run `npm run typecheck && npm test && npm run verify` and note the pass counts. If verify already fails before your change, **STOP** and report which checks fail.
2. **Transcript.** In `src/ui/transcript.ts`:
   - make `case 'tool':` return `null` (match the `Child` type; check how other `null` children are used, e.g. `turn.prompt ? … : null`)
   - remove the `toolCard` import
   - change the agent-head condition as above

   Check: `npm run typecheck` passes.
3. **Dead code.** Delete `toolCard.ts`, `state/tools.ts` and `state/tools.test.ts` (after the grep check). Remove the now-unused CSS and icons (after their grep checks). Check: `npm run typecheck && npm test` passes. The test count drops only by the tests in `tools.test.ts`.
4. **verify.** Rewrite the checks per the table. Check: `npm run verify` passes, with no failures.
5. **Build.** Run `npm run build`. Check: `grep -c "tool-toggle" dist/panel.host.html` prints `0`.
6. **C# gates** (from the repo root, unchanged code, to confirm nothing broke):

   ```sh
   dotnet build rhino/plugin/RhinoAI.csproj -p:RhinoTarget=R9 -clp:NoSummary
   dotnet test tests/Server.Tests/Server.Tests.csproj
   ```

   Expect 0 errors and all tests passing. They don't depend on the panel source except the embedded `dist/panel.host.html`.
7. **Commit** on your branch with the message `Plan 018: remove the lookup lines from the transcript`. Include the rebuilt `dist/panel.host.html`.

## Done criteria

- `npm run typecheck`, `npm test` and `npm run verify` all pass.
- `grep -rn "toolCard\|iconForTool" src` prints nothing.
- `grep -c "tool-toggle" dist/panel.host.html` prints `0`.
- `git diff --stat` touches only in-scope files.
- `dotnet build … -p:RhinoTarget=R9` succeeds and `dotnet test tests/Server.Tests/Server.Tests.csproj` passes.

## Escape hatches

- If `state/tools.ts` turns out to be imported elsewhere (for example by `question.ts`), keep it and delete only what is unused. Report it.
- If a verify check can't be re-anchored to a non-tool progress signal because the mock host produces no text after the tool calls, **STOP** and report the check name and what the mock host emits. Don't weaken the check to always pass.
- If `npm install` is needed and fails, STOP and report.

## Maintenance note

Plan 022 adds options cards and plan 030 adds a check-in card inside turns. They must not reuse the `.tool` class names, because the verify checks now assert `.tool` count is 0.

## Manual Rhino check

Ask "what is selected?" and "how do I loft?". No lookup line appears. The thinking strip shows while Owly works, and the answer appears as before with one "Owly" head above it.
