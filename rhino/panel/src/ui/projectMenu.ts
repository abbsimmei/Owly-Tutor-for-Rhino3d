// Plan 025: the header's project menu. Switch, create, rename, delete, and move this file to
// another project. Every change goes to the host, which answers with a fresh `project` event.

import { el, when } from '../core/dom.js';
import type { Child } from '../core/dom.js';
import { signal } from '../core/signal.js';
import { t } from '../i18n/t.js';
import type { StringKey } from '../i18n/strings.js';
import type { PanelCommand, ProjectInfo } from '../protocol/events.js';
import type { PanelContext } from './context.js';
import type { IconName } from './icons.js';
import { icon } from './icons.js';

/** Matches ProjectRegistry.MaxNameLength on the host, which checks again. */
export const MAX_PROJECT_NAME = 60;

type Mode = 'main' | 'new' | 'rename' | 'delete' | 'move';

/** An inline name field with Cancel and a submit button. Enter submits; the host validates again. */
export function nameField(
  initial: string,
  submitKey: StringKey,
  onSubmit: (name: string) => void,
  onCancel: () => void,
): Child {
  const draft = signal(initial);
  const submit = (): void => {
    const name = draft.peek().trim();
    if (name.length > 0) onSubmit(name);
  };
  return el(
    'div',
    { class: 'project-form' },
    el('input', {
      type: 'text',
      class: 'project-name-field',
      maxlength: MAX_PROJECT_NAME,
      placeholder: () => t('project.namePlaceholder'),
      'aria-label': () => t('project.namePlaceholder'),
      value: initial,
      ref: (node: HTMLInputElement) => queueMicrotask(() => node.focus()),
      onInput: (event: Event) => draft.set((event.target as HTMLInputElement).value),
      onKeyDown: (event: KeyboardEvent) => {
        if (event.key !== 'Enter' || event.isComposing) return;
        event.preventDefault();
        submit();
      },
    }),
    el(
      'div',
      { class: 'ask-row' },
      el('span', { class: 'spacer' }),
      el('button', { class: 'btn ghost', type: 'button', text: () => t('project.cancel'), onClick: onCancel }),
      el('button', {
        class: 'btn primary',
        type: 'button',
        disabled: () => draft().trim().length === 0,
        text: () => t(submitKey),
        onClick: submit,
      }),
    ),
  );
}

export function projectMenu(ctx: PanelContext): Child {
  const { store, ui } = ctx;
  const mode = signal<Mode>('main');

  const projects = (): readonly ProjectInfo[] => store.project()?.projects ?? [];
  const currentId = (): string | null => store.project()?.id ?? null;
  const current = (): ProjectInfo | null => projects().find((p) => p.id === currentId()) ?? null;
  const editable = (): boolean => {
    const project = current();
    return project !== null && !project.builtin;
  };
  const others = (): readonly ProjectInfo[] => projects().filter((p) => p.id !== currentId());

  const send = (command: PanelCommand): void => {
    ctx.send(command);
    ui.closeOverlay();
  };
  const back = (): void => mode.set('main');

  const row = (project: ProjectInfo, onPick: (project: ProjectInfo) => void): Child =>
    el(
      'button',
      {
        class: () => (project.id === currentId() ? 'menu-item project-item selected' : 'menu-item project-item'),
        type: 'button',
        onClick: () => onPick(project),
      },
      el('span', { class: 'body' }, el('b', { text: project.name })),
      when(
        () => project.id === currentId(),
        () => el('span', { class: 'trail' }, icon('check', 14)),
      ),
    );

  const action = (name: IconName, key: StringKey, onClick: () => void, enabled: () => boolean = () => true): Child =>
    el(
      'button',
      { class: `menu-item project-${key.slice('project.'.length)}`, type: 'button', disabled: () => !enabled(), onClick },
      icon(name, 15),
      el('span', { class: 'body' }, el('b', { text: () => t(key) })),
    );

  const main = (): Child => [
    el('div', { class: 'menu-head', text: () => t('project.switch') }),
    () => projects().map((project) => row(project, (picked) => send({ type: 'project.choose', id: picked.id }))),
    el('div', { class: 'menu-sep' }),
    action('plus', 'project.new', () => mode.set('new')),
    action('settings', 'project.rename', () => mode.set('rename'), editable),
    action('close', 'project.delete', () => mode.set('delete'), editable),
    action('chevron', 'project.move', () => mode.set('move'), () => current() !== null),
  ];

  const create = (): Child => [
    el('div', { class: 'menu-head', text: () => t('project.new') }),
    nameField('', 'project.create', (name) => send({ type: 'project.create', name }), back),
  ];

  // Read once, not tracked: a project event arriving while the field is open must not wipe what was typed.
  const currentNow = (): ProjectInfo | null => {
    const view = store.project.peek();
    return view?.projects.find((p) => p.id === view.id) ?? null;
  };

  const rename = (): Child => {
    const project = currentNow();
    if (project === null) return null;
    return [
      el('div', { class: 'menu-head', text: () => t('project.rename') }),
      nameField(project.name, 'project.save', (name) => send({ type: 'project.rename', id: project.id, name }), back),
    ];
  };

  const remove = (): Child => {
    const project = currentNow();
    if (project === null) return null;
    return el(
      'div',
      { class: 'project-form project-confirm' },
      el('p', { text: () => t('project.deleteConfirm', project.name) }),
      el(
        'div',
        { class: 'ask-row' },
        el('span', { class: 'spacer' }),
        el('button', { class: 'btn ghost', type: 'button', text: () => t('project.cancel'), onClick: back }),
        el('button', {
          class: 'btn danger',
          type: 'button',
          text: () => t('project.delete'),
          onClick: () => send({ type: 'project.delete', id: project.id }),
        }),
      ),
    );
  };

  const move = (): Child => [
    el('div', { class: 'menu-head', text: () => t('project.moveHead') }),
    () =>
      others().length === 0
        ? el('div', { class: 'project-empty', text: () => t('project.noOther') })
        : others().map((project) => row(project, (picked) => send({ type: 'project.move', id: picked.id }))),
    el('div', { class: 'menu-sep' }),
    el(
      'button',
      { class: 'menu-item', type: 'button', onClick: back },
      el('span', { class: 'body' }, el('b', { text: () => t('project.cancel') })),
    ),
  ];

  return el(
    'div',
    { class: 'popover project-menu', role: 'menu' },
    () => {
      switch (mode()) {
        case 'new':
          return create();
        case 'rename':
          return rename();
        case 'delete':
          return remove();
        case 'move':
          return move();
        default:
          return main();
      }
    },
  );
}
