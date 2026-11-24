# 019: Name each command once, in the order it is used

- **Written against:** `40f2c38` on `rhino-9.x`.
- **Status:** TODO
- **Effort:** S
- **Risk:** Low. A prompt edit plus a small panel rule.
- **Phase:** Phase 5 rework, commands & tools.

## Why

In mock-up 2, `_Pyramid` appears in the intro and again in step 1 ("Click `_Pyramid` to copy it…"). The product owner wants each command given **once, in the order the student uses it**. The decision was prompt plus panel:

1. **Prompt:** name each command once per answer, where it is used, in order of use. No separate "click it to copy" step.
2. **Panel safety net:** only the **first** mention of each command in one answer becomes a chip. Later mentions render as plain inline code.

## Facts you need

- **`rhino/plugin/Agents/AgentPrompts.cs`:**
  - `TeachingSteer` (`:32-40`) asks to "recommend the command … then give numbered steps".
  - `CommandSteer` (`:42-52`) ends: "…So tell the student to click the command to copy it, not to type it. Never say that you typed, ran, opened or highlighted anything in Rhino."
  - Together these make the repeat likely.
- **`tests/Server.Tests/AgentPromptsTests.ai.cs:19-24`** asserts the phrase "click the command to copy it". Change the test with the prompt.
- **`rhino/panel/src/core/markdown.ts:91-92`:** `if (options.command && isCommandName(code)) … commandChip(…)`. Every match becomes a chip.
  - **One answer can be split into several text blocks.** `ui/transcript.ts` (`block()`) renders each text block separately through `agentMessage` (`ui/message.ts`), and each block's markdown is re-rendered from scratch about once per frame while it streams. So "first mention" must be computed **per turn, across its text blocks**: block *n* gets the set of commands already seen in blocks 0…n-1, plus the commands earlier in its own text.
  - `markdown.ts` uses `document`, and `node --test` has no DOM, so put the logic in a pure helper.
  - Compare names case-insensitively, ignoring a leading `-` after the underscore (`_-Loft` and `_Loft` are the same command).
- **`docs/owly/prompt-checks.md`** has rows 1-12. Add new rows.

## Steps

1. **Prompt.**
   - `TeachingSteer`: "Name each command once, at the step where the student uses it, in the order they use it. Don't mention a command in the introduction and again in a step."
   - `CommandSteer`: replace the "tell the student to click the command to copy it" sentence with a neutral description: "each command becomes a button in the panel that the student can click to copy it". Keep "Never say that you typed, ran, opened or highlighted anything in Rhino." Plan 022 extends this text for Run.
2. **Tests.** Update `AgentPromptsTests` to assert the new once-in-order sentence and the never-claim sentence.
3. **Panel.**
   - New pure helper `rhino/panel/src/core/commandMentions.ts`, with `firstMentions(blockTexts: string[]): Set<string>[]`, giving the commands first seen in each block. It shares `isCommandName` and the inline-code regex with `markdown.ts`.
   - `MarkdownOptions.command` gets `isFirst(name)`. `transcript.ts` passes each text block a predicate built from the turn's earlier blocks.
   - Inside one render, the first `_Name` becomes a chip and later ones plain `<code>`.
   - Tests in `commandMentions.test.ts`:
     - two `_Loft` give one first mention
     - `_Loft` then `_Cap` give two
     - a mention in block 1 then block 2 is first only in block 1
     - `_-Loft` equals `_Loft`
   - A verify check: a repeated `_Loft` makes one chip.
4. **Prompt checks.** Add rows 13 ("How do I make a pyramid roof?": `_Pyramid` appears once, and multi-command answers list commands in order) and 14 (a three-step loft workflow: `_Curve`, `_Loft`, `_Cap` each once, in order).
5. Rebuild `dist/panel.host.html`.

## Advisor addendum (checked against `15b9a13` before execution)

- **How the chip options reach the renderer.** `rhino/panel/src/ui/message.ts`, `agentMessage(ctx, turnId, text, streaming)`, builds one `MarkdownOptions` object with `command: { copy, glyph, label, locate, locateLabel }`. It calls `renderMarkdown(text.peek(), options)` on every animation frame.
  - `renderMarkdown(source, options)` is at `rhino/panel/src/core/markdown.ts:~323`.
  - Inline code is matched by the first alternative of the `INLINE` regex (`` /`([^`\n]+)`/ ``, `markdown.ts:~38-40`).
  - The chip branch is `if (options.command && isCommandName(code)) fragment.appendChild(commandChip(code, options.command));`.
- **Design to implement:**
  1. Add an optional `seenBefore?: () => ReadonlySet<string>` to `CommandChipOptions`, holding the normalised names already used as chips in **earlier** text blocks of the same turn.
  2. `renderMarkdown` creates a fresh `Set` per call, seeded from `seenBefore?.()`. The inline renderer checks and adds to it: the first time a normalised name is seen it renders the chip; otherwise plain `<code>`. Thread the set through `blocks()` → `inline()` via the options object or an extra parameter, whichever fits the existing code. Don't use a module-level variable.
  3. **Normalising:** lowercase, and strip the leading `_` and an optional `-` (`_-Loft` → `loft`).
  4. `src/core/commandMentions.ts` exports `normaliseCommand(name)` and `commandsIn(text): string[]`, the command names in order of first appearance in a markdown text (inline code only, matching `isCommandName`; ignore fenced code blocks, like the renderer). Both are pure and DOM-free. `markdown.ts` and `transcript.ts` use them.
  5. `agentMessage` gets a new last parameter `seenBefore: () => ReadonlySet<string>` and passes it in `options.command`.
  6. `transcript.ts` `block()`, in `case 'text'`, passes `() => new Set(turn.blocks().slice(0, index).filter(b => b.kind === 'text').flatMap(b => commandsIn(b.text.peek())).map(normaliseCommand))`, where `index` is this view's position in `turn.blocks()` at call time. Find it with `turn.blocks().findIndex(b => b.id === view.id)`. Check the `BlockView` type in `src/state/store.ts` for the exact field holding a text block's signal.
- **Tests:** add `src/core/commandMentions.test.ts`, following `src/core/commandName.test.ts`'s style (`node:test`, `assert`). Cover:
  - `commandsIn("Use `_Loft` then `_Loft` and `_Cap`")` → `['_Loft','_Cap']`
  - fenced code ignored
  - `` `layer_name` `` ignored
  - `normaliseCommand('_-Loft') === normaliseCommand('_loft')`
- **verify:** in `rhino/panel/tools/verify.mjs`, next to the existing chip checks (search `md-cmd-copy`), add a check that an answer mentioning `` `_Loft` `` twice renders exactly one `.md-cmd` chip and one plain `code`. If the mock host has no such answer, add one to the scripted reply in `src/protocol/mockHost.ts` that already contains `` `_Loft` `` (search `_Loft`). Don't create a new scenario.
- **Prompt checks:** `docs/owly/prompt-checks.md` rows **1** and **12** currently expect "Tells the student to click `_Loft` to copy it". Change them to "Names `_Loft` once (it becomes a copy button); no separate 'click it to copy' step". Then add rows 13 and 14 in the same table format.
- **`tests/Server.Tests/AgentPromptsTests.ai.cs`:** the test `Compose_tells_model_about_command_buttons` asserts `"click the command to copy it"`. Replace that assertion with the new once-in-order sentence (an exact substring of the new `TeachingSteer` text), and keep the "Never say that you typed, ran, opened or highlighted anything in Rhino." assertion.
- **Out of scope:** the `case 'tool'` line and the agent-head condition in `transcript.ts` (plan 018 changes those), and `rhino/plugin/UI/*`.
- **Gates:**
  - in `rhino/panel`: `npm run typecheck && npm test && npm run verify && npm run build` (commit `dist/panel.host.html`)
  - from the repo root: `dotnet build rhino/plugin/RhinoAI.csproj -p:RhinoTarget=R9 -clp:NoSummary` and `dotnet test tests/Server.Tests/Server.Tests.csproj`
- **Commit message:** `Plan 019: name each command once, in order`.

## Done criteria

- Shared gates pass, and the new markdown tests pass.
- `git grep "click the command to copy it" -- rhino tests` finds nothing.

## Manual Rhino check

Run prompt-checks rows 1, 12, 13 and 14 in Rhino 9.
