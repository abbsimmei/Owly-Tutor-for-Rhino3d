# 023: Options card from McNeel's help pages, with Owly's suggested values

- **Written against:** `40f2c38` on `rhino-9.x`. **Requires plan 022 to be DONE.**
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

## Steps

1. **Check in Rhino (≤1 h).**
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

## Done criteria

- Shared gates pass, and the new parser and macro tests pass.
- No real McNeel HTML in the repo: `git grep -n "mc-main-content" tests` finds only hand-written fixtures.

## Manual Rhino check (Rhino 9)

- Ask "explain the pyramid command". The card shows the help-page options with explanations.
- Set DirectionConstraint to Vertical and click Run. Rhino starts Pyramid with Vertical set.
- Leave everything blank and click Run. Rhino keeps the values you last used.
- Ask "how do I make a hexagonal pyramid roof?". If the option data has NumSides, it is pre-filled with 6 and marked "suggested by Owly". If it doesn't, Owly's suggestion is dropped and the text explains it instead.
- Turn the network off: the card shows Run without options and nothing crashes.
