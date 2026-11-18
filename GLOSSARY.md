# Owly

An educational plugin for Rhino that teaches architecture students how to use Rhino's tools and commands. The student always does the modelling; Owly only helps.

## Language

**Owly**:
The tutor plugin and the AI persona inside it.
_Avoid_: RhinoAI (the upstream project it was forked from), assistant, agent (in user-facing text)

**Student**:
The person using Owly, typically an architecture student, possibly a beginner.
_Avoid_: user (in product text), client

**Tutor**:
The role Owly plays: it explains, recommends and guides, and never acts on the model.
_Avoid_: modelling assistant, copilot

**Recommendation**:
Owly's suggestion of a Rhino command or toolbar tool that achieves what the student wants, with the reason it fits.
_Avoid_: action, operation

**Rhino command**:
A named Rhino operation the student types or clicks (for example `Loft`, `Fillet`). Owly shows it in the chat; it never runs it.
_Avoid_: script, macro

**Toolbar tool**:
A button in Rhino's interface that starts a Rhino command.
_Avoid_: icon, widget

**Tool highlight**:
A temporary visual cue (blink, colour or outline) on a toolbar tool that Owly is recommending.

**Command copy**:
Placing a recommended Rhino command, optionally with parameters the student configured in the chat, on the clipboard or command line *without executing it*.
_Avoid_: command insertion (implies execution), run

**Command-line highlight**:
A visual cue drawing the student's attention to Rhino's command line.

**Agent tool**:
A capability the AI can call (for example `get_selection`). Every agent tool in Owly is read-only.
_Avoid_: tool (on its own, which clashes with Toolbar tool)

**Only Help**:
Owly's core principle. It never generates geometry, modifies the model or executes commands for the student.
