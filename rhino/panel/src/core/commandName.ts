// A Rhino command as Owly writes it in an answer: `_Loft`, `_-Loft`, `_ExtrudeCrv` (plan 014).
// The leading underscore is required. Anything else (spaces, options, newlines) is not a command chip.
const COMMAND = /^_-?[A-Za-z][A-Za-z0-9_]*$/;

export function isCommandName(text: string): boolean {
  return COMMAND.test(text);
}

// Plan 023: a command with suggested option values, as Owly may write it: `_Pyramid DirectionConstraint=Vertical`.
// Plan 024: a value may also be a plain number, like `_Pyramid NumSides=6`.
// The chip is only the name; the pairs seed the card (and the card drops any that the option data doesn't have).
const COMMAND_CODE = /^(_-?[A-Za-z][A-Za-z0-9_]*)((?: +_?[A-Za-z][A-Za-z0-9]*=(?:_?[A-Za-z][A-Za-z0-9]*|-?\d+(?:\.\d+)?))*)$/;

export function parseCommandCode(code: string): { name: string; pairs: Record<string, string> } | null {
  const match = COMMAND_CODE.exec(code);
  if (!match) return null;
  const name = match[1] as string;
  if (!isCommandName(name)) return null;
  const pairs: Record<string, string> = {};
  for (const pair of (match[2] as string).split(' ')) {
    if (!pair) continue;
    const at = pair.indexOf('=');
    const key = pair.slice(0, at).replace(/^_/, '');
    const value = pair.slice(at + 1).replace(/^_/, '');
    pairs[key] = value;
  }
  return { name, pairs };
}
