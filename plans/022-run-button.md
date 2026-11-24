# 022: Run button: start a command on the student's click

- **Written against:** `40f2c38` on `rhino-9.x`.
- **Status:** TODO
- **Effort:** M
- **Risk:** Medium. This is the first panel action that starts a Rhino command. Safety comes from host-side validation, the blocklist and the busy check.
- **Phase:** Phase 5 rework, commands & tools. Decision record: [ADR-0008](../docs/adr/0008-run-on-student-click-and-option-probe.md), which supersedes ADR-0007. Glossary: **Run**, **Options card**, **Blocked command**.

## Why

Mock-up 1 shows a green **"Run _Pyramid"** button under the explanation. The product owner decided:

1. Clicking Run **starts the command in Rhino**. Rhino then waits for the student's own picks. Owly never supplies picks and never finishes a command.
2. **The chip in the text keeps copying** (plan 014). Run is the only thing that sends a command to Rhino.
3. **Card placement.** Every command gets an options card at its **first mention**, directly under the block (paragraph or list item) that contains it. Multi-step answers therefore have one card per step. In this plan the card holds only the Run button; plan 023 adds the options.
4. **Busy Rhino.** If Rhino is already running a command, Run does nothing in Rhino. The panel shows "Finish or cancel the current command first (Esc), then click Run again."
5. **Blocklist, enforced by the host.** These commands never get Run:
   - script and plug-in runners
   - Grasshopper
   - Owly's own commands (`Owly`, `OwlyWelcome`, `OwlyPanel`)
   - **file and app** commands: Save, SaveAs, SaveSmall, Open, New, Close, Exit, Import, Export, Insert, Print and similar
   - **Delete and Purge**
   - **Undo and Redo**: Undo, Redo, UndoMultiple, RedoMultiple, ClearUndo and similar
   - **settings and layout**: Options, DocumentProperties, toolbar and workspace commands, PlugInManager and similar

   A blocked command still gets its card, without Run, with the text "Owly can't start this command. Copy it and run it yourself."
6. **The AI can never trigger Run.**

## Facts you need

- **Panel command pattern** (ADR-0007 consequences still apply):
  - a record in `rhino/plugin/UI/PanelCommands.ai.cs` with a `[JsonDerivedType(..., "name")]`
  - a case in `AIPanelViewModel.IncomingCommand` (`UI/AIPanelViewModel.ai.cs:~186`)
  - a validating handler with try/catch
- `CopyCommand` (`AIPanelViewModel.ai.cs:~423-442`) is the template, including the focus call.
- **Validation helpers:** `UI/CommandName.ai.cs` (`^_-?[A-Za-z][A-Za-z0-9_]*\z`) and `Rhino.Commands.Command.IsCommand(name)`, which `src/Resources/CommandHelpResource.cs` already uses.
- **Notices:** `NoticeEvent(Level, Text)` (`UI/PanelEvents.ai.cs:50`). Strings go in `UI/PanelStrings.ai.cs` (next free id: check the file; plans 014-015 used 277-289), with English fallbacks in `panel/src/i18n/strings.ts`.
- **`RhinoApp.RunScript(uint docSerial, string script, bool echo)`** blocks until the script finishes, and an interactive command keeps the script "running" until the student finishes or cancels. If it runs inside the WebView2 message handler, Rhino's command loop nests and WebView2 events stay blocked until the command ends. So **post it** with `Eto.Forms.Application.Instance.AsyncInvoke(...)`; the precedent is `AIPanelViewModel.ai.cs:~325`. `Command.InCommand()` tells you whether Rhino is busy. Check it both when the click arrives and again in the Idle handler.
- **The macro in this plan** is just `_Name`, the validated name with its leading underscore. Plan 023 adds options through a builder (`RunMacro`).
- **Preselection:** a non-blocked command that acts at once on selected objects (e.g. `_Explode`) runs when the student clicks Run. ADR-0008 records this as the student's choice.
- **The markdown chip** is created inside `renderMarkdown` (`core/markdown.ts:327-356`) and rebuilt each frame while streaming. After plan 019 the "first mention" set exists, so the card must also be created inside the render, appended after the block that held the first mention. The panel needs to know which commands are blocked. It shouldn't duplicate the list: the host sends it once in a `commands.blocked {names}` event at panel start, and still re-checks on every Run.
- **The prompt** (`Agents/AgentPrompts.cs`): `OnlyHelpSteer` says "you never run Rhino commands", which stays true for the AI. `CommandSteer` (as edited by 019) gets one sentence: "Under each command the panel shows a Run button that the student can click to start it in Rhino; you never start it yourself, so never say that you ran it."

## Steps

1. **Blocklist.** `rhino/plugin/UI/CommandBlocklist.ai.cs` is Rhino-free so Server.Tests can link it. It holds a case-insensitive set of English names grouped by category, with a comment per group, plus substring rules: `Script`, `Python`, `Grasshopper`, `Owly`, `Save`, `Export`, `Import`, `Undo`, `Redo`, `Purge`, `PlugIn`, `Toolbar`, `Workspace`. The rules cover variants such as `IncrementalSave`, `ReadCommandFile`, `-RunPythonScript` and plug-in script runners. Expose `IsBlocked(string name)` (strip `_` and `-`) and `Names`. Add `tests/Server.Tests/CommandBlocklistTests.ai.cs`: every category is represented, `_-RunPythonScript` and `_Delete` are blocked, `_Pyramid` and `_Loft` are allowed. Link the file in `Server.Tests.csproj`.
2. **Record and handler.**
   - `RunCommandCommand(string Name)` → `"command.run"`.
   - The handler runs these checks in order:
     1. `CommandName.IsValid`
     2. `Command.IsCommand(bare)`
     3. `!CommandAliasList.IsAlias(bare)`, because an alias can expand to anything, including a script
     4. not blocked
     5. not `Command.InCommand()`

     If any check fails, post a `NoticeEvent` and return.
   - Otherwise focus the main window and `AsyncInvoke(() => { re-check InCommand; RhinoApp.RunScript(doc.RuntimeSerialNumber, macro, true); })` inside try/catch.
   - Log through `HostUtils.LogDebugEvent` with the `[owly]` prefix.
3. **Blocked list to the panel.** A `CommandsBlockedEvent(IReadOnlyList<string> Names)`, posted when the panel is ready (next to the other start-up posts). Add the TS type and store field.
4. **Panel card.** `rhino/panel/src/ui/commandCard.ts` builds the card DOM, which `markdown.ts` calls through options (`core/` must not import `ui/`; pass a factory in, as for icons).
   - Allowed command: a green button "Run _Name" (`.cmd-run`) that sends `{type:'command.run', name}`.
   - Blocked command: the note text instead.
   - The card is appended once per command, after the `<p>`/`<li>` holding its first mention (plan 019's per-turn first-mention set). It is filled only once that block has stopped streaming.
   - Markdown is rebuilt every frame, so **card state lives in `store.ts`** (keyed by turn id and command). It must not live in the DOM.
   - Add CSS for `.cmd-card` and `.cmd-run` (green, as in the mock-up; accessible contrast in both themes).
5. **Strings:** `run.button` ("Run {0}"), `run.blocked`, `run.busy`, `run.unknown`. Use localisation IDs 310-329.
6. **Prompt** sentence (above), plus prompt-checks row 15: "explain the Pyramid command". The answer mentions Run, and never says it ran anything. Update `AgentPromptsTests`.
7. **verify:**
   - a `_Pyramid` answer renders one card with a Run button
   - the click sends `command.run`
   - a `_Delete` answer renders a card with no Run button and the blocked note
   - two mentions of `_Pyramid` give one card

   Update `mockHost.ts` (blocked list, `command.run` → a fake notice) and `tests/host-events.json`.
8. **Docs.** Rewrite the "Phase 4 actions" rule in `CLAUDE.md` to ADR-0008's wording. Mark ADR-0008's Run part as built in `plans/README.md`.

## Done criteria

- Shared gates pass, including new tests: blocklist, handler validation (a Rhino-free part if possible), and verify checks.
- `git grep -n "SendKeystrokes" rhino/plugin` finds nothing (Run uses `RunScript`).

## Manual Rhino check (Rhino 9)

- Ask "explain the pyramid command" and click **Run _Pyramid**. Rhino asks for the base center, and Owly does nothing more. Press Esc: the command cancels normally.
- Start `_Line` yourself, then click Run on the card. The busy notice appears and the line command is untouched.
- Ask how to delete an object. The `_Delete` card has no Run button and shows the blocked note.
- Click the chip in the text: it still copies, as before.
