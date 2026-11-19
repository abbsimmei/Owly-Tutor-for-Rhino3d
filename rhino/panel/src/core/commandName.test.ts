import assert from 'node:assert/strict';
import { test } from 'node:test';
import { isCommandName } from './commandName.js';

test('accepts commands written the way Owly writes them', () => {
  for (const name of ['_Loft', '_-Loft', '_ExtrudeCrv', '_Line2']) assert.equal(isCommandName(name), true, name);
});

test('rejects everything that is not a bare command name', () => {
  for (const text of ['Loft', '_', '_-', '_Loft Enter', '_Loft\n', '__Loft', '_Loft;', '_1Loft', 'layer_name', '']) {
    assert.equal(isCommandName(text), false, JSON.stringify(text));
  }
});
