# 024: Spike: read a command's options with a silent option probe

- **Written against:** `40f2c38` on `rhino-9.x`. **Requires plan 023 to be DONE.**
- **Status:** TODO
- **Effort:** M, time-boxed. Part A is at most one working session. If Part A says "not feasible", the plan ends there.
- **Risk:** Medium to high. It is the only action Owly takes without a click (ADR-0008). Its parsing of Rhino's text output is unofficial.
- **Phase:** Phase 5 rework, commands & tools. Decision record: [ADR-0008](../docs/adr/0008-run-on-student-click-and-option-probe.md). Glossary: **Option probe**.

## Why

The help pages miss options. The Pyramid page has no NumSides or Mode, which the student sees on Rhino's command line. The product owner asked: "can Owly run the command and do nothing with it, only to see the options? If that is possible do that, otherwise use the help pages." The decisions:

- **Probe:** start the command and cancel it at once, then read the first prompt's line, e.g. `Center of base ( NumSides=5 Mode=Inscribed Edge Star DirectionConstraint=Vertical Solid=Yes ):`. That line gives option names and **current values**.
- **Guards** (all must hold, otherwise use the help pages only):
  1. Rhino is not in a command (`Command.InCommand()` is false)
  2. nothing is selected
  3. the command is not blocked (plan 022)
  4. its help page lists command-line options, so it is known to wait at a prompt
- **Pass criteria**, measured in Part A. Every one must hold:
  - no change to the document (no add, delete, modify or attribute events)
  - no undo record added
  - no dialog appears
  - the selection is unchanged
  - Enter or right-click afterwards repeats **the student's own previous command**, not the probed one
  - a cancelled line in the command history **is acceptable**
- **Merging:** the probe gives names, kinds (`=number`, `=Yes/No`, `=Word`, or bare for an action) and current values. The help page gives choices and descriptions. An option whose choices are unknown is shown **read-only** ("Mode: Inscribed"). Its explain popover says "Click Mode in Rhino's command line to see the choices." Run never changes it. A probed option the page doesn't have has no explain text.
- **No deeper probing:** never click into an option's sub-prompt.

## Facts you need

- **Public RhinoCommon APIs** (8.19 and 9, in `RhinoCommon.xml`):
  - `RhinoApp.RunScript(uint, string, bool)`
  - `RhinoApp.CommandWindowCaptureEnabled` and `RhinoApp.CapturedCommandWindowStrings(bool clearBuffer)` (since 7.0; these capture `Write`/`WriteLine` output, which may or may not include prompts)
  - `RhinoApp.CommandHistoryWindowText`
  - `RhinoApp.CommandPrompt`
  - `Command.InCommand()`, `Command.BeginCommand` / `EndCommand`
  - `RhinoDoc` events, for checking that nothing changed
- **Candidate probes** (try both in Part A):
  - `RunScript(serial, "_Name _Cancel", echo)`
  - `RhinoApp.PostCancelEvent(serial)` (since 8.0, the only cancel that types nothing), then `RunScript(serial, "_Name", false)`

  Post the probe with `AsyncInvoke`, as in plan 022. Before the probe, snapshot:
  - the history length
  - `Command.GetMostRecentCommands()`
  - the undo serial
  - the document's modified flag

  Afterwards, diff the history and capture buffer and parse the last line matching `^(?<prompt>.+?) \( (?<opts>.*) \):`.
- **The history cursor** pattern exists already in `rhino/plugin/Agents/CommandInterceptor.ai.cs:15-25`.
- **Option names in the history are localised.** Keep only names that match the help page's English names, or that appear on an English Rhino. Drop the rest.
- **"Repeat last command"** may count a scripted command. This is the most likely failure. Test it explicitly: run `_Line` yourself, let the probe run, then press Enter.
- ADR-0008 allows the probe only under the guards above and only if this spike passes.

## Scope

**Part A (always):**
- A throwaway local command, **never committed**, that probes about 15 commands:
  - Pyramid, Box, Cylinder, Loft, ExtrudeCrv, Fillet, Offset, Polygon, Circle, Rectangle, Sweep2, Revolve, Pipe, Array, Text
  - include at least one that opens a dialog, and one that acts at once if guard 2 is broken
- For each, record:
  - the captured line
  - every pass criterion
  - the time taken
- Write the results in `docs/owly/option-probe-spike.md` with the verdict "feasible" or "not feasible".

**Part B (only if A says feasible):**
- Add `rhino/plugin/UI/CommandOptionProbe.ai.cs`. It enforces the four guards, runs the probe on Idle and parses the line with a Rhino-free `ProbeLineParser.ai.cs` (unit tests on synthetic lines). It caches names and kinds per session, but re-reads current values at most once per card.
- `CommandOptionsSource` merges probe and help-page data. Add a `current` field to each option, and a `readOnly` flag when the choices are unknown.
- The card shows current values as placeholders ("5"), shows read-only rows for unknown choices with the hint, and adds probed options to `RunMacro`'s allowed set (numbers and toggles only, never read-only rows).
- If **any** guard fails at probe time, skip silently and use the help-page data.

**If A says not feasible:** record it in the spike doc, add a one-line note to PRD Phase 5 and plan 023 ("options come from help pages only; probe not feasible, see docs/owly/option-probe-spike.md"), and set this plan to DONE (not feasible).

## Advisor addendum at `4ea69f1`: the Part A test command (read before building it; it overrides Part A's first bullet)

Plans 022 and 023 are merged. Run already posts `RhinoApp.RunScript` with `Application.Instance.AsyncInvoke` (`RunCommand` in `UI/AIPanelViewModel.ai.cs`). Option data from the help pages comes from `UI/CommandOptionsSource.ai.cs`. RhinoCommon 9.0.26300 has everything Part A needs; checked in `RhinoCommon.xml`:
- `RhinoApp.CommandPromptChanged` (an event) and `RhinoApp.CommandPrompt`
- `RhinoApp.PostCancelEvent(uint)`
- `RhinoApp.CommandHistoryWindowText`
- `RhinoApp.CommandWindowCaptureEnabled` and `RhinoApp.CapturedCommandWindowStrings(bool)`
- `RhinoDoc.NextUndoRecordSerialNumber` and `CurrentUndoRecordSerialNumber`
- `RhinoDoc.Modified`
- the `RhinoDoc` events `AddRhinoObject`, `DeleteRhinoObject`, `ReplaceRhinoObject`, `UndeleteRhinoObject`, `ModifyObjectAttributes`, `SelectObjects`, `DeselectObjects` and `DeselectAllObjects`
- `Command.GetMostRecentCommands()`, `Command.InCommand()` and `Command.UndoRedo`

**Two facts that shape the test:**
1. **The probe must not run inside a command.** If a test command ran the probe directly, the test command itself would become "the last command", which spoils the "Enter repeats the student's command" check. Running from Idle is also how the real probe would run. So the test command only **arms** the probe on a delay and returns. The student then runs a command of their own, and the probe fires from `RhinoApp.Idle` once the delay has passed and `Command.InCommand()` is false.
2. **`RunScript` doesn't return while the started command waits for input.** The cancel must therefore be queued **before** the command starts, or be sent **from inside** the `CommandPromptChanged` handler, which runs in the nested command loop.

**The throwaway command.**
- Build it on branch `owly/probe-spike` in its own worktree. That branch is **never merged** into `rhino-9.x`, and it is deleted once the results are recorded.
- Put it in `rhino/plugin/Commands/ProbeTestCommand.ai.cs`, inside `#if R9`, with `[CommandStyle(Style.Hidden)]` and `EnglishName => "OwlyProbeTest"`.
- Change nothing else in the repo.

`OwlyProbeTest` asks with `GetOption` (Enter accepts):
- `Method`: `PreCancel`, `PromptCancel` or `CancelToken`, default `PromptCancel`
- `Set`: `All` or `One`, default `All`. With `One`, it then asks for a command name as a string; the name must pass `CommandName.IsValid`.
- `Delay`: an integer number of seconds, default 8, from 0 to 60

It refuses, with a message, if anything is selected. It prints "Probe armed: runs in N s. Run a command of your own now (for example _Circle, then Esc), then wait." Then it subscribes to `RhinoApp.Idle` and returns `Success`.

The `All` list, in this order, is Pyramid, Box, Cylinder, Loft, ExtrudeCrv, Fillet, Offset, Polygon, Circle, Rectangle, Sweep2, Revolve, Pipe, Array, Text, Hatch and Explode. Hatch may open a dialog; Explode acts on a selection, and with nothing selected it should only prompt.

**On Idle, once the delay has passed:** one probe per Idle tick, and only while `!Command.InCommand()` and nothing is selected. If either fails, wait for the next tick. For each command `X`:
1. **Snapshot:**
   - `doc.NextUndoRecordSerialNumber` and `doc.CurrentUndoRecordSerialNumber`
   - `doc.Modified`
   - `Command.GetMostRecentCommands()`, as the first 3 entries' names, with a property name found by reflection over the returned type only if needed for printing. The plain `ToString()` is acceptable.
   - `RhinoApp.CommandHistoryWindowText.Length`
   - zeroed counters, subscribed to the eight `RhinoDoc` events above and to `Command.UndoRedo`
   - `CommandWindowCaptureEnabled = true`, then `CapturedCommandWindowStrings(true)` to clear the buffer
   - a `Stopwatch`
2. **Subscribe to `CommandPromptChanged`.** Record every distinct non-empty `RhinoApp.CommandPrompt` value, up to 10.
3. **Run by method:**
   - `PreCancel`: `RhinoApp.PostCancelEvent(doc.RuntimeSerialNumber)`, then `RhinoApp.RunScript(doc.RuntimeSerialNumber, "_" + X, false)`.
   - `PromptCancel`: `RunScript(serial, "_" + X, false)`. On the **first** prompt that contains `(` (an options prompt), or on the second prompt of any kind, call `PostCancelEvent(serial)` once.
   - `CancelToken`: `RunScript(serial, "_" + X + " _Cancel", false)`. As a watchdog, the handler calls `PostCancelEvent(serial)` once if a third prompt appears, because `_Cancel` may not be a recognised token.
   - Everything is in try/catch, and an exception is recorded.
4. **Afterwards:** unsubscribe everything and read the same values again. Record:
   - the new part of the history text (its last 400 characters)
   - the captured strings
   - `InCommand()`
   - the elapsed ms
   - set `CommandWindowCaptureEnabled = false`
5. **Append one block per probe** to `%TEMP%\owly-probe-test.txt` (UTF-8). Start each run with a header line: the Rhino version, the method, the set, the date and time. Also `RhinoApp.WriteLine` a one-line summary.

   The block format:
   ```
   ### _Pyramid · PromptCancel
   prompts: ["…", "…"]
   history: "…"
   captured: ["…"]
   inCommandAfter: False
   doc: add=0 delete=0 replace=0 undelete=0 modifyAttr=0 | select=0 deselect=0 deselectAll=0 | undoRedo=0
   undo: next 41->41, current 40->40 | modified False->False
   mru: [Circle, Line, Box] -> [Circle, Line, Box]
   ms: 85
   error: (none)
   ```
6. When the set is done, print "Probe done. Press Enter now and note which command starts. Results: <path>". Unsubscribe from Idle.

**Build gate for the executor:** only `dotnet build rhino/plugin/RhinoAI.csproj -p:RhinoTarget=R9` with 0 errors. Run no tests and make no panel changes. Commit on `owly/probe-spike`.

**What the product owner runs** (Rhino 9, a **new empty file**, Rhino closed first). Launch Rhino 9 with that worktree's build, the same way the "Rhino 9" configuration in `.vscode/launch.json` does it, pointing at the probe worktree's `rhino/plugin/bin/R9-win/Debug`. Then:
1. `OwlyProbeTest`, `Method=PromptCancel`, `Set=All`, `Delay=8`. Within the 8 s, run `_Circle` and press Esc. Wait for "Probe done", press **Enter**, and note what starts.
2. Repeat with `Method=PreCancel`.
3. Repeat with `Method=CancelToken`.
4. Note any dialog that appeared, and any command that stayed waiting until you pressed Esc.
5. Press Ctrl+Z once and note whether anything is undone (it should say nothing to undo, or undo your own last action).
6. Send `%TEMP%\owly-probe-test.txt` back, plus notes 1-5.

The advisor then records the per-command table and the verdict in this plan. The Part B executor (or a doc-only step) writes `docs/owly/option-probe-spike.md` from it.

## Part A results (Rhino 9.1.26314.9001, 2026-11-24)

The product owner ran `OwlyProbeTest` (commit `b2443fa` on `owly/probe-spike`) in an empty file, three or more times per method. The log `%TEMP%\owly-probe-test.txt` stays on their machine and is not copied into the repo. The advisor read it.

| Criterion | PromptCancel | PreCancel | **CancelToken** (`RunScript("_X _Cancel")`) |
|---|---|---|---|
| No document change (add, delete, replace, undelete, modify-attributes events) | pass, all 17 | pass, all 17 | **pass, all 17** |
| Selection unchanged (select and deselect events) | pass | pass | **pass** |
| `doc.Modified` unchanged | pass (False→False) | pass | **pass** |
| No undo record kept: `CurrentUndoRecordSerialNumber` | pass (0→0) | pass | **pass (0→0)** |
| Enter afterwards repeats the student's own command (`_Circle`) | **FAIL**: `_Explode` (the last probed) ran | **FAIL**: `_Explode` ran | **pass: Circle repeated** |
| No dialog | Text opened its dialog | Text opened its dialog | Text opened its dialog |
| Prompt and current values captured | yes, 15 of 17 (Text: none) | yes, 16 of 17 (Text: none) | yes, 14 of 17 (Text, Hatch and Explode: none) |
| Time per probe | ~20 ms with options; up to ~1 s without | ~500 ms | **~15-55 ms** |

Notes:
- **Every** probe advanced `NextUndoRecordSerialNumber` by 1 and fired 5 `Command.UndoRedo` events, while `CurrentUndoRecordSerialNumber` stayed 0. Every command start reserves an undo serial number; a cancelled command that changed nothing keeps no record. The product owner pressed Ctrl+Z after one run, and Rhino said **"nothing to undo"**, which confirms it.
- **Text** opens its dialog with every method. Guard 4 excludes it: Text's R9 help page has no "Command-line options" section, while Hatch, Explode, ExtrudeCrv, Array and Revolve each have one or more. So the probe never starts Text.
- **CancelToken read nothing for Hatch and Explode.** The token cancels before their prompt is shown: no prompt event, no history line, nothing captured. The probe must treat "nothing captured" as "use the help page", not as "no options".
- Captured lines look like `Center of inscribed pyramid ( NumSides=8  Mode=Inscribed  Edge  Star  DirectionConstraint=Vertical  Solid=Yes )` and `Select first curve to fillet ( Radius=1  Join=No  Trim=Yes  ExtendArcsBy=Arc  ExtendOtherCurvesBy=Line  Dynamic=No )`.
  - Options are separated by **two** spaces.
  - Kinds read straight off the line: `=number`, `=Yes/No` (a toggle), `=Word` (a choice; its other values come from the help page), and bare (an action, never written).
  - The line holds the student's **current values** (NumSides=8, not the default 5). Every option in it is at the **first prompt**, so Run can write it without "Unknown command".
- The capture is identical in three places: the `CommandPromptChanged` → `RhinoApp.CommandPrompt` value, `CapturedCommandWindowStrings`, and the new history text (without the trailing `:`).
- The `mru:` lines printed only type names (`MostRecentCommandDescription`), so they say nothing. The Enter check was done by hand.

**Verdict: FEASIBLE, with CancelToken only** (`RunScript(serial, "_X _Cancel", false)`, posted from Idle, under the four guards). PromptCancel and PreCancel are **not acceptable**, because they take over "repeat last command". Part B uses CancelToken only. The branch `owly/probe-spike` is not merged.

## Done criteria

- Part A: the spike doc exists with the per-command table and verdict.
- Part B (if built): shared gates pass, the parser and merge tests pass, and `git grep -n "_Cancel" rhino/plugin` shows only `CommandOptionProbe.ai.cs`.

## Manual Rhino check (Part B)

- With nothing selected, ask about Pyramid. The card shows NumSides with your current value as placeholder, and Mode read-only with the hint. Rhino's model, undo list and selection are unchanged.
- Run `_Circle` yourself, then ask about Pyramid, then press Enter in Rhino. Circle repeats.
- Select an object and ask about Pyramid. No probe runs, and the card uses help-page data only.
