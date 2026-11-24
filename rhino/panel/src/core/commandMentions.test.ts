import assert from 'node:assert/strict';
import { test } from 'node:test';
import { commandsIn, normaliseCommand } from './commandMentions.js';

test('lists commands in order of first appearance', () => {
  assert.deepEqual(commandsIn('Use `_Loft` then `_Loft` and `_Cap`'), ['_Loft', '_Cap']);
});

test('ignores fenced code blocks', () => {
  assert.deepEqual(commandsIn('Try:\n```\n`_Hidden`\n```\nthen `_Loft`'), ['_Loft']);
});

test('ignores inline code that is not a command', () => {
  assert.deepEqual(commandsIn('The layer `layer_name` and `_Loft Enter`'), []);
});

test('_-Loft and _Loft are the same command', () => {
  assert.equal(normaliseCommand('_-Loft'), normaliseCommand('_loft'));
  assert.equal(normaliseCommand('_Loft'), 'loft');
});
