import { el } from '../core/dom.js';
import { t } from '../i18n/t.js';
import type { PanelContext } from './context.js';

// Plan 022 / ADR-0008: the card under a command's chip. In this plan it holds only the Run button (plan 023 adds
// the options). The host decides whether a command may be run; the panel only asks (command.check) and shows the
// answer. Run is sent only by the student's click. The card is rebuilt on every render, so nothing lives in the DOM.
export function commandCard(ctx: PanelContext, name: string): Node {
  ctx.requestCommandCheck(name);
  const run = ctx.store.runnable.peek().get(name);

  if (run === 'ok') {
    const button = el('button', { class: 'cmd-run', type: 'button', text: t('run.button', name) });
    button.addEventListener('click', () => ctx.runCommand(name));
    return el('div', { class: 'cmd-card' }, button);
  }
  // Blocked, or unknown to Rhino: no Run here. The specific reason only comes as a notice if Run is attempted.
  if (run === 'blocked' || run === 'unknown') {
    return el('div', { class: 'cmd-card' }, el('p', { class: 'cmd-card-note', text: t('run.blocked') }));
  }
  // Not answered yet: an empty card keeps the layout stable.
  return el('div', { class: 'cmd-card' });
}
