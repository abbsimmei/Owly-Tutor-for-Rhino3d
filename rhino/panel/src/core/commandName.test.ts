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

test('parseCommandCode reads a plain command and a command with suggested pairs', async () => {
  const { parseCommandCode } = await import('./commandName.js');
  assert.deepEqual(parseCommandCode('_Pyramid'), { name: '_Pyramid', pairs: {} });
  assert.deepEqual(parseCommandCode('_Pyramid DirectionConstraint=Vertical'), {
    name: '_Pyramid',
    pairs: { DirectionConstraint: 'Vertical' },
  });
  assert.deepEqual(parseCommandCode('_Pyramid _Solid=_No  Shape=Round'), {
    name: '_Pyramid',
    pairs: { Solid: 'No', Shape: 'Round' },
  });
  assert.deepEqual(parseCommandCode('_-Loft Closed=Yes'), { name: '_-Loft', pairs: { Closed: 'Yes' } });
});

test('parseCommandCode: a later duplicate key wins', async () => {
  const { parseCommandCode } = await import('./commandName.js');
  assert.deepEqual(parseCommandCode('_Pyramid Shape=Round Shape=Square')?.pairs, { Shape: 'Square' });
});

test('parseCommandCode rejects anything that is not a name with simple pairs', async () => {
  const { parseCommandCode } = await import('./commandName.js');
  for (const text of [
    'Pyramid',
    '_Pyramid NumSides',
    '_Pyramid NumSides=6.5',
    '_Pyramid Shape=Round _Delete',
    '_Pyramid Shape=Round;_Exit',
    '_Pyramid Shape="Round"',
    '_Pyramid Shape=Round\n',
    '_Pyramid =Round',
    '_Pyramid Shape=',
    '',
  ]) {
    assert.equal(parseCommandCode(text), null, JSON.stringify(text));
  }
});
