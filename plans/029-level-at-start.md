# 029: Choose a level at the start

- **Written against:** `40f2c38` on `rhino-9.x`.
- **Status:** DONE: `f694ada`, merged into `rhino-9.x` (`8371148`); manual Rhino check open
- **Effort:** M
- **Risk:** Low to medium. The welcome popup changes, the prompt gets a level block, and a level change needs a CLI relaunch.
- **Phase:** Phase 5 rework, user level (PRD §3.4). Glossary: **Level**, **Welcome popup**.

## Why

"Choose user level at start." The product owner decided:

- There are **three levels**:
  - **Beginner**: new to Rhino; explain the basics and the terms
  - **Intermediate**: knows the interface; focus on the right tool and its options
  - **Advanced**: wants depth; workflows, precision, alternatives
- **Global**, not per project.
- **Chosen in the welcome popup.** If the popup is closed without a choice, the level is Beginner. It can be **changed in settings**.
- **The welcome popup is shown once more to existing installs**, because its privacy text changes (saved conversations, plans 025-028), and because of the level choice and the main-memory switch (plan 028).

## Facts you need

- **Welcome popup:** `rhino/plugin/UI/WelcomeDialog.ai.cs`, an Eto `Dialog<bool>` with a vertical `StackLayout`: logo, title, slogan, four paragraphs (the privacy line at `:~28`), a link, then Close and Open Owly.
  - `ShowFor` only returns whether to open Owly. Return a small result record, or let the dialog write the settings itself on close.
- **Start-up:** `RhinoAIPlugin.OnFirstIdle` (`RhinoAIPlugin.cs:~57-79`) shows the popup once when `AISettings.WelcomeShown` is false, and sets the flag **before** showing. Keep that crash-safe pattern.
  - Re-showing: add an int `AISettings.WelcomeVersion`, and show when it is below `WelcomeDialog.CurrentVersion` (2).
  - Existing installs have `WelcomeShown == true` and version 0, so they see it once.
  - New installs see it once.
  - "About Owly" and `OwlyWelcome` still open it any time.
- **Settings:** `AISettings.cs` uses PersistentSettings (per Rhino version), and the settings UI is in `UI/AISettingsPanel.ai.cs` (model dropdown, `TryCommit`). Keep the level in `AISettings`, so this plan doesn't depend on the memory plans. The main-memory switch lives in `profile.json` (plan 028).
- **Prompt:** `Agents/AgentPrompts.cs`'s `TeachingSteer` says "assume a beginner unless the student shows otherwise". `Compose` builds the system prompt, which is passed on the command line.
  - **Budget:** cmd.exe allows 8,191 characters on the whole command line, and the steers take about 5.6k. Keep each level block ≤400 characters, and add a test that the composed prompt for every level stays under a budget (e.g. 6,500 characters).
- **Applying a change:** the prompt is rebuilt on every CLI launch, but a running CLI keeps its old prompt. On a level change, end the idle CLI processes (the `StreamJsonAgent` kill path), so the next prompt relaunches with `--resume` and the new prompt.
  - **Spike (10 min):** check that `claude --resume <id> --system-prompt <new>` uses the new prompt. If it doesn't, apply the level from the next new conversation, and say so in settings.
- **Localisation IDs:** 440-449.

## Steps

1. `AISettings.Level` (enum string, default `Beginner`) and `AISettings.WelcomeVersion` (int, default 0).
2. **Welcome popup:**
   - a `RadioButtonList` "How well do you know Rhino?" with the three levels and one-line descriptions, preselected to the current level
   - the main-memory check box, if plan 028 is done; it reads and writes `profile.json`
   - the new privacy text, from plans 025-028
   - on close (either button), save the level, plus the switch if present
3. **`OnFirstIdle`:** show the popup when `WelcomeVersion < CurrentVersion`, setting the version first. Keep `WelcomeShown` for compatibility.
4. **Prompt:**
   - `LevelSteer(level)` returns one paragraph per level.
   - `TeachingSteer` drops "assume a beginner…" and refers to the level.
   - `Compose(systemPrompt, level)`. Callers pass `AISettings.Level`.
   - Tests: each level's text is present, and the length budget holds.
5. **Settings:** a Level dropdown in `AISettingsPanel`, saved on commit. On a change, end the idle CLI processes.
6. **Docs:**
   - prompt-checks rows 18-20: the same question at each level gives a visibly different depth
   - the CLAUDE.md settings sentence
   - the PRD §3.4 note

## Done criteria

- Shared gates pass, and the new `AgentPromptsTests` (per level, length budget) pass.
- An existing install (`WelcomeShown = true`) sees the popup exactly once after the update (check manually).

## Manual Rhino check (Rhino 9)

- With an existing install, start Rhino: the new popup appears once. Choose Advanced. Restart Rhino: no popup.
- Ask "how do I loft?". The answer is concise and advanced.
- Switch to Beginner in settings and ask again in the same conversation. The answer explains the basics. If the spike said `--resume` keeps the old prompt, check this in a new conversation instead.

---

## Advisor addendum (re-anchored at `154e994`, 2026-11-25)

This plan was written at `40f2c38`. Plans 022-028 have since been merged. **Plan 028 is done**, so the welcome popup gets the main-memory check box here. **Where this addendum and the steps above disagree, the addendum wins.** Every fact above was re-checked at `154e994`; the corrections are below.

### Facts at `154e994` (read these first)

All paths are under `rhino/plugin/`.

- **`UI/WelcomeDialog.ai.cs`**:
  - `Dialog<bool>`, `Resizable = false`, `TextWidth = 420`, `Paragraph(text)` for wrapped labels.
  - The privacy line is now **id 437** (plan 028), and it already mentions saved conversations, notes and main memory. **Don't change its text.**
  - `Close(true)`/`Close(false)` come from the buttons; `AbortButton = closeButton` handles Esc.
  - `ShowFor(Window? owner)` opens the panel on true.
  - **Three callers:**
    - `RhinoAIPlugin.OnFirstIdle` (`RhinoAIPlugin.cs:~85`)
    - `Commands/WelcomeCommand.ai.cs:15`
    - `UI/AISettingsDialog.cs:35` ("About Owly")
- **`RhinoAIPlugin.OnFirstIdle`:**
  - **Order:** `DeleteLegacyConversations`, then `FirstRunLayout`, then the notes drain (plan 027), then the welcome block, which sets `AISettings.WelcomeShown = true` **before** showing and `return`s early when it is already set.
  - **The welcome block stays last.**
- **`Agents/AgentPrompts.cs`:**
  - `Compose(string systemPrompt)` joins eight steers. `TeachingSteer` contains "and assume a beginner unless the student shows otherwise".
  - The one caller is `Agents/Conversations/ClaudeStreamJsonParser.ai.cs:98`: `AgentPrompts.Compose(AISettings.EffectivePrompt(Definition))`.
  - **Tests:** `tests/Server.Tests/AgentPromptsTests.ai.cs` asserts `Compose("").Length < 7000` (currently about 4.9k).
- **CLI processes persist between turns:** `ClaudeStreamJsonParser.IsOneTurnPerProcess => false`. So a level change needs the idle process ended. `StreamJsonAgent.Kill()` is private. It sets `StartTask = null`, so the next prompt respawns with `--resume` (`HasEverStarted` stays true) and the freshly composed `--system-prompt`.
- **`UI/AISettingsPanel.ai.cs`:**
  - `AgentsTab()` rows: name, availability, `Model:`, then the "Main memory" group (plan 028: `MainMemoryBox`, applied in `TryCommit` only when changed), then Delete all, then `Found at:`.
  - `TryCommit` saves on the settings dialog's Save.
- **`AISettings.cs`:** `Settings.GetBool/SetBool/GetString/SetString` per Rhino version. Copy the `WelcomeShown` property pattern.
- **`OwlyMemory.Main`** (`Agents/Memory/MainMemory.ai.cs`): `Enabled`, and `SetEnabled(bool)`, which clears the list when turned off. The settings check-box text is localisation id **430**: "Remember what I find hard across projects".

### Advisor decisions (fill the gaps; do not re-litigate)

1. **The level type.** Add a Rhino-free `Agents/OwlyLevel.ai.cs` with `enum OwlyLevel { Beginner, Intermediate, Advanced }` and `static OwlyLevel Parse(string?)` (case-insensitive, defaulting to `Beginner`). Link it in Server.Tests.
   - `AISettings.Level` stores the enum name as a string; reading uses `OwlyLevel.Parse`.
   - `AISettings.WelcomeVersion` is an int, default 0.
   - The level is per Rhino version, as the plan says (in `AISettings`). Rhino 8 and Rhino 9 keep their own level; that is accepted.
2. **The prompt.**
   - **`AgentPrompts.LevelSteer(OwlyLevel)`:** one paragraph per level, **each ≤400 characters**, starting "The student chose the level Beginner/Intermediate/Advanced." and saying how to pitch answers:
     - **Beginner:** explain terms and the interface, smaller steps, and why each step matters.
     - **Intermediate:** assume the interface is known; focus on the right tool and the options that matter.
     - **Advanced:** concise; workflows, precision, alternatives and trade-offs; skip basics.

     Each ends with: "If the student asks for simpler or deeper, follow that for the answer."
   - **`TeachingSteer`:** replace "and assume a beginner unless the student shows otherwise" with "and pitch the answer at the student's level (below)". Change nothing else in it.
   - **`Compose(string systemPrompt, OwlyLevel level)`:** puts `LevelSteer(level)` right after `TeachingSteer`. Remove the old one-parameter overload. Update the one caller to pass `AISettings.Level`, and update the existing tests.
   - **Tests:**
     - each level's steer appears in `Compose`, and the others don't
     - each `LevelSteer` is ≤400 characters
     - `Compose("", level).Length < 6500` for every level (keep the 7,000 test or replace it)
     - "assume a beginner" no longer appears
3. **Ending idle CLIs on a level change.**
   - **`IAgentRunner.RestartWhenIdle()`:** "the next prompt relaunches the CLI".
   - **`AgentRunner`:**
     - set a `RestartPending` flag
     - if `TurnGate.Wait(0)` succeeds, release it and restart now
     - otherwise `PromptAsync`'s `finally` restarts after the turn, then clears the flag
   - **"Restart":** the connection is a `StreamJsonAgent`; call a new `internal void EndIdleProcess()` on it, which calls `Kill()` only when `CurrentTurn` is null. **No `NoteSystem` line.**
   - **`AgentHost.RestartIdleAgents()`:** calls it on every pooled runner. UI thread, like the rest of `AgentHost`.
   - **Who calls it:** the settings `TryCommit` when the level changed, and the welcome popup when the level changed.
   - **Check before relying on it:** an idle kill must not trip the rejected-resume path in the read-loop `finally`. That path requires `ResumePending`, which is false once a turn has completed, and no process exists before the first turn. Read the code and confirm. If it could trip, **STOP and report**.
4. **The welcome popup.**
   - **`CurrentVersion = 2`.**
   - **Below `needs` and above the link:**
     - a bold label "How well do you know Rhino?"
     - a `RadioButtonList` (vertical) with three items, each "Name: description", the description in one line:
       - Beginner: "New to Rhino. Explain the basics and the terms."
       - Intermediate: "I know the interface. Show me the right tool and its options."
       - Advanced: "I want depth: workflows, precision and alternatives."
     - then the main-memory `CheckBox`, reusing id **430**'s text, with a short `Paragraph` hint: "Off by default. You can change both in Owly settings."
   - **Preselection:** the current `AISettings.Level` and `OwlyMemory.Main.Enabled`.
   - **Saving:** on **every** close path (Open Owly, Close, Esc, the title-bar ✕), save from the dialog's `Closed` event, not from the buttons:
     - `AISettings.Level` if changed, then `AgentHost.RestartIdleAgents()`
     - `OwlyMemory.Main.SetEnabled(checked)` if changed
   - **Closing without a choice** keeps the preselected value, which is Beginner on a new install. That meets "closed without a choice → Beginner".
   - **`ShowFor` returns `bool`** (true when anything changed), so the settings dialog can refresh. The `OnFirstIdle` and `WelcomeCommand` callers ignore it.
   - **Size:** the dialog has no fixed height and grows with its content. Don't shrink or re-scale the owl logo (`OwlyArt` rules). The manual check covers small screens.
5. **Re-showing (`OnFirstIdle`).**
   - Show when `!RhinoApp.IsRunningHeadless && AISettings.WelcomeVersion < WelcomeDialog.CurrentVersion`.
   - Set `AISettings.WelcomeVersion = WelcomeDialog.CurrentVersion` and `AISettings.WelcomeShown = true` **before** showing, inside the existing try/catch pattern.
   - The block stays last.
6. **Settings.**
   - Add `Level:` (a `DropDown` with the three names) under `Model:`. Saved in `TryCommit` only when changed, then `AgentHost.RestartIdleAgents()`.
   - Add a one-line hint: "Owly uses the new level from your next question."
   - After "About Owly" returns `true`, the settings dialog calls a new `AISettingsPanel.ReloadLevelAndMemory()`. It re-reads the level drop-down and `MainMemoryBox` from storage, so a later Save doesn't undo what the popup saved.
7. **Localisation ids 440-449:**
   - the question
   - the three "Name: description" items
   - the popup hint
   - `Level:`
   - the settings hint
   - the three plain level names for the drop-down, if you need separate ones

   That is up to ten.
8. **Docs.**
   - **`docs/owly/prompt-checks.md`:** add rows 18-20, the same question "How do I loft a roof?" at Beginner, Intermediate and Advanced, with a visibly different depth for each.
   - **CLAUDE.md:** the "Students can't edit…" bullet says "Plan 029 adds the level; it updates this sentence." Rewrite it to list the level (Beginner, Intermediate, Advanced; chosen in the welcome popup, changeable in settings, a `LevelSteer` in the prompt, idle CLIs restarted on change) and drop that sentence. Also mention the welcome popup's `CurrentVersion` re-show rule in the first-start bullet list (`WelcomeVersion`).
   - **PRD §3.4:** add a short note that the level is built (plan 029).
   - **`GLOSSARY.md`** already has **Level**; no change.

### Extra tests

- `OwlyLevelTests` (or inside `AgentPromptsTests`): `Parse` of each name in any case, plus `null`, `""` and junk → `Beginner`.
- The `AgentPrompts` tests listed in decision 2.

### Done criteria (replace the plan's list)

From the repo root, in the worktree:
- `dotnet build rhino/plugin/RhinoAI.csproj -p:RhinoTarget=R9 -clp:NoSummary` and the same with `R8`: `Build succeeded`, 0 errors, 0 warnings.
- `dotnet test tests/Server.Tests/Server.Tests.csproj`: all pass, with more than 487 tests.
- `git diff 154e994 --stat -- rhino/panel art` is empty. If you need a panel change, STOP and report.
- `git grep -n "assume a" rhino/plugin/Agents/AgentPrompts.cs` returns nothing.
- `git grep -n "_Cancel" rhino/plugin` still shows only `UI/CommandOptionProbe.ai.cs`.
- `git grep -n "McpServerTool\]" rhino/plugin | wc -l` is the same as at `154e994`.
- The report includes the three `LevelSteer` texts with their lengths, and `Compose("", level).Length` for each level.

### Extra manual checks

- Run `docs/owly/prompt-checks.md` (CLAUDE.md requires it after any `AgentPrompts.cs` change), including the new rows 18-20.
- The popup fits on a 1366×768 screen at 100% scaling and on a 1920×1080 screen at 150%. If it doesn't, report back; don't shrink the owl.
- Changing the level in settings mid-conversation, then asking again: confirm whether `--resume` picked up the new prompt. This is the spike from "Facts you need". If it didn't, the settings hint is wrong; record it and plan a follow-up.
