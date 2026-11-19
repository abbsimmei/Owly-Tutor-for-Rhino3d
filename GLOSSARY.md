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

**Command option**:
A setting a Rhino command asks for while it runs (for example Loft's *Style*). Owly never invents one.
_Avoid_: parameter, argument

**Tool highlight**:
Showing the student where a recommended toolbar tool is: a location card in the panel, plus a brief on-screen flash of the button where Rhino allows it. It never shows, hides or changes a toolbar.

**Location card**:
A note under Owly's answer naming the toolbar, tab and button that start a recommended command, or saying it is on no open toolbar.

**Command copy**:
The student clicks a recommended Rhino command in the chat and it is placed on the clipboard. Nothing is typed into Rhino and nothing runs.
_Avoid_: command insertion (implies execution), run

**Command-line highlight**:
Drawing the student to Rhino's command line after a command copy: Rhino's window takes focus and Owly says to paste and press Enter. The student always presses Enter.

**Conversation**:
One continuous exchange of questions and answers between a student and Owly. When it ends, it is gone: Owly keeps no record of past conversations.
_Avoid_: chat, session, thread

**Transcript**:
The messages of the current conversation, as shown in the Owly panel.
_Avoid_: conversation history, chat log

**Welcome popup**:
A one-time introduction to Owly, shown the first time Rhino starts with Owly installed.
_Avoid_: splash screen, onboarding

**Agent tool**:
A capability the AI can call (for example `get_selection`). Every agent tool in Owly is read-only.
_Avoid_: tool (on its own, which clashes with Toolbar tool)

**Only Help**:
Owly's core principle. It never generates geometry, modifies the model or executes commands for the student.
