// Plan 025: asks which project an unknown file belongs to, once the student has sent a question.
// It never blocks the question, which has already gone. Choosing sends project.choose or
// project.create; the card goes away when the host reports the file is no longer pending.

import { el, when } from '../core/dom.js';
import type { Child } from '../core/dom.js';
import { signal } from '../core/signal.js';
import { t } from '../i18n/t.js';
import type { ProjectInfo } from '../protocol/events.js';
import type { PanelContext } from './context.js';
import { icon } from './icons.js';
import { nameField } from './projectMenu.js';

export function projectCard(ctx: PanelContext): Child {
  const { store } = ctx;

  const showing = (): boolean => store.project()?.pending === true && store.turns().length > 0;

  return when(showing, () => {
    const creating = signal(false);

    const projects = (): readonly ProjectInfo[] => store.project()?.projects ?? [];
    const suggested = (): ProjectInfo | null => {
      const id = store.project()?.suggestion ?? null;
      return projects().find((p) => p.id === id && !p.builtin) ?? null;
    };
    const rest = (): readonly ProjectInfo[] => projects().filter((p) => !p.builtin && p.id !== suggested()?.id);
    const unsorted = (): ProjectInfo | null => projects().find((p) => p.builtin) ?? null;

    const choice = (project: ProjectInfo, hint: string | null, extra = ''): Child =>
      el(
        'button',
        {
          class: `project-choice${extra}`,
          type: 'button',
          onClick: () => ctx.send({ type: 'project.choose', id: project.id }),
        },
        el('b', { text: project.name }),
        hint === null ? null : el('span', { class: 'hint', text: hint }),
      );

    return el(
      'div',
      { class: 'project-card', role: 'group', 'aria-label': () => t('project.cardTitle') },
      el('div', { class: 'project-card-title', text: () => t('project.cardTitle') }),
      el('p', { text: () => t('project.cardBody') }),
      el(
        'div',
        { class: 'project-choices' },
        () => {
          const first = suggested();
          return first === null ? null : choice(first, t('project.suggestedHint'), ' suggested');
        },
        () => rest().map((project) => choice(project, null)),
        when(
          () => !creating(),
          () =>
            el(
              'button',
              { class: 'project-choice project-new', type: 'button', onClick: () => creating.set(true) },
              icon('plus', 13),
              el('b', { text: () => t('project.new') }),
            ),
        ),
        () => {
          const project = unsorted();
          return project === null ? null : choice(project, t('project.unsortedHint'), ' unsorted');
        },
      ),
      when(
        creating,
        () =>
          nameField(
            '',
            'project.create',
            (name) => ctx.send({ type: 'project.create', name }),
            () => creating.set(false),
          ),
      ),
    );
  });
}
