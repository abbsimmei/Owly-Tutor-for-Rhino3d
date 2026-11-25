// Plan 026 / ADR-0009: the strip of owls on the right edge of the transcript. Each owl is one saved
// conversation of the current project, newest on top, the open one outlined. A click continues that
// conversation; a right-click opens a small in-panel menu (Rename, Delete). Every change goes to the
// host, which checks the ids and answers with a fresh `conversations` event.
//
// The outline is on the <button>, never on the owl: art/owly*.png is the team's and `.owl` gets no
// background, filter, blend mode or rounded clipping (see owl.ts).

import { bind, each, el, when } from '../core/dom.js';
import type { Child } from '../core/dom.js';
import { signal } from '../core/signal.js';
import { locale, t } from '../i18n/t.js';
import type { ConversationItem } from '../protocol/events.js';
import type { PanelContext } from './context.js';
import { icon } from './icons.js';
import { owl } from './owl.js';
import { nameField } from './projectMenu.js';

/** Matches Conversation.MaxTitleLength on the host, which checks again. */
export const MAX_CONVERSATION_TITLE = 80;

/** "{title} · {date}", the date in the panel's language. A start time that can't be read leaves just the title. */
export function conversationTooltip(item: ConversationItem): string {
  const started = new Date(item.startedAt);
  if (Number.isNaN(started.getTime())) return item.title;
  const date = new Intl.DateTimeFormat(locale(), { dateStyle: 'medium', timeStyle: 'short' }).format(started);
  return `${item.title} · ${date}`;
}

export function conversationStrip(ctx: PanelContext): Child {
  const { store, ui } = ctx;

  const items = (): readonly ConversationItem[] => {
    const view = store.conversations();
    return view !== null && view.projectId !== null ? view.items : [];
  };

  const item = (entry: ConversationItem): Child =>
    el(
      'button',
      {
        class: 'convo-item',
        type: 'button',
        title: conversationTooltip(entry),
        'aria-label': entry.title,
        'aria-current': entry.current ? 'true' : undefined,
        onClick: () => {
          if (!entry.current) ctx.send({ type: 'conversation.open', sessionId: entry.sessionId });
        },
        onContextMenu: (event: MouseEvent) => {
          // The host's own right-click menu (zoom, copy) would open as well; this menu is the answer here.
          event.preventDefault();
          event.stopPropagation();
          ui.conversationMenu.set({
            sessionId: entry.sessionId,
            title: entry.title,
            anchor: event.currentTarget as HTMLElement,
          });
          ui.overlay.set('conversation');
        },
      },
      owl(ctx, 28),
    );

  return when(
    () => items().length > 0,
    () =>
      el(
        'nav',
        { class: 'convo-strip', 'aria-label': () => t('convo.strip') },
        // A row is rebuilt when anything shown on it changes, which keeps the outline and tooltip current.
        each(
          items,
          (entry) => `${entry.sessionId}|${entry.current}|${entry.title}|${entry.startedAt}`,
          item,
        ),
      ),
  );
}

type Mode = 'main' | 'rename' | 'delete';

/** Puts the menu just left of the strip, level with the clicked owl, inside the panel. The panel carries a CSS zoom, so this is worked out in its own units. */
function place(root: HTMLElement, anchor: HTMLElement): void {
  const panel = root.closest<HTMLElement>('.panel');
  if (!panel || !anchor.isConnected) return;
  const pr = panel.getBoundingClientRect();
  const scale = pr.width / panel.offsetWidth || 1;
  const ar = anchor.getBoundingClientRect();
  const width = root.offsetWidth;
  const height = root.offsetHeight;
  const left = Math.max(0, (ar.left - pr.left) / scale - width - 4);
  const top = Math.max(0, Math.min((ar.top - pr.top) / scale, panel.offsetHeight - height - 4));
  root.style.left = `${Math.round(left)}px`;
  root.style.top = `${Math.round(top)}px`;
}

/** Mounted while overlay is 'conversation' and a target is set; see app.ts. */
export function conversationMenu(ctx: PanelContext): Child {
  const { ui } = ctx;
  const target = ui.conversationMenu.peek();
  if (target === null) return null;
  const mode = signal<Mode>('main');

  const send = (command: Parameters<PanelContext['send']>[0]): void => {
    ctx.send(command);
    ui.closeOverlay();
  };
  const back = (): void => mode.set('main');

  const action = (name: 'rename' | 'delete', iconName: 'settings' | 'close'): Child =>
    el(
      'button',
      { class: `menu-item convo-${name}`, type: 'button', onClick: () => mode.set(name) },
      icon(iconName, 15),
      el('span', { class: 'body' }, el('b', { text: () => t(name === 'rename' ? 'convo.rename' : 'convo.delete') })),
    );

  const body = (): Child => {
    switch (mode()) {
      case 'rename':
        return [
          el('div', { class: 'menu-head', text: () => t('convo.rename') }),
          nameField(
            target.title,
            'project.save',
            (title) => send({ type: 'conversation.rename', sessionId: target.sessionId, title }),
            back,
            { maxLength: MAX_CONVERSATION_TITLE, placeholder: 'convo.titlePlaceholder' },
          ),
        ];
      case 'delete':
        return el(
          'div',
          { class: 'project-form project-confirm' },
          el('p', { text: () => t('convo.deleteConfirm', target.title) }),
          el(
            'div',
            { class: 'ask-row' },
            el('span', { class: 'spacer' }),
            el('button', { class: 'btn ghost', type: 'button', text: () => t('project.cancel'), onClick: back }),
            el('button', {
              class: 'btn danger',
              type: 'button',
              text: () => t('convo.delete'),
              onClick: () => send({ type: 'conversation.delete', sessionId: target.sessionId }),
            }),
          ),
        );
      default:
        return [action('rename', 'settings'), action('delete', 'close')];
    }
  };

  const root = el('div', { class: 'convo-menu', role: 'menu', 'aria-label': () => t('convo.strip') }, body);
  // After the first render, and again whenever the content changes size (rename and delete are taller).
  bind(() => {
    mode();
    queueMicrotask(() => place(root, target.anchor));
  });
  return root;
}
