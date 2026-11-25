# 030: Level check-in

- **Written against:** `40f2c38` on `rhino-9.x`. **Requires plan 029 to be DONE.**
- **Status:** DONE: `daa4e99`, merged into `rhino-9.x` (`0f2346d`); manual Rhino check open
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

---

## Advisor addendum (re-anchored at `b8208ac`, 2026-11-25)

This plan was written at `40f2c38`. Plans 022-029 have since been merged. Plan 029 (`8371148`) gives you the level. **Where this addendum and the steps above disagree, the addendum wins.**

### Facts at `b8208ac` (read these first)

- **The level (plan 029):**
  - `rhino/plugin/Agents/OwlyLevel.ai.cs`: `enum OwlyLevel { Beginner, Intermediate, Advanced }` and `OwlyLevelExtensions.Parse(string?)`, which falls back to Beginner.
  - `AISettings.Level` (get/set, per Rhino version).
  - `AgentHost.RestartIdleAgents()`: call it after a level change, so the next prompt relaunches the CLI with the new prompt. That is the existing "level setter" path, used by `UI/AISettingsPanel.ai.cs` `TryCommit` and `UI/WelcomeDialog.ai.cs` `SaveChoices`.
  - The settings drop-down uses localisation ids 447-449 for the plain names "Beginner", "Intermediate", "Advanced".
- **A live turn** runs only through `rhino/plugin/Agents/AgentRunner.ai.cs` `PromptAsync`. It calls `Conversation.BeginTurn`, then `await connection.SessionPromptAsync(...)`, which returns a `PromptResponse` with `StopReason` (currently ignored). Its `finally` runs `CompleteTurn` and `PersistTurn` (and plan 029's restart).
  - **A reopened saved conversation** (plan 026) is rebuilt by `Conversation.Restore` and shown through `ConversationFeed.Replay`. It **never** goes through `PromptAsync`.
  - So counting in `PromptAsync` alone already excludes replays.
- **Turn ids in the panel:** `rhino/plugin/UI/ConversationFeed.ai.cs` names turns `turn-{index}`, where index is the turn's position in `Conversation.Turns`. So a turn's panel id is `$"turn-{Conversation.Turns.Count - 1}"` right after it completes.
- **Settings:** `AISettings.cs` has `Settings.GetInteger/SetInteger/GetBool/SetBool`; copy `WelcomeVersion`. PersistentSettings belongs on the UI thread.
- **The view model** posts to the panel with `Bridge.Post(...)`. Commands arrive in `UI/AIPanelViewModel.ai.cs` `IncomingCommand` (a switch over `PanelCommand` records). Records live in `UI/PanelEvents.ai.cs` / `UI/PanelCommands.ai.cs` with `JsonDerivedType` names.
- **Turn rendering:** `rhino/panel/src/ui/transcript.ts` `turnView(ctx, turn)` (~line 92) renders a turn, including `turn-foot` and `turn-error`.
- **Store reset:** the store (`src/state/store.ts`) handles a `conversation` event (new or replayed transcript) at ~line 229. Clear the check-in there.

### Advisor decisions (fill the gaps; do not re-litigate)

1. **`CheckInSchedule` (Rhino-free, `rhino/plugin/Agents/CheckInSchedule.ai.cs`, linked in Server.Tests).**
   - `IsDue(int answers)`: true at 10, 40, 70, … (`answers >= 10 && (answers - 10) % 30 == 0`).
   - `Decide(int answers, bool pending, bool shownInConversation)` → `(bool Show, bool Pending)`:
     - If due or pending: show when not yet shown in this conversation (then Pending=false); otherwise Pending=true.
     - If neither: no show, and Pending is unchanged.
   - `ShouldCount(bool completedNormally, bool hasAnswerText)` → both true.
2. **Counting**, only in `AgentRunner.PromptAsync`:
   - **What counts:** keep the `PromptResponse`. After it returns without throwing, count the turn when `StopReason == EndTurn` and the turn has at least one `AssistantText` event.
   - **Where it goes:** marshal one call with `RhinoApp.InvokeOnUiThread` to a new Rhino-side `rhino/plugin/Agents/CheckIns.ai.cs` `AnswerCompleted(Conversation convo, string turnId)`.
   - **What `AnswerCompleted` does:**
     - increments `AISettings.AnswersGiven` (an int, default 0)
     - runs `CheckInSchedule.Decide` with `AISettings.CheckInPending` (bool) and `convo.CheckInShown`, a new **in-memory** bool on `Conversation`, never saved or restored
     - stores `Pending`
     - when `Show`, raises `CheckIns.Due` (args: `Conversation`, `TurnId`, settable `Delivered`)
     - if nobody set `Delivered` (no panel shows that conversation), sets `CheckInPending = true` instead of marking it shown
   - **Not counted:** cancelled, failed or thrown turns, and answers to `ask_user` that end without text.
   - **Replays:** never counted, by construction.
3. **The view model** subscribes to `CheckIns.Due` (and unsubscribes in `Dispose`). If `ReferenceEquals(e.Conversation, ActiveConversation)` and `View.Loaded`:
   - post `CheckInEvent(e.TurnId, AISettings.Level.ToString())`
   - set `e.Delivered = true` and `e.Conversation.CheckInShown = true`
4. **Protocol:**
   - **Host → panel:** `checkin {turnId, level}`.
   - **Panel → host:**
     - `checkin.answer {verdict}`, verdict `good` | `easy` | `hard`. The host validates it and only calls `HostUtils.LogDebugEvent`; nothing is sent or stored.
     - `level.set {level}`. The host accepts only an exact enum name (`Enum.TryParse` with `ignoreCase: false` plus `Enum.IsDefined`); anything else is ignored. When it differs from the current level: `AISettings.Level = …`, `AgentHost.RestartIdleAgents()`, then post `NoticeEvent("info", "Owly now explains at the {level} level.")`. The level name comes from localisation ids 447-449; the sentence uses a new id.
   - **C# records:** `CheckInEvent`, `CheckInAnswerCommand`, `SetLevelCommand`. Keep `events.ts`, `mockHost.ts` and `tests/host-events.json` in sync, and add a C# test that pins the `checkin` JSON against the fixture line (the plan 026 pattern in `ConversationStoreTests`).
5. **Panel** (`src/ui/checkInCard.ts`, state in the store).
   - **State:** `store.checkIn` holds `{turnId, level, step: 'ask' | 'offer' | 'thanks', target?}` or null. It is set by `checkin`, and cleared by the `conversation` event and by every close.
   - **Placement:** `turnView` renders the card under the answer of the turn whose id matches, only once that turn is no longer running.
   - **Step `ask`:** "How are the explanations?" with **Good / Too easy / Too hard**, plus a small ✕ (`aria-label` "Close") that just clears it.
     - **Good:** sends `checkin.answer good` and clears.
     - **Too easy / Too hard:** sends `checkin.answer easy|hard`. Then, if there is a level up (or down), go to `offer` with that target; at the top (bottom), go to `thanks`.
   - **Step `offer`:** "Switch to {Level}?" with **Switch** (sends `level.set {target}` and clears) and **No thanks** (clears).
   - **Step `thanks`:** "Thanks! Owly keeps the current level." with ✕.
   - **No owl** in this card.
   - **Not saved:** a panel reload or a reopened conversation shows no card.
   - **Panel strings** in `PanelStrings.ai.cs` and `strings.ts`, localisation ids **450-459**. The panel needs the three level names: reuse the C# strings with ids 447-449 under new panel keys; don't add new ids for them.
6. **Mock host:** a mock answer to a prompt containing the word `checkin` is followed by a `checkin` event for that turn (level `Intermediate`). Ordinary mock prompts never show a card, so the existing verify checks are unaffected. `level.set` in the mock answers with the notice.
7. **Docs:**
   - **CLAUDE.md:** add to the "Students can't edit…" bullet that a level check-in card can offer a one-step change, which happens only on the student's click (plan 030).
   - **PRD §3.4:** add "(Built: plan 030.)" to the check-in bullet.
   - The glossary already has **Level check-in**.

### Tests

- **`CheckInScheduleTests`:**
  - due at 10, 40, 70; not due at 0, 9, 11, 39
  - `Decide`: shown at a due count when not shown yet; pending when it is already shown in this conversation; a pending check-in is shown in the next conversation at a non-due count; pending stays unchanged when it is neither due nor pending
  - `ShouldCount` truth table
- **The JSON pin** for `checkin` against `host-events.json`.
- **verify** (mock host):
  - prompt "checkin": the card appears under that turn
  - Too easy, then Switch, sends `level.set Advanced` (from Intermediate) and the card disappears
  - a fresh prompt "checkin", then Too hard, then No thanks: nothing is sent
  - with the mock level `Advanced`, Too easy shows the thanks text and sends no `level.set`
  - the card's ✕ clears it
  - the owl checks still pass

### Done criteria (replace the plan's list)

From the repo root, in the worktree:
- `dotnet build rhino/plugin/RhinoAI.csproj -p:RhinoTarget=R9 -clp:NoSummary` and the same with `R8`: `Build succeeded`, 0 errors, 0 warnings.
- `dotnet test tests/Server.Tests/Server.Tests.csproj`: all pass, with more than 502 tests.
- `cd rhino/panel && npm run typecheck && npm test && npm run build && npm run verify`: all pass. The rebuilt `dist/panel.host.html` is committed.
- **Replay-safety by construction:** `git grep -n "AnswerCompleted" rhino/plugin` shows only `Agents/AgentRunner.ai.cs` and `Agents/CheckIns.ai.cs`.
- `git grep -n "AnswersGiven" rhino/plugin` shows only `AISettings.cs` and `Agents/CheckIns.ai.cs`.
- `git diff b8208ac -- rhino/plugin/Agents/AgentPrompts.cs art` is empty.
- `git grep -n "_Cancel" rhino/plugin` still shows only `UI/CommandOptionProbe.ai.cs`.
- `git grep -n "McpServerTool\]" rhino/plugin | wc -l` is the same as at `b8208ac`.
- **Escape hatch:** if counting needs the `StopReason` and `PromptAsync` can't get it without changing `IAcpAgent`, STOP and report.

### Manual check note

There is no settings UI for `AnswersGiven`. To test, set it in Rhino's PersistentSettings for the Owly plugin (Rhino's Options → Advanced, or a debug build). Alternatively, ask ten short questions.
