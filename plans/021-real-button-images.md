# 021: Show the real toolbar button image

- **Written against:** `40f2c38` on `rhino-9.x`.
- **Status:** DONE (`d104884`, advisor-approved, merged into `rhino-9.x`; the manual Rhino check is pending)
- **Effort:** M
- **Risk:** Medium. Built-in icons live in Rhino-internal resources whose names can change in a Rhino update. Any failure falls back to the generic glyph.
- **Phase:** Phase 5 rework, commands & tools. Glossary: **Button image**, **Tool highlight**.

## Why

"Instead of highlighting, provide the image of the tool." The product owner decided:
- **Source: only the student's own Rhino install.** No web requests (not docs.mcneel.com).
- The real image **replaces the generic toolbar glyph on every command chip**, and also shows in the "where to find" popover (plan 020).
- It uses the light or dark variant to match Rhino's theme.
- If no image is found (no button, R8 differences, Mac, a resource renamed), the chip keeps the generic glyph.

## Facts you need

These were checked on this machine on 2026-11-24.

- **Toolbar file.** `%AppData%\McNeel\Rhinoceros\9.0\UI\default.rui` (≈4.7 MB). 2,275 of 2,277 `macro_item`s carry a `bitmap_id` GUID attribute, e.g.

  ```xml
  <macro_item guid="3da59aa5-…" bitmap_id="96723884-7f2b-4250-9e34-934ef8862182"> … <script>! _Pyramid</script>
  ```
- **Plug-in icons** (including Owly's) are **inline**: `<icons><icon guid="…"><light><svg …/></light><dark><svg …/></dark></icon></icons>`. Owly's own icon is an SVG wrapping a base64 PNG.
- **Built-in icons are not in the `.rui`.** They are embedded in `C:\Program Files\Rhino 9\System\Rhino.UI.dll` (also loaded in Rhino's process):
  - `Rhino.UI.Resources.icon_ids.txt`, CSV lines like `96723884-7f2b-4250-9e34-934ef8862182,Solids_Pyramid.svg,Solids_Pyramid_darkmode.svg` (1,698 lines)
  - `Rhino.UI.Resources.svg.<Name>.svg`, the SVG text itself

  **Confirmed through .NET inside Rhino 9.1.26314.9001 on 2026-11-24 (step 1, a Script Editor probe):**
  - The loaded `Rhino.UI` assembly is `C:\Program Files\Rhino 9\System\netcore\Rhino.UI.dll` (note `netcore\`). It has 4,758 manifest resources. Find it through `AppDomain` as below, never by path.
  - `Rhino.UI.Resources.icon_ids.txt` is readable. It has 1,730 non-empty lines (the 1,698 above was an older count), each with 3 columns: `guid,light.svg,dark.svg`. The Pyramid GUID is present.
  - Both of the Pyramid's SVG files are readable as `Rhino.UI.Resources.svg.<file>`. Each is about 1.5 KB of text starting with `<svg` or `<?xml`.
  - Not every SVG lives under `.svg.`. Some are directly under `Rhino.UI.Resources.<file>` (for example `3DConnection.svg`), and other prefixes exist (`Resources.tabs`, `Resources.LicenseDiscovery`, `Internal…SvgEditorIcons`). So the resolver tries `Rhino.UI.Resources.svg.<file>` first, then `Rhino.UI.Resources.<file>`, and otherwise returns `null`.
  - The light and dark Pyramid files have the same length (1,502 chars). That is likely a colour swap of equal-length hex codes. The manual dark-mode check confirms the dark variant really differs.
- **`Rhino.UI` is a separate assembly from RhinoCommon.** Find it with `AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "Rhino.UI")`, then call `GetManifestResourceStream(name)`. `typeof(Rhino.UI.Dialogs)` is in RhinoCommon, so it is the wrong assembly.
- **Today's parser:**
  - `rhino/plugin/UI/ToolbarIndex.ai.cs` parses macros (`script`, `tooltip`, …) but not `bitmap_id`. `FirstCommand(script)` (`:181`) maps `! _Pyramid` → `Pyramid`.
  - `UI/ToolbarLocator.ai.cs` loops over `RhinoApp.ToolbarFiles`, reads files read-only and caches by write time and language.
- **Copyright rule (plan 015):** never copy `.rui` content, labels or icons into the repo, including test fixtures. Use synthetic XML and CSV in tests.
- **No CSP** in the panel. Render the image as `<img src="data:image/svg+xml;base64,…">`, never inline `innerHTML`. An `<img>` doesn't run SVG scripts.

## Steps

1. **DONE on 2026-11-24 (Rhino 9.1); the results are in Facts. Executors skip this step.** Spike (≤1 h, in Rhino 9). A throwaway local command, not committed, that lists `GetManifestResourceNames()` of `Rhino.UI` matching `icon_ids` and `svg.`, and reads the Pyramid SVG. Record the confirmed names in this plan's Facts section. If the resources aren't readable, ship only the inline-`<icons>` path and record that.
2. **Index.** In `ToolbarIndex`, store the `bitmap_id` per macro and the inline `<icons>` map (`guid → light/dark SVG`). For each command, keep the `bitmap_id` of the first macro whose `FirstCommand` matches; left-click macros win over right-click ones. Add unit tests with synthetic XML.
3. **Resolver.** Add `rhino/plugin/UI/ToolbarIcons.ai.cs`, with `TryGet(commandName, dark) → string? svg`:
   - try the inline icon first
   - then the `icon_ids.txt` lookup (parsed once, case-insensitive GUID) and the resource `Rhino.UI.Resources.svg.<file>`, falling back to `Rhino.UI.Resources.<file>`
   - cache the results
   - wrap everything in try/catch; any failure returns `null`
   - cap the size (e.g. 64 KB)

   Put the CSV parsing in a Rhino-free helper so Server.Tests can link and test it.
4. **Protocol.** Panel → host `toolbar.icon {names: string[]}`, sent once per new command chip. Host → panel `toolbar.iconFound {name, light?, dark?}` with base64 SVG. Add C# records in `PanelCommands.ai.cs` / `PanelEvents.ai.cs` and the TS types in `events.ts`. Add a handler in `AIPanelViewModel.IncomingCommand` that validates names with `CommandName.IsValid`. Update `mockHost.ts` with a tiny synthetic SVG and `tests/host-events.json`.
5. **Panel.** The store keeps `icons: Map<name, {light, dark}>`. The chip's where-button shows `<img class="md-cmd-icon">` when an icon is known, otherwise the glyph. Pick light or dark from the panel's theme class. The popover from plan 020 shows the image larger (24-32 px) next to the place text. If plan 020 isn't done yet, the chip part alone is still complete.
6. Rebuild `dist/panel.host.html`. Localisation IDs for this plan: 300-309.
7. Repeat the spike's resource listing on Rhino 8 if a machine with it is available. If R8 differs, R8 uses inline icons only.

## Advisor addendum at `b47b96b` (read before starting; it overrides Steps 2-6 where they differ)

Plan 020 is merged: the "where to find" popover is `rhino/panel/src/ui/locationPopover.ts`. The chip carries `data-cmd="<name>"` (`core/markdown.ts` `commandChip()`), and `store.location` holds the open popover. The facts above about `ToolbarIndex` and `ToolbarLocator` still hold at this commit.

**C# side**
1. **`ToolbarIndex` (Rhino-free, already linked in Server.Tests).**
   - Extend the private `Hit` record with `string? BitmapId`, taken from the macro's `bitmap_id` attribute.
   - Add `public string? BitmapIdFor(string command)`. It uses the same command match as `Find` (`Key`, case-insensitive) and returns the first non-null `BitmapId` among left-click hits, then among right-click hits.
   - Parse `<icons><icon guid="…"><light>…</light><dark>…</dark></icon></icons>` (anywhere under the root) into a `Dictionary<string, (string? Light, string? Dark)>` keyed case-insensitively on `guid`. For each of `light`/`dark`: if it has a child element, take `FirstElement.ToString(SaveOptions.DisableFormatting)`; otherwise take the trimmed `.Value` if non-empty (CDATA or escaped text); otherwise null.
   - Add `public (string? Light, string? Dark)? InlineIcon(string bitmapId)`.
2. **`UI/IconIds.ai.cs` (new, Rhino-free; link it in `tests/Server.Tests/Server.Tests.csproj` next to `ToolbarIndex.ai.cs`).**
   - `Parse(string csv)` returns `Dictionary<Guid, (string Light, string Dark)>`. It splits lines, needs 3 comma columns, trims, parses the GUID (case doesn't matter), and skips malformed lines.
   - `ResourceNames(string file)` yields `"Rhino.UI.Resources.svg." + file`, then `"Rhino.UI.Resources." + file`.
   - `MaxSvgChars = 64 * 1024`.
   - `Accept(string? svg)` returns the trimmed text only if it is non-null, within `MaxSvgChars`, and starts with `<svg` or `<?xml`; otherwise null.
3. **`ToolbarLocator`.** Add `internal static IEnumerable<ToolbarIndex> LoadedIndexes()`. It walks `RhinoApp.ToolbarFiles` through the existing `IndexFor` cache (the same file, language and try/catch rules as `Find`), so the 4.7 MB `default.rui` is parsed once for both features. Don't change `Find`'s behaviour.
4. **`UI/ToolbarIcons.ai.cs` (new, Rhino-dependent, UI thread).** Its API is `public static (string? Light, string? Dark) TryGet(string command)`. This replaces the plan's `TryGet(name, dark)`, because the protocol carries both variants.
   - For each index in `LoadedIndexes()`: get `BitmapIdFor(command)` and skip it if null. Take `InlineIcon(id)` if present; otherwise, if the id parses as a GUID, take the built-in lookup.
   - The first index that yields a light **or** a dark wins. A missing dark falls back to light, and a missing light falls back to dark. Pass both through `IconIds.Accept`.
   - **The built-in lookup:**
     - Find the assembly with `AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "Rhino.UI")`.
     - Read `Rhino.UI.Resources.icon_ids.txt` once (lazily) and run it through `IconIds.Parse`.
     - For each file, try `IconIds.ResourceNames` in order with `GetManifestResourceStream`, reading through a UTF-8 `StreamReader`.
   - Cache results per command (`StringComparer.OrdinalIgnoreCase`), including "none", for the Rhino session.
   - Wrap everything in try/catch: on failure, `HostUtils.LogDebugEvent("[owly] toolbar icon lookup failed: …")` and return `(null, null)`.
5. **Protocol.**
   - Panel → host: `toolbar.icon {names: string[]}`, record `ToolbarIconCommand(IReadOnlyList<string> Names)`.
   - Host → panel: **`toolbar.icon.found`** (dotted lowercase, like `turn.tool.patch`, not the `iconFound` above) `{name, light?, dark?}`. The record is `ToolbarIconFoundEvent(string Name, string? Light, string? Dark)`, with base64 of the UTF-8 SVG. `PanelJson` drops nulls, so absent means "none".
   - Handler `FindToolbarIcons(names)` in `AIPanelViewModel.IncomingCommand`, next to `FindToolbar`: take at most 20 distinct names. Skip any that fail `CommandName.IsValid`. Post one `ToolbarIconFoundEvent` per valid name, **even when nothing was found**, so the panel never waits.
   - Add a pin test in `ToolbarIndexTests.ai.cs`, the way `The_toolbar_found_event_has_the_json_the_panel_fixture_pins` does. It pins the exact JSON of one `toolbar.icon.found` line with a tiny **synthetic** SVG of your own (for example a 16×16 `<rect>`), with a different `fill` in light and dark. Add the same line to `rhino/panel/tests/host-events.json` after the `toolbar.found` line. Also add a command-deserialisation test for `toolbar.icon`, like the project-command tests in `ProjectRegistryTests.ai.cs`.
6. **C# tests**, synthetic only (no real Rhino GUIDs, file names or icons):
   - `bitmap_id` per command, with left winning over right
   - inline icon parsing (child element and text forms)
   - `IconIds.Parse` (a good line, malformed lines, a GUID's case)
   - `ResourceNames` order
   - `Accept` (null, oversize, not SVG)

**Panel side**
7. **Store.** Add `icons = signal<ReadonlyMap<string, { light?: string; dark?: string }>>`, keyed on the chip name exactly as rendered (`_Loft`). Icons are not conversation content, so they **survive** a `conversation` snapshot.
   - `toolbar.icon.found` always sets the entry, asked for or not: it is harmless data, and the fixture relies on it.
   - Add a private `iconAsked: Set<string>` and `markIconAsked(name): boolean`, which returns true the first time and false if the name is already asked or known.
   - Add `iconUrl(name): string | null`. It picks `dark` when `scheme()` is `'dark'` and falls back to the other variant, returning `data:image/svg+xml;base64,…` or null.
   - Add store unit tests for each of these.
8. **Requests.** Add `ctx.requestToolbarIcon(name)` in `ui/context.ts` and `main.ts`. If `store.markIconAsked(name)`, it queues the name and sends **one** `toolbar.icon {names}` per microtask (`queueMicrotask` batching). Streaming re-renders call it every frame, so the dedupe is what keeps it to one request per name.
9. **Chip.** Add an optional `icon?: (name: string) => string | null` to `CommandChipOptions`. In `commandChip()`, when `locate` is set and `icon(name)` returns a URL, the where-button holds `<img class="md-cmd-icon" src=… alt="">` instead of `glyph('toolbar')`. Keep the button's title and aria-label. Build the `img` with `element()` and set `src`, never `innerHTML`. In `ui/message.ts`, `icon: (name) => { ctx.requestToolbarIcon(name); return ctx.store.iconUrl(name); }`.
10. **Re-render when an icon arrives or the theme flips.** In `agentMessage`'s `bind`, also read `ctx.store.icons()` and `ctx.store.scheme()` before the `queued` check, so both are tracked.
11. **Popover.** In `.location-head` of `locationPopover.ts`, put a reactive `<img class="location-icon" alt="">` (24 px) before the title when `iconUrl(location.name)` is known.
12. **CSS.** `.md-cmd-icon` is 14×14, `display: block`, with no background, filter or blend. `.location-icon` is 24×24. Don't touch the `.owl` rules.
13. **Mock host.** Answer `toolbar.icon` with a synthetic SVG for `_Loft` and `_Box` (different light and dark fills) and an empty answer for anything else.
14. **verify.mjs**, in the host-page section after the plan 019 chip checks:
    - the `_Loft` chip's where-button holds `img.md-cmd-icon` with a `data:image/svg+xml;base64,` src
    - injecting text with an unknown command (for example `` `_Sphere` ``) sends exactly one `toolbar.icon` naming it, even after another delta re-renders the block
    - that chip keeps the glyph (no `img`)
    - the `_Loft` popover shows `img.location-icon`
    - a `theme` event switching the scheme changes the chip's `img` src

    Leave the popover closed afterwards, because later checks click the panel.
15. **No new user-visible strings** are expected. If you add one, use IDs 300-309 in both `i18n/strings.ts` and `PanelStrings.ai.cs`.

**Watch item:** the first icon request now parses `default.rui` on the UI thread when an answer first shows a chip, where before it waited for a click. `ToolbarLocator` already logs the lookup time. The manual check notes it.

## Done criteria

- Shared gates pass, and there are new C# tests (bitmap_id parsing, CSV parsing, fallback to null) and a verify check (a known icon renders as `img`, an unknown command keeps the glyph).
- `git grep -n "Solids_Pyramid" -- . ':!plans'` finds nothing.
- `git grep -n "96723884" -- . ':!plans'` finds nothing (no real Rhino GUIDs in code or tests).
- Panel: `npm run typecheck`, `npm test`, `npm run build` and `npm run verify` pass, and the rebuilt `dist/panel.host.html` is committed.

## Manual Rhino check

Ask about `_Pyramid`, `_Loft` and `_Box`. Each chip shows Rhino's real button image, and the popover shows it too. Switch Rhino to dark mode: the dark variant appears. A command with no toolbar button keeps the generic glyph.
