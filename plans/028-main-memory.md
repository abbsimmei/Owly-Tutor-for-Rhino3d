# 028: Main memory (opt-in)

- **Written against:** `40f2c38` on `rhino-9.x`. **Requires plan 027 to be DONE.**
- **Status:** TODO
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
