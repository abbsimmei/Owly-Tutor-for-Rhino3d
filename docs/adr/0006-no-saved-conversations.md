---
status: superseded by ADR-0009
---

# Owly keeps no saved conversations, only the live transcript

> Superseded by [ADR-0009](0009-saved-conversations-per-project.md). Kept for history.

RhinoAI saved every conversation to Rhino's settings (`ConversationStore`, the newest 50, no expiry) and offered a searchable history drawer, a read-only review and Resume. For the Owly prototype we delete all of that, on both the C# and panel sides. A conversation lives only in memory. It ends when the student presses New conversation, closes the file (on Windows this includes File › Open and File › New) or quits Rhino. Closing or hiding the panel does not end it. When Owly loads, it deletes any `Conversations` settings node left by earlier builds. We chose this because PRD §7 asks for minimal data retention, and a tutor prototype doesn't need students to look up old chats. PRD §3.3's "displays conversation history" is met by the transcript of the current conversation.

## Considered options

- **Keep the saved history and add delete/expiry controls.** It's more useful to a student going back to an old explanation, but it means more data kept and more UI. The phase 1 investigation suggested reusing it; the product owner chose not to.
- **Keep history only while Rhino is open.** This keeps the drawer and review code for little benefit, because a conversation already lasts for the whole session of its file.
- **One conversation across all open files.** This would mean redesigning the per-document agent and MCP listener, an architecture change we don't need.

## Consequences

- **Claude Code still writes its own copy.** The CLI stores each session under `~/.claude/projects/<folder>/<id>.jsonl` and deletes it itself after about 30 days by default. Owly needs that file: Stop kills the CLI, and the next prompt restarts it with `--resume` so the conversation continues. This is disclosed in the welcome popup. Turning the file off with `--no-session-persistence` would make Owly forget the conversation after every Stop. Having Owly delete the file would mean guessing Claude Code's folder layout inside the student's profile.
- **Possible Phase 5 privacy work:** run with `--no-session-persistence`, and after a Stop feed the visible transcript back to the new CLI process as context. Write an ADR that amends this one if you do.
- Opening another file on Windows discards the current conversation without warning. The New conversation tooltip says that it clears the current one.
- Bringing history back means rebuilding the store, the protocol records and the drawer from scratch. Git history has the RhinoAI version (before plan 009).
