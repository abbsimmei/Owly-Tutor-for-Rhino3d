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
The role Owly plays: it explains, recommends and guides, and never models for the student.
_Avoid_: modelling assistant, copilot

**Only Help**:
Owly's core principle. It never generates geometry, never modifies the model and never supplies a command's picks. A command starts only when the student clicks Run; the one exception is the option probe, which starts and cancels a command without changing anything.

**Agent tool**:
A capability the AI can call (for example `get_selection`). Every agent tool in Owly is read-only, and the AI can never trigger a panel action.
_Avoid_: tool (on its own, which clashes with Toolbar tool)

### Commands and tools

**Recommendation**:
Owly's suggestion of a Rhino command or toolbar tool that achieves what the student wants, with the reason it fits.
_Avoid_: action, operation

**Rhino command**:
A named Rhino operation the student types or clicks (for example `Loft`, `Fillet`). Owly names each command once per answer, in the order the student uses it.
_Avoid_: script, macro

**Toolbar tool**:
A button in Rhino's interface that starts a Rhino command.
_Avoid_: icon, widget

**Button image**:
The picture on a toolbar tool, exactly as the student's own Rhino draws it.
_Avoid_: icon (on its own), thumbnail

**Command option**:
A setting a Rhino command asks for while it runs (for example Loft's *Style*). Owly never invents one.
_Avoid_: parameter, argument

**Options card**:
The box under the step that names a command. It lists that command's options for the student to set, and holds the Run button.
_Avoid_: form, settings panel

**Run**:
The student clicks Run on an options card, and Owly starts that command in Rhino with the options the student set. Rhino then waits for the student's own picks.
_Avoid_: execute, insert, auto-run

**Blocked command**:
A command Owly never starts with Run, for example a script runner, Save, Delete, Undo or Options. Its options card says to copy it and run it yourself.
_Avoid_: forbidden command, disabled command

**Option probe**:
Owly briefly starting a command and cancelling it at once, without a click, only to read the command's options and their current values.
_Avoid_: test run, dry run

**Command copy**:
The student clicks a recommended Rhino command in the text, and it is placed on the clipboard. Nothing is typed into Rhino and nothing runs.
_Avoid_: command insertion (implies execution)

**Command-line highlight**:
Drawing the student to Rhino's command line after a command copy: Rhino's window takes focus and Owly says to paste and press Enter.

**Tool highlight**:
Showing the student where a recommended toolbar tool is: its button image and a location popover. It never shows, hides or changes a toolbar.

**Location popover**:
A small overlay on top of the transcript naming the toolbar, tab and button that start a recommended command, or saying it is on no open toolbar.
_Avoid_: location card (its earlier form, under the answer)

### Conversations and memory

**Project**:
A piece of the student's work, named by the student, that one or more .3dm files belong to. Owly keeps conversations and notes per project.
_Avoid_: folder, workspace, file

**Unsorted**:
The built-in project for files the student hasn't put in a project of their own.

**Conversation**:
One continuous exchange of questions and answers between a student and Owly, inside one project. It is saved until the student deletes it, and can be reopened and continued.
_Avoid_: chat, session, thread

**Transcript**:
The messages of the open conversation, as shown in the Owly panel.
_Avoid_: conversation history, chat log

**Project notes**:
A few short notes Owly writes when a conversation ends. They tell later conversations in that project what the student is working on and what has been explained.
_Avoid_: summary, memory (on its own)

**Main memory**:
An opt-in, cross-project list of the student's recurring difficulties and questions, which every new conversation receives.
_Avoid_: profile, learner model

### Teaching level

**Level**:
How much a student already knows, as they choose it: Beginner, Intermediate or Advanced. Owly pitches its explanations to it.
_Avoid_: user level, difficulty, skill

**Level check-in**:
A small card that now and then asks the student whether Owly's explanations are good, too easy or too hard, and offers to change the level.
_Avoid_: survey, rating

**Welcome popup**:
An introduction to Owly, shown the first time Rhino starts with Owly installed (and once more after a major change). The student chooses their level there.
_Avoid_: splash screen, onboarding
