# 025: Projects

- **Written against:** `40f2c38` on `rhino-9.x`.
- **Status:** TODO
- **Effort:** L
- **Risk:** Medium. This adds a new on-disk store that Rhino 8 and 9 can write at the same time, and a new piece of panel UI.
- **Phase:** Phase 5 rework, memory. Decision record: [ADR-0009](../docs/adr/0009-saved-conversations-per-project.md), which supersedes ADR-0006. Glossary: **Project**, **Unsorted**.

## Why

"For each project it has its own memory. Each project can have multiple chats." This plan creates **projects**. Plans 026-028 build saved conversations, notes and main memory on top. The product owner decided:

- **Projects are named by the student.** Owly remembers which .3dm file paths belong to which project. Nothing is written into the .3dm. Versions of a design (`pavilion_v01`, `v02`) can sit in one project.
- **Storage:** plain files under `%AppData%\Owly`, shared by Rhino 8 and 9. Data is kept until the student deletes it, and settings get **"Delete all Owly memory"**.
- **The header shows the project name.** Clicking it opens a menu with Switch project, New project, Rename, Delete, and Move this file to another project. "View notes" comes in plan 027.
- **Unknown file.** On the first question from a file Owly doesn't know, a small card asks which project the file belongs to:
  - it suggests the project of other files in the same folder
  - it lists the existing projects
  - it offers **New project** and **Unsorted**

  The question is still sent; the card doesn't block it.
- **Untitled file.** The card's choice is remembered and applied when the file is first saved.

## Facts you need

- **Documents are keyed by `doc.RuntimeSerialNumber`**, which only lasts for the session: `Agents/AgentHost.ai.cs:10,62-73`. `doc.Path` is available, and is empty when Untitled.
- **Document events:**
  - `AgentHost` subscribes to `RhinoDoc.CloseDocument` (`AgentHost.ai.cs:17-23`).
  - Use `RhinoDoc.EndSaveDocument` for the Untitled case.
- **The old store** is a reference for its JSON shape and lock pattern: `git show a2b0ef7^:rhino/plugin/Agents/ConversationStore.ai.cs`.
- **JSON:**
  - `System.Text.Json` reaches net48 through `rhino/acp/Acp.csproj`, and the plugin already uses it (`PanelCommands.ai.cs`).
  - **`File.Move(src, dst, overwrite)` doesn't exist on net48.** Write to a temp file and use `File.Replace` (or `File.Move` when the target is new).
- **Two Rhinos can run at once.** Hold a named mutex (`Local\OwlyStore`) across read-modify-write, and re-read before writing.
- **Panel:**
  - header: `rhino/panel/src/ui/header.ts` (owl, agent chip, `+`, settings)
  - popover pattern: `ui/agentMenu.ts` and `state/ui.ts` `overlay`
  - the question still goes through `prompt` as today
- **Settings:** `UI/AISettingsPanel.ai.cs` (model dropdown), also hosted in `UI/AIOptionsPage.cs`. The welcome privacy line is at `UI/WelcomeDialog.ai.cs:~28`.
- **Localisation IDs:** 360-389.

## Steps

1. **Store** (Rhino-free; link it in Server.Tests). Add `rhino/plugin/Agents/Memory/OwlyStore.ai.cs`, with the root path injected (tests use a temp folder). It provides `ReadJson<T>` and `WriteJson<T>` (atomic, under the mutex), `DeleteAll()`, and a corrupt file read as empty, with a logged error.
2. **Registry.** Add `Agents/Memory/ProjectRegistry.ai.cs` over `projects.json`:

   ```json
   { "version": 1,
     "projects": [{ "id", "name", "created" }],
     "files": { "<full path, case-insensitive>": "<projectId>" } }
   ```

   It provides:
   - `Resolve(path) → projectId?`
   - `SuggestFor(path)`: the project of other mapped files in the same folder
   - `Create`, `Rename`, `Delete`, `Assign(path, id)`
   - a built-in **Unsorted** project with a fixed id, which can't be deleted or renamed
3. **Host wiring.**
   - Hold a per-document "current project": resolved when the panel attaches to a document, or **pending** if the file is unknown.
   - Untitled: remember the choice per runtime serial number, and `Assign` on `EndSaveDocument` once the path exists.
   - Protocol:
     - `project {id?, name?, pending, suggestion?, projects[]}` (host → panel)
     - `project.choose {id}`, `project.create {name}`, `project.rename {id, name}`, `project.delete {id}`, `project.move {id}` (panel → host)
   - Validate names (trimmed, 1-60 characters, no control characters).
4. **Panel.**
   - The header gets a project button showing the name (or "No project yet"), which opens the project menu (`ui/projectMenu.ts`). Delete asks for confirmation inline, because it deletes the project's conversations and notes once 026 and 027 exist.
   - `ui/projectCard.ts` is shown above the composer after the first question when `pending`: the suggestion first, then the projects, New project (inline name field) and Unsorted.
5. **Settings.** Add a "Delete all Owly memory" button with a confirmation, calling `OwlyStore.DeleteAll()`.
6. **Privacy text** (welcome popup): "Owly saves your conversations and short notes about your projects on this computer until you delete them. When used, they are sent to Anthropic through Claude Code." Plan 029 re-shows the popup.
7. **Docs:**
   - `CLAUDE.md`: the settings sentence (Delete all) and the start of the conversations rule (projects exist).
   - `docs/owly/phase1-investigation.md` privacy section: one line.
8. **Tests:**
   - `OwlyStoreTests`: round-trip, atomic write leaves no temp files, a corrupt file reads as empty, Delete all
   - `ProjectRegistryTests`: resolve, suggestion by folder, rename, delete re-maps files to nothing, Unsorted is protected, case-insensitive paths
   - verify: the header name, the menu opens, the card appears for a pending project and choosing hides it

   Update `mockHost.ts` and `host-events.json`. Rebuild the panel.

## Done criteria

- Shared gates pass.
- Nothing writes to `RhinoDoc.Strings` or to user data: `git grep -n "SetUserString\|UserData" rhino/plugin` finds nothing new.

## Manual Rhino check (Rhino 9)

- Open an unsaved file and ask something. The card appears. Choose New project "Pavilion", then save the file as `pavilion_v01.3dm`. Close it and reopen it: the header shows Pavilion.
- Save As `pavilion_v02.3dm` in the same folder and ask something. The card suggests Pavilion.
- Rename and delete a project. Use Delete all Owly memory: `%AppData%\Owly` is empty.
- If both Rhino 8 and 9 are installed, run both at once, create a project in each, and check that both are saved.
