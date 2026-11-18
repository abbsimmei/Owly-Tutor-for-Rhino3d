import { el, when } from '../core/dom.js';
import type { Child } from '../core/dom.js';
import type { Signal } from '../core/signal.js';
import { formatDuration } from '../state/format.js';
import { iconForTool } from '../state/tools.js';
import type { ToolCall } from '../protocol/events.js';
import type { PanelContext } from './context.js';
import { icon } from './icons.js';

export function toolCard(_ctx: PanelContext, call: Signal<ToolCall>): Child {
  const mark = iconForTool(call.peek().name);
  const running = () => call().status === 'running';

  return el(
    'div',
    { class: () => `tool ${call().status}` },
    el(
      'div',
      { class: 'tool-head' },
      () =>
        running()
          ? el('span', { class: 'spinner' })
          : el('span', { class: 'fam' }, icon(mark, 14)),
      el('span', { class: 'title', text: () => call().title }),
      when(
        () => call().durationMs !== undefined,
        () => el('span', { class: 'dur', text: () => formatDuration(call().durationMs ?? 0) }),
      ),
    ),
  );
}
