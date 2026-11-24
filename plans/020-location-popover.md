# 020: "Where to find" becomes a popover over the transcript

- **Written against:** `40f2c38` on `rhino-9.x`.
- **Status:** DONE (`cac2459` + `e7eb66d`, advisor-approved, merged into `rhino-9.x`; the manual Rhino check is pending)
- **Effort:** M
- **Risk:** Low. Panel only; the `toolbar.find` / `toolbar.found` protocol is reused unchanged.
- **Phase:** Phase 5 rework, commands & tools. Glossary: **Location popover**, which replaces Location card.

## Why

Today the toolbar icon on a command chip adds a **location card under the answer**, after all of the turn's blocks. The product owner wants it to come up **over the text, as a separate overlay**. The decision is a popover inside the Owly panel, drawn over the transcript next to the clicked icon:
- It shows the same text as today: group or tab, toolbar, button, left/right click, "hidden", or "not on any open toolbar".
- Plan 021 adds the button image.
- It closes with ✕, Esc or a click anywhere else.

## Facts you need

- **The card today:**
  - `rhino/panel/src/ui/locationCard.ts`: `card()`, `placeText()`, `placeRow()`, and `locationCards()`, which is mounted at `ui/transcript.ts` (`locationCards(ctx, turn.id)` after the turn's blocks).
  - Store: `rhino/panel/src/state/store.ts:253-297`. `locations()` is a map from turn id to `LocationEntry[]`, with `dismissLocation`, and a new conversation clears it.
  - CSS: `styles/panel.css:514-532` (`.location-*`).
- **The chip's where-button:** `.md-cmd-where` in `core/markdown.ts:327-356`. Its click sends `toolbar.find {name, turnId}` (`ui/message.ts`, `main.ts`). The host answers with `toolbar.found {name, turnId, places}`.
- **The overlay pattern already exists:**
  - `ui/app.ts:144-147` mounts `scrim` plus `agentMenu` when `ui.overlay() === 'agents'`.
  - `state/ui.ts` has `overlay`, `openOverlay` and `closeOverlay`.
  - z-index layers (`panel.css`): `.notices` 11, `.popover` 9, `.scrim` 8, `.ask-scrim` 6.
- **The answer arrives asynchronously.** The popover opens at once in a "looking…" state and fills in when `toolbar.found` arrives for that name. A click on a different chip replaces the popover.
- **verify:** `tools/verify.mjs:692-706` (the fixture shows one location card with "Surface Tools", the where button sends `toolbar.find`, close removes it) and `:947-954` (replay clears cards). The store tests are in `state/store.test.ts:64-109`.

## Steps

1. **Store.** Replace the per-turn card list with:
   - `location`: a signal holding `{ turnId, name, places?: ToolbarPlace[] } | null`, the one open popover
   - a cache `Map<name, ToolbarPlace[]>` that lives for the conversation

   `toolbar.found` fills the cache and the open popover if the names match. A `toolbar.found` that nothing asked for (the replay fixture) opens nothing. Results for turns that are gone are still ignored, as today.
2. **Overlay.** Add `'location'` to the `Overlay` type. When the chip's where-button is clicked:
   - open the overlay, anchored to that chip
   - mark the chip with `data-cmd`
   - send `toolbar.find`, unless the result is cached
3. **Popover component.** Turn `ui/locationCard.ts` into `locationPopover(ctx)`, mounted in `app.ts` next to the agents popover.
   - Position it under the anchor, flipped above it if it would run off the bottom, and clamped to the width of `.stream`.
   - **The panel has a CSS zoom** (`transcript.ts:133-141` documents the scale mismatch). So position in layout units (`offsetTop`/`offsetLeft` inside the turn), not `getBoundingClientRect()`.
   - While the text streams, the chip is rebuilt every frame. Look the anchor up again by `data-cmd`, and close the popover if the chip is gone.
   - Reuse `placeText`/`placeRow`.
   - It closes on ✕, Esc (keydown on the document while open), a scrim click, or scrolling the transcript.
   - Give it `role="dialog"` and an `aria-label`, and move focus to ✕.
4. **Remove** `locationCards` from `transcript.ts` and the card CSS. Add `.location-popover` CSS (max-width ≈ 300px, the same card look).
5. **Tests.** Update the store tests (open, fill, cache, a new conversation clears) and `verify.mjs`:
   - the where-click opens a popover containing "Surface Tools" from the fixture
   - Esc closes it
   - a click outside closes it
   - no `.location-card` exists anywhere
6. Update `mockHost.ts` if needed, then rebuild `dist/panel.host.html`.

Localisation IDs for this plan: 290-299 (the last ID in use is 289).

## Advisor addendum at `acc2756` (read before starting; it overrides the steps above where they differ)

Plans 018, 019 and 025 merged after this plan was written, so line numbers moved. Current locations:
- Store: `state/store.ts`. `LocationEntry` is at :28, the `locations` signal at :116-117, the reset in the `conversation` case at :176, the `toolbar.found` case at :262-275, and `dismissLocation` at :310-318.
- `ui/transcript.ts`: the import is at :11 and `locationCards(ctx, turn.id)` at :115. The `turnView` `<article class="turn …">` starts at :93.
- CSS: `.location-*` is at `styles/panel.css:535-553`, `.scrim` at :921 and `.popover` at :923. `.panel` is `position: relative` (:234).
- The chip is built in `commandChip()` at `core/markdown.ts:339-368`. The where-click calls `command.locate(name)`, which `ui/message.ts:33` wires to `ctx.findToolbar(name, turnId)`.
- `ui/app.ts:100-104` already closes any overlay on Esc (a window keydown listener). Reuse it and don't add a second Esc handler.
- verify: the fixture card check, the where-click and the close are at `tools/verify.mjs:752-769`. The "replay clears it" check is at :1022-1029.
- The C# strings mirror is at `rhino/plugin/UI/PanelStrings.ai.cs:49-58` (IDs 280-289).

Decisions that fill gaps in the steps:
1. **Finding the anchor.** Chips are rebuilt on every render, so a mark set on an old element is lost. Set it where the chip is born: in `commandChip()`, `chip.dataset['cmd'] = name`. In `transcript.ts` `turnView`, give the article `'data-turn': turn.id`. Plan 019 makes a command a chip only once per turn, so `[data-turn="<id>"] .md-cmd[data-cmd="<name>"]` is unique. Escape both values with `CSS.escape`.
2. **Store API.** It never sends anything:
   - `location` signal: `{ turnId, name, places?: readonly ToolbarPlace[] } | null`.
   - Private `cache: Map<string, readonly ToolbarPlace[]>` and private `asked: Set<string>`, both per conversation.
   - `openLocation(turnId, name): boolean` sets `location` (with the cached places if any). It returns `true` when the host must be asked, and then adds the name to `asked`.
   - `closeLocation()`.
   - `toolbar.found`: ignore it if the turn is gone, and ignore it if the name is not in `asked` (an unsolicited answer, such as the contract fixture's replay line, caches and opens nothing). Otherwise cache it, and if `location()?.name === event.name`, set `location` with the places.
   - The `conversation` snapshot sets `location` to null and clears `cache` and `asked`.
   - Delete `LocationEntry`, `locations` and `dismissLocation`.
3. **Opening.** In `ui/locationPopover.ts`, export `openLocationPopover(ctx, turnId, name)`. It calls `store.openLocation`, calls `ctx.findToolbar(name, turnId)` if that returns true, and then `ui.overlay.set('location')`. Use a direct set, not `openOverlay`, which toggles. `message.ts` calls it from `locate`. Update the `findToolbar` doc comment in `ui/context.ts` (it still says "location card").
4. **Overlay lifetime.** In `app.ts`, mount it as `when(() => ui.overlay() === 'location' && store.location() !== null, () => [scrim, locationPopover(ctx)])`. Add a `bind` in `app.ts` that closes the overlay when `store.location()` turns null while `ui.overlay.peek() === 'location'`, so a new conversation can't leave a stray scrim. Every close path (✕, Esc, scrim click, wheel) goes through `ui.closeOverlay()` plus `store.closeLocation()`, or through `closeLocation` and that bind.
5. **Scrim.** Use `class: 'scrim clear'` and add `.scrim.clear { background: transparent; }`, so the answer stays readable under the popover. Close on the scrim's `click` and `wheel` (the scrim covers the transcript, so a scroll gesture lands on it).
6. **Positioning.** The popover is mounted in `.panel`, outside the turn, so `offsetTop` inside the turn doesn't apply. Use `position: absolute` inside `.panel` and compute in the panel's own (unzoomed) units:
   - Let `pr = panel.getBoundingClientRect()` and `scale = pr.width / panel.offsetWidth || 1`.
   - Take the anchor's rect and convert: `top = (ar.bottom - pr.top) / scale + 4`, `left = (ar.left - pr.left) / scale`.
   - Clamp `left` so the popover stays inside the `.stream` box (converted the same way).
   - Flip above the anchor if `top + popover.offsetHeight` would pass the `.transcript` box's bottom.

   This cancels the zoom whether the engine reports rects zoomed or not. Recompute in a `requestAnimationFrame` loop while the popover is mounted (stop it in `onCleanup`). That covers streaming re-renders and autoscroll. If the anchor isn't found, close the popover.
7. **Looking state.** While `places` is undefined, show the new string `location.looking` = `Looking for {0} on the toolbars…`. Add it to `i18n/strings.ts` and to `PanelStrings.ai.cs` with ID **290**. That is the only C# change.
8. **Style.** `.location-popover` gets z-index 9, `max-width: 300px`, and the old card's border, background, radius, padding and font size, plus a small shadow if one exists in the stylesheet (search for `box-shadow` on `.popover`). Do **not** add the `.popover` class; its `top`/`inset-inline` would fight the positioning. Keep `.location-head`, `.location-title`, `.location-place` and `.location-tag`, and delete only the `.location-card` rules.
9. **verify.mjs**, rewriting the two sections:
   - Section :752-769 checks, in order:
     - after the replay, no `.location-popover` and no `.location-card` exist
     - a where-click sends `toolbar.find` for `_Loft`/`turn-0` and shows a `.location-popover` with `role="dialog"`
     - focus is on its close button
     - feeding the fixture's `toolbar.found` puts "Surface Tools" in the popover
     - Esc closes it
     - a second where-click opens it at once with "Surface Tools" and sends **no** new `toolbar.find` (cached)
     - a `.scrim` click closes it

     Leave it closed, because the project-card checks that follow need clicks to reach the panel.
   - Section :1022-1029: open the popover with a where-click, check that it shows, deliver `firstReplay[0]` and check that the popover and scrim are both gone.
10. **Store tests** (`state/store.test.ts`, replacing the location tests): open-then-found fills; an unsolicited found does nothing; a second open of the same name returns false and has the places at once; a found for a gone turn is ignored; a conversation snapshot clears the location and the cache, so the next open returns true again.

## Done criteria

- Shared gates pass (R9 and R8 builds, because `PanelStrings.ai.cs` changes, plus Server.Tests).
- Panel: `cd rhino/panel && npm run typecheck && npm test && npm run build && npm run verify` all pass, and the rebuilt `dist/panel.host.html` is committed.
- `git grep -n "locationCards\|location-card\|dismissLocation\|LocationEntry" rhino/panel/src rhino/panel/tools` finds nothing.
- `git grep -n "290)" rhino/plugin/UI/PanelStrings.ai.cs` finds exactly the `location.looking` line.

## Manual Rhino check

Ask about `_Loft` and click the toolbar icon next to it. The popover appears next to the icon, over the text, and shows Surface Tools. Esc, ✕ and an outside click each close it. Then click the icon of a command with no toolbar button: the popover says "not on any open toolbar".
