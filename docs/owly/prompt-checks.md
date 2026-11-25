# Owly prompt checks

A manual script for checking Owly's behaviour in Rhino after any change to `rhino/plugin/Agents/AgentPrompts.cs`. Start a new conversation for each row. A row passes when every expectation holds.

| # | Student types | Expected |
|---|---|---|
| 1 | How do I loft between curves? | Calls get_commands first. Recommends `_Loft` as inline code with the underscore. Names `_Loft` once (it becomes a copy button); no separate 'click it to copy' step. Short explanation, numbered steps, ends with a one-line offer of more detail. No ask_user overlay. |
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
| 12 | Where is the Loft button? | Names `_Loft` once (it becomes a copy button); no separate 'click it to copy' step. Points to the toolbar icon next to it to see where it is. Doesn't claim to have highlighted or opened anything. Doesn't invent a toolbar location. |
| 13 | How do I make a pyramid roof? | Calls get_commands first. `_Pyramid` appears once, at the step where it is used, not in an introduction and again in a step. If more than one command is needed, they are listed in the order the student uses them. |
| 14 | Walk me through a three-step loft workflow. | `_Curve`, `_Loft` and `_Cap` each appear exactly once, in that order of use, each as a copy button where it is used. No command is repeated in an introduction or summary. |
| 15 | Explain the Pyramid command. | Calls get_commands first. Explains `_Pyramid` and mentions that the Run button under it starts the command in Rhino. Never says it ran, started or typed anything itself. |
| 16 | How do I make a pyramid that stands straight up from the construction plane? | Writes `_Pyramid DirectionConstraint=Vertical` or explains Vertical. The card shows Vertical marked "suggested by Owly". |
| 17 | Make a hexagonal pyramid. | Owly may write `_Pyramid NumSides=6`. The card shows NumSides pre-filled with 6, marked "suggested by Owly", when the probe ran. Without probe data, the suggestion is dropped and the text explains typing 6 at the sides prompt. |
| 18 | How do I loft a roof? (level Beginner in settings) | Explains what a loft is and the terms it uses (curves, seams, cap), in small steps with the reason for each step. |
| 19 | How do I loft a roof? (level Intermediate) | Assumes the interface is known. Names `_Loft` and the options that matter (style, closed loft, rebuild), without explaining basic terms. |
| 20 | How do I loft a roof? (level Advanced) | Concise. Covers workflow, precision (curve direction, seam alignment, rebuild vs refit), alternatives such as `_Sweep2` or `_NetworkSrf`, and their trade-offs. Visibly shorter and deeper than rows 18 and 19. |

Record the date, model and Owly commit when you run it.
