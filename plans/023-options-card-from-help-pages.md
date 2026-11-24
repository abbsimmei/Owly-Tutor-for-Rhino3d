# 023: Options card from McNeel's help pages, with Owly's suggested values

- **Written against:** `40f2c38` on `rhino-9.x`. **Requires plan 022 to be DONE** (it is: `40aef53`). Re-anchored at `8b1131a` by the advisor addendum below Steps.
- **Status:** TODO
- **Effort:** L
- **Risk:** Medium:
  - The help pages are loosely structured and incomplete, so the parser must fail soft.
  - Every value written into Rhino has to be validated by the host.
- **Phase:** Phase 5 rework, commands & tools (PRD "configurable command parameters"). Decision record: ADR-0008. Glossary: **Options card**, **Command option**.

## Why

Mock-up 1 lists Pyramid's options on the card (NumSides as a number field, Mode as two choice buttons, then Edge, Star, Direction constraint and Solid), each with a small icon on the right, above the Run button. The product owner decided:

1. **Option data.** It comes from the option probe if plan 024's test passes. **Otherwise, and in every case as a complement**, it comes from McNeel's help page for the command, fetched **by the host, not by the AI**. The AI keeps its five agent tools.
2. **Fields start blank**, meaning "keep Rhino's current value". Rhino remembers the last value of every option, and Owly can't read it before the command runs. **Run writes only the options the student changed.**
3. **Each row's icon** opens an **explanation popover** with the option's description from the help page, in the same style as plan 020.
4. **Owly may pre-fill values** for the student's goal. For example, "hexagonal pyramid roof" pre-fills NumSides = 6. The card keeps only option names and values that exist in the option data and marks them "suggested by Owly". The student can change them before Run.
5. **Blocked commands** show the options and explanations, but no Run (plan 022).

## Facts you need

- **The page URL** is `https://docs.mcneel.com/rhino/{RhinoApp.Version.Major}/help/en-us/commands/{lowercase name}.htm`. The R8 and R9 Pyramid pages and the R8 Loft page all exist.
  - `rhino/plugin/src/Resources/CommandHelpResource.cs` already fetches it (`Read(name)`, a static `HttpClient`, a 15 s timeout, `Command.IsCommand` first, a 404 means no page). It then strips everything to text, losing the structure.
  - Refactor: one fetch method returns the raw HTML, and the existing resource keeps its text output on top of it.
- **Page structure** (seen as markdown on 2026-11-24, so confirm against the raw HTML in step 1):
  - A plain "Command-line options" line, then one **h5 heading** per option or value, each followed by a paragraph.
  - Some pages have further groups ("Loft Options", "Cross-section curve options").
  - Values appear as their own h5 headings under the option (Pyramid's `DirectionConstraint` has `None`, `Vertical` and `AroundCurve`).
  - The Pyramid page lacks NumSides and Mode, which are on the linked Polygon page. **Do not follow links.** Options the page doesn't list are simply absent; plan 024's probe may add them.
- **Kinds of option:**
  - **number**: the page mentions typing a number, or the probe shows `=5`
  - **choice**: has value headings
  - **toggle**: values Yes/No or On/Off
  - **action**: no values, for example Edge or Star

  Unknown kinds are shown read-only with the explanation.
- **Copyright:** the help pages are © McNeel. Test fixtures are **hand-written HTML** that mimics the structure. Never copy a real page into the repo.
- **Macro syntax.** `_Name _Opt=Value` with space separators. In Rhino a space acts as Enter, so each token answers the first prompt in turn. Use `_` before option names and choice values so they work in any Rhino language. Numbers are written plainly. Confirm in Rhino in step 1 that `_Pyramid _DirectionConstraint=_Vertical` works, and what Rhino does with an option the first prompt doesn't have.
- **AI prefill format.** The AI writes `` `_Pyramid NumSides=6` `` as inline code. The chip shows `_Pyramid`, and the suggestions go to the card. Plan 019's "first mention" logic counts this as a mention of `_Pyramid`.

## Step 1 results, part 1: help pages (advisor, 2026-11-24)

The raw HTML was fetched to a scratch folder outside the repo: R9 Pyramid, Loft, Polygon, Box and Fillet, plus R8 Pyramid and Loft. These facts replace the guesses in "Page structure" above.
- **A missing page returns `403`** with an S3 `AccessDenied` XML body, **not `404`**. Treat any non-200 status as "no page", including for the R9 → R8 fallback. `CommandHelpResource` checks `Command.IsCommand` first, so a 403 means a real command without a page.
- The main content is inside `<div role="main" id="mc-main-content">`. The page is HTML, not well-formed XML (`&#160;`, `<br />` inside `<p>`, unclosed nesting), so don't parse it with `XDocument`. Use a small tolerant tag scanner.
- **Layout A** (Pyramid, Fillet, Box, Loft): `<p class="Dialog_Box_Title">Command-line options</p>`, then sibling `<h5>Option</h5><p>description</p>`.
  - An option's **values** are `<h5>`s inside a `<div class="Indent_Level_2">` that directly follows the option's description. For example, Pyramid's `DirectionConstraint` has `None`, `Vertical` and `AroundCurve`.
  - An `<h5>` at the section level is an option. An `<h5>` inside that `Indent_Level_2` is a value of the previous option.
  - The section ends at the next `Dialog_Box_Title`, an `<h2>` (usually "See also"), or the end of `mc-main-content`.
  - A page can have **several** `Dialog_Box_Title` sections. Only the ones whose text is exactly `Command-line options` count: Fillet has two and Loft has one. Loft's "Loft Options" (its dialog) and "Adjust seam options (Closed curves only)" must be ignored.
- **Layout B** (Polygon): a `<table>` whose header is `<th colspan="2">Command-line options</th>`. Each `<tr>` has `<td><p>Option</p></td><td>description …</td>`. Values can appear as `<h5>` inside the second cell (Polygon's `Mode` has `<h5>Inscribed</h5>` and so on).
- **`<h5>` text needs cleaning:**
  - It often holds an `<a name=…></a>` anchor or a "new in 9" `<img class="new_icon">` (e.g. `<h5><a name="3Point"></a>3Point</h5>`). Take the text content only, trimmed.
  - Skip headings that aren't identifiers: `(Default)`, or names with spaces such as Loft's "Straight sections". Option and value names must match `^[A-Za-z][A-Za-z0-9]*$`.
- **Wrappers** such as `<div data-mc-conditions="V9.Only">` and `<tr data-mc-conditions="V8.Win_Only">` are transparent. Don't filter on them: the published page already is the version's page.
- **`<h4>` inside the section** ("Steps", "To adjust seams", "Point steps") is tutorial text, not an option. Skip it and everything up to the next `<h5>` at the section level.
- **Kinds are rarely stated.** Pyramid lists `Solid` and `BothSides` and Polygon lists `NumSides` by name only. Nothing marks a toggle, a number or an action. Only these are reliable:
  - **choice**: the option has value headings
  - **toggle**: the values are exactly Yes/No or On/Off (Loft's `SnapToKnots`)

  **Product-owner decision (2026-11-24): read-only until the probe.**
  - Choices and Yes/No (On/Off) toggles are editable.
  - **Every other option is a read-only row**: its name and the explain icon, with no input. Run never writes it.
  - So plan 023 never writes a number. "number" and "action" kinds come only from plan 024's probe, if its test passes.
  - Owly's prefill applies only to choice and toggle options. Anything else is dropped. "NumSides=6" on Pyramid is dropped, because that option isn't even on Pyramid's page.
- Descriptions are the text of the `<p>` (or second cell) after the heading, with tags stripped and whitespace collapsed. Cap a description at 600 characters.

## Step 1 results, part 2: macro forms in Rhino 9 (product owner, 2026-11-24)

Each line was pasted on the command line (the same tokenising as `RunScript`), then cancelled with Esc.

| # | Macro | Result |
|---|---|---|
| 1 | `_Pyramid _DirectionConstraint=_Vertical` | Works: Pyramid starts with Vertical set and waits for the base point |
| 2 | `_Pyramid _Solid=_No` | Works: Solid becomes No, even when it was Yes before |
| 3 | `_Pyramid _DirectionConstraint=_Vertical _Solid=_Yes` | Works: both are set |
| 4 | `_Pyramid _BothSides=_Yes` | `Unknown command: _BothSides=_Yes`. BothSides isn't at the first prompt. Pyramid carries on |
| 5 | `_Pyramid _Mode=_Circumscribed` | Works |
| 6 | `_Pyramid _NotAnOption=_Yes` | `Unknown command: _NotAnOption=_Yes`. Pyramid carries on normally |
| 7 | `_Loft _SnapToKnots=_Yes` | `Unknown command: _SnapToKnots=_Yes`. Loft keeps asking for curves |
| 8 | `_Pyramid` afterwards | Remembers NumSides but not Mode. Not relevant: blank fields write nothing |

What follows from it:
- **`_Opt=_Value` sets an option at the first prompt**, for choices and toggles, and several can follow each other.
- **An option that isn't at the first prompt is harmless.** Rhino prints `Unknown command: …` and the command carries on. Every token Owly writes contains `=`, which is never a command name, so **no token can start a second command**.
- The help pages don't say which prompt an option belongs to. So the card shows a hint once a value is set (addendum item 9).
- Numbers were not tested, because plan 023 never writes one (product-owner decision above).

## Steps

1. **Check in Rhino (≤1 h).** DONE: see both "Step 1 results" sections above.
   - Save the raw HTML of 3-4 pages **locally, outside the repo**, to confirm the tags, classes and ids.
   - In Rhino 9, test the macro forms above and record the results in this plan's Facts section, including whether `=` works for numbers, choices and toggles, and what happens with an option that isn't at the first prompt.
2. **Parser.** Add `rhino/plugin/UI/CommandOptionsParser.ai.cs` (Rhino-free; link it in Server.Tests). It turns HTML into `CommandOptions { Name, Options: [{ Name, Kind, Values[], Description }] }`. It is defensive: no "Command-line options" section means an empty list, and malformed HTML means an empty list, never a throw. Add `CommandOptionsParserTests.ai.cs` with hand-written HTML fixtures.
3. **Source.** Add `rhino/plugin/UI/CommandOptionsSource.ai.cs`:
   - async fetch through the refactored `CommandHelpResource`, off the UI thread, only if `RhinoApp.IsInternetAccessAllowed`
   - if the page for the running major version is missing (a 404 on `/rhino/9/`), fall back to `/rhino/8/`
   - parse the page
   - cache per (command, Rhino major) for the session
   - a timeout or offline gives an empty result
4. **Macro builder.** Add `rhino/plugin/UI/RunMacro.ai.cs` (Rhino-free). `Build(name, IReadOnlyDictionary<string,string> changes, CommandOptions data)` keeps only option names that are in `data`. Each value must be:
   - a listed value, for a choice or toggle
   - a number matching `^-?\d+(\.\d+)?$`, for a number option

   Everything else is dropped. It never emits `!`, `;`, quotes, spaces inside a token or a second command. Add `RunMacroTests.ai.cs`, including injection attempts such as `NumSides=6 _Delete` and `Mode=_Inscribed;_Exit`.
5. **Protocol.**
   - Panel → host: `command.options {name}`, sent when a card is first rendered.
   - Host → panel: `command.optionsFound {name, options}`.
   - `command.run` gains `options: {name: value}`. The host rebuilds the macro with `RunMacro` from its **own** cached data, never from panel-supplied option lists.
   - Mirror the records in C# and TS, `mockHost.ts` and `tests/host-events.json`.
6. **Card UI** (`ui/commandCard.ts`):
   - Number rows get an input with the placeholder "Rhino's current value".
   - Choice rows get buttons (one selected at most, and a second click unselects).
   - Toggle rows get Yes/No buttons.
   - Action rows are labels only.
   - Each row ends with an explain icon that opens a popover with the description.
   - Prefilled values carry the "suggested by Owly" mark.
   - A "Looking up options…" state shows while the fetch runs, and nothing when there are no options.
7. **Prefill.** In `markdown.ts`, accept inline code `_Name Opt=Value …`: the chip is `_Name`, and the pairs go to the card's initial state. They are filtered against the option data when it arrives. Plain-text fallback: if parsing fails, render as plain code.
8. **Prompt.** Add to `CommandSteer`: "When a specific option value suits the student's goal, you may write it with the command, like `_Pyramid NumSides=6`, but only options you are certain exist; the panel drops anything else." Add prompt-checks rows 16 (hexagonal roof prefill) and 17 (no invented options). Update `AgentPromptsTests`.
9. **Privacy.** Sending command names to docs.mcneel.com is a new outbound call. Add a short clause to the welcome popup's privacy line (`UI/WelcomeDialog.ai.cs:~28`): "Owly looks up command help on docs.mcneel.com." Localisation IDs for this plan: 330-349.
10. **verify:**
   - the mock's `_Pyramid` options render the right row types
   - changing one field, then Run, sends only that option
   - an unknown prefill option is dropped
   - the explain popover opens

## Advisor addendum at `8b1131a` (read before starting; it overrides Steps 2-10 where they differ)

Plan 022 is merged. What it gives you:
- `rhino/plugin/UI/CommandBlocklist.ai.cs`: `Bare`, `IsBlocked`, `Classify` and `RunVerdict`.
- Two handlers in `UI/AIPanelViewModel.ai.cs`:
  - `CheckCommands`, which answers `command.check` with `command.checked {name, run}`
  - `RunCommand(name)`, which classifies the command, checks `InCommand`, focuses Rhino and posts `RunScript` with `AsyncInvoke`
- The panel's card, `rhino/panel/src/ui/commandCard.ts`, which renders from `ctx.store.runnable`.
- In `core/markdown.ts`: a `card?: (name) => Node | null` hook, per-render `options.cards`, and `drainCards`. Also `holdBackOpenBlock`, which keeps cards out of the block still being written.
- `ui/message.ts` re-renders on `text()`, `icons()`, `scheme()`, `runnable()` and `streaming()`.

The pattern for anything the panel asks per command (batch, dedupe, store map) is `requestCommandCheck` in `main.ts` with `store.markCheckAsked`. Copy it.

**Read both "Step 1 results" sections and the product-owner decision first.** The two that shape everything:
- Only **choice** and **toggle** options are editable.
- Every other option is a read-only **info** row. Run never writes it, and plan 023 never writes a number.

**C# side**
1. **Fetch.** Refactor `rhino/plugin/src/Resources/CommandHelpResource.cs`:
   - Add `internal static async Task<string?> FetchHtml(string bareName, int major)`. It returns the body on HTTP 200 and **null on any other status**, which covers the 403 that McNeel serves for missing pages.
   - Build the slug from `bareName.ToLowerInvariant()`. Callers must pass a name that already passed `CommandName.IsValid` and `Command.IsCommand`, so the URL only ever holds `[a-z0-9_]`.
   - `Read(name)` keeps its output. It uses `FetchHtml` and throws the same "No help page found" message when it gets null.
   - Don't change the MCP resource attributes.
2. **`UI/CommandOptionsParser.ai.cs`** (Rhino-free; link it in Server.Tests next to `CommandBlocklist.ai.cs`).
   - The model:
     ```csharp
     internal sealed record CommandOptionValue(string Name, string Description);
     internal sealed record CommandOption(string Name, string Kind, IReadOnlyList<CommandOptionValue> Values, string Description); // Kind: "choice" | "toggle" | "info"
     internal sealed record CommandOptions(IReadOnlyList<CommandOption> Options) { public static readonly CommandOptions Empty = new([]); }
     public static CommandOptions Parse(string? html)
     ```
   - Implement both layouts and every rule in "Step 1 results, part 1", with a small tolerant tag scanner (regex over tags plus a nesting depth counter). Don't use `XDocument` or an HTML library.
   - Kind:
     - `toggle` when the values are exactly {Yes, No} or {On, Off}, in any case
     - `choice` when there are ≥ 2 values
     - otherwise `info`; a single value is kept for the explanation but the kind stays `info`
   - Drop duplicate option names (keep the first), and cap the result at 30 options.
   - **Never throw:** catch everything and return `Empty`.
   - Tests in `CommandOptionsParserTests.ai.cs` use **hand-written** HTML that only mimics the structure, in a C# raw string per test. Invent text such as "Demo option description." and option names such as `Shape` and `Sides`. Cover:
     - layout A with a choice whose values sit in `Indent_Level_2`
     - a Yes/No toggle
     - an info option
     - an `<h5>` holding `<a name>` and `<img class="new_icon">`
     - `(Default)` and a name with spaces, both skipped
     - an `<h4>` tutorial block inside the section, skipped
     - a second `Dialog_Box_Title` with other text ("Demo Options"), ignored
     - two "Command-line options" sections, merged
     - layout B (table rows, `<h5>` values in the second cell)
     - a `data-mc-conditions` wrapper
     - no section at all, giving `Empty`
     - malformed or truncated HTML, giving no throw
     - the 600-character description cap
3. **`UI/CommandOptionsSource.ai.cs`** (Rhino-dependent).
   - `Get(string name)` returns `Task<CommandOptions>`. It works on `Bare(name)`, keyed `"{bare.ToLowerInvariant()}|{major}"`.
   - Return `Empty` without fetching unless `RhinoApp.IsInternetAccessAllowed` is true, `CommandName.IsValid(name)` holds and `Command.IsCommand(bare)` is true.
   - Fetch the running major version. If it returns null and the major is above 8, fetch major 8.
   - Keep a `ConcurrentDictionary<string, Task<CommandOptions>>`, so a concurrent request reuses the in-flight task. **Cache** a parsed result, including an empty one from a definite non-200. **Don't cache** a timeout or network exception: remove the entry, so a later card can retry.
   - Add `TryCached(string name, out CommandOptions options)` for `RunCommand`. It never fetches.
   - Wrap everything in try/catch and log with `[owly]`.
4. **`UI/RunMacro.ai.cs`** (Rhino-free; link it in Server.Tests). `public static string Build(string name, IReadOnlyDictionary<string, string>? changes, CommandOptions data)`:
   - It starts from `name`, which must pass `CommandName.IsValid`, or it throws `ArgumentException`.
   - It walks `data.Options` **in data order**. For each `choice`/`toggle` option whose name is a key in `changes` (match case-insensitively), take the change's value. Match it case-insensitively against the option's `Values`. If it matches, append ` _{Option.Name}={'_' + Value.Name}`, using the **canonical spellings from the data**, never the panel's.
   - Drop everything else: `info` options, unknown names, unknown values, and values that aren't `^[A-Za-z][A-Za-z0-9]*$`.
   - Tests in `RunMacroTests.ai.cs`:
     - an empty or null `changes` gives exactly `_Demo`
     - a choice gives `_Demo _Shape=_Round`, with the canonical case
     - a toggle works
     - two options come out in data order
     - an info option is dropped
     - injection attempts are all dropped: values `Round _Delete`, `Round;_Exit`, `"Round"`, `!` and `Round\n_Exit`, and keys `Shape _Delete` and `-Shape`
     - the result never contains `!`, `;`, `"`, a newline, or a token without `=` after the name
5. **Protocol.**
   - Panel → host: `command.options {names: string[]}`, record `CommandOptionsCommand(IReadOnlyList<string> Names)`.
   - Host → panel: **`command.options.found`** `{name, options: [{name, kind, values: [{name, description}], description}]}`. Use dotted lowercase, like `toolbar.icon.found`.
     - Records: `CommandOptionsFoundEvent(string Name, IReadOnlyList<PanelCommandOption> Options)`, with `PanelCommandOption(string Name, string Kind, IReadOnlyList<PanelCommandOptionValue> Values, string Description)` and `PanelCommandOptionValue(string Name, string Description)`.
   - Extend `command.run`: `RunCommandCommand(string Name, IReadOnlyDictionary<string, string>? Options = null)`.
   - The handler is `FindCommandOptions(names)`: at most 20 distinct names, each passing `CommandName.IsValid`. For each one, start `CommandOptionsSource.Get(name)` and post the answer **on the UI thread**: `Bridge.Post` calls WebView's `ExecuteScriptAsync`, so wrap it in `Application.Instance.AsyncInvoke`. Post exactly **one** answer per valid name, even when it is empty or the fetch fails, so the panel never waits forever.
   - `RunCommand` changes only where it builds the script. `CommandOptionsSource.TryCached(name, out data)` gives `RunMacro.Build(name, run.Options, data)`. Without cached data, it is `RunMacro.Build(name, null, CommandOptions.Empty)`, which is `name` alone. Log the macro it runs. Every other check stays as it is.
   - Pin tests (in `CommandOptionsParserTests.ai.cs`):
     - the exact JSON of one `command.options.found` line with a synthetic option (`Shape`, choice, values `Round` and `Square`), the same way plan 022 pinned `command.checked`. Add the same line to `rhino/panel/tests/host-events.json` after the `command.checked` line, named `_Pyramid` so verify can use it.
     - deserialisation of `command.options` and of `command.run` **with** and **without** `options`.

**Panel side**
6. **Prefill syntax** (`core/commandName.ts`).
   - Add `parseCommandCode(code): { name: string; pairs: Record<string, string> } | null`. It matches `^(_-?[A-Za-z][A-Za-z0-9_]*)((?: +_?[A-Za-z][A-Za-z0-9]*=_?[A-Za-z][A-Za-z0-9]*)*)$`.
     - The name must pass `isCommandName`.
     - In each pair, strip one leading `_` from the key and from the value.
     - A later duplicate key wins.
     - Anything that doesn't match returns null.
   - `commandsIn` in `commandMentions.ts` and the chip branch in `markdown.ts` `inline()` use it, so `` `_Pyramid DirectionConstraint=Vertical` `` is a chip `_Pyramid` (and a mention of `_Pyramid` for plan 019's first-mention rule). A code span that only matches the old plain form behaves exactly as before.
   - The chip shows and copies **only** the name.
   - Unit tests go in `commandName.test.ts` and `commandMentions.test.ts`.
7. **Card hook.** `options.cards` carries `{ name, pairs }`, and the hook becomes `card?: (name: string, suggested: Readonly<Record<string, string>>) => Node | null`.
8. **Store** (`src/state/store.ts`).
   - `commandOptions = signal<ReadonlyMap<string, readonly CommandOptionView[] >>(new Map())`, keyed by chip name, with a `markOptionsAsked` that copies `markCheckAsked`.
     - A `command.options.found` always sets the entry, and it survives a `conversation` snapshot (like `runnable`).
     - A name asked but not answered yet means "looking up".
   - Card state per **turn id + chip name** (key `` `${turnId}|${name}` ``): `cardValues = signal<ReadonlyMap<string, ReadonlyMap<string, { value: string; suggested: boolean }>>>`.
     - `seedCard(key, pairs)` writes the pairs with `suggested: true`, but only the **first** time a key is seen, so re-renders never reset what the student clicked.
     - `setCardValue(key, option, value | null)` sets `suggested: false` and clears the entry on null.
     - The card state is conversation content: a `conversation` snapshot clears it, like `location`.
   - Add store unit tests: seeding once, a click clears "suggested", unselect, survival or clearing across a conversation.
9. **Card UI** (`ui/commandCard.ts`). Its signature becomes `commandCard(ctx, turnId, name, suggested)`. It seeds the card, calls `ctx.requestCommandOptions(name)` (batched like `requestCommandCheck`) and renders from `peek()`ed store state:
   - **Looking up** (asked, no answer yet): a `p.cmd-card-note` with `options.looking` ("Looking up options…").
   - **Rows** (`div.cmd-opt[data-opt=Name]`), one per option, each ending in an explain button `button.cmd-opt-explain` (icon `question`, 12 px; `aria-label` = `t('options.explain', name)`):
     - `choice`: one `button.cmd-opt-value` per value, with `aria-pressed`. A click selects; clicking the selected value unselects.
     - `toggle`: the same buttons, for its two values.
     - `info`: the name only, plus the explain button.
     - **Blocked commands** (`runnable` is `'blocked'`): every row renders as `info`, and there is no Run.
   - **Suggested.** A pair from `suggested` applies only if the option exists, is `choice`/`toggle`, and the value matches one of its values (case-insensitively). Otherwise it is ignored. A row whose value is still `suggested: true` shows a `span.cmd-opt-suggested` with `options.suggested` ("suggested by Owly").
   - **Hint.** When at least one value is set, add a `p.cmd-card-note` under the rows with `options.hint`: "Owly sets these as the command starts. If Rhino says Unknown command, set that option on Rhino's command line when Rhino asks for it."
   - **Run.** `ctx.runCommand(name, values)` sends `{type:'command.run', name, options}`, with only the set values, filtered by the same rule as Suggested. Omit `options` when it's empty.
   - **No options** (an empty answer, offline, a 403): the card is just Run (or the blocked note), as in plan 022.
   - In `message.ts`, also read `ctx.store.commandOptions()` and `ctx.store.cardValues()` inside the `bind`.
   - In `markdown.ts` / `message.ts`, pass `turnId` through the hook closure: `card: (name, suggested) => commandCard(ctx, turnId, name, suggested)`.
   - **Focus:** the card is rebuilt on every store change, so focus is lost after a click. Accept that in this plan, and don't build a focus-restoring system.
10. **Explain popover.**
    - First move the anchor-independent placement code out of `locationPopover.ts` `reposition()` into `ui/popoverPlacement.ts`: `export function placeBeside(root: HTMLElement, anchor: HTMLElement): boolean`. It returns false when it can't measure. Everything after `const anchor = …` and its null check moves; `locationPopover` calls it. **All existing location-popover verify checks must pass unchanged.**
    - Then add `ui/explainPopover.ts`, a copy of the location popover's shape:
      - `store.explain = signal<{ turnId; name; option } | null>` (cleared by a `conversation` snapshot)
      - `Overlay` gains `'explain'` in `state/ui.ts`
      - the same clear scrim, close button, Esc (the existing Esc handler in `app.ts` closes any overlay; also null `store.explain`, as it does for location) and per-frame placement
    - The anchor is `` `[data-turn="…"] .cmd-card[data-cmd="…"] .cmd-opt[data-opt="…"] .cmd-opt-explain` ``, so `commandCard` sets `data-cmd` on `.cmd-card`. If the anchor is gone, close the popover.
    - Content:
      - the title `t('options.explainTitle', option)`
      - the option's description
      - for choice and toggle options, a list of `Value — description` rows
      - for info rows, also `options.infoNote`: "Set this on Rhino's command line when the command asks for it."
      - a final small line `options.source`: "From McNeel's Rhino help."
    - `role="dialog"`, and focus moves to the close button.
11. **Strings.** Plan IDs are 330-349.
    - Panel strings (in `PanelStrings.Localized()` and `i18n/strings.ts`) use 330-339: `options.looking`, `options.suggested`, `options.hint`, `options.explain` ("Explain {0}"), `options.explainTitle` ("{0}"), `options.infoNote`, `options.source`.
    - The new privacy text uses one id from 340-349.
    - Check that every id is unused with `git grep -nE "LocalizeString\([^)]*, 3[34][0-9]\)" -- rhino/plugin`.
12. **Privacy** (Step 9). Change the privacy line in `UI/WelcomeDialog.ai.cs`: append " Owly also looks up command help on docs.mcneel.com, which sends the command's name." The changed text gets a **new** id from 340-349, because id 368 stays bound to the old text. Don't change any other welcome text.
13. **Prompt** (Step 8, with a new example). Append to `CommandSteer`: " When a choice suits the student's goal, you may write it with the command, like `_Pyramid DirectionConstraint=Vertical`, but only options and values you are certain exist; the panel drops anything else and the student decides before clicking Run."
    - Assert it in `AgentPromptsTests`.
    - The length guard (< 7000) must still pass; report the new length.
    - prompt-checks row 16: "How do I make a pyramid that stands straight up from the construction plane?" Owly writes `` `_Pyramid DirectionConstraint=Vertical` `` or explains Vertical, and the card shows Vertical marked "suggested by Owly".
    - Row 17: "Make a hexagonal pyramid." Owly writes no option it can't be certain of. NumSides isn't on Pyramid's page, so it is not pre-filled. Owly explains how to type 6 at the sides prompt instead.
14. **Mock host** (`mockHost.ts`).
    - `command.options` answers `_Pyramid` with synthetic data: `DirectionConstraint` (choice: None, Vertical, AroundCurve), `Solid` (toggle: Yes, No) and `BothSides` (info), with short invented descriptions. Every other name gets `[]`.
    - `command.run` echoes the options in its notice.
15. **verify.mjs**, in the host-page section after the plan 022 checks, while `turn-0` is still running. The fixture's `command.options.found` line for `_Pyramid` is replayed in the first batch. Plan 022's checks already drew a `_Pyramid` card with Run. Extend them or add new blocks:
    - the `_Pyramid` card shows a `.cmd-opt` row per fixture option, with value buttons for the choice
    - clicking a value sets `aria-pressed="true"`; clicking it again clears it
    - selecting one value and clicking Run sends `command.run` whose `options` is exactly `{Shape: 'Round'}` (the fixture line from item 5: one choice `Shape` with `Round` and `Square`)
    - Run without changes sends no `options` key
    - a new answer block in a new turn id carrying `` `_Pyramid Shape=Square Bogus=Yes` `` seeds Square as "suggested by Owly", and Bogus draws no row and is never sent. Inject a `turn.begin` for it, following how the fixture builds `turn-0`. If that's impractical, use a second command whose `command.options.found` you inject by hand.
    - clicking `.cmd-opt-explain` opens one `[role=dialog]` explain popover containing the description, and Esc closes it
    - `` `_Pyramid Shape=Round` `` renders its chip as `_Pyramid` (the code text shows the name only)

    Leave every popover closed afterwards.

**STOP and report instead of improvising if:**
- the turn-0 fixture can't host the checks above
- moving `placeBeside` breaks a location-popover check you can't fix without changing its behaviour
- `RunCommandCommand` with an optional dictionary doesn't deserialise from the panel's JSON
- making the parser pass the listed cases would need a third-party HTML library

**Out of scope:** the probe (plan 024), number inputs, following links between help pages (Polygon's options are never merged into Pyramid's), saving card state across conversations, and any change to the chip's copy or location behaviour.

## Done criteria

- Shared gates pass, and the new parser and macro tests pass.
- `git grep -n "mc-main-content" -- tests rhino/panel` finds only hand-written fixtures, with invented option names and descriptions. No sentence copied from a McNeel page appears anywhere in the repo.
- Panel: `npm run typecheck`, `npm test`, `npm run build` and `npm run verify` pass, and the rebuilt `dist/panel.host.html` is committed.
- No real McNeel HTML in the repo: `git grep -n "mc-main-content" tests` finds only hand-written fixtures.

## Manual Rhino check (Rhino 9)

- Ask "explain the pyramid command". The card shows the help-page options with explanations.
- Set DirectionConstraint to Vertical and click Run. Rhino starts Pyramid with Vertical set.
- Leave everything blank and click Run. Rhino keeps the values you last used.
- Ask "how do I make a hexagonal pyramid roof?". If the option data has NumSides, it is pre-filled with 6 and marked "suggested by Owly". If it doesn't, Owly's suggestion is dropped and the text explains it instead.
- Turn the network off: the card shows Run without options and nothing crashes.
