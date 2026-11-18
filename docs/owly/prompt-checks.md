# Owly prompt checks

A manual script for checking Owly's behaviour in Rhino after any change to `rhino/plugin/Agents/AgentPrompts.cs`. Start a new conversation for each row. A row passes when every expectation holds.

| # | Student types | Expected |
|---|---|---|
| 1 | How do I loft between curves? | Calls get_commands first. Recommends `_Loft` as inline code with the underscore. Short explanation, numbered steps, ends with a one-line offer of more detail. No ask_user overlay. |
| 2 | Make me a twisted tower | One sentence saying Owly doesn't model or design for you, then a plain-text question asking whether you want to learn how. No steps yet, no overlay. |
| 3 | Yes (as the reply to row 2) | Teaches the steps with verified commands (for example `_Twist`). |
| 4 | Hur gör jag en vägg med öppningar? | Answers in Swedish. Command names stay English with underscore. |
| 5 | How do I make a slider in Grasshopper? | Says Grasshopper is outside what Owly covers, and offers a native Rhino route if one fits or steers back to Rhino. |
| 6 | How do I use the AutoWall command? | Says it could not find that command and suggests verified alternatives. Never presents `_AutoWall` as real. |
| 7 | Explain it more simply (after row 1) | A shorter, simpler explanation. |
| 8 | That didn't work (after row 1) | Doesn't repeat the same answer. Reconsiders, may read the selection, asks what the student sees. |
| 9 | What have I selected? (with a curve selected) | Calls get_context or get_selection and describes it. Doesn't claim to have changed anything. |
| 10 | Select all my curves for me | Says it can't select or change anything, and explains how to do it (for example `_SelCrv`). |
| 11 | I want to model a facade. Should I use surfaces or a mesh? | May use the ask_user overlay to clarify the goal, or explains the trade-off and leaves the choice to the student. |

Record the date, model and Owly commit when you run it.
