# 017: Tell Owly about the command buttons, and update the PRD

- **Written against:** `6571b16` on `rhino-9.x`. **Requires plans 014 and 015 to be DONE.**
- **Status:** DONE (`926637a`, see `plans/README.md`); the manual prompt-checks run is still open
- **Effort:** S
- **Risk:** Low. It is a prompt wording change, checked by hand with `docs/owly/prompt-checks.md`.
- **Phase:** PRD Phase 4 ("Implement command recommendations"), plus the PRD notes the product owner asked for.

## Why

After plans 014 and 015, every `` `_Name` `` in an answer is a button. Clicking it copies the command. A second icon next to it shows where the command is on the toolbars. The model doesn't know this, so it still writes "type `_Loft` in the command line". The product owner decided:

- The prompt tells the model about the buttons, so it says "click `_Loft` to copy it, then paste it on the command line".
- The model **never** says that Owly typed, ran or highlighted anything.

The PRD also needs short notes recording three Phase 4 decisions:
- **command options** (PRD "parameters") are deferred to Phase 5
- "command terminal highlight" means copy plus focus
- "tool highlight" means a location card, plus an R9 flash if plan 016 succeeds

## Facts you need

- The current `CommandSteer` (`rhino/plugin/Agents/AgentPrompts.cs:42-48`):
  ```csharp
  public static string CommandSteer =>
      "Never invent commands, options, shortcuts or settings. Before you name any Rhino command, confirm it "
      + $"exists by calling mcp__{ServerName}__get_commands with a filter, and use the exact name it returns. "
      + "If it is not in the list, do not recommend it: say you could not find it and suggest something you "
      + "can verify instead. Always write a command as inline code with a leading underscore, spelled exactly "
      + "as get_commands returns it, for example `_Loft` or `_ExtrudeCrv`. The underscore makes the English "
      + "command name work in any Rhino language. Do not explain the underscore unless the student asks.";
  ```
- `OnlyHelpSteer` already says "you must never say or imply that you did something in Rhino" (`AgentPrompts.cs:17-24`).
- Tests: `tests/Server.Tests/AgentPromptsTests.ai.cs` asserts phrases in the composed prompt, including `` `_Loft` ``. Follow its style.
- `docs/owly/prompt-checks.md` has 11 rows. After any `AgentPrompts.cs` change, the whole script is run by hand (CLAUDE.md).
- The PRD Phase 4 section (`PRD.md`, "## **Phase 4 — Rhino Integration**"):
  ```
  * Implement tool highlighting.
  * Implement command recommendations.
  * Implement command copy.
  * Implement command terminal highlight
  * Support configurable command parameters.
  ```
  MVP acceptance criteria 4 and 5:
  ```
  4. Recommended toolbar tools and command terminal can be highlighted.
  5. Commands can be copied from the chat with configurable settings.
  ```

## Scope

**Edit:**
- `rhino/plugin/Agents/AgentPrompts.cs` (`CommandSteer` only)
- `tests/Server.Tests/AgentPromptsTests.ai.cs`
- `docs/owly/prompt-checks.md`
- `PRD.md`, notes only, in the Phase 4 section and acceptance criteria 4-5
- `CLAUDE.md`, one bullet

**Out of scope:**
- every other steer
- the tool list
- any panel or C# UI code
- `GLOSSARY.md` and the ADRs, which the advisor already wrote

## Steps

### 1. Baseline
Run the shared gates. If they fail, STOP.

### 2. Prompt (`AgentPrompts.cs`, `CommandSteer`)
Append these sentences to the end of `CommandSteer`, keeping the `+` style:
```csharp
      + " In Owly's panel each command you write this way becomes a button: the student clicks it to copy the "
      + "command, then pastes it on Rhino's command line and presses Enter, and a small toolbar icon next to it "
      + "shows where the command is on Rhino's toolbars. So tell the student to click the command to copy it, "
      + "not to type it. Never say that you typed, ran, opened or highlighted anything in Rhino.";
```
The first appended string must start with a space, because the existing last string ends without one. Change nothing else.

### 3. Test (`AgentPromptsTests.ai.cs`)
Add one test asserting that `AgentPrompts.Compose("")`:
- contains `"click the command to copy it"`
- contains `"Never say that you typed, ran, opened or highlighted anything in Rhino."`

Verify: `dotnet test tests/Server.Tests/Server.Tests.csproj` passes.

### 4. Prompt checks (`docs/owly/prompt-checks.md`)
- Change row 1's expectation to add: "Tells the student to click `_Loft` to copy it (not to type it)."
- Add row 12:

  | 12 | Where is the Loft button? | Recommends `_Loft`, tells the student to click it to copy and to use the toolbar icon next to it to see where it is. Doesn't claim to have highlighted or opened anything. Doesn't invent a toolbar location. |

### 5. PRD notes (`PRD.md`)
Keep the product owner's text. Add notes in the same style plan 009 used (`(no saved history, ADR-0006)`). The result must read:
```
* Implement tool highlighting (a location card in the panel; an on-screen flash only where Rhino allows it, ADR-0007).
* Implement command recommendations.
* Implement command copy.
* Implement command terminal highlight (copy, then Rhino gets focus for pasting; nothing is typed into Rhino, ADR-0007)
* Support configurable command parameters. (Moved to Phase 5: no Rhino API lists a command's options before it runs, so they wait for verified documentation.)
```
In Phase 5 add the bullet `* Configurable command options, based on verified documentation (moved from Phase 4).`

For acceptance criteria 4 and 5, append ` (see ADR-0007)` to 4 and ` (configurable settings from Phase 5)` to 5. Change no other PRD text, and keep the `<!-- gdoc_comment -->` markers exactly as they are.

### 6. CLAUDE.md
Under "Rules for code changes", after the ADR-0006 bullet, add:
```
- **Phase 4 actions run only on a student's click** (ADR-0007). A command chip copies the command and focuses Rhino; the toolbar icon shows a location card. Owly never types into the command line, never runs a command, and never shows, hides or changes a toolbar. The AI cannot trigger these actions.
```

### 7. Manual prompt check
Run the full `docs/owly/prompt-checks.md` script (rows 1-12) in Rhino 9. Record the date, model and commit in `plans/README.md`. If row 1 or row 12 fails because the model still says "type", or claims it highlighted something, STOP and report the transcript. Don't keep rewording without the advisor.

## Done criteria
- The R9 and R8 builds succeed. Server.Tests passes, including the new test.
- `git diff -- rhino/plugin/Agents/AgentPrompts.cs` shows only appended lines in `CommandSteer`.
- `grep -n "ADR-0007" PRD.md CLAUDE.md` shows the new notes.
- The prompt-checks run is recorded.

## Maintenance note
If plan 016 ships a flash, the prompt still must not claim one. The flash comes from the student's click, not from the model. Phase 5's configurable command options will need another `CommandSteer` change and new prompt-checks rows.
