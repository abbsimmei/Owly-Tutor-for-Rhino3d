---
status: accepted (supersedes ADR-0007)
---

# A student's click on Run may start a command; a guarded probe may start and cancel one

ADR-0007 let a student's click copy a command and focus Rhino, but never type into the command line or run anything. The product owner reversed this for the Phase 5 rework (plans 022-024). Under each recommended command, an **options card** now holds a green **Run** button. When the student clicks it, the host starts the command in Rhino with the options the student set in the chat, and Rhino then waits for the student's own picks.

The host:
- never supplies picks or finishes a command
- refuses while Rhino is already in a command
- never starts a **blocked command**: script and plug-in runners, Grasshopper, Owly's own commands, file and app commands, Delete and Purge, Undo and Redo, and settings and layout commands
- builds the macro itself, only from tokens it has validated

The AI cannot trigger Run. It can only write `` `_Name` `` (optionally with suggested options) in an answer. So the AI keeps its five read-only agent tools (ADR-0002).

The one action without a click is the **option probe**. The host starts a command and cancels it at once to read its first prompt's options and current values. It is allowed only if all four guards hold:
- Rhino is idle
- nothing is selected
- the command isn't blocked
- its help page lists command-line options, so it is known to wait at a prompt

It is also allowed only if plan 024's Rhino test shows no traces: no model change, no undo step, no dialog, no selection change, and Enter still repeats the student's own last command. A cancelled line in the command history is acceptable. If the test fails, the probe is recorded as not feasible and the options come from McNeel's help pages only.

PRD §4's "Never execute Rhino commands on behalf of the user" now reads: Owly starts a command only when the student clicks Run, and never supplies the picks. The guarded option probe is the only exception.

## Considered options

- **Keep copy only (ADR-0007).** Safest, but the product owner wants the command written into Rhino and its options set in the chat.
- **Type the command without Enter (`SendKeystrokes(text, false)`).** Doesn't work with options: in Rhino a space acts as Enter, so writing the options already starts the command.
- **An allowlist of team-verified commands instead of a blocklist.** Safer against an unexpected command, but the product owner chose wide coverage with a blocklist.
- **Reading options only after the student clicks Run.** No click-less action at all, but the card would show nothing verified before Run. The product owner chose the probe, conditional on the test.

## Consequences

- Each Run goes through a PanelCommand, a validating handler in `AIPanelViewModel` and a try/catch, as ADR-0007 already required. `RunScript` blocks while a command waits for picks, so it is posted with `AsyncInvoke` and the WebView handler returns at once. Aliases are refused, because an alias can expand to anything.
- The blocklist lives in one C# file with unit tests. Adding a group of commands to it needs no ADR; removing a group does.
- A command not on the blocklist that acts at once on a selection (for example `_BooleanUnion` with objects selected) runs when the student clicks Run. The student's click is the consent.
- Letting the AI start Run, or adding any other click-less action, needs a new ADR that supersedes this one.
- Plan 024 Part A passed with the `_X _Cancel` form only; see docs/owly/option-probe-spike.md.
