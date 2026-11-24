# 020: "Where to find" becomes a popover over the transcript

- **Written against:** `40f2c38` on `rhino-9.x`.
- **Status:** TODO
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

## Done criteria

- Shared gates pass.
- `git grep -n "locationCards" rhino/panel/src` finds nothing.

## Manual Rhino check

Ask about `_Loft` and click the toolbar icon next to it. The popover appears next to the icon, over the text, and shows Surface Tools. Esc, ✕ and an outside click each close it. Then click the icon of a command with no toolbar button: the popover says "not on any open toolbar".
