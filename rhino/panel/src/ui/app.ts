import { bind, el, onCleanup, when } from '../core/dom.js';
import type { Child } from '../core/dom.js';
import { signal } from '../core/signal.js';
import { BUSY_KEYS, busyWord } from '../state/busy.js';
import { formatElapsed } from '../state/format.js';
import { t } from '../i18n/t.js';
import { agentMenu } from './agentMenu.js';
import { composer } from './composer.js';
import { hostMenu } from './hostMenu.js';
import type { PanelContext } from './context.js';
import { header } from './header.js';
import { questionOverlay } from './question.js';
import { owl } from './owl.js';
import { projectCard } from './projectCard.js';
import { projectMenu } from './projectMenu.js';
import { transcript } from './transcript.js';

function statusStrip(ctx: PanelContext): Child {
  const { store } = ctx;
  const now = signal(Date.now());
  let offset = 0;

  // One clock for the whole strip, so the word and the counter can never disagree about the turn.
  bind(() => {
    if (!store.thinking()) return;
    offset = Math.floor(Math.random() * BUSY_KEYS.length);
    now.set(Date.now());
    const timer = setInterval(() => now.set(Date.now()), 1000);
    return () => clearInterval(timer);
  });

  const elapsed = (): number | null => {
    const startedAt = store.currentTurn()?.startedAt;
    const started = startedAt === undefined ? Number.NaN : Date.parse(startedAt);
    return Number.isNaN(started) ? null : Math.max(0, now() - started);
  };

  const hosted = () => store.status();
  const text = () => {
    const hostedText = hosted();
    if (hostedText !== null) return hostedText;
    if (!store.thinking()) return null;
    const key = busyWord(elapsed() ?? 0, offset);
    return key === null ? null : `${t(key)}…`;
  };
  const counting = () => store.thinking() && elapsed() !== null;

  return when(
    () => text() !== null,
    () =>
      el(
        'div',
        { class: 'status-strip', role: 'status' },
        owl(ctx, 16, 'spark'),
        el('span', {
          class: 'shimmer',
          'aria-hidden': () => (hosted() === null ? 'true' : false),
          text: () => text() ?? '',
        }),
        el('span', { class: 'sr-only', text: () => (hosted() === null ? t('status.workingForScreenReader') : '') }),
        el('span', { class: 'spacer' }),
        when(
          counting,
          () =>
            el('span', {
              class: 'elapsed',
              'aria-hidden': 'true',
              text: () => formatElapsed(elapsed() ?? 0),
            }),
        ),
      ),
  );
}

export function app(ctx: PanelContext): Child {
  const { ui } = ctx;

  // One step per gesture, not per wheel tick: a trackpad pinch fires dozens of events.
  let lastWheelStep = 0;

  const onKeyDown = (event: KeyboardEvent): void => {
    const mod = event.metaKey || event.ctrlKey;

    // Match against code as well as key: on several layouts "+" needs Shift, and the numpad
    // reports different key values again.
    if (mod) {
      const zoomIn = event.key === '+' || event.key === '=' || event.code === 'Equal' || event.code === 'NumpadAdd';
      const zoomOut = event.key === '-' || event.key === '_' || event.code === 'Minus' || event.code === 'NumpadSubtract';
      const zoomReset = event.key === '0' || event.code === 'Digit0' || event.code === 'Numpad0';

      if (zoomIn || zoomOut || zoomReset) {
        event.preventDefault();
        if (zoomIn) ctx.zoom.in();
        else if (zoomOut) ctx.zoom.out();
        else ctx.zoom.reset();
        return;
      }
    }

    if (event.key === 'Escape' && ui.overlay() !== 'none') {
      ui.closeOverlay();
      event.preventDefault();
      return;
    }
    if (mod && event.shiftKey && event.key.toLowerCase() === 'n') {
      ctx.send({ type: 'conversation.new' });
      event.preventDefault();
    }
  };

  const onWheel = (event: WheelEvent): void => {
    if (!event.ctrlKey && !event.metaKey) return;
    event.preventDefault();
    const now = Date.now();
    if (now - lastWheelStep < 110) return;
    lastWheelStep = now;
    if (event.deltaY < 0) ctx.zoom.in();
    else if (event.deltaY > 0) ctx.zoom.out();
  };

  // Both on the window rather than the panel element. After a menu item is clicked its button is
  // gone and focus falls back to the body, whose key events never reach a handler bound to the
  // panel div; and inside Rhino the webview is the panel, so there is nowhere else for a wheel
  // gesture to land. passive:false is required or preventDefault is ignored and the page scrolls.
  hostMenu(ctx);
  window.addEventListener('keydown', onKeyDown);
  window.addEventListener('wheel', onWheel, { passive: false });
  onCleanup(() => {
    window.removeEventListener('keydown', onKeyDown);
    window.removeEventListener('wheel', onWheel);
  });

  return el(
    'div',
    {
      class: 'panel',
      ref: (node: HTMLElement) => ctx.zoom.attach(node),
    },
    header(ctx),
    transcript(ctx),
    // Plan 025: asks for an unknown file's project above the composer; the question has already gone.
    projectCard(ctx),
    // Below the transcript, not above it: what the agent is doing now belongs next to where its
    // output is landing and next to the Stop button, not up by the agent picker.
    statusStrip(ctx),
    composer(ctx),
    questionOverlay(ctx),
    when(
      () => ui.overlay() === 'agents',
      () => [el('div', { class: 'scrim', onClick: () => ui.closeOverlay() }), agentMenu(ctx)],
    ),
    when(
      () => ui.overlay() === 'project',
      () => [el('div', { class: 'scrim', onClick: () => ui.closeOverlay() }), projectMenu(ctx)],
    ),
  );
}
