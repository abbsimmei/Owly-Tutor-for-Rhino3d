# 026: Saved conversations per project, with the owl strip

- **Written against:** `40f2c38` on `rhino-9.x`. **Requires plan 025 to be DONE.**
- **Status:** DONE: `e92fd7b` + `4cd3e9e` + `e144231`, merged into `rhino-9.x` (`8f40415`); manual Rhino check open
- **Effort:** L
- **Risk:** Medium. It restores resume code removed in plan 009 and changes the CLI's working directory.
- **Phase:** Phase 5 rework, memory. Decision record: ADR-0009. Glossary: **Conversation**, **Transcript**.

## Why

Mock-up 2 shows a narrow strip of owl buttons on the right edge of the panel. Each owl is one saved conversation of the current project. The product owner decided:

- **Strip:**
  - newest on top
  - the open conversation outlined
  - it scrolls when long
  - hover shows the title (from the first question) and the date
  - right-click offers Rename and Delete
- The header's **+** still starts a new conversation.
- **Reopening a file starts a new, empty conversation.** The old ones are in the strip.
- **Opening an old conversation continues it:**
  - `--resume <session id>` while Claude Code's own session file still exists (about 30 days)
  - otherwise a fresh session, with the saved transcript given back as context
- **The word stays "conversation"** in the UI and docs ("New conversation", "Delete conversation").

## Facts you need

- **Removed code to restore** (commit `a2b0ef7`; read with `git show a2b0ef7^:<path>`):
  - `rhino/plugin/Agents/ConversationStore.ai.cs` (152 lines, PersistentSettings; **replace the storage** with `OwlyStore` files)
  - `Agents/ConversationDto.ai.cs`: SessionId, AgentName, DocTitle, StartedAt, Lifecycle, Turns (each with Prompt, times, Events, Usage, Attachments)
  - `Conversation.Restore(dto)`
  - `AgentHost.TryResume` / `CreateResumed`
  - the `StreamJsonAgent` constructor that seeds `HasEverStarted`, plus `ResumePending`: if the CLI rejects `--resume`, switch to a fresh GUID and note it
  - `AgentRunner.PersistTurn` (saved after every turn)
  - the panel's old `ui/history.ts` drawer, as a UI reference only, since the strip replaces it
- **Session ids** are minted by Owly (`AgentRunner.ai.cs:34`, `StreamJsonAgent.ai.cs:70-71`). The first launch passes `--session-id`, later ones `--resume` (`ClaudeStreamJsonParser.ai.cs:103-104`).
- **The CLI's working directory follows the document** today: its folder, or Temp when Untitled (`AgentDispatch.ai.cs:150-152`). **Claude Code finds a session only from the working directory it was started in.** Make the working directory a fixed `%AppData%\Owly\cli`. That is also better for privacy, because Claude Code's own files then always sit under one folder.
- **The panel snapshot** already carries the session id (`UI/ConversationFeed.ai.cs:69`).
- **Layout:** `.stage` is a grid (`panel.css:~1167`) inside `transcript()` (`ui/transcript.ts:247-266`). Add `grid-template-columns: 1fr auto` with the strip as the second child. Watch `scrollbar-gutter: stable` (`panel.css:~366`) and the z-index layers.
- **Owl art rules (CLAUDE.md):**
  - Use `owl()` from `src/ui/owl.ts`.
  - Outline the `<button>`, never the `.owl` element.
  - The `.owl` element gets no background, filter, blend mode or rounded clipping.
  - `npm run verify`'s owl checks must still pass.
- **Right-click menu:** `UI/PanelMenu.ai.cs` / `OpenMenuCommand` already exist for native menus.
- **`AISettings.DeleteLegacyConversations`** stays: it only touches RhinoAI's old settings node.
- **Localisation IDs:** 390-409.

## Steps

1. **Working directory.** `AgentDispatch` launches the CLI in `%AppData%\Owly\cli`, creating it if missing, so `--resume` works from any document. Images and attachments keep their absolute paths. Check that nothing relied on the document folder.
2. **DTO and store.**
   - Restore `ConversationDto` and add `ProjectId` and `Title`: the first line of the first prompt, at most 80 characters.
   - `ConversationStore` is a new file over `OwlyStore`, with `projects/<projectId>/conversations/<sessionId>.json`.
   - It provides `List(projectId)`, `Load`, `Save` (after each turn, off the UI thread, under the store mutex), `Rename` and `Delete`.
   - Deleting a project in 025 now also deletes its folder.
3. **Resume.**
   - Restore `Conversation.Restore`, `AgentHost.TryResume`/`CreateResumed` and the resume constructor with `ResumePending`.
   - If the resume is rejected: start a fresh session and put the **capped saved transcript** (the last turns, at most about 4,000 characters, framed "Earlier in this conversation (for context only):") at the start of the **first user message over stdin**. Never put it on the command line, because of cmd.exe's 8,191-character limit (ADR-0009).
4. **Lifecycle:**
   - New conversation saves the current one and starts an empty one in the same project.
   - Closing a file saves the conversation.
   - Opening a file starts empty.
   - Opening an old conversation from the strip saves the current one, then resumes the chosen one.
   - Plan 027 adds "conversation ended" hooks here.
5. **Protocol:**
   - `conversations {projectId, items:[{sessionId, title, startedAt, current}]}` (host → panel)
   - `conversation.open {sessionId}`, `conversation.rename {sessionId, title}`, `conversation.delete {sessionId}` (panel → host)

   Validate the ids against the store.
6. **Panel strip** (`ui/conversationStrip.ts`):
   - a vertical list of owl `<button>`s with `title` tooltips ("{title} · {date}")
   - `aria-current` on the open one, and an outline on the button
   - right-click (`contextmenu`) sends `OpenMenuCommand` for Rename and Delete, or an in-panel menu if that is simpler
   - Rename uses a small inline prompt. Delete asks for confirmation.
7. **Docs.**
   - Rewrite the CLAUDE.md conversations rule to ADR-0009.
   - Update the welcome privacy line about Claude Code's own copy: it now sits under `%AppData%\Owly\cli`'s project folder in `~/.claude/projects`.
8. **Tests:**
   - DTO round-trip; list ordering; the rename and delete store tests
   - a Rhino-free test of the transcript-context builder (cap, framing)
   - verify:
     - the strip lists the mock's conversations newest first
     - the current one is outlined
     - the tooltip text
     - a click sends `conversation.open`
     - the owl checks still pass

   Update `mockHost.ts` and `host-events.json`. Rebuild the panel.

## Done criteria

- Shared gates pass.
- `git grep -n "Conversations\"" rhino/plugin` shows only `DeleteLegacyConversations`, and no PersistentSettings conversation storage exists.

## Manual Rhino check (Rhino 9)

- Ask two questions, press +, and ask another. The strip shows two owls, and hovering shows titles and dates.
- Click the older owl and ask a follow-up. Owly remembers the earlier answer.
- Quit Rhino, reopen the file and click an old owl. It continues.
- Rename a stored session's `.jsonl` in `~/.claude/projects` (simulating expiry), then reopen the conversation. Owly continues with the saved transcript as context and mentions nothing odd.
- Delete a conversation: it disappears from the strip and from disk.

---

## Advisor addendum (re-anchored at `69abd49`, 2026-11-25)

The plan was written at `40f2c38`. Plans 022-025 have landed since. Every fact above was re-checked at `69abd49`: the line numbers moved a little, but the code is as described. **Where this addendum and the steps above disagree, the addendum wins.** The product owner settled the two behaviour questions below on 2026-11-25.

### Product-owner decisions

- **D1. A conversation without a project is filed under Unsorted.** The project card is unanswered, or the project is unknown. While `Conversation.ProjectId` is null, every save writes to `projects/unsorted/conversations/`. Once a project is chosen, the file moves there. Nothing is ever lost because the card was ignored.
- **D2. The open conversation always belongs to the panel's current project.** Switch, Move and the project card's answer all move the open conversation, and its saved file, to the new project. It then shows, outlined, in that project's strip.

### Advisor decisions (fill the gaps; do not re-litigate)

1. **One working-directory helper.** There are two call sites, `AgentDispatch.TryDispatch` (`AgentDispatch.ai.cs:150-152`) and `AIPanelViewModel.Login` (`AIPanelViewModel.ai.cs:~287-289`). Both must use the same new helper: a static `OwlyStore.CliDirectory(string root)` plus a Rhino-side `OwlyMemory.CliDirectory` that creates `%AppData%\Owly\cli` and returns it. `StreamJsonAgent.Cwd` is fixed when the agent is built, so if Login ran first with the document folder, every later turn would use that folder.
2. **"Delete all Owly memory" keeps the `cli` folder.** A running CLI holds `cli` as its working directory, so Windows refuses to delete it, and `DeleteAll` would report failure. `OwlyStore.DeleteAll` skips a top-level directory named `cli` (const `OwlyStore.CliFolder = "cli"`). Add a test.
3. **`OwlyStore` gets the file operations the conversation store needs.** Each runs under the existing mutex and never throws (log, then return false or empty), matching the existing methods:
   - `IReadOnlyList<string> ListJson(string folder)`: the relative names of the `*.json` files in that folder, with the `.tmp` leftovers excluded.
   - `bool Delete(string name)`
   - `bool DeleteFolder(string folder)`

   Add `JsonStringEnumConverter` to `OwlyStore.Options`, so `TurnEventKind` is stored by name and reordering the enum can't corrupt saved files. `projects.json` has no enums, so it is unaffected.
4. **Ids are validated before they become paths.** No path traversal.
   - A session id must pass `Guid.TryParse` and is always written in `"D"` format.
   - A project id must be `ProjectRegistry.UnsortedId` or 32 lowercase hex characters (the `"N"` format that `ProjectRegistry.Create` mints).
   - Anything else: the store method returns false or null, and the host ignores the panel command.
5. **New Rhino-free files** (each linked in `tests/Server.Tests/Server.Tests.csproj`):
   - `Agents/Memory/ConversationDto.ai.cs`: the DTOs from `git show a2b0ef7^:rhino/plugin/Agents/ConversationDto.ai.cs`, with `ProjectId` and `Title` added to `ConversationDto`.
   - `Agents/Memory/ConversationStore.ai.cs` (over `OwlyStore`).
   - `Agents/Memory/ResumeContext.ai.cs`: the transcript-context builder.

   Do **not** restore the old PersistentSettings `ConversationStore`. Write the new one from scratch.
6. **`Conversation` gains, all Rhino-free:**
   - `ProjectId` (string?, mutable)
   - `Title` (string, mutable). Set on the first `BeginTurn` when it is empty: the first non-blank line of the prompt, trimmed, cut to 80 characters. A prompt with only attachments gets the localisable fallback, which the host passes in.
   - `Discarded` (bool). Once set, every save is a no-op.
   - `SavedAs` (sessionId, projectId)?: where it was last written. `ConversationStore.Save(convo)` writes under the current `AgentSessionId`/`ProjectId ?? UnsortedId`. If `SavedAs` differs, it deletes the old file. Then it updates `SavedAs`. This one rule covers project moves (D2), `AdoptMintedSessionId`, and the rotation after a rejected resume, so `StreamJsonAgent` never references the store.
   - The restored `Conversation.Restore(dto)`, which also restores `ProjectId`, `Title` and `SavedAs`.
7. **What gets saved, and when.**
   - A conversation with zero turns is never saved or listed.
   - Save after every turn: the restored `AgentRunner.PersistTurn`, which runs on the turn's background thread, catches everything and must never fault the turn.
   - Save again when a conversation ends. Put that in `AgentHost.SafeDispose`, before `Dispose`, so +, opening another conversation, closing the file, `Drop` and `Shutdown` are all covered.
   - After every successful save, rename, delete or move, raise a new `OwlyMemory.ConversationsChanged` event **on the UI thread** (`RhinoApp.InvokeOnUiThread`). Every open panel then re-sends its `conversations` event.
8. **Keeping the project in sync (D1/D2).**
   - `AIPanelViewModel` keeps `ActiveConversation.ProjectId == ProjectId`: at the end of `SendProject`, and after `Resubscribe` picks up a new conversation.
   - If the value changed and the conversation has turns, save it (that moves the file).
   - `SendProject` then also sends `conversations`.
9. **Deleting discards; it never resurrects.** In each case below, set `Discarded = true` on the open conversation and start a new, empty one (the `NewConversation` path):
   - **Delete conversation** on the open one.
   - **Delete project** on the open conversation's project. That covers this panel (`DeleteProject`) and any other panel in this Rhino, which notices in `SendProject`'s "deleted since" branch: the previous `ProjectId` was non-null and no longer exists.
   - **Delete all Owly memory.** Add `OwlyMemory.Cleared`, raised by `OwlyMemory.DeleteAll`, which every view model handles.

   Without this, the in-flight `PersistTurn`, or the end-save in `SafeDispose`, would write the file back.
   - `ProjectRegistry.Delete` succeeding also calls `ConversationStore.DeleteProject(id)`. Do that in `AIPanelViewModel.DeleteProject`, not inside `ProjectRegistry`, so `ProjectRegistryTests` stay as they are.
   - Update the project delete confirmation text so it says the project's conversations are deleted too. Use a **new** localisation id from 390-409, not 37x.
10. **Opening a saved conversation** (`conversation.open {sessionId}`):
    - **The id must be in this project's store listing.**
    - **It must not be live in another document of this Rhino.** Add `AgentHost.IsLive(Guid, uint exceptDoc)`. If it is live, post the notice "This conversation is open in another file." with a new id.
    - **The open conversation itself:** a no-op.
    - **Otherwise:** `ConversationStore.Load` → `AgentHost.TryResume(doc, dto)`. `SafeDispose` end-saves the old one and replaces the pooled runner. Then `ShowLive()`, which replays the restored turns as on a panel reload. This is allowed mid-turn, like + today.
    - Set the restored conversation's `ProjectId` to the panel's current project (D2).
    - A failed load posts "Owly couldn't open that conversation." with a new id.
11. **Resume, then fallback without losing the question.** The old code (`git show a2b0ef7 -- rhino/plugin/Agents/StreamJsonAgent.ai.cs`) noted "started fresh" and dropped the student's question. That is not acceptable. Restore it with these changes:
    - The resume constructor takes `Guid resumeSessionId` and `string resumeContext`. `AgentHost.CreateResumed` builds the context with `ResumeContext.Build(dto.Turns)`.
    - Restore `ResumePending` exactly as it was: it is cleared by the first completed turn, by a clean cancel and by `AdoptMintedSessionId`. Keep the old rejection detection in the read-loop `finally`: `ResumePending && signedOut.Length == 0`, plus not `Lifetime.IsCancellationRequested`. Instead of the old `ConversationStore.Delete(stale)`:
      - rotate the id
      - set `HasEverStarted = false`
      - `Conversation.AdoptSessionId(new)`
      - set a `ResumeRejected` flag

      Rule 6's `SavedAs` cleans up the old file on the next save.
    - Store the read-loop task in a field when the process spawns. In `SessionPromptAsync`:
      - Note whether `ResumePending` was true before the start.
      - If that turn then throws (anything except `ObjectDisposedException`, and not while `Lifetime` is cancelled), await the read-loop task (at most 10 s).
      - If `ResumeRejected` is set, clear it and retry **once** inside the same `TurnGate`. Prepend `TextContentBlock(resumeContext)` to the original prompt blocks, then `EnsureStartedAsync` and `SendTurnAsync` with a fresh turn TCS.
      - Otherwise rethrow.

      The context goes **only** into that stdin message, never into `ConfigureArguments`.
    - **No `NoteSystem` line** for the fallback. Log it with `HostUtils.LogDebugEvent`. The manual check is "mentions nothing odd".
    - Claude Code's real behaviour for an unknown `--resume` id (exiting before or after reading stdin) could not be verified offline. The design above works either way. If the code shows it can't, **STOP and report**; don't improvise another detection.
12. **`ResumeContext.Build(IReadOnlyList<TurnDto> turns, int cap = 4000)`**: returns `""` for no turns. Otherwise:

    ```
    Earlier in this conversation (for context only):
    Student: <prompt>
    Owly: <the turn's AssistantText events joined>
    ...
    End of the earlier conversation. The student's new message follows.
    ```

    - Whole turns only, the **newest** turns that fit.
    - A single turn longer than the cap keeps its tail, cut on a character boundary.
    - The total, including the framing, is ≤ `cap`.
    - Tool calls and results are left out.
13. **Panel strip** (`src/ui/conversationStrip.ts`):
    - **Class name:** the root class is `.convo-strip`. **Not** `.strip` or `.status-strip`: `.status-strip` is the thinking strip, and verify already checks it.
    - **Placement:** the strip is the second child of `.stage`, with `grid-template-columns: 1fr auto`. It renders nothing (no column) while the project is pending or the list is empty.
    - **Each item** is a `<button class="convo-item">` around `owl(ctx, 28)`:
      - `title` = `"{title} · {date}"`, where the date is `Intl.DateTimeFormat(<panel language>, {dateStyle:'medium', timeStyle:'short'})` of `startedAt`.
      - `aria-label` = the title.
      - `aria-current="true"` and an outline on the **button** for the current one.
    - **Left-click** sends `conversation.open`. It doesn't send anything for the current conversation.
    - **Right-click** opens an **in-panel** menu, using the panel's existing overlay mechanism (`ui.openOverlay` / `closeOverlay` in `src/state/ui.ts`) with a new overlay value. It offers Rename and Delete:
      - Rename reuses `nameField` from `src/ui/projectMenu.ts` (export it if needed; don't copy it).
      - Delete copies the `project-confirm` markup pattern.
      - The `contextmenu` handler calls `preventDefault()` **and** `stopPropagation()`. Otherwise `src/ui/hostMenu.ts`'s window listener also opens the native menu.
    - **Don't use `OpenMenuCommand`/`PanelMenu`.**
14. **Protocol names**:
    - **Host → panel:** a new event `conversations` (plural). The existing `conversation` event is the transcript snapshot and must not change.
    - **Panel → host:** `conversation.open`, `conversation.rename`, `conversation.delete`. The C# records are `ConversationsEvent`, `PanelConversationItem`, `OpenConversationCommand`, `RenameConversationCommand` and `DeleteConversationCommand`, registered with `JsonDerivedType` beside the plan 025 project records.
    - **Rename title:** 1-80 characters, trimmed, no control characters. The host checks this again.
    - **Keep these in sync:** `events.ts`, `mockHost.ts` (three conversations for `p-1`, newest first, one current) and `tests/host-events.json`.
15. **The welcome privacy line (id 340) stays as it is.** It already says conversations are saved and that Claude Code keeps its own copy. A folder path is noise for students. Put the `%AppData%\Owly\cli` / `~/.claude/projects` detail in `docs/owly/phase1-investigation.md`'s privacy section instead. This replaces step 7's second bullet.
16. **The CLAUDE.md rule.** Replace the whole "Projects exist, but conversations are not saved yet" bullet with one that describes ADR-0009 as built:
    - projects and conversations in `%AppData%\Owly`
    - the strip
    - resume, then the transcript fallback over stdin
    - the CLI working directory `%AppData%\Owly\cli`
    - Stop still relies on `--resume` and `HasEverStarted`
    - Plans 027-028 add notes and main memory

    Also mark ADR-0006 as superseded if its front matter doesn't already say so. Update the PRD §3.4 note only if it says conversations aren't saved.
17. **The option probe on replay.** Reopening a conversation re-renders its command cards, and the panel sends `command.options`, exactly as a panel reload does today. No change is needed, but add it to the manual check: "reopening a conversation with several command cards doesn't flash Rhino's command line more than a panel reload does."

### Extra tests (in addition to step 8)

`ConversationStoreTests.ai.cs`, a temp root, following `ProjectRegistryTests.ai.cs`:
- Save, Load round-trip, including enum by name and attachments.
- `List` newest first, skipping corrupt files and `.tmp` files.
- A zero-turn conversation isn't saved.
- A move between projects leaves one file.
- An id change leaves one file.
- `Discarded` blocks saves.
- Rename, and the title rules.
- Delete.
- `DeleteProject`.
- Invalid ids are refused (`..\x`, a non-GUID, an uppercase or short project id).
- `DeleteAll` keeps `cli`.

`ResumeContextTests.ai.cs`: empty; framing; the cap holds; newest turns kept; a single oversize turn; tool events left out.

### Done criteria (replace the plan's list)

From the repo root, in the worktree:
- `dotnet build rhino/plugin/RhinoAI.csproj -p:RhinoTarget=R9 -clp:NoSummary` and the same with `R8`: `Build succeeded`, 0 errors, no new warnings.
- `dotnet test tests/Server.Tests/Server.Tests.csproj`: all pass, with more than 342 tests.
- `cd rhino/panel && npm run typecheck && npm test && npm run build && npm run verify`: all pass. `git status` shows `dist/panel.host.html` changed and committed.
- `git grep -n "Conversations\"" rhino/plugin` shows only the `DeleteLegacyConversations` lines.
- `git grep -n "PersistentSettings" rhino/plugin/Agents` returns nothing.
- `git grep -n "GetTempPath" rhino/plugin/Agents/AgentDispatch.ai.cs rhino/plugin/UI/AIPanelViewModel.ai.cs` returns nothing.
- `git grep -n "_Cancel" rhino/plugin` still shows only `CommandOptionProbe.ai.cs`.
- No new agent tool: `git grep -n "McpServerTool\]" rhino/plugin` gives the same count as at `69abd49`.
