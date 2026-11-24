# Option probe spike: results

Plan 024 Part A tested whether Owly can read a command's options by starting and cancelling it; the results, the number-macro tests and the verdict are copied from the plan below.

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
