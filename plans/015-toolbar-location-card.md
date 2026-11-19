# 015: Toolbar location card: "where is this button?"

- **Written against:** `6571b16` on `rhino-9.x`. **Requires plan 014 to be DONE.** Re-read the files this plan quotes, because 014 changed them.
- **Status:** DONE (`980aa7f`, see `plans/README.md`)
- **Revised:** after a cold-read review. The `.rui` rules below take into account the structure of a real Rhino 9 `default.rui`: flyouts via `<link>`, tab sidebars via `<sidebar>`, `button_text` labels, and `platforms` attributes.
- **Effort:** M–L
- **Risk:** Medium. Real `.rui` contents vary; step 2 is a gate.
- **Phase:** PRD Phase 4, bullet "Implement tool highlighting" (PRD §3.2: "Highlight the relevant tool… Explain where the tool is located"). Decision record: [ADR-0007](../docs/adr/0007-panel-ui-actions-on-student-click.md).

## Why

The PRD asks Owly to highlight the toolbar button for a recommended command. In RhinoCommon 8.19 and 9.0 there is **no public API** to flash a toolbar button, get its screen position, or list a toolbar's buttons. `Rhino.UI.Toolbar` has only `Id`, `Name`, `BitmapSize` and `TabSize`. The product owner decided:

- Each command chip from plan 014 gets a **second small toolbar icon**.
- Clicking it asks Rhino where that command lives on the open toolbars.
- A text-only **location card** then appears **under that answer**. For each match it shows the toolbar group, the tab, the toolbar (with the flyout path), and the button's label. Small tags mark "right-click" and "hidden". With no match the card says it is on no open toolbar.
- There is no button image and no menu path. The card **never shows, hides or changes a toolbar**.
- The toolbar files are read as saved on disk. Unsaved toolbar edits may be missed, which is accepted.
- Cards belong to the conversation and are not kept (ADR-0006). They disappear on New conversation **and** whenever the host re-sends the transcript, which happens on panel reload or reconnect. The student can click again, and the product owner accepts this.
- An on-screen flash is a separate R9 spike (plan 016).

Glossary terms: **Tool highlight**, **Location card**, **Toolbar tool**.

## Facts you need

### RhinoCommon (R8 8.19 and R9 9.0)
- `RhinoApp.ToolbarFiles` (`ToolbarFileCollection`): `Count`, an indexer, enumerable (`foreach`), `FindByName`, `FindByPath`, `Open`.
- `Rhino.UI.ToolbarFile`: `Id`, `Path`, `Name`, `GroupCount`, `GetGroup(int)`, `GetGroup(string)`.
- `Rhino.UI.ToolbarGroup`: `Id`, `Name`, `Visible` (get/set; **read it only, never set it**), `IsDocked`.
- `Rhino.UI.Localization.CurrentLanguageId` is the UI language as an `int` LCID (1033 English, 1053 Swedish). It is already used at `rhino/plugin/UI/PanelStrings.ai.cs:119`.
- Access `RhinoApp.ToolbarFiles` only on the UI thread. Panel commands arrive on it.

### `.rui` structure
`rhino/plugin/Owly.rui` is the minimal example (icon data omitted):
```xml
<RhinoUI ...>
  <tool_bar_groups>
    <tool_bar_group guid="17aac2ea-...">                 <!-- a group: one docked/floating container -->
      <text><locale_1033>Owly</locale_1033></text>
      <tool_bar_group_item guid="0f422f56-...">          <!-- a tab inside the group -->
        <text><locale_1033>Owly</locale_1033></text>
        <tool_bar_id>771e320b-...</tool_bar_id>           <!-- the tab's toolbar -->
      </tool_bar_group_item>
    </tool_bar_group>
  </tool_bar_groups>
  <tool_bars>
    <tool_bar guid="771e320b-...">
      <text><locale_1033>Owly</locale_1033></text>
      <tool_bar_item guid="5e8f9308-..." button_style="normal">
        <text><locale_1033>Owly</locale_1033></text>
        <left_macro_id>71e6904b-...</left_macro_id>
        <right_macro_id>ec2df67f-...</right_macro_id>
      </tool_bar_item>
    </tool_bar>
  </tool_bars>
  <macros>
    <macro_item guid="71e6904b-...">
      <text><locale_1033>Owly</locale_1033></text>
      <tooltip><locale_1033>Owly – your Rhino tutor</locale_1033></tooltip>
      <button_text><locale_1033>Owly</locale_1033></button_text>
      <script>! _Owly</script>
    </macro_item>
    <macro_item guid="ec2df67f-...">
      <text><locale_1033>Owly welcome</locale_1033></text>
      <tooltip><locale_1033>Owly welcome</locale_1033></tooltip>
      <script>! _OwlyWelcome</script>
    </macro_item>
  </macros>
</RhinoUI>
```
A review of a real Rhino 9 `default.rui` (`%APPDATA%\McNeel\Rhinoceros\9.0\UI\default.rui`, about 4.7 MB) found, by structure only:
- **About 125 `tool_bar`s, but only about 22 `tool_bar_group_item`s.** Most toolbars are reached in other ways:
  - **Flyouts.** A `tool_bar_item` contains `<link style="normal">{child toolbar guid}</link>`. There are about 190 of these. The child toolbar opens from that button.
  - **Tab sidebars.** A `tool_bar_group_item` can contain `<sidebar guid="…">{toolbar guid}</sidebar>`. There are about 19. This is the vertical toolbar shown beside a tab.
- Labels: only about 253 of about 2277 `macro_item`s have `<tooltip>`, while about 1773 have `<button_text>`.
- Some elements carry `platforms="Win"` or `platforms="Mac"` (about 58 and 1, mostly menus, one `tool_bar_item`).
- Rhino 8 was **not installed** on the review machine. The R8 structure is unverified.

### Panel, after plan 014
- `rhino/panel/src/core/markdown.ts` `commandChip()` renders `<span class="md-cmd"><button class="md-cmd-copy">…</button></span>` from `CommandChipOptions { copy, glyph, label }`. Read the current file.
- `rhino/panel/src/ui/message.ts`: `agentMessage(ctx, text, streaming)`.
- `rhino/panel/src/ui/transcript.ts`:
  - `block(ctx, turn, view)` calls `agentMessage(ctx, view.text, () => …)` (about line 28).
  - `turnView(ctx, turn)` (about lines 78-111) builds an `article.turn` **once per turn**, with an `each(...)` over the blocks, the error and cancelled lines, and `turnFooter`.
  - Anything that changes later must be reactive: `each(...)`, `when(...)` or a function-valued prop, as in the existing code.
- `rhino/panel/src/state/store.ts`:
  - `case 'conversation':` (about line 148) replaces `turns` and clears `questions`.
  - `this.turn(id)` finds a turn.
  - `HostEvent`s reach `store.apply` from `main.ts:77`.

### C# protocol
Host events live in `rhino/plugin/UI/PanelEvents.ai.cs`, for example:
```csharp
[JsonDerivedType(typeof(NoticeEvent), "notice")]
...
internal sealed record NoticeEvent(string Level, string Text) : PanelEvent;
```
They are serialised camelCase with nulls omitted (`rhino/plugin/UI/PanelJson.cs`). `rhino/panel/tests/host-events.json` is **maintained by hand**. The `verify.mjs:584` comment claims "PanelContractTests" writes it, but no such test exists, so this plan adds a C# test that pins the new event's JSON.

### `verify.mjs` host-contract section (`rhino/panel/tools/verify.mjs`, about lines 583-936), in order
1. Load `dist/panel.host.html` with a fake `window.eto` that pushes sent commands into `window.__sent`.
2. Replay `tests/host-events.json`, staged. Rendered checks run at about lines 640-680.
3. Context-menu checks (about 776-811), zoom (about 814), question answer (about 822-835), attachments (about 838-881). These rely on transcript and question state.
4. "Replay the way Rhino replays it" (about 884-933). This posts an **empty `conversation` snapshot** and then re-feeds the turns.
5. `await host.close()` (about line 935).

A `conversation` snapshot clears questions and turns, so any new check that sends one must go in block 4, not earlier.

## Repo conventions you must follow
- New C# files are `*.ai.cs`. `rhino/plugin/GlobalUsings.cs` provides `System`, `System.Linq` and `System.Collections.Generic`, and `ImplicitUsings` is off, so add any other `using` explicitly.
- Rhino-free files used by tests are linked in `tests/Server.Tests/Server.Tests.csproj`, in the `ItemGroup` with `LinkBase="PanelSources"`, following `<Compile Include="../../rhino/plugin/UI/PanelEvents.ai.cs" LinkBase="PanelSources" />`.
- Panel strings go in both `rhino/panel/src/i18n/strings.ts` (`EN`) and `rhino/plugin/UI/PanelStrings.ai.cs`, with the next free localization IDs. Find the current highest with:
  ```sh
  grep -rhoE "LocalizeString\([^;]*, *[0-9]+\)" rhino/plugin --include=*.cs | grep -oE "[0-9]+\)$" | tr -d ')' | sort -n | tail -1
  ```
  It was 276 before plan 014, which used 277-279.
- After changing `rhino/panel/src`, rebuild and commit `rhino/panel/dist/panel.host.html`. Pure TS logic gets `node:test` tests (`rhino/panel/src/state/store.test.ts` is the pattern for store tests).
- **McNeel content stays out of the repo.** Record element names, counts and single-line `<script>` strings only. Never copy XML blocks, labels or icons from `default.rui` into a plan, test or fixture.

## Scope

**Add:**
- `rhino/plugin/UI/ToolbarIndex.ai.cs` (Rhino-free)
- `rhino/plugin/UI/ToolbarLocator.ai.cs` (Rhino side)
- `tests/Server.Tests/ToolbarIndexTests.ai.cs`
- `tests/Server.Tests/Fixtures/toolbars-fixture.rui` (hand-written)
- `rhino/panel/src/ui/locationCard.ts`

**Edit:**
- C#:
  - `rhino/plugin/UI/PanelCommands.ai.cs`
  - `rhino/plugin/UI/PanelEvents.ai.cs`
  - `rhino/plugin/UI/AIPanelViewModel.ai.cs`
  - `rhino/plugin/UI/PanelStrings.ai.cs`
  - `tests/Server.Tests/Server.Tests.csproj`
- Panel:
  - `rhino/panel/src/core/markdown.ts`
  - `rhino/panel/src/ui/message.ts`
  - `rhino/panel/src/ui/transcript.ts`
  - `rhino/panel/src/ui/context.ts`
  - `rhino/panel/src/main.ts`
  - `rhino/panel/src/ui/icons.ts`
  - `rhino/panel/src/state/store.ts`
  - `rhino/panel/src/state/store.test.ts`
  - `rhino/panel/src/protocol/events.ts`
  - `rhino/panel/src/protocol/mockHost.ts`
  - `rhino/panel/src/i18n/strings.ts`
  - `rhino/panel/src/styles/panel.css`
  - `rhino/panel/tests/host-events.json`
  - `rhino/panel/tools/verify.mjs`
  - `rhino/panel/dist/panel.host.html`
- Plans:
  - this plan file: step 2's record
  - `plans/README.md`: status and the manual check

**Out of scope, do not touch:**
- setting `ToolbarGroup.Visible`
- `ToolbarFile.Save`, `SaveAs` and `Close`
- McNeel's `.rui` files, which are read only and never copied
- any agent tool
- `AgentPrompts.cs` (plan 017)
- any on-screen flash (plan 016)
- `Owly.rui`
- `Rhino.UI.Internal.*`
- non-public reflection

## Steps

### 1. Baseline
Run the shared gates from `plans/README.md`. Then:
```sh
cd rhino/panel && npm install && npm run typecheck && npm test && npm run build && npm run verify
```
If anything fails, STOP. Note the Server.Tests count (148 plus plan 014's tests).

### 2. Gate: confirm the real `.rui` structure (no code yet)
An agent can do 2.1–2.3 with read-only file access. 2.4 needs Rhino running and is done by a person, or by an agent driving a Rhino debug session.

1. Open `%APPDATA%\McNeel\Rhinoceros\9.0\UI\default.rui` **read-only**. If Rhino 8 is installed, also open `%APPDATA%\McNeel\Rhinoceros\8.0\UI\default.rui`. If it isn't, record "R8 not available on this machine"; the R8 check then happens in the manual Rhino check.
2. Confirm that each element named in "`.rui` structure" above exists: `tool_bar_group/tool_bar_group_item/tool_bar_id`, `tool_bar_group_item/sidebar`, `tool_bar/tool_bar_item/left_macro_id|right_macro_id|link`, `macro_item/script|tooltip|button_text|text`, `locale_*`, `@platforms`.
3. Find the button or buttons whose script's first command is `Loft`. Record:
   - the `<script>` strings (one line each)
   - how each is reached: a tab's `tool_bar_id`, a `sidebar`, or a `link` from which parent
4. In a Rhino 9 debug session, with a throwaway command that is **not committed**, print each `ToolbarFile`'s `Path` and, for each group, `Id` and `Name`. Record whether `ToolbarGroup.Id` equals the XML `tool_bar_group/@guid` for the same group.
   **If no Rhino session is available** (an agent executor), skip 2.4 and record "2.4 not run: needs Rhino". Step 5's Id-then-Name fallback covers both outcomes, and the manual Rhino check verifies "hidden".
5. Write the results under a new heading **"Recorded `.rui` structure"** at the end of this plan, following the McNeel-content rule.
6. **STOP and report** if:
   - the loaded toolbars don't come from readable `.rui` XML files (an empty `Path`, a binary file)
   - macros aren't in `macros/macro_item`
   - Loft is reached in a way none of the rules in step 3 cover

### 3. `rhino/plugin/UI/ToolbarIndex.ai.cs` (new, Rhino-free)
```csharp
using System.IO;
using System.Xml;
using System.Xml.Linq;

namespace Rhino.AI.UI;

// One place a command appears on a toolbar (plan 015). All text is already localised.
// Path is the toolbar, or "Parent › Flyout" when the button sits on a flyout. Tab is "" when there isn't one.
internal sealed record ToolbarPlace(string Group, string Tab, string Path, string Button, string Click, Guid GroupId);

// A parsed .rui file. Parse once (it can be several MB), then Find per command.
// Pure and Rhino-free so it is unit-tested; ToolbarLocator feeds it Rhino's loaded files.
internal sealed class ToolbarIndex
{
    public static ToolbarIndex Parse(string ruiXml, int languageId, string platform) { … } // platform: "Win" or "Mac"
    public IReadOnlyList<ToolbarPlace> Find(string command) { … }
    internal static string? FirstCommand(string script) { … }
}
```
**Parsing:**
- Use `XDocument.Load(XmlReader.Create(new StringReader(ruiXml), new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null }))`.
- On `XmlException`, return an empty index. Never throw.

**Platform:**
- Skip any `tool_bar_item` or `macro_item` that has a `platforms` attribute not containing `platform` (ordinal, case-insensitive substring).
- An element without the attribute is kept.

**Text** (`Localised(element, childName)`):
- Use the child's `locale_{languageId}`, else `locale_1033`, else the first `locale_*`, else `""`.
- Trim the result.

**Command key:**
- Strip the leading `_` and `-` from `command`, and compare case-insensitively (`StringComparison.OrdinalIgnoreCase`).

**`FirstCommand(script)`:**
1. Trim.
2. Repeatedly strip a leading `!`, `'` or whitespace.
3. Take the token up to the first whitespace or `;`.
4. Strip that token's leading `_` and `-`.
5. Return `null` if nothing is left.

**Button label:**
- The macro's `tooltip`, else its `button_text`, else its `text`, else the `tool_bar_item`'s `text`.

**Where a toolbar is shown:**
1. Build a map: toolbar guid → list of **entries**. Each entry is either `(Group, Tab, GroupId)`, or a flyout parent `(parent toolbar guid, parent item)`.
2. Fill it from:
   - each `tool_bar_group` → `tool_bar_group_item` → `tool_bar_id`, giving `(group text, item text, group guid)`
   - the same item's `sidebar` value, giving `(group text, item text + " sidebar", group guid)`. The word "sidebar" is the localized string `location.sidebar`; pass it in as a `Parse` argument `string sidebarWord`.
   - each `tool_bar_item` → `link`, giving the parent toolbar and item
3. To resolve a toolbar to its places:
   - Walk up through flyout parents **at most 3 levels**, building `Path` as `"{parent toolbar text} › {child toolbar text}"`.
   - Stop when a toolbar has a group entry.
   - Produce one place per group entry reached.
   - If none is reached, produce one place with `Group = ""`, `Tab = ""`, `GroupId = Guid.Empty` and `Path` = the toolbar text.
   - Guard against link cycles with a visited set.

**`Find(command)`:**
1. For each `tool_bar` → `tool_bar_item` (after platform filtering), look up its `left_macro_id` and `right_macro_id` macros.
2. If a macro's `FirstCommand` matches, emit the places of that toolbar with `Click = "left"` or `"right"`.
3. Remove duplicates, comparing all fields except `GroupId`.
4. Order by: left before right, places with a tab before places without, then `Path`.
5. Return **at most 5** places.

### 4. Tests: `tests/Server.Tests/ToolbarIndexTests.ai.cs`
**csproj:**
- Add `<Compile Include="../../rhino/plugin/UI/ToolbarIndex.ai.cs" LinkBase="PanelSources" />` to the PanelSources `ItemGroup`.
- In a new `ItemGroup`, add `<None Include="../../rhino/plugin/Owly.rui" Link="Fixtures/Owly.rui" CopyToOutputDirectory="PreserveNewest" />` and `<None Include="Fixtures/toolbars-fixture.rui" CopyToOutputDirectory="PreserveNewest" />`.
- Load the fixtures with `File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory, "Fixtures", name))`.

**Write `Fixtures/toolbars-fixture.rui` by hand, using made-up GUIDs and names. It contains:**
- Group **"Main"**, guid G1, with:
  - tab item **"Surface"** → toolbar **"Surface Tools"** (T1)
  - a `sidebar` → toolbar **"Side"** (T4)
- Toolbar T1 "Surface Tools" with:
  - item I1: left macro M1 `! _Loft`. M1 has tooltip `locale_1033` "Loft" and `locale_1053` "Loft (sv)". Right macro M2 is `_-Loft` with **no tooltip** and `button_text` `locale_1033` "Loft scripted".
  - item I2: `<link>` → toolbar **"More Surfaces"** (T2), with left macro M3 `_SelNone _Loft`.
- Toolbar T2 "More Surfaces": item I3 with left macro M4 `'_Sweep2`.
- Toolbar T3 **"Loose"** (in no group, linked from nowhere): item I4 with left macro M5 `_Sweep2`.
- Toolbar T4 "Side": item I5 with left macro M6 `_Loft`, and `platforms="Mac"` on the **`tool_bar_item`**.
- A link cycle: give T3's item I4 a `<link>` to T3 itself.

**Cases, with exact expectations (use `languageId` 1033, `platform` "Win" and `sidebarWord` "sidebar" unless stated):**

| # | Input | Expected |
|---|---|---|
| 1 | Owly.rui, `_Owly` | exactly 1: `Group "Owly"`, `Tab "Owly"`, `Path "Owly"`, `Button "Owly – your Rhino tutor"`, `Click "left"` |
| 2 | Owly.rui, `_OwlyWelcome` | exactly 1, `Click "right"`, `Button "Owly welcome"` |
| 3 | Owly.rui, `_Loft` | empty |
| 4 | fixture, `_Loft` | exactly 2, in order: `("Main","Surface","Surface Tools","Loft","left")`, then `("Main","Surface","Surface Tools","Loft scripted","right")`. Not the T4 Mac-only item, not M3 |
| 5 | fixture, `_Loft`, platform "Mac" | 3 places; the extra one is `("Main","Surface sidebar","Side","Loft","left")` |
| 6 | fixture, `_Loft`, languageId 1053 | the first place's `Button` is `"Loft (sv)"` |
| 7 | fixture, `_Loft`, languageId 9999 | the first place's `Button` is `"Loft"` (falls back to 1033) |
| 8 | fixture, `_SelNone` | exactly 1 with `Path "Surface Tools"` (M3 sits on T1's item I2) |
| 9 | fixture, `_Sweep2` | exactly 2, in order: `("Main","Surface","Surface Tools › More Surfaces", …)` (flyout path via I2's link), then `("","","Loose", …)` with `GroupId == Guid.Empty`. Finishes despite the cycle |
| 10 | `"<not xml"` | empty |
| 11 | `FirstCommand` | `"! _Loft"` → `"Loft"`; `"'_-Loft"` → `"Loft"`; `"_Loft _Pause"` → `"Loft"`; `"  !  _Loft;"` → `"Loft"`; `""` → `null`; `"!"` → `null` |

Also add a protocol test in the same file:
- `PanelJson.Serialize(new ToolbarFoundEvent("_Loft", "turn-0", new[] { new PanelToolbarPlace("Main", "Surface", "Surface Tools", "Loft", "left", false) }))` equals **exactly** the line added to `host-events.json` in step 7.
- `PanelJson.Deserialize("""{"type":"toolbar.find","name":"_Loft","turnId":"turn-0"}""")` is a `FindToolbarCommand` with those values.

Verify: `dotnet test tests/Server.Tests/Server.Tests.csproj`. All pass.

### 5. `rhino/plugin/UI/ToolbarLocator.ai.cs` (new, Rhino side)
```csharp
using System.Diagnostics;
using System.IO;
using Rhino.UI;

namespace Rhino.AI.UI;

// Plan 015 / ADR-0007: where does a command live on the open toolbars? Reads the loaded .rui files as saved on
// disk (read-only, shared). Never shows, hides or changes a toolbar.
internal static class ToolbarLocator
{
    public static IReadOnlyList<PanelToolbarPlace> Find(string command) { … }
}
```
**Cache:**
- Keep a `Dictionary<string, (DateTime Stamp, int Language, ToolbarIndex Index)>` keyed by path.
- Re-parse only when `File.GetLastWriteTimeUtc(path)` or `Localization.CurrentLanguageId` changed.

**Reading:**
1. For each `ToolbarFile file in RhinoApp.ToolbarFiles`: skip it if `file.Path` is empty or the file doesn't exist.
2. Read it with `new StreamReader(new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))`, because Rhino may hold the file open.
3. Skip the file on `IOException` or `UnauthorizedAccessException`.

**Parsing:**
- Call `ToolbarIndex.Parse(xml, Localization.CurrentLanguageId, OperatingSystem.IsMacOS() ? "Mac" : "Win", sidebarWord)`.
- `sidebarWord` is the localized `location.sidebar` string from `PanelStrings`; add a small accessor if needed.

**Hidden:**
- For each place with a non-empty `GroupId`, loop `file.GetGroup(i)` for `i < file.GroupCount`, matching `group.Id == place.GroupId`. **If no group matches by Id**, loop again matching `group.Name == place.Group` (ordinal). Always do both passes, whatever step 2.4 found.
- `Hidden = group is { Visible: false }`. Read only.

**Result:**
- Map each place to `PanelToolbarPlace(Group, Tab, Path, Button, Click, Hidden)`.
- Remove duplicates across files on `(Group, Tab, Path, Button, Click)`.
- Return at most 5.

**Timing:**
- Wrap the whole lookup in a `Stopwatch`.
- Log `HostUtils.LogDebugEvent($"[owly] toolbar lookup took {ms} ms ({files} files, {parsed} parsed)\n")`.
- Wrap everything in try/catch: log and return what was found so far.

### 6. Protocol
- C# `PanelCommands.ai.cs`:
  ```csharp
  [JsonDerivedType(typeof(FindToolbarCommand), "toolbar.find")]
  ```
  plus
  ```csharp
  // Plan 015 / ADR-0007: where is this command on the toolbars? Read-only.
  internal sealed record FindToolbarCommand(string Name, string TurnId) : PanelCommand;
  ```
- C# `PanelEvents.ai.cs`:
  ```csharp
  [JsonDerivedType(typeof(ToolbarFoundEvent), "toolbar.found")]
  ```
  plus
  ```csharp
  internal sealed record ToolbarFoundEvent(string Name, string TurnId, IReadOnlyList<PanelToolbarPlace> Places) : PanelEvent;
  internal sealed record PanelToolbarPlace(string Group, string Tab, string Path, string Button, string Click, bool Hidden);
  ```
- C# `AIPanelViewModel.ai.cs`:
  - Add the arm `FindToolbarCommand find => FindToolbar(find.Name, find.TurnId),` after plan 014's arm `CopyCommandCommand chip => CopyCommand(chip.Name),`. Don't name a pattern variable `command`, because `IncomingCommand`'s parameter already has that name (CS0136).
  - Add the handler:
    ```csharp
    // Plan 015 / ADR-0007: the student asked where a command is on the toolbars. Read-only lookup; plan 016 may add a flash here.
    private bool FindToolbar(string name, string turnId)
    {
        if (!CommandName.IsValid(name)) return false;
        Bridge.Post(new ToolbarFoundEvent(name, turnId, ToolbarLocator.Find(name)));
        return true;
    }
    ```
- TS `events.ts`:
  ```ts
  export interface ToolbarPlace { group: string; tab: string; path: string; button: string; click: 'left' | 'right'; hidden: boolean }
  // PanelCommand:
  | { type: 'toolbar.find'; name: string; turnId: string }
  // HostEvent:
  | { type: 'toolbar.found'; name: string; turnId: string; places: ToolbarPlace[] }
  ```

### 7. Panel
- **`icons.ts`:** add `toolbar: [{ rect: [3, 7, 18, 10, 2] }, { line: [9, 7, 9, 17] }, { line: [15, 7, 15, 17] }],` to `ICONS`.
- **`core/markdown.ts`:**
  - Widen `CommandChipOptions.glyph` to `'copy' | 'check' | 'toolbar'`.
  - Add `locate?: (name: string) => void;` and `locateLabel?: (name: string) => string;`.
  - In `commandChip`, when `command.locate` is set, append to the `.md-cmd` span a second `button.md-cmd-where`. It has `type = 'button'`, `title` and `aria-label` from `locateLabel(name)`, and contains `glyph('toolbar')`. On click it calls `command.locate(name)`.
- **`ui/message.ts`:**
  - The signature becomes `agentMessage(ctx, turnId: string, text, streaming)`, with `turnId` as the **second** parameter.
  - Add `locate: (name) => ctx.findToolbar(name, turnId)` and `locateLabel: (name) => t('command.whereNamed', name)` to `command`.
  - In `transcript.ts` `block()`, pass `turn.id`.
- **`ui/context.ts` + `main.ts`:** add `findToolbar(name: string, turnId: string): void`, which sends `{ type: 'toolbar.find', name, turnId }`.
- **`state/store.ts`:**
  - Add:
    ```ts
    export interface LocationEntry { name: string; places: readonly ToolbarPlace[] }
    readonly locations = signal<ReadonlyMap<string, readonly LocationEntry[]>>(new Map());
    ```
  - `case 'toolbar.found':`
    1. If `!this.turn(event.turnId)`, return, because the turn is gone (a race with New conversation).
    2. Copy the map. For that turnId, replace the entry with the same `name` or append one, so the latest wins and there is one entry per name.
    3. Set the map.
  - `dismissLocation(turnId: string, name: string): void` removes that entry, and removes the key when its list is empty.
  - In `case 'conversation':`, also call `this.locations.set(new Map())`.
- **`state/store.test.ts`:** add tests for these four cases:
  - a `toolbar.found` for a known turn adds an entry
  - a second one with the same name replaces it
  - an unknown `turnId` is ignored
  - `conversation` clears everything
- **`ui/locationCard.ts` (new):** `locationCards(ctx: PanelContext, turnId: string): Child` must be **reactive**:
  ```ts
  return each(
    () => ctx.store.locations().get(turnId) ?? [],
    (entry) => entry.name,
    (entry) => card(ctx, turnId, entry),
  );
  ```
  `card(...)` renders a `div.location-card` containing:
  1. A header row with `t('location.title', entry.name)`, plus a close `button` whose `title` and `aria-label` are `t('location.close')`, showing `icon('close', 12)`. Clicking it calls `ctx.store.dismissLocation(turnId, entry.name)`.
  2. If `places` is empty: one `p` with `t('location.none', entry.name)`.
  3. Otherwise, one `p.location-place` per place:
     - Main text: `t('location.place', button, path, tab, group)` when tab and group are non-empty; else `t('location.placeNoTab', button, path, group)` when only the group is non-empty; else `t('location.placeLoose', button, path)`.
     - Then separate `span.location-tag` elements: `t('location.right')` when `click === 'right'`, and `t('location.hidden')` when `hidden`.
     - Don't glue the tags into the sentence.
- **`ui/transcript.ts` `turnView()`:** insert `locationCards(ctx, turn.id),` right after the blocks' `each(...)` and before the error `when(...)`.
- **Strings:** add to EN and to `PanelStrings.ai.cs`, with the next free IDs:
  ```
  'command.whereNamed':  'Where is {0} on the toolbars?'
  'location.title':      'Where to find {0}'
  'location.none':       '{0} is not on any open toolbar. Copy it and paste it on the command line instead.'
  'location.place':      '{0} button on the {1} toolbar, {2} tab ({3})'
  'location.placeNoTab': '{0} button on the {1} toolbar ({2})'
  'location.placeLoose': '{0} button on the {1} toolbar'
  'location.sidebar':    'sidebar'
  'location.right':      'right-click'
  'location.hidden':     'hidden'
  'location.close':      'Close'
  ```
- **`panel.css`:**
  - `.md-cmd-where` gets the same reset as `.md-cmd-copy` (`display: inline-flex; padding: 0; border: 0; background: none; cursor: pointer; border-radius: var(--r-sm)`), `color: var(--text-dim)` and `margin-inline-start: 2px`. On hover it is `color: var(--accent)`; with `:focus-visible` it gets an outline in `var(--accent-ring)`.
  - `.location-card { margin: 0 0 .7em; padding: 5px 8px; border: 1px solid var(--border); border-radius: var(--r); background: var(--surface); font-size: var(--fs-xs); }`
  - Lay the header row out with flex, title first and close button at the end.
  - `.location-tag` is a small pill (`var(--surface-2)`, `var(--r-sm)`, padding `0 4px`, `margin-inline-start: 4px`).
  - No owl rules.
- **`mockHost.ts`:**
  - Add a `case 'toolbar.find':`. For `name === '_Loft'` it emits `{ type: 'toolbar.found', name, turnId, places: [{ group: 'Main', tab: 'Surface', path: 'Surface Tools', button: 'Loft', click: 'left', hidden: false }] }`; for any other name it emits `places: []`.
  - Use the mock's existing `this.emit(...)`.
- **`tests/host-events.json`:** add, as the **last line before the closing `]`**, exactly the string the step 4 protocol test asserts:
  ```json
  {"type":"toolbar.found","name":"_Loft","turnId":"turn-0","places":[{"group":"Main","tab":"Surface","path":"Surface Tools","button":"Loft","click":"left","hidden":false}]}
  ```
  Mind the commas. If the C# output differs (for example in property order), make the JSON line match what C# produces, never the reverse.

### 8. Browser checks (`verify.mjs`, host-contract section only)
Use the existing `check(name, condition, detail)` style.

1. **Card from the contract replay.** Place this right after the existing rendered checks (about line 680) and **before** the context-menu checks. After the staged replay has finished (both batches), check:
   - `document.querySelectorAll('.location-card').length === 1` (only turn-0 exists at this point)
   - its text includes `Loft` and `Surface Tools`
2. **The where button.** In the same place, after plan 014's own `_Loft` chip check (014 adds a `turn.text` event with `turnId: 'turn-0'`, `blockId: 'turn-0-cmd'`):
   - Click `.md-cmd-where`.
   - Check that `window.__sent.some((m) => m.type === 'toolbar.find' && m.name === '_Loft' && m.turnId === 'turn-0')`.
3. **The close button.** Click `.location-card button` (the close button). Check that `.location-card` count is 0. Then re-send the `toolbar.found` event with `window.rhinoAI.receive(...)` so the state is the same as before for later checks.
4. **Replay clears cards.** In the "replay the way Rhino replays it" block (about lines 884-933), directly after its empty `conversation` snapshot is received and **before** the turns are re-fed, check that `.location-card` count is 0.

There are no mock-section checks for this plan: the mock streams `_Loft` only inside a scripted scenario that verify cancels early.

Then run `npm run typecheck && npm test && npm run build && npm run verify`. All pass, with no console errors. Commit `dist/panel.host.html`.

### 9. Record the result
Fill in "Recorded `.rui` structure" (step 2). Update this plan's status line and the 015 row in `plans/README.md`.

## Done criteria
- The R9 and R8 builds succeed with 0 errors. `dotnet test tests/Server.Tests/Server.Tests.csproj` passes, with all `ToolbarIndexTests` cases (11 table rows plus 2 protocol tests).
- `cd rhino/panel && npm run typecheck && npm test && npm run verify` all pass.
- `grep -nE "Visible\s*=[^=]" rhino/plugin/UI/ToolbarLocator.ai.cs rhino/plugin/UI/AIPanelViewModel.ai.cs` prints nothing.
- `grep -rn "NonPublic\|Rhino.UI.Internal" rhino/plugin` prints nothing.
- `grep -rn "default.rui" tests/` prints nothing; tests never refer to McNeel's file.
- `grep -c "<tool_bar_item" tests/Server.Tests/Fixtures/toolbars-fixture.rui` prints 5. The fixture is hand-written and small.
- `git diff --stat` touches only files listed in Scope.
- "Recorded `.rui` structure" exists in this plan.

## Manual Rhino check (record the result in `plans/README.md`)
Run in Rhino 9 and Rhino 8. If R8 isn't installed, the product owner runs the R8 part.
1. Ask "How do I loft between curves?" and click the toolbar icon next to `_Loft`. The card names Rhino's real Loft button and its toolbar, tab and group, or flyout path.
2. Ask about `_OwlyWelcome`. The card names the Owly toolbar with a "right-click" tag.
3. Hide the Owly toolbar and ask again. The card shows "hidden", and the toolbar **stays hidden**.
4. A command with no toolbar button gives "not on any open toolbar".
5. Start a new conversation. The cards are gone.
6. Check the debug log for the `[owly] toolbar lookup took … ms` lines: one for the first click (which parses) and one for a second click (cached). Write both times in the README row.
7. If a second Rhino UI language is installed, switch to it. Labels appear in that language.

## Escape hatches
- If the **first** lookup takes more than about 1000 ms (step 6 of the manual check), STOP and report the numbers. The advisor decides whether parsing moves to a background task (`Task.Run`, posting through `RhinoApp.InvokeOnUiThread`) or is pre-warmed on first idle.
- If step 2 shows a structure the rules in step 3 can't express, STOP and report what you found. Use element names and counts only.
- If real Loft results include obviously wrong buttons, record the `<script>` strings and STOP.

## Maintenance note
Plan 016 may call a flash helper from `FindToolbar`; it may need toolbar and item GUIDs in `ToolbarPlace`, and must add tests for them. If Rhino ever adds a public API that lists toolbar items, replace the XML reading in `ToolbarLocator` and keep `ToolbarIndexTests` as the expected behaviour. `host-events.json` is maintained by hand. The step 4 protocol test is what keeps its `toolbar.found` line honest.

## Recorded `.rui` structure
Recorded by the executor on 2026-11-19 from Rhino 9's `%APPDATA%\McNeel\Rhinoceros\9.0\UI\default.rui` (read-only, about 4.7 MB). Labels are paraphrased.
- **R8:** not available on the executor machine (only `6.0` and `9.0` under `%APPDATA%\McNeel\Rhinoceros`). The R8 check happens in the manual Rhino check.
- **Structure confirmed:**
  - `tool_bar_group` 7; `tool_bar_group_item` 22, each with one `tool_bar_id`; `sidebar` 19
  - `tool_bar` 125; `tool_bar_item` 1845; `left_macro_id` 1781; `right_macro_id` 506; `link` 191
  - `macro_item` 2277, all under `macros`; `script` 2269; `tooltip` 253; `button_text` 1773
  - `locale_*` present (1033, 1031, 1036 and others)
  - Root children: `extend_rhino_menus`, `menus`, `tool_bar_groups`, `tool_bars`, `macros`, `icons`, `bitmaps`, `scripts`
- **`platforms`:** `macro_item="Win"` 50, `tool_bar="Win"` 4, `menu_item="Win"` 3, `menu="Win"` 1, `tool_bar_item="Mac"` 1.
  - **Not filtered:** `platforms` on `tool_bar` itself. This only matters on Mac, which is degrade-only; see the follow-up note in `plans/README.md`.
- **Loft:** exactly two buttons, both left-click, both with script `! _Loft`.
  1. On a flyout toolbar opened via `link` from a toolbar in the main group (1 level).
  2. On a tab sidebar toolbar, as the `tool_bar_id` of a group item.

  No right-click Loft. Neither carries a `platforms` attribute. Both are covered by the step 3 rules.
- **2.4:** not run, because it needs Rhino. The Id-then-Name fallback is implemented.
- **2.6 STOP conditions:** none hit.
