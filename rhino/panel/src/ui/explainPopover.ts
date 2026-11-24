import { el, onCleanup } from '../core/dom.js';
import type { Child } from '../core/dom.js';
import { t } from '../i18n/t.js';
import type { PanelContext } from './context.js';
import { icon } from './icons.js';
import { placeBeside } from './popoverPlacement.js';

// Plan 023: what an option means, from McNeel's help page, in the same shape as the location popover (plan 020):
// one popover over the transcript, next to the option's explain button. It never changes anything in Rhino.

function closePopover(ctx: PanelContext): void {
  ctx.store.closeExplain();
  ctx.ui.closeOverlay();
}

/** Clear scrim: the text stays readable underneath, and a click or scroll anywhere else closes the popover. */
export function explainScrim(ctx: PanelContext): Child {
  return el('div', {
    class: 'scrim clear',
    onClick: () => closePopover(ctx),
    onWheel: () => closePopover(ctx),
  });
}

/** Mounted while overlay is 'explain' and an explanation is open; see app.ts. */
export function explainPopover(ctx: PanelContext): Child {
  const { store } = ctx;
  const option = (): string => store.explain()?.option ?? '';

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

  const body = (): Child => {
    const entry = store.explain();
    if (!entry) return null;
    const info = store.commandOptions().get(entry.name)?.find((o) => o.name === entry.option);
    if (!info) return null;
    return el(
      'div',
      { class: 'explain-body' },
      info.description ? el('p', { text: info.description }) : null,
      info.kind !== 'info' && info.values.length > 0
        ? el(
            'ul',
            { class: 'explain-values' },
            ...info.values.map((value) =>
              el('li', {}, el('strong', { text: value.name }), value.description ? ` — ${value.description}` : ''),
            ),
          )
        : null,
      info.kind === 'info' ? el('p', { class: 'explain-note', text: t('options.infoNote') }) : null,
      el('p', { class: 'explain-source', text: t('options.source') }),
    );
  };

  const root = el(
    'div',
    { class: 'location-popover explain-popover', role: 'dialog', 'aria-label': () => t('options.explainTitle', option()) },
    el(
      'div',
      { class: 'location-head' },
      el('span', { class: 'location-lead' }, el('span', { class: 'location-title', text: () => t('options.explainTitle', option()) })),
      close,
    ),
    body,
  );

  const reposition = (): void => {
    const entry = store.explain.peek();
    const panel = root.closest<HTMLElement>('.panel');
    if (!entry || !panel) return;
    const anchor = panel.querySelector<HTMLElement>(
      `[data-turn="${CSS.escape(entry.turnId)}"] .cmd-card[data-cmd="${CSS.escape(entry.name)}"] .cmd-opt[data-opt="${CSS.escape(entry.option)}"] .cmd-opt-explain`,
    );
    // The button is gone (the turn was removed, or the card no longer lists it): nothing to point at.
    if (!anchor) {
      closePopover(ctx);
      return;
    }
    placeBeside(root, anchor);
  };

  // Every frame, because streaming rebuilds the card and autoscroll moves it.
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
