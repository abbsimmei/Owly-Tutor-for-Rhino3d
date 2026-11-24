# 026: Saved conversations per project, with the owl strip

- **Written against:** `40f2c38` on `rhino-9.x`. **Requires plan 025 to be DONE.**
- **Status:** TODO
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
