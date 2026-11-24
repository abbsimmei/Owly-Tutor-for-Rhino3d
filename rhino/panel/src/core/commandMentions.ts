// Which Rhino commands an answer mentions, and in what order (plan 019). Pure and DOM-free so that
// node --test can run it; markdown.ts and ui/transcript.ts share it.

import { parseCommandCode } from './commandName.js';

const FENCE = /^ {0,3}(```+|~~~+)/;
const CODE = /`([^`\n]+)`/g;

/** `_-Loft` and `_loft` are the same command: lowercase, no leading underscore, no leading dash. */
export function normaliseCommand(name: string): string {
  return name.toLowerCase().replace(/^_-?/, '');
}

/** Command names in inline code, in order of first appearance. Fenced code blocks are ignored. */
export function commandsIn(text: string): string[] {
  const found: string[] = [];
  let fence: string | undefined;
  for (const line of text.replace(/\r\n?/g, '\n').split('\n')) {
    const marker = FENCE.exec(line)?.[1];
    if (marker) {
      if (fence === undefined) fence = marker[0];
      else if (marker[0] === fence) fence = undefined;
      continue;
    }
    if (fence !== undefined) continue;
    for (const match of line.matchAll(CODE)) {
      const code = match[1] as string;
      const name = parseCommandCode(code)?.name;
      if (name !== undefined && !found.includes(name)) found.push(name);
    }
  }
  return found;
}
