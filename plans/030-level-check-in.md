# 030: Level check-in

- **Written against:** `40f2c38` on `rhino-9.x`. **Requires plan 029 to be DONE.**
- **Status:** TODO
- **Effort:** S
- **Risk:** Low.
- **Phase:** Phase 5 rework, user level (PRD §3.4). Glossary: **Level check-in**.

## Why

"Ask users also after a while if they feel like the explanations are good, too easy or too hard." The product owner decided:

- **When:** after the **10th answer** Owly has given in total, then **every 30 answers**. At most once per conversation, and never while an answer is still running. If a conversation already had a check-in, the next one waits for the next conversation.
- **What:** a small card under the answer with three buttons, **Good / Too easy / Too hard**. The student can ignore it.
- **Effect:**
  - "Good" closes the card.
  - "Too easy" offers a one-click "Switch to Intermediate?" (one level up). "Too hard" offers one level down.
  - **Nothing changes unless the student clicks.**
  - At the top or bottom level, the card just thanks the student.
- The card is **not saved** with the conversation.

## Facts you need

- `AISettings.Level` and the level-change path (end the idle CLI, then relaunch with `--resume`) come from plan 029.
- **A completed answer** is the end of a live turn in the host (`ConversationFeed` / `AgentRunner` turn end). Count only live turns, **never replays** of saved conversations (plan 026).
- **Panel cards** follow the pattern of the other turn-attached UI (`ui/transcript.ts`). The state lives in the store, not the DOM.
- **Localisation IDs:** 450-459.

## Steps

1. **Schedule** (Rhino-free; link it in Server.Tests). `CheckInSchedule.IsDue(answersGiven)` is true at 10, 40, 70, …. Persist `AISettings.AnswersGiven`, plus a per-conversation "already asked" flag in memory and a `CheckInPending` flag when a due check-in was skipped. Add `CheckInScheduleTests`.
2. **Host.** After each completed live turn, increment the counter. If a check-in is due (or pending) and none has been shown in this conversation, post `checkin {turnId, level}`. A handler takes `checkin.answer {verdict}` (only logged as a debug event, never sent anywhere) and `level.set {level}`, which goes through plan 029's setter.
3. **Panel.** `ui/checkInCard.ts` sits under the turn's answer. After Too easy or Too hard it shows "Switch to {level}?" with Switch and No thanks buttons. Switch sends `level.set` and shows a confirmation notice.
4. **Tests:**
   - the schedule
   - verify:
     - the card appears for the mock's `checkin` event
     - Too easy then Switch sends `level.set` with the next level up
     - at Advanced, Too easy shows only thanks

   Update `mockHost.ts` and `host-events.json`. Rebuild the panel.

## Done criteria

- Shared gates pass.
- Replaying a saved conversation never increments the counter (unit or integration test).

## Manual Rhino check (Rhino 9)

- Set `AnswersGiven` to 9 in settings (or a debug build) and ask a question. The card appears under the answer.
- Click Too hard, then Switch. The level in settings is one lower.
- Ask more questions: no second card in the same conversation.
