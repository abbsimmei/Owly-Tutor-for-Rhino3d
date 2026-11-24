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

// Plan 024: a number is written as a plain number only (the host checks it again).
const NUMBER = /^-?\d+(\.\d+)?$/;
const MAX_NUMBER = 15;

function isNumber(text: string): boolean {
  return text.length <= MAX_NUMBER && NUMBER.test(text);
}

/**
 * The value an option shows: only choice/toggle options and a value the option really has, spelt as the option spells it,
 * or (plan 024) a number option with a plain number. Info options never have one.
 */
function effectiveValue(info: CommandOptionInfo, values: ReadonlyMap<string, CardValue> | undefined): CardValue | undefined {
  if (info.kind === 'info' || !values) return undefined;
  const lower = info.name.toLowerCase();
  for (const [key, entry] of values) {
    if (key.toLowerCase() !== lower) continue;
    if (info.kind === 'number') return isNumber(entry.value.trim()) ? { value: entry.value.trim(), suggested: entry.suggested } : undefined;
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

  // What Run sends: only what the student set or accepted. Read from the store when Run is clicked, because a typed
  // number is stored without a redraw (see store.commitCardNumber).
  const collect = (): Record<string, string> => {
    const out: Record<string, string> = {};
    if (!editable) return out;
    const latest = store.cardValues.peek().get(key);
    for (const info of infos) {
      const set = effectiveValue(info, latest);
      if (set) out[info.name] = set.value;
    }
    return out;
  };

  const rows: HTMLElement[] = [];
  let anySet = false;
  for (const info of infos) {
    const current = editable ? effectiveValue(info, values) : undefined;
    if (current) anySet = true;

    const row = el('div', { class: 'cmd-opt', 'data-opt': info.name }, el('span', { class: 'cmd-opt-name', text: info.name }));
    if (editable && info.kind === 'number') {
      const field = el('input', {
        class: 'cmd-opt-number',
        type: 'text',
        inputmode: 'decimal',
        'aria-label': info.name,
        placeholder: info.current !== undefined ? t('options.currentValue', info.current) : t('options.keep'),
      }) as HTMLInputElement;
      field.value = current?.value ?? '';
      // Commit on change and Enter, never per keystroke: a redraw would destroy the field while the student types.
      const commit = (): void => {
        const text = field.value.trim();
        if (text === '') {
          field.removeAttribute('aria-invalid');
          store.commitCardNumber(key, info.name, null);
        } else if (isNumber(text)) {
          field.removeAttribute('aria-invalid');
          store.commitCardNumber(key, info.name, text);
        } else {
          field.setAttribute('aria-invalid', 'true');
          store.commitCardNumber(key, info.name, null);
        }
      };
      field.addEventListener('change', commit);
      field.addEventListener('keydown', (event) => {
        if (event.key === 'Enter') commit();
      });
      row.appendChild(field);
      if (current?.suggested) row.appendChild(el('span', { class: 'cmd-opt-suggested', text: t('options.suggested') }));
    } else if (editable && info.kind !== 'info') {
      for (const value of info.values) {
        const selected = current?.value === value.name;
        const isNow = info.current !== undefined && info.current.toLowerCase() === value.name.toLowerCase();
        const button = el('button', {
          class: isNow ? 'cmd-opt-value is-current' : 'cmd-opt-value',
          type: 'button',
          text: value.name,
          'aria-pressed': selected ? 'true' : 'false',
          ...(isNow ? { title: t('options.currentValue', info.current as string) } : {}),
        });
        button.addEventListener('click', () => store.setCardValue(key, info.name, selected ? null : value.name));
        row.appendChild(button);
      }
      if (current?.suggested) row.appendChild(el('span', { class: 'cmd-opt-suggested', text: t('options.suggested') }));
    } else if (info.current !== undefined) {
      // Info rows, and every row of a command that can't be run: the value Rhino shows now, read-only.
      row.appendChild(el('span', { class: 'cmd-opt-current', text: t('options.currentValue', info.current) }));
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
  if (anySet) card.appendChild(el('p', { class: 'cmd-card-note', text: t('options.hint') }));

  if (run === 'ok') {
    const button = el('button', { class: 'cmd-run', type: 'button', text: t('run.button', name) });
    button.addEventListener('click', () => ctx.runCommand(name, collect()));
    card.appendChild(button);
  } else if (run === 'blocked' || run === 'unknown') {
    // No Run here. The specific reason only comes as a notice if Run is attempted.
    card.appendChild(el('p', { class: 'cmd-card-note', text: t('run.blocked') }));
  }
  // Not answered yet: the card stays as it is, so the layout is stable.
  return card;
}
