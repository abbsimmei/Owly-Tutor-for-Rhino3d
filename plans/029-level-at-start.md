# 029: Choose a level at the start

- **Written against:** `40f2c38` on `rhino-9.x`.
- **Status:** TODO
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
