# 009: Remove saved conversation history (keep only the live transcript)

- **Written against:** `17f9ef6` on `rhino-9.x`. Apply it after plans 006-008.
- **Status:** TODO
- **Effort:** M
- **Risk:** Medium. It deletes code on both sides of the panel protocol, and touches `StreamJsonAgent`, whose restart-after-Stop path must keep working.
- **Phase:** PRD Phase 3, bullet "Implement conversation history and message input"
- **Decision record:** [ADR-0006](../docs/adr/0006-no-saved-conversations.md). Read it first.

## Why

RhinoAI saved every conversation to Rhino's settings: `ConversationStore`, the newest 50 kept, no expiry. It also offered a searchable history drawer, a read-only review and a "Resume" button. The product owner decided the Owly prototype keeps **no saved conversations**, only the **transcript** of the current conversation (see `GLOSSARY.md`). The reasons are PRD §7 (minimal retention) and the fact that a tutor prototype doesn't need students to look up old chats.

These behaviours are fixed. Do not change them:

- A conversation lives in memory only. It ends when the student presses **New conversation**, closes the file (on Windows this includes File › Open and File › New), or quits Rhino. This is already how `AgentHost` works: `AgentHost.ai.cs:20-23` disposes a document's agents on `CloseDocument`.
- Closing or hiding the Owly panel does **not** end the conversation.
- **Stop** must keep working as it does now. Stop kills the Claude CLI, and the next prompt restarts it with `--resume <id>`, so Owly still remembers the conversation. This relies on Claude Code's own session file under `~/.claude/projects/`. That file stays (ADR-0006).
- When Owly loads, it deletes the `Conversations` settings node left by older builds, once and silently.
- New conversation gets no confirmation dialog. Its tooltip now says it clears the current conversation.

## Repo conventions you must follow

- A new C# file written by an AI agent is named `*.ai.cs`. Editing an existing file does not rename it. This plan creates no new C# file.
- **Localization IDs.** Each C# user string is `Localization.LocalizeString("text", <id>)`. Keep the ID when you reword a string. Deleting a string retires its ID: don't reuse it.
- **Panel strings live on both sides.** Each key in `rhino/plugin/UI/PanelStrings.ai.cs` has an English twin in `rhino/panel/src/i18n/strings.ts`, whose keys define the `StringKey` type. Change both together.
- **Protocol records live on both sides.** C# `UI/PanelCommands.cs` and `UI/PanelEvents.ai.cs` mirror `rhino/panel/src/protocol/events.ts`, and `rhino/panel/src/protocol/mockHost.ts` plus `rhino/panel/tests/host-events.json` copy the host's behaviour. CLAUDE.md says: "Remove both sides together or not at all."
- `tests/Server.Tests/Server.Tests.csproj` links plugin source files by explicit path. If you delete a linked file, remove its `<Compile Include>` line.
- After changing anything under `rhino/panel/src`, run `cd rhino/panel && npm install && npm run build` and commit the rebuilt `rhino/panel/dist/panel.host.html`.
- Don't rename the `Rhino.AI` namespace, `RhinoAI.csproj` or classes (ADR-0004).

## Scope

**Delete:**
- `rhino/plugin/Agents/ConversationStore.ai.cs`
- `rhino/plugin/Agents/ConversationDto.ai.cs`
- `rhino/panel/src/ui/history.ts`

**Edit (C#):**
- `rhino/plugin/Agents/Conversation.ai.cs`
- `rhino/plugin/Agents/AgentHost.ai.cs`
- `rhino/plugin/Agents/AgentRunner.ai.cs`
- `rhino/plugin/Agents/StreamJsonAgent.ai.cs`
- `rhino/plugin/AISettings.cs`
- `rhino/plugin/RhinoAIPlugin.cs`
- `rhino/plugin/UI/AIPanelViewModel.ai.cs`
- `rhino/plugin/UI/PanelCommands.cs`
- `rhino/plugin/UI/PanelEvents.ai.cs`
- `rhino/plugin/UI/ConversationFeed.ai.cs`
- `rhino/plugin/UI/PanelStrings.ai.cs`
- `tests/Server.Tests/Server.Tests.csproj`
- Comment-only: `rhino/plugin/Agents/TokenUsage.ai.cs`, `rhino/plugin/Agents/Attachment.ai.cs`

**Edit (panel):**
- `rhino/panel/src/protocol/events.ts`, `src/protocol/mockHost.ts`
- `src/state/store.ts`, `src/state/ui.ts`, `src/state/format.ts` (comment only)
- `src/ui/app.ts`, `src/ui/header.ts`, `src/ui/composer.ts`, `src/ui/transcript.ts`
- `src/main.ts`, `src/i18n/strings.ts`, `src/styles/panel.css`
- `tests/host-events.json`, `tools/verify.mjs`, `README.md`, `dist/panel.host.html` (rebuilt)

**Edit (docs):**
- `PRD.md`
- `docs/owly/phase1-investigation.md`

**Out of scope. Do not touch:**
- `HasEverStarted` in `StreamJsonAgent` and the `--resume` / `--session-id` switch in `ClaudeStreamJsonParser.ConfigureArguments`. The restart after Stop needs them.
- `Conversation.AgentSessionId` and `Conversation.AdoptSessionId`. They are still used by `StreamJsonAgent` and by `ConversationFeed` (the snapshot's session id).
- `AIPanelViewModel.PinnedAgentName`. It is not history-only: `SendAgents` sets it and `NewConversation` reads it.
- The `session` signal in `store.ts`. `transcript.ts:216` uses it to scroll to the newest message whenever a snapshot arrives.
- The `history.tokenCount` **string** is not history: the turn footer uses it. Rename it (step 3), don't delete it.
- `rhino/sdk/**`, `tests/sdk.tests/**`. Their `--resume` hits belong to the dormant SDK.
- `CommandInterceptor*.cs`. Its "command history" is Rhino's command line, not conversations.
- `rhino/panel/src/dev/frame.ts`, and the "review chrome" in `main.ts` and `README.md:15`. That is the dev-only resizable frame, not conversation review.

## Steps

### 1. Baseline

From the repo root, run the shared gates from `plans/README.md` (R9 build, R8 build, Server.Tests), plus the panel checks:

```sh
cd rhino/panel && npm install && npm run typecheck && npm test && npm run verify; cd ../..
```

If anything fails before you change code, STOP and report. `npm run verify` exits 0 with a "skipped" message when Chrome/Edge or puppeteer-core isn't found. Note whether it really ran.

### 2. C#: delete the store and the resume-from-history path

**2a. Delete** `rhino/plugin/Agents/ConversationStore.ai.cs` and `rhino/plugin/Agents/ConversationDto.ai.cs`.

**2b. `tests/Server.Tests/Server.Tests.csproj`.** Delete this line (line 41):
```xml
    <Compile Include="../../rhino/plugin/Agents/ConversationDto.ai.cs" LinkBase="PanelSources" />
```
Keep line 40, which links `Conversation.ai.cs`. That's why step 2c must remove `Restore`, or the test project stops compiling.

**2c. `rhino/plugin/Agents/Conversation.ai.cs`.** Delete the `Restore` method and the comment above it (lines 108-137):
```csharp
    // Rebuild a live conversation from a persisted transcript so a resumed session shows its prior
    ...
    public static Conversation Restore(ConversationDto dto)
    {
        ...
        return convo;
    }
```
Also fix the stale comments:
- Lines 139-142 above `AgentSessionId`. Replace them with:
  `// The CLI's own session id: passed as --session-id on the first spawn and as --resume when the CLI is restarted after Stop.`
- Line 94 mentions "any persistence"; line 225 mentions a stale `--resume`. Reword them so they no longer mention a saved transcript.

Leave `StartedAt { get; private init; }`, the `Turn` constructor's `startedAt` parameter and `Turn.Complete(at)` alone. They become less used but are harmless.

**2d. `rhino/plugin/Agents/AgentHost.ai.cs`.** Delete `TryResume` and `CreateResumed` with their comments (lines 75-118). They start with:
```csharp
    // Adopt a runner restored from a past conversation as the doc's active agent for its kind,
    ...
    public static bool TryResume(RhinoDoc doc, ConversationDto dto, out IAgentRunner agent)
```
and end with the closing brace of `CreateResumed`.

**2e. `rhino/plugin/Agents/AgentRunner.ai.cs`.** Lines 30-44 currently read:
```csharp
    public AgentRunner(AgentDefinition def, string docTitle, Func<IAcpClient, Conversation, string, IAcpAgent> connect)
        : this(def, new Conversation(Guid.NewGuid(), def.Name, docTitle), connect)
    {
    }

    // Resume path: drive a restored past Conversation (its prior turns are already shown) rather than
    // a fresh one. The connection factory is expected to seed the CLI's --resume id from the restored
    // conversation's AgentSessionId, so the next prompt continues the agent's prior context.
    public AgentRunner(AgentDefinition def, Conversation conversation, Func<IAcpClient, Conversation, string, IAcpAgent> connect)
    {
        Definition = def;
        Connect = connect;
        Conversation = conversation;
        Client = new RhinoAcpClient(Conversation);
    }
```
Merge them into a single constructor:
```csharp
    public AgentRunner(AgentDefinition def, string docTitle, Func<IAcpClient, Conversation, string, IAcpAgent> connect)
    {
        Definition = def;
        Connect = connect;
        Conversation = new Conversation(Guid.NewGuid(), def.Name, docTitle);
        Client = new RhinoAcpClient(Conversation);
    }
```
In `PromptAsync`'s `finally` (around line 66), delete the `PersistTurn();` line. Delete the whole `PersistTurn()` method (lines 156-166).

**2f. `rhino/plugin/Agents/StreamJsonAgent.ai.cs`.** Make these changes, and **keep `HasEverStarted`**.

1. Lines 72-92: the two constructors. Replace them with one 5-argument constructor:
   ```csharp
    public StreamJsonAgent(AgentDefinition def, IAcpClient client, Conversation conversation, string cwd, IStreamJsonParser parser)
    {
        Definition = def;
        Parser = parser;
        Client = client;
        Conversation = conversation;
        Cwd = cwd;
        // The conversation's id IS the CLI session id: --session-id on the first spawn, --resume on a respawn after Stop.
        AgentSessionId = conversation.AgentSessionId;
    }
   ```
   `AgentDefinition.cs:49` already calls the 5-argument form, so its call site doesn't change.
2. Delete the `ResumePending` property and its comment (lines 57-60).
3. Update the comments on `AgentSessionId` (44-48) and `HasEverStarted` (52-54). Remove the phrases about "a resumed past conversation" and "Pre-seeded true when resuming a past conversation". Keep the meaning: a stable id, so a respawn after cancel or crash continues the same CLI conversation, and `HasEverStarted` is sticky.
4. In `SessionCancelAsync` (around 133-147), delete the `ResumePending = false;` line and the comment just above it ("A clean cancel is proof…"). Keep the `lock (Gate) { turn = CurrentTurn; }` that remains.
5. In `ReadLoopAsync`'s `finally`:
   - Delete the `bool resumeRejected = false;` declaration.
   - Delete the whole `if (ResumePending && signedOut.Length == 0) { ... }` block and its long comment (around 330-352). It ends with `ConversationStore.Delete(stale.ToString());`.
   - Delete the two lines after the lock:
     ```csharp
            if (resumeRejected)
                Conversation.NoteSystem("could not resume the saved session (it may have expired); started fresh");
     ```
   - Change the comment at the top of the `finally` ("A CLI that exits mid-turn has either lost the resumed SESSION or lost the LOGIN…") to say only that a CLI exiting mid-turn may have lost its login, so Owly asks before deciding.
   - Keep `signedOut` and everything else.
6. In `AdoptMintedSessionId` (around 450-467), delete `ResumePending = false;` and `ConversationStore.Delete(stale.ToString());`. With the `Delete` gone, `stale` is only assigned and never read. Remove the `Guid stale;` declaration and the `stale = AgentSessionId;` assignment, so the method is: parse, then under the lock return if equal and assign `AgentSessionId = parsed`, then `Conversation.AdoptSessionId(parsed)`. Reword its one-line comment to: `// Adopt the session id the CLI says it minted, so a later --resume names the right session.`
7. In `CompleteTurn` (around 481), delete `ResumePending = false; // a turn landed, so the --resume target was accepted`.

Verify: `git grep -n "ResumePending\|resumeSessionId\|resumeRejected" -- rhino/plugin` prints nothing.

### 3. C#: settings, plugin load, panel view model, protocol records, strings

**3a. `rhino/plugin/AISettings.cs`.** Lines 63-67 read:
```csharp
    // Per-session conversation history lives under its own child node (Part 6).
    public static PersistentSettings Conversations =>
        Settings.TryGetChild(nameof(Conversations), out PersistentSettings child)
            ? child
            : Settings.AddChild(nameof(Conversations));
```
Replace them with this. Reading the old property *created* the node, so it must not survive.
```csharp
    // Owly keeps no saved conversations (ADR-0006). Older builds stored them under this child node;
    // delete it once so nothing from those builds stays on the student's computer.
    public static void DeleteLegacyConversations()
    {
        const string legacyNode = "Conversations";
        if (Settings.TryGetChild(legacyNode, out _))
            Settings.DeleteChild(legacyNode);
    }
```
`PersistentSettings.DeleteChild(string)` exists in RhinoCommon 8 and 9 ("since 6.0"). If it doesn't compile, STOP and report.

**3b. `rhino/plugin/RhinoAIPlugin.cs`.** `AISettings.Settings` finds the plugin with `PlugIn.Find`, so it can't safely be used inside `OnLoad`. Run the cleanup on the first idle instead. In `OnLoad`, just before `return base.OnLoad(ref errorMessage);`, add:
```csharp
        RhinoApp.Idle += OnFirstIdle;
```
Add this method to the class:
```csharp
    // One-shot work that needs the plugin fully loaded (AISettings resolves it with PlugIn.Find).
    private void OnFirstIdle(object? sender, EventArgs e)
    {
        RhinoApp.Idle -= OnFirstIdle;
        try
        {
            AISettings.DeleteLegacyConversations();
        }
        catch (Exception ex)
        {
            RhinoApp.WriteLine($"[owly] could not clear old conversations: {ex.Message}");
        }
    }
```
Plan 011 extends this method with the welcome popup, so keep it small and named exactly `OnFirstIdle`. `EventArgs` and `Exception` resolve through `rhino/plugin/GlobalUsings.cs` (`global using System;`), so don't add a `using`.

**3c. `rhino/plugin/UI/PanelCommands.cs`.** Delete these attribute lines (9-11):
```csharp
[JsonDerivedType(typeof(LoadConversationCommand), "conversation.load")]
[JsonDerivedType(typeof(ResumeConversationCommand), "conversation.resume")]
[JsonDerivedType(typeof(ExitReviewCommand), "conversation.exitReview")]
```
and these records (lines 28-30):
```csharp
internal sealed record LoadConversationCommand(string SessionId) : PanelCommand;
internal sealed record ResumeConversationCommand(string SessionId) : PanelCommand;
internal sealed record ExitReviewCommand : PanelCommand;
```

**3d. `rhino/plugin/UI/PanelEvents.ai.cs`.**
- Delete `[JsonDerivedType(typeof(HistoryEvent), "history")]` (line 17).
- Delete `internal sealed record HistoryEvent(IReadOnlyList<PanelHistoryEntry> Entries) : PanelEvent;` (line 39).
- Delete the `PanelHistoryEntry` record and its two-line comment (lines 131-141).
- In `PanelConversation` (lines 143-149), delete the last parameter `bool ReadOnly` and the comma before it, so the record ends with `IReadOnlyList<PanelTurn> Turns);`.

**3e. `rhino/plugin/UI/ConversationFeed.ai.cs`.** Lines 60-77: change `public void Replay(bool readOnly = false)` to `public void Replay()`, and delete the `readOnly` argument passed to `new PanelConversation(...)`. Reword the comment to: `// Replay the whole conversation as if it were arriving live. Cheaper than a snapshot type, and it means the attach path and the streaming path are the same code.`

**3f. `rhino/plugin/UI/AIPanelViewModel.ai.cs`.**
- Delete the `Review` property (line 24).
- In the dispatch switch (lines 175-178), delete the three cases `LoadConversationCommand`, `ResumeConversationCommand` and `ExitReviewCommand`.
- `NewConversation()` (237-251): delete `Persist();` and `SendHistory();`. Keep the rest:
  ```csharp
    private bool NewConversation()
    {
        if (Document is { } doc)
        {
            AgentHost.Stop(doc);
            if (PinnedAgentName is { } pinned)
                AgentHost.SetActive(doc, pinned);
        }

        ShowLive();
        return true;
    }
  ```
- Delete `LoadConversation`, `ExitReview` and `ResumeConversation` (253-297).
- In `Login()` (around 323), delete the line `ExitReview();`.
- In `RunToolChip` (around 367), delete `Review is not null || ` from the condition.
- `OpenSettings()` (429-437): delete `SendHistory();`.
- `SendEnvironment()` (477-494): delete `SendHistory();`.
- Delete `SendHistory`, `Title` and `Resumable` (529-564).
- Delete `ShowCurrent()` (573-583) with its comment, and change both callers, `Attach()` (line 38) and `Ready()` (line 200), to call `ShowLive();`.
- `ShowLive()` (585-590): delete `Review = null;`.
- `PumpIfLive()` (618-622): change `if (View.Loaded && Review is null)` to `if (View.Loaded)`.
- Delete `Persist()` (624-628) and `IsTurnRunning` (630-631).
- Remove any `using` directive that becomes unused. Nothing else in the file should still mention `ConversationStore`, `ConversationDto` or `Review`.

**3g. `rhino/plugin/UI/PanelStrings.ai.cs`.**
- Delete these keys:
  - `header.history` (63)
  - `review.readOnlyAtTime`, `review.backToLive`, `review.resumeTitle`, `review.resume` (126-129)
  - `history.title`, `history.close`, `history.search`, `history.empty`, `history.noMatch`, `history.turnCountOne`, `history.turnCountMany` (130-136)
  - `cmd.historyHint` (156)
- Rename the key `history.tokenCount` to `turn.tokenCount`, keeping ID 137: `["turn.tokenCount"] = Localization.LocalizeString("{0} tok", 137),`.
- Change line 14 to `["header.newConversation"] = Localization.LocalizeString("New conversation (clears this one)  (Ctrl+Shift+N)", 64),`.

**3h. Comment-only.**
- `Agents/TokenUsage.ai.cs:9` ("Persisted inside TurnDto…"): reword it so it no longer mentions persistence or TurnDto.
- `Agents/Attachment.ai.cs:9` ("hangs off a persisted turn"): reword it the same way.

Verify the C# side builds before you touch the panel:
```sh
dotnet build rhino/plugin/RhinoAI.csproj -p:RhinoTarget=R9 -clp:NoSummary
dotnet test tests/Server.Tests/Server.Tests.csproj
```

### 4. Panel: protocol, state and UI

**4a. `src/protocol/events.ts`.**
- Line 6 comment: change "exists only for load / resume / reconnect" to "exists only for attach and reconnect".
- Delete the `HistoryEntry` interface (123-132).
- In `ConversationSnapshot`, delete the `readOnly` field and its doc comment (163-164).
- In `HostEvent`, delete `| { type: 'history'; entries: HistoryEntry[] }` (188).
- In `PanelCommand`, delete the `conversation.load`, `conversation.resume` and `conversation.exitReview` members (227-229).

**4b. `src/state/store.ts`.**
- Delete the `type HistoryEntry,` import (14).
- Delete `readOnly: boolean;` from `SessionView` (51).
- Delete the `history` signal (99).
- Delete the `readOnly` computed (125).
- Delete the `case 'history':` block (152-154).
- Delete `readOnly: event.snapshot.readOnly,` (161).
- **Keep** the `session` signal.

**4c. `src/state/ui.ts`.** Change `export type Overlay = 'none' | 'history' | 'agents' | 'context';` to `'none' | 'agents' | 'context'`. Delete `readonly historyQuery = signal('');`.

**4d. `src/ui/app.ts`.**
- Delete the `historyDrawer` import (12).
- Delete the whole `reviewBar` function (74-101).
- Remove `clockTime` from the `../state/format.js` import and keep `formatElapsed`.
- Replace
  ```ts
    when(
      () => store.readOnly(),
      () => reviewBar(ctx),
      () => composer(ctx),
    ),
  ```
  with `composer(ctx),`.
- Delete the `ui.overlay() === 'history'` `when(...)` block (182-185).

**4e. `src/ui/header.ts`.** Delete the history `el('button', …)` (lines 42-51, the one with `onClick: () => ui.openOverlay('history')`). The header's `.icon-btn` buttons are then New conversation (index 0) and settings (index 1).

**4f. `src/ui/composer.ts`.** Delete the `/history` entry from `COMMANDS` (line 22). Three commands remain: `/new`, `/stop`, `/settings`.

**4g. `src/main.ts`** line 36: change `if (store.readOnly.peek() || !store.hasReadyAgent.peek()) return;` to `if (!store.hasReadyAgent.peek()) return;`.

**4h. `src/ui/transcript.ts`.**
- Line 63: `t('history.tokenCount', …)` becomes `t('turn.tokenCount', …)`.
- The comment at 207-209 ("A snapshot replaces the whole transcript (resume, load, reconnect)…"): change "(resume, load, reconnect)" to "(a new conversation, a panel reload or a reconnect)".

**4i. `src/i18n/strings.ts`.** Make the same changes as 3g:
- delete `header.history`, the four `review.*` keys, the seven history keys other than `history.tokenCount`, and `cmd.historyHint`
- rename `history.tokenCount` to `turn.tokenCount` (`'{0} tok'`)
- set `'header.newConversation': 'New conversation (clears this one)  (Ctrl+Shift+N)'`

**4j. `src/state/format.ts:67`** comment: change "for a turn header or a history row" to "for a turn header".

**4k. Delete `src/ui/history.ts`.** In `src/ui/icons.ts`, delete the `history` and `search` icon entries if nothing else uses them; check with `grep -rn "icon('history'\|icon('search'\|'history'\|'search'" src`. Leave `agent` and `close`. Plan 010 deals with `agent`.

**4l. `src/protocol/mockHost.ts`.**
- Delete the `HistoryEntry` import (10).
- Delete the `HISTORY` constant (104-135).
- Delete the `readOnly: false` lines (150 and 861).
- Delete `reviewSnapshot` (304-337).
- Delete the `case 'conversation.load'`, `case 'conversation.resume'` and `case 'conversation.exitReview'` blocks (867-880).
- Delete the line that emits `{ type: 'history', … }` (1058).
- If anything now refers to a removed symbol, `npm run typecheck` will say so; fix it by deleting the reference, not by bringing the symbol back.

**4m. `src/styles/panel.css`.** Delete these rules:
- `.drawer` (1026-1034) and `@keyframes slide` (1035)
- `.drawer-head` and `.drawer-head b` (1037-1045)
- `.search` and `.search input` (1047-1065)
- `.drawer-list` (1067)
- `.convo` and its four sub-rules (1069-1083)
- `.review-bar` and `.review-bar .spacer` (1190-1200)

Before you delete each selector, confirm that nothing else in `src/` uses it: `grep -rn "'drawer\|\"drawer\|class: 'search'\|convo\|review-bar" src --include=*.ts`. Keep `.lifecycle`.

**4n. `tests/host-events.json`** line 5: delete `,"readOnly":false` from the snapshot object. Nothing generates this file, so edit it by hand.

**4o. `tools/verify.mjs`.**
- Line 263: `check('slash opens the command menu', slash >= 4, …)` becomes `slash >= 3`.
- Lines 290 and 404: `.header .icon-btn')][1]` (New conversation) becomes `[0]`.
- Lines 506 and 526: `.header .icon-btn')][2]` (settings) becomes `[1]`.
- Delete the whole history block, from the `// --- history` comment line (427) through `check('resuming a conversation starts at the newest message', resumed !== null);` (467). That includes the `07-review` screenshot.
- Line 505's comment ("Resume above already left review, so the composer is back.") no longer applies; delete it.
- Line 913: delete `, readOnly: false` from the fixture.
- The `// ---- resume, the way Rhino replays it` test (around 906-956) stays. It checks reload and reconnect landing at the bottom. Rename its heading comment and `check` text from "resume" to "replay" if they say resume.

**4p. `README.md` (panel).**
- Line 43: "exists only for load, resume and reconnect" becomes "exists only for attach and reconnect".
- Line 62: remove `history` from the `ui/` file list.
- Line 92: remove "history" from the `/` commands list. Also remove "agent", which plan 007 removed and the list never got updated for.
- Line 95: delete the "Searchable history" bullet.

**4q. Build the panel and run its checks:**
```sh
cd rhino/panel && npm install && npm run build && npm run typecheck && npm test && npm run verify; cd ../..
```
`npm run build` rewrites `dist/panel.host.html`; commit it.

### 5. Docs

**5a. `PRD.md`.**
- In §3.3 "Conversation Interface" (line 68), change the bullet `* Displays conversation history and AI responses.` to `* Displays the transcript of the current conversation and the AI's responses. Owly keeps no saved conversations (ADR-0006).`
- In §9 Phase 3 (line 190), change `* Implement conversation history and message input.` to `* Implement the conversation transcript and message input (no saved history, ADR-0006).`
- Don't change anything else. The `gdoc_comment` markers stay as they are.

**5b. `docs/owly/phase1-investigation.md`.**
- Line 63, the table row `` | `ConversationStore` (history) | **Reuse** | Suggested by the team in a PRD comment | ``: change it to `` | `ConversationStore` (history) | **Removed** | The product owner chose no saved conversations ([ADR-0006](../adr/0006-no-saved-conversations.md)) | ``.
- Line 90, the privacy row starting `| Conversations: prompts, answers, …`: change the Where and Concern cells to `` In memory only, discarded when the conversation ends (plan 009) `` and `` Resolved by ADR-0006. Old builds' saved conversations are deleted on load ``.
- Line 91, the "CLI transcripts" row: change the Concern cell to `Outside Owly's control; Claude Code deletes them after about 30 days by default. Must be disclosed (welcome popup, plan 011). Phase 5 option: --no-session-persistence (ADR-0006)`.

### 6. Full gates

Run the shared gates from `plans/README.md` and step 4q's panel checks again.

## Done criteria

From the repo root:
```sh
dotnet build rhino/plugin/RhinoAI.csproj -p:RhinoTarget=R9 -clp:NoSummary   # Build succeeded, 0 errors
dotnet build rhino/plugin/RhinoAI.csproj -p:RhinoTarget=R8 -clp:NoSummary   # Build succeeded, 0 errors
dotnet test tests/Server.Tests/Server.Tests.csproj                          # all pass (148 at 17f9ef6; none of them touched history)
(cd rhino/panel && npm run typecheck && npm test && npm run verify)         # all pass
git grep -n -E "ConversationStore|ConversationDto|TurnDto|TurnEventDto|HistoryEvent|PanelHistoryEntry|TryResume|CreateResumed|ResumePending|ExitReview|LoadConversation|ResumeConversation|SendHistory" -- rhino/plugin tests/Server.Tests   # nothing
git grep -n -E "conversation\.load|conversation\.resume|exitReview|historyDrawer|HistoryEntry|historyQuery|review-bar|reviewBar|'history'" -- rhino/panel/src rhino/panel/tests rhino/panel/tools   # nothing
git grep -n -E "readOnly" -- rhino/panel/src rhino/panel/tests rhino/panel/tools rhino/plugin/UI/ConversationFeed.ai.cs rhino/plugin/UI/PanelEvents.ai.cs rhino/plugin/UI/AIPanelViewModel.ai.cs   # nothing
git grep -n "history\." -- rhino/panel/src rhino/plugin/UI/PanelStrings.ai.cs   # nothing
git grep -n "HasEverStarted" -- rhino/plugin/Agents/StreamJsonAgent.ai.cs   # still present (several hits)
test ! -e rhino/plugin/Agents/ConversationStore.ai.cs && test ! -e rhino/plugin/Agents/ConversationDto.ai.cs && test ! -e rhino/panel/src/ui/history.ts && echo deleted   # prints "deleted"
git status --short rhino/panel/dist/panel.host.html   # shows M (rebuilt) before you commit
```

## Manual check (by the user, Rhino 9; repeat 1-4 in Rhino 8)

1. Open the Owly panel. The header has only **+** (New conversation) and the gear. Its tooltip reads "New conversation (clears this one)  (Ctrl+Shift+N)". Typing `/` lists `/new`, `/stop` and `/settings`, and no `/history`.
2. **Stop keeps memory.** Ask "How do I loft between curves?". While it answers, press Stop. Then ask "Can you repeat the first step?". Owly answers about lofting, so it still remembers the conversation.
3. **Panel close keeps the transcript.** Close the Owly panel with the `Owly` command, then run `Owly` again. The transcript is still there.
4. **New file ends it.** On Windows, run File › New (or open another .3dm). The Owly panel shows the empty "Owly is ready" state.
5. **Old data wiped.** On a machine that ran an older Owly or RhinoAI build: start Rhino once and quit. Open Owly's plugin settings XML under `%APPDATA%\McNeel\Rhinoceros\<version>\Plug-ins\` (the folder whose name contains `1f0762a7-df2f-4d11-8ff3-22ec1a40391c`). It has no `Conversations` child entry.

## Tests

- No new automated C# tests. `ConversationStore`, `AgentHost` and `AIPanelViewModel` aren't linked into Server.Tests, and no existing test referred to history.
- Panel: `tools/verify.mjs` loses its history block on purpose. The replay test that stays (around line 906) still covers "a snapshot lands at the newest message".

## Maintenance

- To bring saved history back, write an ADR that supersedes ADR-0006 first. Git history before this plan has the RhinoAI implementation.
- Phase 5 may switch the CLI to `--no-session-persistence` and feed the transcript back after a Stop (ADR-0006, Consequences). That change would touch `ClaudeStreamJsonParser.ConfigureArguments` and the respawn in `StreamJsonAgent.StartAsync`, both of which this plan leaves alone.
- Plan 011 adds the welcome popup to `RhinoAIPlugin.OnFirstIdle`. Keep that method.

## STOP and report if

- Removing `Restore` or the DTO leaves a compile error in a file not listed in Scope.
- `PersistentSettings.DeleteChild` doesn't exist or throws in the manual check.
- Manual check 2 shows Owly forgetting the conversation after Stop. That means `HasEverStarted` or the `--resume` path was damaged.
- `npm run verify` fails on a check other than the ones this plan edits or deletes.
- Something other than history turns out to read `store.readOnly` or `PanelConversation.ReadOnly`.
