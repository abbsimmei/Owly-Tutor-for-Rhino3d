# A student's click in the panel may touch Rhino's UI, never the model

PRD Phase 4 asks Owly to highlight toolbar tools and the command line and to let students copy commands. None of that can be an agent tool: ADR-0002 keeps the AI's toolset at five read-only tools and treats a tool that changes UI state as acting, which is why `get_viewport_image` was removed. So Phase 4's actions are **panel buttons the student clicks**. On a click, the host may do four things: write a command name to the clipboard, move keyboard focus to Rhino's main window, read Rhino's loaded toolbar files from disk (read-only) to find where a command's button is, and show a temporary on-screen flash drawn by Owly. The host never touches the document. It never types into the command line, never runs a command, and never shows, hides, moves or edits Rhino's toolbars or panels. The AI cannot trigger any of these actions; it can only write `` `_Name` `` in an answer, which the panel turns into a button.

## Considered options

- **A highlight agent tool** that lets the AI flash a button or the command line whenever it thinks that helps. Rejected: the AI would then act on Rhino's UI, which contradicts ADR-0002 and the "exactly five tools" rule, and the student would lose control of when it happens.
- **Typing the command into the command line** (`RhinoApp.SendKeystrokes(text, false)`) as the "command-line highlight". Rejected by the product owner: Owly would be writing into Rhino's input. In Rhino a space acts like Enter, so any text with options in it could run the command. Copying plus moving focus achieves nearly the same with no risk.
- **Switching on a hidden toolbar** so the recommended button can be seen. Rejected: it changes the student's Rhino layout. Plans 012 and 013 already rule this out for anything except Owly's own toolbar.

## Consequences

- Each new panel action needs a `PanelCommand` record, a handler in `AIPanelViewModel.IncomingCommand` that validates its input, and a try/catch so a Rhino API failure never reaches the student as a crash.
- The clipboard only ever receives text that matches the command-name pattern, and the host checks this again.
- Rhino has no public API for a button's or the command line's screen position (RhinoCommon 8.19 and 9.0). The "tool highlight" is therefore a location card in the panel. An on-screen flash is added only where public types reach the button (plan 016).
- Adding an action that changes Rhino's state, or letting the AI start one, needs a new ADR that supersedes this one.
