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

## Done criteria

- Part A: the spike doc exists with the per-command table and verdict.
- Part B (if built): shared gates pass, the parser and merge tests pass, and `git grep -n "_Cancel" rhino/plugin` shows only `CommandOptionProbe.ai.cs`.

## Manual Rhino check (Part B)

- With nothing selected, ask about Pyramid. The card shows NumSides with your current value as placeholder, and Mode read-only with the hint. Rhino's model, undo list and selection are unchanged.
- Run `_Circle` yourself, then ask about Pyramid, then press Enter in Rhino. Circle repeats.
- Select an object and ask about Pyramid. No probe runs, and the card uses help-page data only.
