import { el, onCleanup } from '../core/dom.js';
import type { Child } from '../core/dom.js';
import { t } from '../i18n/t.js';
import type { ToolbarPlace } from '../protocol/events.js';
import type { LocationView } from '../state/store.js';
import type { PanelContext } from './context.js';
import { icon } from './icons.js';

// Plan 015 / ADR-0007, Plan 020: a text-only answer to "where is this button?", drawn as one popover over the
// transcript next to the clicked chip. It never shows, hides or changes a toolbar.

function placeText(place: ToolbarPlace): string {
  if (place.tab && place.group) return t('location.place', place.button, place.path, place.tab, place.group);
  if (place.group) return t('location.placeNoTab', place.button, place.path, place.group);
  return t('location.placeLoose', place.button, place.path);
}

function placeRow(place: ToolbarPlace): Child {
  return el(
    'p',
    { class: 'location-place' },
    el('span', { text: placeText(place) }),
    place.click === 'right' ? el('span', { class: 'location-tag', text: t('location.right') }) : null,
    place.hidden ? el('span', { class: 'location-tag', text: t('location.hidden') }) : null,
  );
}

function closePopover(ctx: PanelContext): void {
  ctx.store.closeLocation();
  ctx.ui.closeOverlay();
}

/** A where-click: open at once in a "looking" state (or from the cache), and ask the host only when it must. */
export function openLocationPopover(ctx: PanelContext, turnId: string, name: string): void {
  if (ctx.store.openLocation(turnId, name)) ctx.findToolbar(name, turnId);
  // A direct set, not openOverlay: that one toggles, and a click on another chip must replace this popover.
  ctx.ui.overlay.set('location');
}

/** Clear scrim: the answer stays readable underneath, and a click or scroll anywhere else closes the popover. */
export function locationScrim(ctx: PanelContext): Child {
  return el('div', {
    class: 'scrim clear',
    onClick: () => closePopover(ctx),
    onWheel: () => closePopover(ctx),
  });
}

function body(entry: LocationView): Child {
  if (!entry.places) return el('p', { text: t('location.looking', entry.name) });
  return entry.places.length === 0
    ? el('p', { text: t('location.none', entry.name) })
    : el('div', { class: 'location-places' }, ...entry.places.map(placeRow));
}

/** Mounted while overlay is 'location' and a location is open; see app.ts. */
export function locationPopover(ctx: PanelContext): Child {
  const { store } = ctx;
  // One popover serves every chip: a click on another chip changes the location, not the mount.
  const name = (): string => store.location()?.name ?? '';

  const close = el(
    'button',
    {
      class: 'icon-btn',
      type: 'button',
      title: t('location.close'),
      'aria-label': t('location.close'),
      onClick: () => closePopover(ctx),
    },
    icon('close', 12),
  );
  const root = el(
    'div',
    { class: 'location-popover', role: 'dialog', 'aria-label': () => t('location.title', name()) },
    el(
      'div',
      { class: 'location-head' },
      el('span', { class: 'location-title', text: () => t('location.title', name()) }),
      close,
    ),
    () => {
      const entry = store.location();
      return entry ? body(entry) : null;
    },
  );

  // The panel carries a CSS zoom, so the position is worked out in the panel's own unzoomed units: the scale
  // is measured, which cancels the zoom whether the engine reports rects zoomed or not.
  const reposition = (): void => {
    const entry = store.location.peek();
    const panel = root.closest<HTMLElement>('.panel');
    const stream = panel?.querySelector<HTMLElement>('.stream');
    const view = panel?.querySelector<HTMLElement>('.transcript');
    if (!entry || !panel || !stream || !view) return;
    const anchor = panel.querySelector<HTMLElement>(
      `[data-turn="${CSS.escape(entry.turnId)}"] .md-cmd[data-cmd="${CSS.escape(entry.name)}"]`,
    );
    // The chip is gone (the turn was removed, or the text no longer names it): nothing to point at.
    if (!anchor) {
      closePopover(ctx);
      return;
    }
    const pr = panel.getBoundingClientRect();
    const scale = pr.width / panel.offsetWidth || 1;
    const ar = anchor.getBoundingClientRect();
    const sr = stream.getBoundingClientRect();
    const vr = view.getBoundingClientRect();
    const viewTop = (vr.top - pr.top) / scale;
    const bottom = (vr.bottom - pr.top) / scale;
    // At a high zoom 300px is wider than the stream: narrow it first, so the measurements below are the final size.
    root.style.maxWidth = `${Math.min(300, (sr.width) / scale)}px`;
    const width = root.offsetWidth;
    const height = root.offsetHeight;

    const minLeft = (sr.left - pr.left) / scale;
    const maxLeft = Math.max(minLeft, (sr.right - pr.left) / scale - width);
    const left = Math.min(Math.max((ar.left - pr.left) / scale, minLeft), maxLeft);
    let top = (ar.bottom - pr.top) / scale + 4;
    // Flip above only when it doesn't fit below and there is more room above; never rise over the header.
    if (top + height > bottom && (ar.top - pr.top) / scale - viewTop > bottom - top)
      top = (ar.top - pr.top) / scale - height - 4;
    top = Math.max(top, viewTop);

    root.style.left = `${Math.round(left)}px`;
    root.style.top = `${Math.round(top)}px`;
  };

  // Every frame, because streaming rebuilds the chip and autoscroll moves it.
  let frame = 0;
  let focused = false;
  const tick = (): void => {
    reposition();
    if (!focused) {
      focused = true;
      close.focus();
    }
    frame = requestAnimationFrame(tick);
  };
  frame = requestAnimationFrame(tick);
  onCleanup(() => cancelAnimationFrame(frame));

  return root;
}
