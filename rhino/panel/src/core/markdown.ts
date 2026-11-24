// Markdown -> DOM. A deliberate subset: what an agent actually writes into a chat reply.
//
// Never builds an HTML string, so model output cannot inject markup. Unterminated fences render as
// code anyway, which is what a reply still streaming looks like.

import { normaliseCommand } from './commandMentions.js';
import { parseCommandCode } from './commandName.js';
import { highlight, normalizeLanguage } from './highlight.js';

export interface CommandChipOptions {
  /** Called with the command name (`_Loft`) when the student clicks the chip. */
  copy: (name: string) => void;
  /** Builds the chip's glyphs; passed in so core/ does not depend on ui/. */
  glyph: (name: 'copy' | 'check' | 'toolbar') => Node;
  /** Accessible label, e.g. "Copy _Loft". */
  label: (name: string) => string;
  /** When supplied, the chip gets a second button that asks where the command is on the toolbars (plan 015). */
  locate?: (name: string) => void;
  /** Accessible label for that button, e.g. "Where is _Loft on the toolbars?". */
  locateLabel?: (name: string) => string;
  /** Plan 021: a data: URL of the real toolbar button image, or null to keep the generic glyph. */
  icon?: (name: string) => string | null;
  /**
   * Normalised names of commands already used as chips in earlier text blocks of the same turn
   * (plan 019). Only the first mention of a command in an answer becomes a chip.
   */
  seenBefore?: () => ReadonlySet<string>;
  /**
   * Plan 022: builds the options card that sits under the block holding a command's chip (one per chip, so one
   * per command per turn). Passed in so core/ does not depend on ui/. Null draws nothing.
   */
  card?: (name: string, suggested: Readonly<Record<string, string>>) => Node | null;
}

export interface MarkdownOptions {
  /** Rendered as a button on every code block when supplied. */
  copy?: (text: string) => void;
  /** A webview cannot navigate itself, so links are handed back to the host. */
  openLink?: (url: string) => void;
  /** When supplied, inline code that is a Rhino command (`_Loft`) renders as a copy chip. */
  command?: CommandChipOptions;
  /** Internal: commands already given a chip during this render. Set by renderMarkdown. */
  seen?: Set<string>;
  /** Internal: chip names whose card has not been placed yet. Drained by blocks(). Set by renderMarkdown. */
  cards?: { name: string; pairs: Record<string, string> }[];
  /**
   * True while the text is still streaming. The open block is the one the model is still writing, so its cards
   * are held back until the block closes (plan 022).
   */
  growing?: boolean;
}

const FENCE = /^ {0,3}(```+|~~~+)\s*([\w#+.-]*)\s*$/;
const HEADING = /^ {0,3}(#{1,6})\s+(.*?)\s*#*\s*$/;
const RULE = /^ {0,3}(?:-{3,}|\*{3,}|_{3,})\s*$/;
const QUOTE = /^ {0,3}> ?(.*)$/;
const ITEM = /^(\s*)([-*+]|\d+[.)])\s+(.*)$/;
const TABLE_RULE = /^\s*\|?[\s:-]*-[\s:|-]*\|?\s*$/;

const INLINE = new RegExp(
  [
    /`([^`\n]+)`/,
    /\*\*([\s\S]+?)\*\*/,
    /__([\s\S]+?)__/,
    /\*([^*\n]+)\*/,
    /_([^_\n]+)_(?![A-Za-z0-9])/,
    /~~([\s\S]+?)~~/,
    /\[([^\]\n]*)\]\((<[^>\n]*>|[^)\s]+)(?:\s+"[^"\n]*")?\)/,
    /(https?:\/\/[^\s<>()[\]]+)/,
  ]
    .map((r) => `(?:${r.source})`)
    .join('|'),
  'g',
);

function element(tag: string, className?: string): HTMLElement {
  const node = document.createElement(tag);
  if (className) node.className = className;
  return node;
}

function inline(source: string, options: MarkdownOptions): DocumentFragment {
  const fragment = document.createDocumentFragment();
  // A fresh matcher per call: inline() recurses into emphasis and link bodies, and a shared `g`
  // regex would have its lastIndex rewound by the inner call and never terminate.
  const scanner = new RegExp(INLINE.source, 'g');
  let cursor = 0;
  let match: RegExpExecArray | null;

  const push = (text: string) => {
    // A single newline inside a paragraph reads as a break in a chat reply, not a space.
    const parts = text.split('\n');
    parts.forEach((part, index) => {
      if (index > 0) fragment.appendChild(element('br'));
      if (part) fragment.appendChild(document.createTextNode(part));
    });
  };

  while ((match = scanner.exec(source)) !== null) {
    if (match.index > cursor) push(source.slice(cursor, match.index));
    cursor = match.index + match[0].length;

    const [, code, strongStar, strongUnder, emStar, emUnder, strike, linkText, linkHref, bareUrl] = match;

    // Underscore emphasis must not fire mid-identifier (`layer_name_2`). A lookbehind would say this
    // in the pattern, but lookbehind only landed in Safari 16.4 and a module-level RegExp that
    // throws takes the whole panel down, so the boundary check lives here instead.
    if ((strongUnder !== undefined || emUnder !== undefined) && /[\w]/.test(source[match.index - 1] ?? '')) {
      push(match[0]);
      continue;
    }

    if (code !== undefined) {
      // Plan 023: `_Pyramid DirectionConstraint=Vertical` is a chip for `_Pyramid`; the pairs go to its card.
      const parsed = options.command ? parseCommandCode(code) : null;
      const key = normaliseCommand(parsed?.name ?? code);
      if (options.command && parsed && !options.seen?.has(key)) {
        options.seen?.add(key);
        fragment.appendChild(commandChip(parsed.name, options.command));
        options.cards?.push({ name: parsed.name, pairs: parsed.pairs });
      } else {
        const node = element('code');
        node.textContent = code;
        fragment.appendChild(node);
      }
    } else if (strongStar !== undefined || strongUnder !== undefined) {
      const node = element('strong');
      node.appendChild(inline((strongStar ?? strongUnder) as string, options));
      fragment.appendChild(node);
    } else if (emStar !== undefined || emUnder !== undefined) {
      const node = element('em');
      node.appendChild(inline((emStar ?? emUnder) as string, options));
      fragment.appendChild(node);
    } else if (strike !== undefined) {
      const node = element('s');
      node.appendChild(inline(strike, options));
      fragment.appendChild(node);
    } else if (linkHref !== undefined) {
      // The bracketed destination is the only spelling that survives a space, which local paths have.
      const href = linkHref.startsWith('<') && linkHref.endsWith('>') ? linkHref.slice(1, -1) : linkHref;
      fragment.appendChild(anchor(href, linkText || href, options));
    } else if (bareUrl !== undefined) {
      fragment.appendChild(anchor(bareUrl, bareUrl, options));
    }
  }

  if (cursor < source.length) push(source.slice(cursor));
  return fragment;
}

function anchor(href: string, label: string, options: MarkdownOptions): HTMLElement {
  const node = element('a') as HTMLAnchorElement;
  node.href = href;
  node.textContent = label;
  node.addEventListener('click', (event) => {
    event.preventDefault();
    options.openLink?.(href);
  });
  return node;
}

function codeBlock(code: string, language: string, options: MarkdownOptions): HTMLElement {
  const figure = element('figure', 'md-code');
  const caption = element('figcaption');
  const label = element('span', 'md-code-lang');
  label.textContent = normalizeLanguage(language) || 'text';
  caption.appendChild(label);

  if (options.copy) {
    const button = element('button', 'md-code-copy') as HTMLButtonElement;
    button.type = 'button';
    button.textContent = 'Copy';
    button.addEventListener('click', () => {
      options.copy?.(code);
      button.textContent = 'Copied';
      setTimeout(() => { button.textContent = 'Copy'; }, 1200);
    });
    caption.appendChild(button);
  }

  const pre = element('pre');
  const codeNode = element('code');
  codeNode.appendChild(highlight(code, language));
  pre.appendChild(codeNode);

  figure.appendChild(caption);
  figure.appendChild(pre);
  return figure;
}

/** Tables scroll inside their own box rather than widening the transcript. */
function el_wrap(table: HTMLElement): HTMLElement {
  const wrap = element('div', 'md-table');
  wrap.appendChild(table);
  return wrap;
}

/** Places the cards of the chips drawn since the last call, as the next children of `parent`. */
function drainCards(parent: Node, options: MarkdownOptions): void {
  const names = options.cards;
  if (!names || names.length === 0) return;
  for (const { name, pairs } of names.splice(0)) {
    const card = options.command?.card?.(name, pairs);
    if (card) parent.appendChild(card);
  }
}

function indentWidth(line: string): number {
  const match = /^\s*/.exec(line);
  return match ? match[0].replace(/\t/g, '    ').length : 0;
}

function blocks(lines: readonly string[], options: MarkdownOptions): DocumentFragment {
  const fragment = document.createDocumentFragment();
  let index = 0;

  while (index < lines.length) {
    const line = lines[index] as string;

    if (line.trim() === '') {
      index++;
      continue;
    }

    const fence = FENCE.exec(line);
    if (fence) {
      const marker = fence[1] as string;
      const body: string[] = [];
      index++;
      while (index < lines.length && !(lines[index] as string).trimStart().startsWith(marker)) {
        body.push(lines[index] as string);
        index++;
      }
      index++; // closing fence, or past the end while still streaming
      fragment.appendChild(codeBlock(body.join('\n'), fence[2] ?? '', options));
      continue;
    }

    const heading = HEADING.exec(line);
    if (heading) {
      const node = element(`h${(heading[1] as string).length}`);
      node.appendChild(inline(heading[2] as string, options));
      fragment.appendChild(node);
      drainCards(fragment, options);
      index++;
      continue;
    }

    if (RULE.test(line)) {
      fragment.appendChild(element('hr'));
      index++;
      continue;
    }

    if (QUOTE.test(line)) {
      const body: string[] = [];
      while (index < lines.length) {
        const quoted = QUOTE.exec(lines[index] as string);
        if (!quoted) break;
        body.push(quoted[1] as string);
        index++;
      }
      const node = element('blockquote');
      node.appendChild(blocks(body, options));
      fragment.appendChild(node);
      continue;
    }

    const nextLine = lines[index + 1];
    if (line.includes('|') && nextLine !== undefined && nextLine.includes('-') && TABLE_RULE.test(nextLine)) {
      const cells = (row: string) =>
        row.replace(/^\s*\|/, '').replace(/\|\s*$/, '').split('|').map((cell) => cell.trim());
      const table = element('table');
      const head = element('thead');
      const headRow = element('tr');
      for (const cell of cells(line)) {
        const th = element('th');
        th.appendChild(inline(cell, options));
        headRow.appendChild(th);
      }
      head.appendChild(headRow);
      table.appendChild(head);

      const body = element('tbody');
      index += 2;
      while (index < lines.length && (lines[index] as string).includes('|')) {
        const tr = element('tr');
        for (const cell of cells(lines[index] as string)) {
          const td = element('td');
          td.appendChild(inline(cell, options));
          tr.appendChild(td);
        }
        body.appendChild(tr);
        index++;
      }
      table.appendChild(body);
      fragment.appendChild(el_wrap(table));
      drainCards(fragment, options);
      continue;
    }

    const item = ITEM.exec(line);
    if (item) {
      const ordered = /\d/.test(item[2] as string);
      const baseIndent = indentWidth(line);
      const list = element(ordered ? 'ol' : 'ul');

      while (index < lines.length) {
        const current = lines[index] as string;
        const nextItem = ITEM.exec(current);
        if (!nextItem || indentWidth(current) < baseIndent) break;
        if (indentWidth(current) > baseIndent) break;

        const body: string[] = [nextItem[3] as string];
        index++;
        // Continuation and nested lines belong to this item.
        while (index < lines.length) {
          const follow = lines[index] as string;
          if (follow.trim() === '') break;
          if (ITEM.test(follow) && indentWidth(follow) <= baseIndent) break;
          body.push(follow.slice(Math.min(indentWidth(follow), baseIndent + 2)));
          index++;
        }

        const li = element('li');
        const nested = body.findIndex((l) => ITEM.test(l));
        if (nested > 0) {
          li.appendChild(inline(body.slice(0, nested).join('\n').trim(), options));
          // A step's card sits under the step text, before the nested list.
          drainCards(li, options);
          li.appendChild(blocks(body.slice(nested), options));
        } else {
          li.appendChild(inline(body.join('\n').trim(), options));
          drainCards(li, options);
        }
        list.appendChild(li);
      }
      fragment.appendChild(list);
      continue;
    }

    const paragraph: string[] = [];
    while (index < lines.length) {
      const current = lines[index] as string;
      if (
        current.trim() === '' ||
        FENCE.test(current) ||
        HEADING.test(current) ||
        RULE.test(current) ||
        QUOTE.test(current) ||
        ITEM.test(current) ||
        (current.includes('|') && TABLE_RULE.test(lines[index + 1] ?? ''))
      )
        break;
      paragraph.push(current);
      index++;
    }
    const node = element('p');
    node.appendChild(inline(paragraph.join('\n'), options));
    fragment.appendChild(node);
    drainCards(fragment, options);
  }

  return fragment;
}

export function renderMarkdown(source: string, options: MarkdownOptions = {}): DocumentFragment {
  const seen = new Set<string>(options.command?.seenBefore?.() ?? []);
  const cards: { name: string; pairs: Record<string, string> }[] = [];
  const fragment = blocks(source.replace(/\r\n?/g, '\n').split('\n'), { ...options, seen, cards });
  if (options.growing) holdBackOpenBlock(fragment);
  return fragment;
}

/**
 * Plan 022: while streaming, the last block is still being written (a command in it may not be finished, and
 * the answer may still become a list item or a table). Take the cards out of it; the next render draws them
 * once the block has closed. The open block is the last top-level element that is not a card, or that list's
 * last item.
 */
function holdBackOpenBlock(fragment: DocumentFragment): void {
  const children = Array.from(fragment.children);
  let at = children.length - 1;
  while (at >= 0 && children[at]!.classList.contains('cmd-card')) at--;
  if (at < 0) return;
  for (let i = at + 1; i < children.length; i++) children[i]!.remove();
  let open: Element = children[at]!;
  if (open.tagName === 'UL' || open.tagName === 'OL') open = open.lastElementChild ?? open;
  open.querySelectorAll('.cmd-card').forEach((card) => card.remove());
}

function commandChip(name: string, command: CommandChipOptions): HTMLElement {
  const chip = element('span', 'md-cmd');
  // Plan 020: the location popover finds its anchor again by this, because every render rebuilds the chip.
  chip.dataset['cmd'] = name;
  const button = element('button', 'md-cmd-copy') as HTMLButtonElement;
  button.type = 'button';
  button.title = command.label(name);
  button.setAttribute('aria-label', command.label(name));
  const code = element('code');
  code.textContent = name;
  const glyph = element('span', 'md-cmd-glyph');
  glyph.appendChild(command.glyph('copy'));
  button.append(code, glyph);
  button.addEventListener('click', () => {
    command.copy(name);
    glyph.replaceChildren(command.glyph('check'));
    setTimeout(() => glyph.replaceChildren(command.glyph('copy')), 1200);
  });
  chip.appendChild(button);
  const locate = command.locate;
  if (locate) {
    const where = element('button', 'md-cmd-where') as HTMLButtonElement;
    where.type = 'button';
    const label = command.locateLabel ? command.locateLabel(name) : name;
    where.title = label;
    where.setAttribute('aria-label', label);
    const src = command.icon?.(name) ?? null;
    if (src) {
      const image = element('img', 'md-cmd-icon') as HTMLImageElement;
      image.alt = '';
      image.src = src;
      where.appendChild(image);
    } else {
      where.appendChild(command.glyph('toolbar'));
    }
    where.addEventListener('click', () => locate(name));
    chip.appendChild(where);
  }
  return chip;
}
