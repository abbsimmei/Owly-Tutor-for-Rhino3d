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

## Done criteria

- Shared gates pass, and the new markdown tests pass.
- `git grep "click the command to copy it" -- rhino tests` finds nothing.

## Manual Rhino check

Run prompt-checks rows 1, 12, 13 and 14 in Rhino 9.
