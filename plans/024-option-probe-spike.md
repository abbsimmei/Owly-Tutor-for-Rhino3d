# 024: Spike: read a command's options with a silent option probe

- **Written against:** `40f2c38` on `rhino-9.x`. **Requires plan 023 to be DONE** (it is: `bc43ff5`). Part A ran at `3136b35`; Part B is re-anchored at `771eae5` by the advisor addendum after "Part A results".
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

**Product-owner decision for Part B (2026-11-24): edit first-prompt options.** Once probe data exists for a command:
- **Editable:**
  - numbers (`=number` in the probe line)
  - Yes/No toggles
  - choices whose values the help page lists

  All three only **if the option is in the probe line**. The current value is the placeholder or the pre-selected button.
- **Read-only:**
  - an option in the probe line whose choices the help page doesn't list, such as Pyramid's `Mode`. It shows its current value with the hint "Click Mode in Rhino's command line to see the choices."
  - actions (bare names)
  - every option that is only on the help page, such as `BothSides`. These are explain-only rows, so Run never makes Rhino say "Unknown command".
- **Without probe data** (Hatch, Explode, a guard failed, offline): plan 023's help-page behaviour, unchanged.

**Number macros, first test (product owner, Rhino 9, 2026-11-24):**

| # | Macro | Result |
|---|---|---|
| 1 | `_Pyramid _NumSides=6` | Works: NumSides becomes 6 |
| 2 | `_Fillet _Radius=2.5` | Doesn't work: `_Radius` acts as a click on the option, Rhino opens its sub-prompt and waits for the radius, and `=2.5` is lost |
| 3 | `_Offset _Distance=0.5 _Corner=_Round` | Doesn't work: Rhino waits at the offset-distance sub-prompt |
| 4 | `_Pyramid _NumSides=abc` | Keeps the old value and waits at the number-of-sides sub-prompt |

So `_Opt=number` works for some number options and not others, and the probe line (`NumSides=8` vs `Radius=1`) can't tell them apart. **Don't use `=` for numbers.** Choices and toggles keep `_Opt=_Value`, which plan 023's test verified.

**Number macros, second test, the two-token form:**

| # | Macro | Result |
|---|---|---|
| 5 | `_Fillet _Radius 2.5` | Works: Radius 2.5, and Rhino is back at "Select first curve to fillet" |
| 6 | `_Offset _Distance 0.5 _Corner=_Round` | Works: both set, back at the first prompt |
| 7 | `_Pyramid _NumSides 6` | Works |
| 8 | `_Pyramid _NumSides 6 _Solid=_No` | Works: a number followed by a toggle |

**Rule:** a number option is written as two tokens, `_Opt value` (a click on the option, then the value at its sub-prompt). A choice or toggle is written as one token, `_Opt=_Value`. Tokens can be mixed in any order.

**Verdict: FEASIBLE, with CancelToken only** (`RunScript(serial, "_X _Cancel", false)`, posted from Idle, under the four guards). PromptCancel and PreCancel are **not acceptable**, because they take over "repeat last command". Part B uses CancelToken only. The branch `owly/probe-spike` is not merged.

## Advisor addendum for Part B at `771eae5` (read before starting; it overrides the "Part B" bullets in Scope)

Read first, in this order:
- "Part A results"
- the product-owner decision
- both number-macro tables and their **Rule**

Also read plan 023's addendum (`plans/023-options-card-from-help-pages.md`), which describes the code you extend.

What exists (merged):
- **The Rhino-free help-page model**, in `UI/CommandOptionsParser.ai.cs`: `CommandOptionsParser.Parse(html)` returns `CommandOptions { Options: CommandOption(Name, Kind "choice"|"toggle"|"info", Values, Description) }`.
- **The help-data source and cache**, `UI/CommandOptionsSource.ai.cs`:
  - `Get(name)` is async, with the HTML fetched off the UI thread.
  - `TryCached(name, out data)` is what `RunCommand` uses.
- **The macro builder**, `UI/RunMacro.ai.cs`. `RunMacro.Build(name, changes, data)` writes `choice` and `toggle` options as ` _Opt=_Value`.
- **The handlers in `UI/AIPanelViewModel.ai.cs`:**
  - `FindCommandOptions` → `AnswerOptions(name)`, which awaits `Get` and posts `CommandOptionsFoundEvent` through `Application.Instance.AsyncInvoke`.
  - `RunCommand`, which builds the macro from `TryCached`.
- **The panel:**
  - `rhino/panel/src/ui/commandCard.ts`, which renders rows from `store.commandOptions` and keeps values in `store.cardValues` (key `turnId|name`)
  - `ui/explainPopover.ts`
  - `parseCommandCode` in `core/commandName.ts`, the prefill syntax `` `_Name Opt=Value` ``

**Scope boundaries.** Rhino 9 and later only: on Rhino 8 the probe never runs, and the card behaves exactly as plan 023. Add no agent tool. The AI can't trigger the probe. Never use `SendKeystrokes`. The only script the probe ever runs is `<validated chip name> + " _Cancel"`.

**C# side**
1. **`UI/ProbeLineParser.ai.cs`** (Rhino-free; link it in `tests/Server.Tests/Server.Tests.csproj`).
   - `public static IReadOnlyList<ProbeOption>? Parse(string? line)`, with `record ProbeOption(string Name, string? Value)`.
   - Trim the line, strip one trailing `:`, and match `^(?<prompt>.*?)\s\(\s+(?<opts>.*?)\s+\)\s*$`. No match, or zero options, gives null.
   - Split `opts` on runs of **2 or more** spaces. A token is `Name` or `Name=Value`.
     - Name must match `^[A-Za-z][A-Za-z0-9]*$`.
     - Value is trimmed and must match `^[A-Za-z0-9.\-]+$`.
     - Drop any token that fails, and drop duplicate names (keep the first).
   - Never throw.
   - Tests in `ProbeLineParserTests.ai.cs` use **synthetic** lines with invented names, for example `"Center of demo shape ( Sides=8  Shape=Round  Edge  Solid=Yes )"`. Cover:
     - a number, a word, a toggle and a bare action
     - a trailing `:`
     - no parentheses, giving null
     - empty parentheses, giving null
     - a non-ASCII name, dropped
     - a value with a comma (`2,5`), dropped
     - a single-space-separated line (`( A  B C )` gives the tokens `A` and `B C`, and `B C` fails the name rule and is dropped)
     - garbage, giving null
2. **`UI/OptionMerge.ai.cs`** (Rhino-free; link it).
   - Extend the model in `CommandOptionsParser.ai.cs` without breaking existing callers: `CommandOption(string Name, string Kind, IReadOnlyList<CommandOptionValue> Values, string Description, string? Current = null, bool? FirstPrompt = null)`. Kind gains `"number"`.
   - `public static CommandOptions Merge(CommandOptions help, IReadOnlyList<ProbeOption>? probe)`. A null or empty probe returns `help` unchanged. Otherwise, take the probe options **in probe order**, each with `FirstPrompt = true` (h is the help option of the same name, matched case-insensitively):
     - **Value null** (action): `info`, `Current = null`, h's description and values (for the explanation).
     - **Value matches `^-?\d+(\.\d+)?$`**: `number`, `Current = value`, h's description, no values.
     - **Value is Yes/No or On/Off** (any case): `toggle`, `Current` in canonical case. Values are h's values if h is a `toggle`, otherwise `[Yes, No]` or `[On, Off]` with empty descriptions.
     - **Any other word**:
       - if h is a `choice` and one of its values matches (case-insensitively): `choice`, h's values, `Current` = that value's canonical name
       - otherwise `info` with `Current = value` and h's description (this is Pyramid's `Mode`)
   - Then append every help option that is **not** in the probe, as `info`, keeping its values and description, with `Current` and `FirstPrompt` null.
   - Tests in `OptionMergeTests.ai.cs` (synthetic): every row above, the order, the case-insensitive match, a help-only choice becoming info, and a null probe returning `help` by reference.
3. **`RunMacro.Build` additions.**
   - A `number` option whose change matches `^-?\d+(\.\d+)?$` (at most 15 characters) is written as ` _{Opt} {value}`: two tokens.
   - `choice` and `toggle` are unchanged (` _Opt=_Value`). `info` is never written.
   - Keep data order.
   - Add to `RunMacroTests`:
     - a number gives `_Demo _Sides 6`
     - a decimal and a negative number
     - mixed with a toggle gives `_Demo _Sides 6 _Solid=_No`
     - rejected values: `2,5`, `1e5`, `6 _Delete`, `6;_Exit`, `-`, `.`, `abc`, and 16+ characters
     - a number change for an `info` option is dropped
4. **`UI/CommandOptionProbe.ai.cs`** (Rhino-dependent).
   - `internal static IReadOnlyList<ProbeOption>? TryProbe(RhinoDoc doc, string name, CommandOptions help)`. It runs **synchronously, on the UI thread, outside any WebView handler** (the caller guarantees it). It returns null unless **every guard** holds:
     1. `RhinoApp.ExeVersion >= 9`
     2. `!Command.InCommand()`
     3. no selected objects: `doc.Objects.GetSelectedObjects(false, false)` is empty
     4. `CommandBlocklist.Classify(name, Command.IsCommand, CommandAliasList.IsAlias) == RunVerdict.Ok`
     5. `help.Options.Count > 0` (the help page lists command-line options, which keeps Text and its dialog out)
     6. the Rhino UI language is English: `(Rhino.ApplicationSettings.AppearanceSettings.LanguageIdentifier & 0x3FF) == 0x09`. Localised option names can't be matched to the help page.
   - Then:
     1. Subscribe `RhinoApp.CommandPromptChanged`, recording distinct non-empty `RhinoApp.CommandPrompt` values, up to 5.
     2. `CommandWindowCaptureEnabled = true`, then `CapturedCommandWindowStrings(true)` to clear it.
     3. `RhinoApp.RunScript(doc.RuntimeSerialNumber, name + " _Cancel", false)`.
     4. In `finally`: unsubscribe and set capture off.
     5. Parse the recorded prompts, then the captured strings, with `ProbeLineParser`. Return the first non-null result.
     6. Catch everything, `HostUtils.LogDebugEvent("[owly] option probe …")`, and return null.
     7. Log each probe with its elapsed ms.
   - This file is the **only** place in `rhino/plugin` that contains `_Cancel`.
5. **Wiring** (`AIPanelViewModel.ai.cs` and `CommandOptionsSource.ai.cs`).
   - `CommandOptionsSource` gains a merged cache, `SetMerged(name, CommandOptions)`. `TryCached` returns the merged data if present, otherwise the help data. Use the same key function.
   - In `AnswerOptions`, after the help data arrives, go to the UI thread with `Application.Instance.AsyncInvoke`, as now. **There**, if `Document` is non-null:
     - call `TryProbe`
     - merge
     - if the probe returned data, `SetMerged`
     - post the merged options

     The `PanelCommandOption` mapping gains `Current` and `FirstPrompt`.
   - **Retry when a guard blocked the probe.** For example, the student had objects selected or was in a command. Remember the name in a small pending set with a 2-minute deadline. From `RhinoApp.Idle`, at most one per tick, re-try `TryProbe` with the cached help data. On success, merge, `SetMerged`, and post a new `command.options.found` (the store overwrites).
     - Drop the name once its deadline passes or the version, blocklist, empty-help or language guard fails (those won't change).
     - Unsubscribe `Idle` when the set is empty, and on `Dispose`.
     - Put this in `UI/OptionProbeRetry.ai.cs` or inside the view model; keep it small.
   - **Records:** `PanelCommandOption(string Name, string Kind, IReadOnlyList<PanelCommandOptionValue> Values, string Description, string? Current = null, bool? FirstPrompt = null)`. `PanelJson` drops nulls, so plan 023's pinned JSON line **must stay byte-identical**: keep that test unchanged. Add a new pin test for a merged line, with a synthetic `_Pyramid`-free command `_Demo`: one `number` option `Sides` with current `8` and firstPrompt `true`, and one `info` option with current `Round` and firstPrompt `true`. Add the same line to `rhino/panel/tests/host-events.json` after the plan 023 line.

**Panel side**
6. **Protocol.** In `src/protocol/events.ts`, `CommandOptionInfo` gains `kind: … | 'number'`, `current?: string` and `firstPrompt?: boolean`. Mirror it in `mockHost.ts`. The mock's `_Pyramid` becomes merged-style data:
   - `NumSides` number, current `8`
   - `Mode` info, current `Inscribed`, firstPrompt
   - `DirectionConstraint` choice, current `Vertical`
   - `Solid` toggle, current `Yes`
   - `Edge` info, firstPrompt
   - `BothSides` info

   Use short invented descriptions, as now.
7. **Card** (`ui/commandCard.ts`).
   - **number row:** `input.cmd-opt-number` with `type="text"`, `inputmode="decimal"` and `aria-label` = option name.
     - Placeholder: `t('options.currentValue', current)` ("{0} now") when there is a current value, else `t('options.keep')` ("Rhino's value").
     - Its value is the stored card value.
     - **Commit on `change` and on Enter, not on `input`.** The card is rebuilt on every store change, and committing per keystroke would destroy the field while the student types.
     - On commit, a value matching `^-?\d+(\.\d+)?$` (trimmed, at most 15 characters) → `setCardValue(key, opt, value)`. Empty → `setCardValue(key, opt, null)`. Anything else → `setCardValue(key, opt, null)` and set `aria-invalid="true"` on the rebuilt field. Use a one-shot flag in the store keyed `key|opt`, or simply accept that it clears.
   - **choice/toggle rows:** as now. The button whose value equals `current` also gets the class `is-current` and `title` = `t('options.currentValue', current)`. It is **not** `aria-pressed`. Pressed means the student chose it.
   - **info row with `current`:** show `span.cmd-opt-current` with `t('options.currentValue', current)`.
   - `effectiveValue` accepts a number value only if it matches the regex.
   - Run sends set values only, as now. **A current value is never sent unless the student sets it.**
   - Blocked commands render every row read-only (as now), still showing current values.
8. **Explain popover.** For an info option with `current` and `firstPrompt`, add `t('options.choicesHint', name)`: "Click {0} in Rhino's command line to see the choices." For an action (info, firstPrompt, no current), keep plan 023's `options.infoNote`.
9. **Prefill numbers.** In `parseCommandCode`, allow pair values that match `-?\d+(\.\d+)?` as well as identifiers, so `` `_Pyramid NumSides=6` `` seeds `NumSides: '6'`. Update `commandName.test.ts`. The card already filters suggestions through `effectiveValue`, so a number suggestion only appears on a `number` row.
10. **Strings**, IDs 350-359 (unused, checked):
    - `options.currentValue` ("{0} now")
    - `options.keep` ("Rhino's value")
    - `options.choicesHint`

    Put them in `PanelStrings.Localized()` and `i18n/strings.ts`. Check with `git grep -nE "LocalizeString\([^)]*, 35[0-9]\)" -- rhino/plugin`.
11. **Prompt.** Change the plan 023 sentence in `CommandSteer` to: " When a choice or a number suits the student's goal, you may write it with the command, like `_Pyramid NumSides=6` or `_Pyramid DirectionConstraint=Vertical`, but only options and values you are certain exist; the panel drops anything else and the student decides before clicking Run."
    - Update the `AgentPromptsTests` assertion.
    - The length guard must pass; report the length.
    - In `docs/owly/prompt-checks.md`, change row 17 to: "Make a hexagonal pyramid." Owly may write `` `_Pyramid NumSides=6` ``. The card shows NumSides pre-filled with 6, marked "suggested by Owly", when the probe ran. Without probe data, the suggestion is dropped and the text explains typing 6 at the sides prompt.
12. **verify.mjs** (host-page section, after the plan 023 checks, while `turn-0` is running). Inject a paragraph naming `` `_Demo` ``, plus `command.checked {_Demo, ok}` and the merged fixture line. Then check:
    - the `Sides` row has `input.cmd-opt-number` with placeholder "8 now"
    - typing `6` and dispatching `change` makes Run send `options` exactly `{Sides: '6'}`
    - typing `abc` and dispatching `change` makes Run send no `Sides`
    - the info row shows `.cmd-opt-current` with "Round now"
    - its explain popover contains "to see the choices"
    - a choice row's current button has `.is-current` and `aria-pressed="false"`. Inject a choice option in the same fixture line, or in a second injected event.
    - Number prefill needs no verify check: item 9's `commandName.test.ts` unit test covers the syntax, and plan 023's verify already covers how suggestions are seeded.

    Leave every popover closed.
13. **Docs.**
    - Create `docs/owly/option-probe-spike.md`: copy the "Part A results" section, both number-macro tables, the Rule and the verdict from this plan, with a one-line intro. Don't copy the log.
    - In `docs/adr/0008-run-on-student-click-and-option-probe.md`, append one line under Consequences: "Plan 024 Part A passed with the `_X _Cancel` form only; see docs/owly/option-probe-spike.md."
    - In `CLAUDE.md`, rewrite the "Being replaced" tail of the panel-actions rule to describe the probe as built:
      - the option probe (`UI/CommandOptionProbe.ai.cs`) starts and cancels a command with `_X _Cancel`, from Idle, under six guards, to read its first prompt
      - it is the only click-less action
      - the AI can't trigger it

**STOP and report instead of improvising if:**
- `AppearanceSettings.LanguageIdentifier` or `RhinoApp.ExeVersion` doesn't exist in RhinoCommon 8.19. Both must compile for R8.
- Keeping plan 023's pinned JSON byte-identical turns out impossible.
- The `change`-based number commit can't be made to work with the card's rebuild.

**Out of scope:**
- probing sub-prompts
- localised option names
- Rhino 8 probing
- saving current values across sessions
- changing which commands are blocked

## Done criteria

- Part A: the spike doc exists with the per-command table and verdict.
- Part B (if built): shared gates pass, the parser and merge tests pass, and `git grep -n "_Cancel" rhino/plugin` shows only `CommandOptionProbe.ai.cs`.

## Manual Rhino check (Part B)

- With nothing selected, ask about Pyramid. The card shows NumSides with your current value as placeholder, and Mode read-only with the hint. Rhino's model, undo list and selection are unchanged.
- Run `_Circle` yourself, then ask about Pyramid, then press Enter in Rhino. Circle repeats.
- Select an object and ask about Pyramid. No probe runs, and the card uses help-page data only.
