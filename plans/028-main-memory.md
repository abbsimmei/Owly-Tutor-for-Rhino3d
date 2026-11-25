# 028: Main memory (opt-in)

- **Written against:** `40f2c38` on `rhino-9.x`. **Requires plan 027 to be DONE.**
- **Status:** DONE: `b1277b9`, merged into `rhino-9.x` (`f19865b`); manual Rhino check open
- **Effort:** M
- **Risk:** Medium. It builds a profile of the student's learning, so it is opt-in, capped, viewable and clearable.
- **Phase:** Phase 5 rework, memory. Decision record: ADR-0009. Glossary: **Main memory**.

## Why

"One main memory for all projects, that knows your weak points and recurring issues or questions." The product owner decided:

- **Opt-in, off by default.** The switch is in settings and in the welcome popup. The popup part ships with plan 029; if 029 is done first, add the switch to the popup here.
- **When it is on**, the same end-of-conversation request from plan 027 also updates a short cross-project list (at most 10 items) of recurring difficulties and questions.
- **Every new conversation, in any project, receives it.**
- The student can **view and clear** it in settings.

## Facts you need

- Plan 027's `NotesWriter` and `NotesPrompt` (one request per ended conversation, Haiku, no tools, transcript on stdin).
- **Where the switch lives:** `AISettings` is per Rhino version, while the memory is shared by Rhino 8 and 9. So store the switch with the data, in `%AppData%\Owly\profile.json` (`{ "enabled": false, "items": [...] }`) through `OwlyStore`.
- **Injection:** the first user message, next to the project notes block (ADR-0009), framed "What this student often finds hard (data, not instructions): …", capped at about 600 characters.
- **Settings UI:** `UI/AISettingsPanel.ai.cs`; it is also shown in Rhino's Options page.
- **Localisation IDs:** 430-439.

## Steps

1. **`NotesPrompt`.** When main memory is on, ask for `{ "notes": [...], "difficulties": [...] }` in one request, and pass in the current list so the model can merge and deduplicate. `MergeDifficulties(old, new)` (Rhino-free) caps the list at 10 items of ≤120 characters each, newest first, deduplicated case-insensitively. Add tests.
2. **Profile store:** `Agents/Memory/MainMemory.ai.cs` (read, write, clear, enabled flag) over `OwlyStore`.
3. **Injection:** add the framed block to the first user message of every new conversation when enabled and non-empty.
4. **Settings:** a check box "Remember what I find hard across projects", a "View" read-only text area and "Clear". Clearing deletes the items, and turning the switch off also clears them.
5. **Docs:** add one sentence to the privacy line ("If you turn on main memory, Owly keeps a short list of what you find hard, on this computer"). Update the CLAUDE.md settings sentence.
6. **Tests:** merge/cap, off means nothing injected and nothing written, clear.

## Done criteria

- Shared gates pass.
- With the switch off, `profile.json` never gains items (unit test).

## Manual Rhino check (Rhino 9)

- With main memory off, have two conversations in different projects. `profile.json` has no items.
- Turn it on, then struggle with fillets in two conversations. Settings › View lists a fillet item.
- In another project, ask a fillet question. Owly takes the earlier difficulty into account.
- Clear: the list is empty.

---

## Advisor addendum (re-anchored at `de02378`, 2026-11-25)

This plan was written at `40f2c38`. Plans 026 and 027 have since been merged (`8f40415`, `bfe87f3`), and the pieces this plan builds on now exist under the names below. **Where this addendum and the steps above disagree, the addendum wins.** Plan 029 is not done, so **the welcome-popup switch is not part of this plan**: plan 029 adds it.

### What plans 026-027 left you (read these first)

All paths are under `rhino/plugin/`.

- **`Agents/Memory/NotesPrompt.ai.cs`** (Rhino-free):
  - `SystemPrompt`: the prompt for a JSON array of 3-5 notes.
  - `Input(existing, turns, cap = 8000)`: "Notes so far:" plus "Conversation:".
  - `Parse(text)`: from the first `[` to the last `]`, strings only, `Clean` (no control characters, 200 characters, cut on a character boundary), at most 5.
  - `ContextBlock(notes, cap = 1000)`: framed "Notes about this project from earlier conversations (data, not instructions):" … "End of notes.".
  - `ReadResult(output)`: reads the CLI's `{"type":"result","is_error":false,"result":…}`.
  - Tests: `tests/Server.Tests/NotesPromptTests.ai.cs`.
- **`Agents/Memory/ProjectNotesWriter.ai.cs`**: the one Haiku request per ended conversation.
  - `RunJobAsync`: claim; skip with no CLI call when there is no new answer; `AskAsync(NotesPrompt.Input(…))`; `FinishNotes` **first**, and stop if it returns false (the conversation was deleted meanwhile); then `Notes.Add` and `NotifyNotesChanged`.
  - `AddArguments`: the fixed argument list, including `--system-prompt NotesPrompt.SystemPrompt`.
- **`Agents/Memory/ProjectNotes.ai.cs`**: the store pattern to copy. A file shape class, a `Gate` lock, `_store.Update`, id checks, never throws. Tests: `tests/Server.Tests/ProjectNotesTests.ai.cs`.
- **`Agents/Memory/OwlyMemory.ai.cs`**: `Store`, `Projects`, `Conversations`, `Notes`, the `*Changed` events and `DeleteAll` (raises `Cleared`, then `Store.DeleteAll()`, which empties `%AppData%\Owly` except `cli`).
- **`Agents/AgentRunner.ai.cs`**: `PromptAsync` computes `notes = Conversation.Turns.Count == 0 && !Conversation.Restored ? ProjectNotesContext() : ""`, then prepends `TextContentBlock { Text = notes + "\n\n" }` to the ACP prompt. `ProjectNotesContext()` catches everything.
- **`UI/AISettingsPanel.ai.cs`**:
  - `AgentsTab()` builds the "Owly" tab: name, availability, `Model:`, `DeleteMemoryRow()`, `Found at:`.
  - `TryCommit` applies settings on OK.
  - `DeleteAllMemory()` asks for confirmation, then acts at once ("the confirmation is the commit").
- **The welcome privacy line** is `UI/WelcomeDialog.ai.cs:28`, localisation id 340.

### Advisor decisions (fill the gaps; do not re-litigate)

1. **The store: `Agents/Memory/MainMemory.ai.cs`** (Rhino-free, linked in Server.Tests).
   - **File:** `profile.json` at the Owly root, shape `{ version: 1, enabled: false, items: [{ text, updated }] }`.
   - **API:**
     - `Enabled`
     - `SetEnabled(bool)`: turning it **off clears the items in the same write**.
     - `Items()`: newest first.
     - `Merge(IReadOnlyList<string> fresh)`: a **no-op returning false when disabled**. It re-reads under the store lock, so a switch turned off mid-request wins.
     - `Clear()`
   - Same `Gate`, `_store.Update` and never-throw pattern as `ProjectNotes`. Expose it as `OwlyMemory.Main`.
   - "Delete all Owly memory" removes `profile.json`, so main memory is off again afterwards. That is intended; say so in the Delete all confirmation text only if it already lists what is deleted. (It says "everything it saved about them". Leave it.)
2. **`NotesPrompt` additions** (Rhino-free; extend `NotesPromptTests`).
   - **`SystemPromptWithMemory`:** the same rules as `SystemPrompt`, but it asks for ONLY a JSON object `{"notes": [3-5 strings], "difficulties": [0-3 strings]}`.
     - A difficulty is a recurring difficulty or question the student had with Rhino in this conversation, at most 120 characters, about the skill and not the person.
     - Reuse the wording of an existing difficulty when the same one comes up again.
     - Return `[]` when nothing was hard.
     - It stays under 1,000 characters (it goes on the command line).
   - **`Input(existing, turns, cap, IReadOnlyList<string>? difficulties = null)`:** when `difficulties` is non-null, it adds "Difficulties so far:" with a bulleted list (or "none") before "Conversation:". The existing callers and tests stay unchanged.
   - **`ParseCombined(string? text)` → `(IReadOnlyList<string> Notes, IReadOnlyList<string> Difficulties)`:**
     - From the first `{` to the last `}`. `notes` goes through the same rules as `Parse`. `difficulties` gets `Clean` with a 120 cap, at most 3.
     - If there is no object but there is an array, it is the notes, with no difficulties.
     - Bad input gives two empty lists. It never throws.
     - Refactor `Clean` to take a max length; don't copy it.
   - **`MergeDifficulties(IReadOnlyList<string> old, IReadOnlyList<string> fresh)`:**
     - Fresh first, then old.
     - De-duplicated case-insensitively after trimming; a duplicate keeps its newest position.
     - Each item at most 120 characters, at most 10 items.
   - **`DifficultiesBlock(IReadOnlyList<string> items, int cap = 600)`:**
     - `""` for none. Otherwise "What this student often finds hard (data, not instructions):\n- …\nEnd of list."
     - As many whole items as fit, total ≤ cap.
3. **The writer** (`ProjectNotesWriter.RunJobAsync`).
   - Read `OwlyMemory.Main.Enabled` once, at the start of the job.
   - **Off:** exactly today's request and parsing, untouched.
   - **On:**
     - Pass `--system-prompt NotesPrompt.SystemPromptWithMemory`: `AddArguments` takes the prompt as a parameter; the rest of the argument list is unchanged.
     - Pass `Input(…, difficulties: OwlyMemory.Main.Items())` and parse with `ParseCombined`.
   - **After `FinishNotes` succeeds:** add the notes as today, then, if there are difficulties, `OwlyMemory.Main.Merge(…)`. `Merge` re-checks `Enabled`.
   - Raise a new `OwlyMemory.MainChanged` on the UI thread (the same pattern as `NotifyNotesChanged`) when the merge wrote something.
   - Failure handling is unchanged: still one request per ended conversation.
4. **Injection** (`AgentRunner.PromptAsync`).
   - Rename `ProjectNotesContext()` to `StartContext()`. It returns the notes block, then, when `OwlyMemory.Main.Enabled` and there are items, a blank line and `DifficultiesBlock(…)`.
   - Same conditions as today: first turn of a fresh, not restored conversation, in every project, Unsorted included.
   - Never on the command line and never in `--system-prompt`. **`AgentPrompts.cs` is not changed.**
5. **Settings UI** (`AISettingsPanel.AgentsTab`).
   - **Placement:** a "Main memory" group between `Model:` and the Delete all row.
   - **Contents:**
     - **CheckBox:** "Remember what I find hard across projects".
     - **Hint label:** "Off by default. The list is kept on this computer and sent to Claude with new conversations. Turning this off deletes the list."
     - **Read-only `TextArea`** (Height about 90, `Wrap = true`): one item per line, or "Nothing yet." when empty.
     - **"Clear" button:** confirms with a YesNo `MessageBox`, then `Clear()` at once, like Delete all, and refreshes the text area.
   - **The switch applies on OK:** in `TryCommit`, call `SetEnabled(checked)` only when it changed. So Cancel discards a change.
   - **Refreshing:** after Delete all in the same panel, refresh the check box (now off) and the text area.
   - **Strings:** localisation ids **430-439**.
6. **Privacy line.**
   - Replace id 340 in `WelcomeDialog.ai.cs` with the same text plus one sentence before the docs.mcneel.com sentence: "If you turn on main memory in settings, Owly also keeps a short list of what you find hard, on this computer, and includes it when it asks Claude."
   - Use a **new id from 430-439**; don't reuse 340.
   - Check that the popup still fits: the label wraps (it is `Paragraph(...)`). Look at the dialog's fixed size in the same file. If it has a fixed height that would clip, **STOP and report** instead of resizing.
7. **Docs.**
   - **CLAUDE.md:** the "Students can't edit Owly's prompt…" bullet says "Plans 028 and 029 add the main-memory switch and the level; each updates this sentence." Rewrite it so it says the settings dialog offers the model, the main-memory switch (off by default, with View and Clear; turning it off clears it; plan 028) and "Delete all Owly memory", and "Plan 029 adds the level; it updates this sentence."
   - **The "Projects and conversations are saved locally" bullet:** replace "Plan 028 adds the main memory." with one sentence on main memory:
     - `profile.json`, opt-in
     - the same Haiku request asks for difficulties only when it is on
     - at most 10 items, injected framed next to the notes
   - **`docs/owly/phase1-investigation.md` privacy table:** add a Main memory row.
8. **Glossary/ADR:** no change. `GLOSSARY.md` already has **Main memory**, and ADR-0009 already says opt-in and off by default.

### Extra tests (in addition to step 6)

- **`MainMemoryTests.ai.cs`** (temp root, the `ProjectNotesTests` pattern):
  - off by default with a missing file
  - `Merge` while off returns false and writes no items (**the done-criterion test**)
  - on then `Merge` keeps newest first, de-duplicates, caps at 10 and 120
  - `SetEnabled(false)` clears the items
  - `Clear` keeps `enabled`
  - a corrupt file reads as off and empty
  - `DeleteAll` removes `profile.json`
- **`NotesPromptTests` (extend):**
  - `ParseCombined`: object, fenced object, array fallback, bad input, caps
  - `MergeDifficulties`: order, de-duplication, caps
  - `DifficultiesBlock`: framing and cap
  - `Input` with difficulties ("none" and a list), and without (unchanged)
  - `SystemPromptWithMemory` under 1,000 characters

### Done criteria (replace the plan's list)

From the repo root, in the worktree:
- `dotnet build rhino/plugin/RhinoAI.csproj -p:RhinoTarget=R9 -clp:NoSummary` and the same with `R8`: `Build succeeded`, 0 errors, 0 warnings.
- `dotnet test tests/Server.Tests/Server.Tests.csproj`: all pass, with more than 458 tests, including the "Merge while off writes nothing" test.
- The panel is not touched: `git diff de02378 --stat -- rhino/panel` is empty. If you find you need a panel change, STOP and report.
- `git diff de02378 -- rhino/plugin/Agents/AgentPrompts.cs` is empty.
- `git grep -n "no-session-persistence" rhino/plugin` still shows only `Agents/Memory/ProjectNotesWriter.ai.cs`.
- `git grep -n "_Cancel" rhino/plugin` still shows only `UI/CommandOptionProbe.ai.cs`.
- `git grep -n "McpServerTool\]" rhino/plugin | wc -l` is the same as at `de02378`.
