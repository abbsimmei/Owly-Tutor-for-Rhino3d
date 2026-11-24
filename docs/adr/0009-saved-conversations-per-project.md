---
status: accepted (supersedes ADR-0006)
---

# Owly saves conversations per project in a local Owly folder

ADR-0006 kept only the live transcript. The product owner reversed this for the Phase 5 rework (plans 025-028). Students name **projects** in Owly, and Owly remembers which .3dm file paths belong to which project. Each project keeps its saved **conversations**, which the student reopens from a strip of owl buttons on the panel's right edge and can continue. Each project also keeps short **project notes**.

Everything is stored as plain files under `%AppData%\Owly`, shared by Rhino 8 and 9, and kept until the student deletes a conversation, a project or everything ("Delete all Owly memory" in settings). Nothing is written into the student's .3dm file.

Project notes are written by one separate Claude Code CLI request when a conversation ends. That request has no tools, no MCP server and no session file, and uses Haiku. The notes reach later conversations as a capped block, framed as data rather than instructions. The host puts this block at the start of the conversation's first user message, sent over stdin. It does not go in the system prompt: Owly launches Claude Code through `cmd.exe`, which caps the whole command line at 8,191 characters, and Owly's prompt already takes about 5.6k of them. So the AI keeps exactly five read-only agent tools (ADR-0002), Claude Code stays the only backend (ADR-0003), and Owly's system prompt stays fixed (ADR-0005).

The cross-project **main memory** (recurring difficulties) is opt-in and off by default.

## Considered options

- **Rhino's PersistentSettings, as RhinoAI did.** Kept per Rhino version, and it bloats Rhino's settings file.
- **A project ID stored inside the .3dm.** It would travel with the file, but Owly would then write into the student's file.
- **Folder or file path as the project.** No UI needed, but Desktop and Downloads would merge, and "Save As v02" would start an empty project.
- **Giving whole earlier transcripts as context.** Owly would know the most, but it gets slower and more expensive as a project grows.
- **Notes written by the AI during the conversation, or by the student.** Less reliable, or extra work for the student.

## Consequences

- Opening a saved conversation resumes Claude Code's own session (`--resume <id>`) while that file still exists (about 30 days). Otherwise a fresh session starts and is given the saved transcript (capped) in its first message.
- Claude Code finds a session only from the working directory it was started in. Today that directory follows the document (its folder, or Temp when Untitled), so the CLI's working directory becomes a fixed `%AppData%\Owly\cli` (plan 026).
- The welcome popup's privacy line changes: conversations and notes are saved on this computer until deleted, and are sent to Anthropic when used as context. Existing installs see the popup once more (plan 029).
- `AISettings.DeleteLegacyConversations` stays: it only removes RhinoAI's old settings node.
- Injected notes and main memory are capped (notes about 1,000 characters, main memory about 600) to keep cost and latency low.
- Rhino 8 and Rhino 9 can run at the same time, so writes to the store are atomic (temp file plus `File.Replace`) and go through a named mutex.
