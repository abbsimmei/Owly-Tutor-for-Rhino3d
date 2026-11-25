import assert from 'node:assert/strict';
import test from 'node:test';
import { Store } from './store.ts';

test('an identical notice bumps the toast already showing instead of stacking', () => {
  const store = new Store();

  store.apply({ type: 'notice', level: 'warn', text: 'Nothing selected.' });
  store.apply({ type: 'notice', level: 'warn', text: 'Nothing selected.' });
  store.apply({ type: 'notice', level: 'warn', text: 'Nothing selected.' });

  assert.equal(store.notices().length, 1);
  assert.equal(store.notices()[0]?.repeats(), 2);
});

test('a different text or level is its own notice', () => {
  const store = new Store();

  store.apply({ type: 'notice', level: 'info', text: 'Saved.' });
  store.apply({ type: 'notice', level: 'info', text: 'Saved to disk.' });
  store.apply({ type: 'notice', level: 'error', text: 'Saved.' });

  assert.deepEqual(
    store.notices().map((notice) => `${notice.level}:${notice.text}`),
    ['info:Saved.', 'info:Saved to disk.', 'error:Saved.'],
  );
});

test('the same text shows again once the earlier one is gone', () => {
  const store = new Store();

  store.apply({ type: 'notice', level: 'info', text: 'Reverted that turn.' });
  const first = store.notices()[0];
  assert.ok(first);
  store.dismissNotice(first.id);
  store.apply({ type: 'notice', level: 'info', text: 'Reverted that turn.' });

  assert.equal(store.notices().length, 1);
  assert.notEqual(store.notices()[0]?.id, first.id);
});

const place = { group: 'Main', tab: 'Surface', path: 'Surface Tools', button: 'Loft', click: 'left', hidden: false } as const;

function storeWithTurn(): Store {
  const store = new Store();
  store.apply({
    type: 'turn.begin',
    turn: {
      id: 'turn-0',
      prompt: 'How do I loft?',
      attachments: [],
      context: [],
      startedAt: '2026-01-01T00:00:00Z',
      status: 'ok',
      usage: null,
      blocks: [],
      plan: [],
      undoable: false,
    },
  });
  return store;
}

test('opening a location asks the host, and the answer fills the open popover', () => {
  const store = storeWithTurn();

  assert.equal(store.openLocation('turn-0', '_Loft'), true);
  assert.deepEqual(store.location(), { turnId: 'turn-0', name: '_Loft' });

  store.apply({ type: 'toolbar.found', name: '_Loft', turnId: 'turn-0', places: [place] });
  assert.deepEqual(store.location(), { turnId: 'turn-0', name: '_Loft', places: [place] });
});

test('an answer nobody asked for opens nothing', () => {
  const store = storeWithTurn();

  store.apply({ type: 'toolbar.found', name: '_Loft', turnId: 'turn-0', places: [place] });

  assert.equal(store.location(), null);
  assert.equal(store.openLocation('turn-0', '_Loft'), true);
});

test('a second open of the same name is answered from the cache', () => {
  const store = storeWithTurn();
  store.openLocation('turn-0', '_Loft');
  store.apply({ type: 'toolbar.found', name: '_Loft', turnId: 'turn-0', places: [place] });
  store.closeLocation();
  assert.equal(store.location(), null);

  assert.equal(store.openLocation('turn-0', '_Loft'), false);
  assert.deepEqual(store.location()?.places, [place]);
});

test('a toolbar answer for a turn that is gone is ignored', () => {
  const store = storeWithTurn();
  store.openLocation('turn-0', '_Loft');

  store.apply({ type: 'toolbar.found', name: '_Loft', turnId: 'turn-9', places: [place] });

  assert.equal(store.location()?.places, undefined);
});

test('a new conversation clears the popover and the cache', () => {
  const store = storeWithTurn();
  store.openLocation('turn-0', '_Loft');
  store.apply({ type: 'toolbar.found', name: '_Loft', turnId: 'turn-0', places: [place] });

  store.apply({
    type: 'conversation',
    snapshot: { sessionId: 's', agent: 'Owly', docTitle: 'd', startedAt: '2026-01-01T00:00:00Z', turns: [] },
  });

  assert.equal(store.location(), null);
  assert.equal(store.openLocation('turn-0', '_Loft'), true);
});

// Plan 025: absent id, name and suggestion are unset, which is how the host's serialiser sends nulls.
test('a project event is kept with absent fields as null', () => {
  const store = new Store();
  const projects = [
    { id: 'p-1', name: 'Pavilion', builtin: false },
    { id: 'unsorted', name: 'Unsorted', builtin: true },
  ];

  store.apply({ type: 'project', pending: true, suggestion: 'p-1', projects });
  assert.deepEqual(store.project(), { id: null, name: null, pending: true, suggestion: 'p-1', projects });

  store.apply({ type: 'project', id: 'p-1', name: 'Pavilion', pending: false, projects });
  assert.deepEqual(store.project(), { id: 'p-1', name: 'Pavilion', pending: false, suggestion: null, projects });
});

// Plan 021: toolbar button images.
test('an icon answer is kept whether it was asked for or not, and builds a data URL for the scheme', () => {
  const store = new Store();
  store.apply({ type: 'toolbar.icon.found', name: '_Loft', light: 'TElHSFQ=', dark: 'REFSSw==' });

  store.apply({ type: 'theme', scheme: 'light' });
  assert.equal(store.iconUrl('_Loft'), 'data:image/svg+xml;base64,TElHSFQ=');
  store.apply({ type: 'theme', scheme: 'dark' });
  assert.equal(store.iconUrl('_Loft'), 'data:image/svg+xml;base64,REFSSw==');
});

test('a missing variant falls back to the other one, and an empty answer gives no URL', () => {
  const store = new Store();
  store.apply({ type: 'toolbar.icon.found', name: '_Box', light: 'TElHSFQ=' });
  store.apply({ type: 'toolbar.icon.found', name: '_Sphere' });

  store.apply({ type: 'theme', scheme: 'dark' });
  assert.equal(store.iconUrl('_Box'), 'data:image/svg+xml;base64,TElHSFQ=');
  assert.equal(store.iconUrl('_Sphere'), null);
  assert.equal(store.iconUrl('_Unknown'), null);
});

test('a name is asked about once, and never again once it is known', () => {
  const store = new Store();

  assert.equal(store.markIconAsked('_Loft'), true);
  assert.equal(store.markIconAsked('_Loft'), false);

  store.apply({ type: 'toolbar.icon.found', name: '_Box' });
  assert.equal(store.markIconAsked('_Box'), false);
});

test('icons survive a new conversation', () => {
  const store = new Store();
  store.apply({ type: 'toolbar.icon.found', name: '_Loft', light: 'TElHSFQ=' });

  store.apply({
    type: 'conversation',
    snapshot: { sessionId: 's', agent: 'Owly', docTitle: 'd', startedAt: '2026-01-01T00:00:00Z', turns: [] },
  });

  assert.equal(store.iconUrl('_Loft'), 'data:image/svg+xml;base64,TElHSFQ=');
  assert.equal(store.markIconAsked('_Loft'), false);
});

// Plan 022: whether the host lets Run start a command.
test('a command.checked answer is kept whether it was asked for or not', () => {
  const store = new Store();
  store.apply({ type: 'command.checked', name: '_Pyramid', run: 'ok' });
  store.apply({ type: 'command.checked', name: '_Delete', run: 'blocked' });

  assert.equal(store.runnable().get('_Pyramid'), 'ok');
  assert.equal(store.runnable().get('_Delete'), 'blocked');
});

test('a command is checked once, and never again once it is known', () => {
  const store = new Store();

  assert.equal(store.markCheckAsked('_Loft'), true);
  assert.equal(store.markCheckAsked('_Loft'), false);

  store.apply({ type: 'command.checked', name: '_Box', run: 'ok' });
  assert.equal(store.markCheckAsked('_Box'), false);
});

test('Run answers survive a new conversation', () => {
  const store = new Store();
  store.apply({ type: 'command.checked', name: '_Pyramid', run: 'ok' });

  store.apply({
    type: 'conversation',
    snapshot: { sessionId: 's', agent: 'Owly', docTitle: 'd', startedAt: '2026-01-01T00:00:00Z', turns: [] },
  });

  assert.equal(store.runnable().get('_Pyramid'), 'ok');
});

// Plan 023: command options and per-card values.
const fresh = {
  type: 'conversation' as const,
  snapshot: { sessionId: 's', agent: 'Owly', docTitle: 'd', startedAt: '2026-01-01T00:00:00Z', turns: [] },
};

test('a command.options.found answer is kept, and a name is looked up once', () => {
  const store = new Store();
  assert.equal(store.markOptionsAsked('_Pyramid'), true);
  assert.equal(store.markOptionsAsked('_Pyramid'), false);
  assert.equal(store.optionsLookingUp('_Pyramid'), true);

  store.apply({ type: 'command.options.found', name: '_Pyramid', options: [] });
  assert.equal(store.optionsLookingUp('_Pyramid'), false);
  assert.deepEqual(store.commandOptions().get('_Pyramid'), []);
  assert.equal(store.markOptionsAsked('_Pyramid'), false);
});

test('option data survives a new conversation', () => {
  const store = new Store();
  store.apply({ type: 'command.options.found', name: '_Pyramid', options: [] });
  store.apply(fresh);
  assert.equal(store.commandOptions().has('_Pyramid'), true);
});

test('a card is seeded once, so a re-render never resets what the student clicked', () => {
  const store = new Store();
  store.seedCard('t1|_Pyramid', { Shape: 'Round' });
  assert.deepEqual(store.cardValues().get('t1|_Pyramid')?.get('Shape'), { value: 'Round', suggested: true });

  store.setCardValue('t1|_Pyramid', 'Shape', 'Square');
  store.seedCard('t1|_Pyramid', { Shape: 'Round' });
  assert.deepEqual(store.cardValues().get('t1|_Pyramid')?.get('Shape'), { value: 'Square', suggested: false });
});

test('a click clears "suggested", and clicking again unselects', () => {
  const store = new Store();
  store.seedCard('t1|_Pyramid', { shape: 'Round' });
  store.setCardValue('t1|_Pyramid', 'Shape', 'Round');
  const values = store.cardValues().get('t1|_Pyramid');
  assert.equal(values?.size, 1);
  assert.equal(values?.get('Shape')?.suggested, false);

  store.setCardValue('t1|_Pyramid', 'Shape', null);
  assert.equal(store.cardValues().get('t1|_Pyramid')?.size, 0);
});

test('card values belong to one turn and one chip', () => {
  const store = new Store();
  store.setCardValue('t1|_Pyramid', 'Shape', 'Round');
  assert.equal(store.cardValues().get('t2|_Pyramid'), undefined);
});

test('a new conversation clears card values, and a card can be seeded again', () => {
  const store = new Store();
  store.seedCard('t1|_Pyramid', { Shape: 'Round' });
  store.openExplain('t1', '_Pyramid', 'Shape');
  store.apply(fresh);
  assert.equal(store.cardValues().size, 0);
  assert.equal(store.explain(), null);
  store.seedCard('t1|_Pyramid', { Shape: 'Square' });
  assert.equal(store.cardValues().get('t1|_Pyramid')?.get('Shape')?.value, 'Square');
});

test('a typed number is stored without redrawing the cards, and replaces any suggestion', () => {
  const store = new Store();
  store.seedCard('t1|_Pyramid', { numsides: '6' });
  const before = store.cardValues.peek();
  store.commitCardNumber('t1|_Pyramid', 'NumSides', '8');
  assert.equal(store.cardValues.peek(), before, 'the signal value keeps its identity, so nothing redraws');
  assert.deepEqual(store.cardValues.peek().get('t1|_Pyramid')?.get('NumSides'), { value: '8', suggested: false });
  assert.equal(store.cardValues.peek().get('t1|_Pyramid')?.size, 1);
  store.commitCardNumber('t1|_Pyramid', 'NumSides', null);
  assert.equal(store.cardValues.peek().get('t1|_Pyramid')?.size, 0);
});
