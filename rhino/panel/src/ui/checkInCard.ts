// Plan 030: the level check-in under an answer. The host decides when to ask (checkin event); the card only
// reports the verdict and, if the student clicks Switch, asks for one step up or down (level.set). Nothing
// changes unless the student clicks. Not saved: a reload or a reopened conversation shows no card.

import { el, when } from '../core/dom.js';
import type { Child } from '../core/dom.js';
import { t } from '../i18n/t.js';
import type { StringKey } from '../i18n/strings.js';
import type { TurnView } from '../state/store.js';
import type { PanelContext } from './context.js';
import { icon } from './icons.js';

const LEVELS = ['Beginner', 'Intermediate', 'Advanced'] as const;
type Level = (typeof LEVELS)[number];

function levelName(level: Level): string {
  return t(`level.${level}` as StringKey);
}

export function checkInCard(ctx: PanelContext, turn: TurnView): Child {
  const { store } = ctx;

  const showing = (): boolean => {
    const state = store.checkIn();
    return state !== null && state.turnId === turn.id && turn.status() !== 'running';
  };

  return when(showing, () => {
    const close = (): void => store.checkIn.set(null);

    // Too easy offers one level up, Too hard one level down; at the end of the ladder there is nothing to offer.
    const answer = (verdict: 'good' | 'easy' | 'hard'): void => {
      const state = store.checkIn.peek();
      if (state === null) return;
      ctx.send({ type: 'checkin.answer', verdict });
      if (verdict === 'good') return close();
      const index = LEVELS.indexOf(state.level);
      const target = LEVELS[verdict === 'easy' ? index + 1 : index - 1];
      store.checkIn.set(
        target === undefined ? { ...state, step: 'thanks' } : { ...state, step: 'offer', target },
      );
    };

    const choose = (verdict: 'good' | 'easy' | 'hard', key: StringKey): Child =>
      el('button', { class: 'btn', type: 'button', text: () => t(key), onClick: () => answer(verdict) });

    const body = (): Child => {
      const state = store.checkIn();
      if (state === null) return null;
      if (state.step === 'ask')
        return [
          el('span', { class: 'checkin-text', text: () => t('checkin.ask') }),
          el(
            'div',
            { class: 'checkin-buttons' },
            choose('good', 'checkin.good'),
            choose('easy', 'checkin.easy'),
            choose('hard', 'checkin.hard'),
          ),
        ];
      if (state.step === 'offer' && state.target !== undefined) {
        const target = state.target;
        return [
          el('span', { class: 'checkin-text', text: () => t('checkin.offer', levelName(target)) }),
          el(
            'div',
            { class: 'checkin-buttons' },
            el('button', {
              class: 'btn primary checkin-switch',
              type: 'button',
              text: () => t('checkin.switch'),
              onClick: () => {
                ctx.send({ type: 'level.set', level: target });
                close();
              },
            }),
            el('button', { class: 'btn checkin-no', type: 'button', text: () => t('checkin.no'), onClick: close }),
          ),
        ];
      }
      return el('span', { class: 'checkin-text checkin-thanks', text: () => t('checkin.thanks') });
    };

    return el(
      'div',
      { class: 'checkin-card', role: 'group', 'aria-label': () => t('checkin.ask') },
      el('div', { class: 'checkin-body' }, body),
      el(
        'button',
        { class: 'checkin-close', type: 'button', 'aria-label': () => t('checkin.close'), title: () => t('checkin.close'), onClick: close },
        icon('close', 12),
      ),
    );
  });
}
