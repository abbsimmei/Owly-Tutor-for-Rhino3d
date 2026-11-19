import { bind, el } from '../core/dom.js';
import type { Child } from '../core/dom.js';
import { renderMarkdown, type MarkdownOptions } from '../core/markdown.js';
import type { Signal } from '../core/signal.js';
import { t } from '../i18n/t.js';
import type { PanelContext } from './context.js';
import { icon } from './icons.js';

/**
 * Markdown re-rendered on the animation frame after a delta lands, so a fast stream costs one
 * render per frame rather than one per token. The Eto panel had to grow a bubble, re-measure its
 * wrapped height and re-pin its width on every chunk; here the browser does all of that.
 */
export function agentMessage(ctx: PanelContext, text: Signal<string>, streaming: () => boolean): Child {
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
    },
  };
  let queued = false;

  bind(() => {
    text();
    if (queued) return;
    queued = true;
    requestAnimationFrame(() => {
      queued = false;
      host.replaceChildren(renderMarkdown(text.peek(), options));
    });
  });

  return host;
}
