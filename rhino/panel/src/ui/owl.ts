import { el } from '../core/dom.js';
import type { Child } from '../core/dom.js';
import type { PanelContext } from './context.js';
import owlyLarge from '../../../../art/owly.png';
import owlyLargeDark from '../../../../art/owly-dark.png';
import owlySmall from '../../../../art/owly-small.png';
import owlySmallDark from '../../../../art/owly-small-dark.png';

/**
 * Owly's owl (plan 010). The team's art is used as-is; replacing art/owly*.png with transparent
 * PNGs must need no code change, so never give `.owl` a background, filter, blend mode or rounded
 * clipping, and never wrap it in a coloured backdrop.
 *
 * Decorative: it always sits next to text that already says "Owly", so it is hidden from screen readers.
 */
export function owl(ctx: PanelContext, size: number, extraClass?: string): Child {
  const small = size <= 32;
  return el('img', {
    class: extraClass ? `owl ${extraClass}` : 'owl',
    alt: '',
    'aria-hidden': 'true',
    draggable: 'false',
    width: size,
    height: size,
    src: () =>
      ctx.store.scheme() === 'dark'
        ? small ? owlySmallDark : owlyLargeDark
        : small ? owlySmall : owlyLarge,
  });
}
