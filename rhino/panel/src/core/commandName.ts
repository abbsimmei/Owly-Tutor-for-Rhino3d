// A Rhino command as Owly writes it in an answer: `_Loft`, `_-Loft`, `_ExtrudeCrv` (plan 014).
// The leading underscore is required. Anything else (spaces, options, newlines) is not a command chip.
const COMMAND = /^_-?[A-Za-z][A-Za-z0-9_]*$/;

export function isCommandName(text: string): boolean {
  return COMMAND.test(text);
}
