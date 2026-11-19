import { each, el } from '../core/dom.js';
import type { Child } from '../core/dom.js';
import { t } from '../i18n/t.js';
import type { ToolbarPlace } from '../protocol/events.js';
import type { LocationEntry } from '../state/store.js';
import type { PanelContext } from './context.js';
import { icon } from './icons.js';

// Plan 015 / ADR-0007: a text-only answer to "where is this button?". It never shows, hides or changes a toolbar.

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

function card(ctx: PanelContext, turnId: string, entry: LocationEntry): Child {
  return el(
    'div',
    { class: 'location-card' },
    el(
      'div',
      { class: 'location-head' },
      el('span', { class: 'location-title', text: t('location.title', entry.name) }),
      el(
        'button',
        {
          class: 'icon-btn',
          type: 'button',
          title: t('location.close'),
          'aria-label': t('location.close'),
          onClick: () => ctx.store.dismissLocation(turnId, entry.name),
        },
        icon('close', 12),
      ),
    ),
    entry.places.length === 0
      ? el('p', { text: t('location.none', entry.name) })
      : el('div', { class: 'location-places' }, ...entry.places.map(placeRow)),
  );
}

/** Reactive: cards appear when the host answers a chip click and go with the conversation. */
export function locationCards(ctx: PanelContext, turnId: string): Child {
  return each(
    () => ctx.store.locations().get(turnId) ?? [],
    // A newer answer for the same command has to rebuild the card, so the key carries the places too.
    (entry) => `${entry.name}|${JSON.stringify(entry.places)}`,
    (entry) => card(ctx, turnId, entry),
  );
}
