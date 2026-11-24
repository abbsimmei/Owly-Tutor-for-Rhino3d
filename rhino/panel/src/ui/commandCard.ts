import { el } from '../core/dom.js';
import { t } from '../i18n/t.js';
import type { CommandOptionInfo } from '../protocol/events.js';
import type { CardValue } from '../state/store.js';
import type { PanelContext } from './context.js';
import { icon } from './icons.js';

// Plan 022 / ADR-0008, Plan 023: the card under a command's chip. It lists the command's options from McNeel's help
// page (fetched by the host) and the Run button. The host decides whether a command may be run and which options it can
// write; the panel only asks (command.check, command.options) and shows the answers. Run is sent only by the student's
// click, and carries only the values the student set or accepted. The card is rebuilt on every render, so nothing lives
// in the DOM: the values live in store.cardValues.

/** The value an option shows: only choice/toggle options, only a value the option really has, spelt as the option spells it. */
function effectiveValue(info: CommandOptionInfo, values: ReadonlyMap<string, CardValue> | undefined): CardValue | undefined {
  if (info.kind === 'info' || !values) return undefined;
  const lower = info.name.toLowerCase();
  for (const [key, entry] of values) {
    if (key.toLowerCase() !== lower) continue;
    const match = info.values.find((value) => value.name.toLowerCase() === entry.value.toLowerCase());
    return match ? { value: match.name, suggested: entry.suggested } : undefined;
  }
  return undefined;
}

export function commandCard(
  ctx: PanelContext,
  turnId: string,
  name: string,
  suggested: Readonly<Record<string, string>>,
): Node {
  const { store } = ctx;
  ctx.requestCommandCheck(name);
  ctx.requestCommandOptions(name);
  const key = `${turnId}|${name}`;
  store.seedCard(key, suggested);

  const run = store.runnable.peek().get(name);
  const infos = store.commandOptions.peek().get(name) ?? [];
  const values = store.cardValues.peek().get(key);
  // Only a command the host lets Run start can have its options set; blocked or unknown ones show them read-only.
  const editable = run === 'ok';

  const rows: HTMLElement[] = [];
  const setValues: Record<string, string> = {};
  for (const info of infos) {
    const current = editable ? effectiveValue(info, values) : undefined;
    if (current) setValues[info.name] = current.value;

    const row = el('div', { class: 'cmd-opt', 'data-opt': info.name }, el('span', { class: 'cmd-opt-name', text: info.name }));
    if (editable && info.kind !== 'info') {
      for (const value of info.values) {
        const selected = current?.value === value.name;
        const button = el('button', {
          class: 'cmd-opt-value',
          type: 'button',
          text: value.name,
          'aria-pressed': selected ? 'true' : 'false',
        });
        button.addEventListener('click', () => store.setCardValue(key, info.name, selected ? null : value.name));
        row.appendChild(button);
      }
      if (current?.suggested) row.appendChild(el('span', { class: 'cmd-opt-suggested', text: t('options.suggested') }));
    }
    const explain = el(
      'button',
      { class: 'cmd-opt-explain', type: 'button', 'aria-label': t('options.explain', info.name) },
      icon('question', 12),
    );
    explain.addEventListener('click', () => {
      store.openExplain(turnId, name, info.name);
      ctx.ui.overlay.set('explain');
    });
    row.appendChild(explain);
    rows.push(row);
  }

  const card = el('div', { class: 'cmd-card', 'data-cmd': name });
  if (infos.length === 0 && store.optionsLookingUp(name))
    card.appendChild(el('p', { class: 'cmd-card-note', text: t('options.looking') }));
  for (const row of rows) card.appendChild(row);
  if (Object.keys(setValues).length > 0) card.appendChild(el('p', { class: 'cmd-card-note', text: t('options.hint') }));

  if (run === 'ok') {
    const button = el('button', { class: 'cmd-run', type: 'button', text: t('run.button', name) });
    button.addEventListener('click', () => ctx.runCommand(name, setValues));
    card.appendChild(button);
  } else if (run === 'blocked' || run === 'unknown') {
    // No Run here. The specific reason only comes as a notice if Run is attempted.
    card.appendChild(el('p', { class: 'cmd-card-note', text: t('run.blocked') }));
  }
  // Not answered yet: the card stays as it is, so the layout is stable.
  return card;
}
