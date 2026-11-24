import { bind, el } from '../core/dom.js';
import type { Child } from '../core/dom.js';
import { renderMarkdown, type MarkdownOptions } from '../core/markdown.js';
import type { Signal } from '../core/signal.js';
import { t } from '../i18n/t.js';
import type { PanelContext } from './context.js';
import { icon } from './icons.js';
import { commandCard } from './commandCard.js';
import { openLocationPopover } from './locationPopover.js';

/**
 * Markdown re-rendered on the animation frame after a delta lands, so a fast stream costs one
 * render per frame rather than one per token. The Eto panel had to grow a bubble, re-measure its
 * wrapped height and re-pin its width on every chunk; here the browser does all of that.
 */
export function agentMessage(
  ctx: PanelContext,
  turnId: string,
  text: Signal<string>,
  streaming: () => boolean,
  seenBefore: () => ReadonlySet<string>,
): Child {
  const host = el('div', {
    class: () => `msg-agent md${streaming() ? ' streaming' : ''}`,
    'aria-live': 'polite',
  });

  const options: MarkdownOptions = {
    copy: ctx.copy,
    openLink: ctx.openLink,
    command: {
      copy: ctx.copyCommand,
      glyph: (name) => icon(name, 12),
      label: (name) => t('command.copyNamed', name),
      locate: (name) => openLocationPopover(ctx, turnId, name),
      locateLabel: (name) => t('command.whereNamed', name),
      icon: (name) => {
        ctx.requestToolbarIcon(name);
        return ctx.store.iconUrl(name);
      },
      seenBefore,
      // Plan 022: the Run card under the block holding a command's chip.
      card: (name, suggested) => commandCard(ctx, turnId, name, suggested),
    },
  };
  let queued = false;

  bind(() => {
    text();
    // Plan 021: a late button image or a theme change redraws the chips.
    ctx.store.icons();
    ctx.store.scheme();
    // Plan 022: a Run answer, and the end of streaming (which releases the open block's cards), redraw the cards.
    ctx.store.runnable();
    // Plan 023: option data and what the card has set redraw the cards.
    ctx.store.commandOptions();
    ctx.store.cardValues();
    streaming();
    if (queued) return;
    queued = true;
    requestAnimationFrame(() => {
      queued = false;
      host.replaceChildren(renderMarkdown(text.peek(), { ...options, growing: streaming() }));
    });
  });

  return host;
}
