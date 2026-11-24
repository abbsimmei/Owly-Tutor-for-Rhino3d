# 022: Run button: start a command on the student's click

- **Written against:** `40f2c38` on `rhino-9.x`. Re-anchored at `fb72651` by the advisor addendum below Steps.
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

## Advisor addendum at `fb72651` (read before starting; it overrides Steps 1-8 where they differ)

Plans 018-021 and 025 are merged since this plan was written. What changed for you:
- Chips are built in `inline()` in `rhino/panel/src/core/markdown.ts` (the `if (code !== undefined)` branch, ~line 101). `renderMarkdown()` (~line 336) seeds a per-render `seen` set from `command.seenBefore()`, so **only the first mention of a command in a turn becomes a chip**. Cards hang off chips, so one card per command per turn comes for free.
- `ui/message.ts` `agentMessage(ctx, turnId, text, streaming, seenBefore)` builds the `CommandChipOptions` and re-renders on `text()`, `ctx.store.icons()` and `ctx.store.scheme()`.
- Plan 021's batched request (`ctx.requestToolbarIcon` in `main.ts`, `store.markIconAsked`, `toolbar.icon` → `toolbar.icon.found`) is the pattern to copy for anything the panel asks per command name.
- Host notices are posted inline as `Bridge.Post(new NoticeEvent("info", Localization.LocalizeString("…", id)))`, for example `AIPanelViewModel.ai.cs:225`. `PanelStrings.Localized()` is only for strings the **panel** renders.
- IDs 310-329 are free (checked with `git grep`).

**Design change: the host answers per command, it does not send a list.** The blocklist has substring rules (`Save` blocks `IncrementalSave`), and Run must also refuse aliases and names Rhino doesn't know. A plain `commands.blocked {names}` list can't express that without copying the rules into TypeScript. So drop Step 3 and do this instead:
- Panel → host: `command.check {names: string[]}`. Record `CheckCommandsCommand(IReadOnlyList<string> Names)` → `"command.check"`.
- Host → panel: `command.checked {name, run}`, where `run` is `"ok"`, `"blocked"` or `"unknown"`. Record `CommandCheckedEvent(string Name, string Run)` → `"command.checked"`.
- The handler `CheckCommands(names)` mirrors `FindToolbarIcons`: at most 20 distinct names; skip any that fail `CommandName.IsValid`; post one event per valid name. It **does not** check `InCommand`. Busy is a moment, not a property of the command.

**C# side**
1. **`UI/CommandBlocklist.ai.cs`** (Rhino-free; link it in `tests/Server.Tests/Server.Tests.csproj` next to `CommandName.ai.cs`). This is Step 1, plus the following:
   - `public static string Bare(string name)` strips one leading `_`, then one leading `-`.
   - `IsBlocked(name)` works on `Bare(name)`, case-insensitively. It is true if the bare name is in `Names` or contains any substring rule.
   - `Names` must also hold `ReadCommandFile` and `MacroEditor`, which no substring catches. It also needs these:
     - file and app: `Open`, `New`, `Close`, `Exit`, `Insert`, `Print`, `Revert`, `Worksession`, `Recover`
     - Delete and Purge: `Delete`
     - settings and layout: `Options`, `DocumentProperties`, `Units`, `WindowLayout`, `PackageManager`, `Alias`, `CommandAlias`

     Group them with one comment per category, as Step 1 says.
   - Add a pure classifier both handlers share, so the order of checks is unit-tested without Rhino:
     ```csharp
     internal enum RunVerdict { Invalid, Unknown, Blocked, Ok }
     public static RunVerdict Classify(string? name, Func<string, bool> isCommand, Func<string, bool> isAlias)
     ```
     The order is: `CommandName.IsValid`, else `Invalid`. Then `!isCommand(bare)`, else `Unknown`. Then `isAlias(bare) || IsBlocked(name)`, else `Blocked`. Otherwise `Ok`.
   - Tests in `tests/Server.Tests/CommandBlocklistTests.ai.cs` (NUnit `[TestFixture]`/`[TestCase]`, like `AgentPromptsTests.ai.cs`):
     - one blocked name per category
     - `_-RunPythonScript`, `_Delete`, `_IncrementalSave`, `_ReadCommandFile` and `_OwlyWelcome` are blocked
     - `_Pyramid`, `_Loft` and `_-Loft` are allowed
     - `Classify`: an invalid shape (`"Loft"`, `"_Loft x"`) gives `Invalid` before `isCommand` is called (use a delegate that throws); `Unknown`; an alias gives `Blocked`; `Ok`
2. **Records.**
   - `RunCommandCommand(string Name)` → `"command.run"` and `CheckCommandsCommand` go in `PanelCommands.ai.cs` with plan comments (`// Plan 022 / ADR-0008: …`).
   - `CommandCheckedEvent` goes in `PanelEvents.ai.cs`.
   - Add a pin test like `The_toolbar_found_event_has_the_json_the_panel_fixture_pins` in `ToolbarIndexTests.ai.cs` (put the new one in `CommandBlocklistTests.ai.cs`). It pins `{"type":"command.checked","name":"_Pyramid","run":"ok"}`.
   - Add the same line to `rhino/panel/tests/host-events.json`, after the `toolbar.icon.found` line.
   - Add deserialisation tests for `command.run` and `command.check`, like the project-command tests in `ProjectRegistryTests.ai.cs`.
3. **Handlers in `AIPanelViewModel.ai.cs`.** Put `CheckCommands` and `RunCommand` next to `CopyCommand`, with a case each in `IncomingCommand`. `Classify` is called with `Rhino.Commands.Command.IsCommand` and `Rhino.ApplicationSettings.CommandAliasList.IsAlias`.
   - `RunCommand(name)`:
     - `Invalid`: log only (as `CopyCommand` does) and return false.
     - `Unknown`: notice `run.unknown`.
     - `Blocked`: notice `run.blocked`.
     - `Command.InCommand()`: notice `run.busy`.
     - Otherwise: focus the main window exactly as `CopyCommand` does, then `Application.Instance.AsyncInvoke(() => { … })`. Inside it:
       - re-check `Command.InCommand()`; if busy, post the busy notice and return
       - otherwise `RhinoApp.RunScript(doc.RuntimeSerialNumber, name, true)`, where `name` is the validated `_Name` and nothing else
       - wrap the body in try/catch with `HostUtils.LogDebugEvent("[owly] Run failed: …")`
     - Log every Run and every refusal with the `[owly]` prefix.
     - **If** the `(uint, string, bool)` overload doesn't compile for R8/net48, use `RhinoApp.RunScript(name, true)` when `Document` is null or the overload is missing, and say so in your report. Don't use reflection.
   - All notices are `"info"` level, with the text inline through `Localization.LocalizeString(…, 310-329)`:
     - `run.unknown`: "Rhino doesn't know {0} here, so Owly can't start it."
     - `run.blocked`: "Owly can't start this command. Copy it and run it yourself."
     - `run.busy`: "Finish or cancel the current command first (Esc), then click Run again."
   - `run.button` ("Run {0}") and `run.blocked` are **panel** strings: add them to `PanelStrings.Localized()` (IDs 310-311) and to `rhino/panel/src/i18n/strings.ts`. The host notices use their own IDs (312-314), even where the text repeats, because a localisation ID is one call site.

**Panel side**
4. **Protocol mirrors.** Add both commands and the event to `src/protocol/events.ts`. Then update:
   - `mockHost.ts`: `command.check` answers `blocked` for names that `/delete|save|script|python|grasshopper|owly|undo|redo|purge/i` matches, `unknown` for `_Nonsense`, and `ok` otherwise (a mock only; the comment says the host is authoritative). `command.run` emits the notice "The Rhino host would start {name} now."
   - `tests/host-events.json`, as in item 2.
5. **Store** (`src/state/store.ts`).
   - Add `runnable = signal<ReadonlyMap<string, 'ok' | 'blocked' | 'unknown'>>(new Map())`, keyed by the chip name as rendered.
   - Add a private asked set and `markCheckAsked(name)`, a copy of the `markIconAsked` pattern: false once the name is asked **or already in the map**.
   - `command.checked` always sets the entry, asked for or not, like `toolbar.icon.found`. verify relies on it.
   - The map survives a `conversation` snapshot, like `icons`.
   - This plan stores **no per-card state**. The options and their values are plan 023's, and 023 will key them by turn id + command as Step 4 says.
   - Add store unit tests next to the plan 021 icon tests in `store.test.ts`.
6. **Context.** Add `ctx.requestCommandCheck(name)`, a copy of `requestToolbarIcon` with its own queue, and `ctx.runCommand(name)`, which sends `{type:'command.run', name}`. Add both in `ui/context.ts` and `main.ts`.
7. **Markdown hook** (`core/markdown.ts`).
   - Add `card?: (name: string) => Node | null` to `CommandChipOptions`.
   - In the chip branch of `inline()`, after appending the chip, push `code` onto a per-render `options.cards: string[]`. `renderMarkdown` creates it next to `seen`.
   - In `blocks()`, drain that array into card nodes (`command.card(name)`, skipping null) at these points:
     - after a `<p>` or a heading: as siblings, right after it
     - inside a `<li>`: after its inline content and **before** a nested list (call the drain between the two `appendChild`s), so a step's card sits under the step text. Never put a `div` directly in a `ul`/`ol`.
     - after a table: after its `.md-table` wrapper
     - blockquotes recurse into `blocks()` and drain themselves
   - Add `MarkdownOptions.growing?: boolean`. When it is true, `renderMarkdown` takes the cards out of the **open block** before returning:
     - the open block is the last top-level element that isn't a `.cmd-card`; if it is a `ul`/`ol`, take its last `li` instead
     - remove every `.cmd-card` inside it, and every top-level `.cmd-card` after it

     The next render, after the block closes, draws them. This is how "filled only once that block has stopped streaming" is met.
   - Add `node --test` unit tests in `src/core/` only if the existing tests already render DOM. Otherwise cover it in verify (item 10).
8. **`ui/commandCard.ts`** exports `commandCard(ctx, name): Node`, built with `el()` from `core/dom.ts`.
   - It returns `div.cmd-card` and calls `ctx.requestCommandCheck(name)`.
   - Then it reads `ctx.store.runnable.peek().get(name)`:
     - `'ok'`: a `button.cmd-run` with the text `t('run.button', name)` and `type="button"`. A click calls `ctx.runCommand(name)`.
     - `'blocked'`: a `p.cmd-card-note` with `t('run.blocked')`.
     - `'unknown'`: the same note element with `t('run.blocked')`. The specific reason only comes as a notice if Run is clicked, and there is no Run here.
     - not known yet: an empty card (keeps layout stable; the answer fills it).
   - In `message.ts`, pass `card: (name) => commandCard(ctx, name)` and `growing: streaming()`. Also read `ctx.store.runnable()` and `streaming()` inside the `bind` next to `icons()`, so an answer and the end of streaming redraw.
9. **CSS** in `src/styles/panel.css`, next to the `.md-cmd` rules:
   - `.cmd-card`: a small top margin, no border.
   - `.cmd-run`: background `var(--ok)`, a white or theme-contrast label, a rounded button, and a `:focus-visible` ring like `.md-cmd-where`.
   - Check `--ok` in the dark block. If the dark `--ok` with white text is under 4.5:1, add a dark-theme `--ok-text` token rather than hard-coding a colour.
   - Don't touch `.owl` rules.
10. **verify.mjs**, in the host-page section after the plan 021 checks. Inject turn text with new block ids in `turn-0`, as the existing checks do:
    The fixture's `command.checked` line for `_Pyramid` is replayed in the first batch, before any `_Pyramid` text. The store keeps every `command.checked` it receives, asked for or not, like `toolbar.icon.found`. So `_Pyramid` is already known and is never asked. `turn-0` stays running until the settling batch (~line 857), and every check below runs before it.
    - **Growing.** Inject a block `turn-0-run` with the text "Make it with `_Pyramid`.". It is the open block, so the message has no `.cmd-card`. Then inject `\n\nNext.` into the same block. Now exactly one `.cmd-card` is the `<p>`'s next sibling, with a `.cmd-run` whose text is "Run _Pyramid".
    - Clicking that button sends `command.run` with `name: '_Pyramid'`.
    - Inject `` `_Pyramid` `` again in a later paragraph: still one `.cmd-card` naming it.
    - **Ask once.** Inject "Try `_Cone`.\n\nMore." (`_Cone` is unknown to the fixture). Then inject another delta. `window.__sent` holds exactly one `command.check` whose `names` includes `_Cone`.
    - **Blocked.** Inject "`_Delete` it.\n\nMore.", then receive `{type:'command.checked', name:'_Delete', run:'blocked'}`. The `_Delete` card has a `.cmd-card-note` and no `.cmd-run`.
    - **Lists.** Inject "1. Draw with `_Box`.\n2. Close with `_Cap`.\n\nDone.", then answer both `ok`. Each `li` holds exactly one `.cmd-card`, and no `.cmd-card` is a direct child of the `ol`.

    Leave the mock dev page (`npm run dev`) alone apart from the `mockHost.ts` cases in item 4.
11. **Prompt** (Step 6 stands). Append the sentence to `CommandSteer` as its own `+ " …"` line. Add the test assertion in `Compose_tells_model_about_command_buttons`. Add a guard test: `AgentPrompts.Compose("").Length` is under 7,000. The CLI is launched through `cmd.exe`, whose limit is 8,191, and the other arguments need room. If it's already over, STOP and report the length. Add row 15 to `docs/owly/prompt-checks.md` with the wording from Step 6.
12. **Docs** (Step 8 stands). Rewrite the CLAUDE.md "Phase 4 actions" bullet to describe what is now true:
    - A command chip copies the command and focuses Rhino.
    - The toolbar icon opens the "where to find" popover.
    - A student's click on **Run** in a command's card starts a non-blocked command (blocklist in `UI/CommandBlocklist.ai.cs`), refused while Rhino is in a command, and Rhino waits for the student's picks.
    - Owly never types into the command line, never supplies picks, and never shows, hides or changes a toolbar. The AI cannot trigger any of this (ADR-0008).
    - Keep the "Being replaced" note only for the option probe (plans 023-024).

    Don't edit `plans/README.md`; the advisor updates the status row.

**STOP and report instead of improvising if:**
- `Command.IsCommand` or `CommandAliasList.IsAlias` doesn't exist in RhinoCommon 8.
- The card hook in `blocks()` would need restructuring beyond adding drain calls.
- The verify fixture doesn't leave `turn-0` running, so the growing check can't be written as described.

**Out of scope:** options in the card (plan 023), the probe (plan 024), any change to the chip's copy behaviour or the location popover, and `rhino/sdk/`.

## Done criteria

- Shared gates pass, including new tests: blocklist, handler validation (a Rhino-free part if possible), and verify checks.
- `git grep -n "commands.blocked" -- rhino` finds nothing (the addendum replaced it with `command.check`).
- Panel: `npm run typecheck`, `npm test`, `npm run build` and `npm run verify` pass, and the rebuilt `dist/panel.host.html` is committed.
- `git grep -n "SendKeystrokes" rhino/plugin` finds nothing (Run uses `RunScript`).

## Manual Rhino check (Rhino 9)

- Ask "explain the pyramid command" and click **Run _Pyramid**. Rhino asks for the base center, and Owly does nothing more. Press Esc: the command cancels normally.
- Start `_Line` yourself, then click Run on the card. The busy notice appears and the line command is untouched.
- Ask how to delete an object. The `_Delete` card has no Run button and shows the blocked note.
- Click the chip in the text: it still copies, as before.
