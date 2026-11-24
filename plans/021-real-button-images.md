# 021: Show the real toolbar button image

- **Written against:** `40f2c38` on `rhino-9.x`.
- **Status:** TODO
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

  These names were seen as strings in the DLL, not yet read through .NET. Step 1 confirms them.
- **`Rhino.UI` is a separate assembly from RhinoCommon.** Find it with `AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "Rhino.UI")`, then call `GetManifestResourceStream(name)`. `typeof(Rhino.UI.Dialogs)` is in RhinoCommon, so it is the wrong assembly.
- **Today's parser:**
  - `rhino/plugin/UI/ToolbarIndex.ai.cs` parses macros (`script`, `tooltip`, …) but not `bitmap_id`. `FirstCommand(script)` (`:181`) maps `! _Pyramid` → `Pyramid`.
  - `UI/ToolbarLocator.ai.cs` loops over `RhinoApp.ToolbarFiles`, reads files read-only and caches by write time and language.
- **Copyright rule (plan 015):** never copy `.rui` content, labels or icons into the repo, including test fixtures. Use synthetic XML and CSV in tests.
- **No CSP** in the panel. Render the image as `<img src="data:image/svg+xml;base64,…">`, never inline `innerHTML`. An `<img>` doesn't run SVG scripts.

## Steps

1. **Spike (≤1 h, in Rhino 9).** A throwaway local command, not committed, that lists `GetManifestResourceNames()` of `Rhino.UI` matching `icon_ids` and `svg.`, and reads the Pyramid SVG. Record the confirmed names in this plan's Facts section. If the resources aren't readable, ship only the inline-`<icons>` path and record that.
2. **Index.** In `ToolbarIndex`, store the `bitmap_id` per macro and the inline `<icons>` map (`guid → light/dark SVG`). For each command, keep the `bitmap_id` of the first macro whose `FirstCommand` matches; left-click macros win over right-click ones. Add unit tests with synthetic XML.
3. **Resolver.** Add `rhino/plugin/UI/ToolbarIcons.ai.cs`, with `TryGet(commandName, dark) → string? svg`:
   - try the inline icon first
   - then the `icon_ids.txt` lookup (parsed once, case-insensitive GUID) and `svg.<file>`
   - cache the results
   - wrap everything in try/catch; any failure returns `null`
   - cap the size (e.g. 64 KB)

   Put the CSV parsing in a Rhino-free helper so Server.Tests can link and test it.
4. **Protocol.** Panel → host `toolbar.icon {names: string[]}`, sent once per new command chip. Host → panel `toolbar.iconFound {name, light?, dark?}` with base64 SVG. Add C# records in `PanelCommands.ai.cs` / `PanelEvents.ai.cs` and the TS types in `events.ts`. Add a handler in `AIPanelViewModel.IncomingCommand` that validates names with `CommandName.IsValid`. Update `mockHost.ts` with a tiny synthetic SVG and `tests/host-events.json`.
5. **Panel.** The store keeps `icons: Map<name, {light, dark}>`. The chip's where-button shows `<img class="md-cmd-icon">` when an icon is known, otherwise the glyph. Pick light or dark from the panel's theme class. The popover from plan 020 shows the image larger (24-32 px) next to the place text. If plan 020 isn't done yet, the chip part alone is still complete.
6. Rebuild `dist/panel.host.html`. Localisation IDs for this plan: 300-309.
7. Repeat the spike's resource listing on Rhino 8 if a machine with it is available. If R8 differs, R8 uses inline icons only.

## Done criteria

- Shared gates pass, and there are new C# tests (bitmap_id parsing, CSV parsing, fallback to null) and a verify check (a known icon renders as `img`, an unknown command keeps the glyph).
- `git grep -n "Solids_Pyramid" -- . ':!plans'` finds nothing.

## Manual Rhino check

Ask about `_Pyramid`, `_Loft` and `_Box`. Each chip shows Rhino's real button image, and the popover shows it too. Switch Rhino to dark mode: the dark variant appears. A command with no toolbar button keeps the generic glyph.
