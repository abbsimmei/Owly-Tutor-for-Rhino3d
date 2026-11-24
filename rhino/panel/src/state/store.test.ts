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

test('a toolbar answer for a known turn adds a location card', () => {
  const store = storeWithTurn();

  store.apply({ type: 'toolbar.found', name: '_Loft', turnId: 'turn-0', places: [place] });

  assert.deepEqual(store.locations().get('turn-0'), [{ name: '_Loft', places: [place] }]);
});

test('a second answer for the same command replaces the first', () => {
  const store = storeWithTurn();

  store.apply({ type: 'toolbar.found', name: '_Loft', turnId: 'turn-0', places: [place] });
  store.apply({ type: 'toolbar.found', name: '_Loft', turnId: 'turn-0', places: [] });
  store.apply({ type: 'toolbar.found', name: '_Extrude', turnId: 'turn-0', places: [] });

  assert.deepEqual(
    store.locations().get('turn-0')?.map((entry) => [entry.name, entry.places.length]),
    [
      ['_Loft', 0],
      ['_Extrude', 0],
    ],
  );
});

test('a toolbar answer for a turn that is gone is ignored', () => {
  const store = storeWithTurn();

  store.apply({ type: 'toolbar.found', name: '_Loft', turnId: 'turn-9', places: [place] });

  assert.equal(store.locations().size, 0);
});

test('a card can be dismissed and a new conversation clears them all', () => {
  const store = storeWithTurn();
  store.apply({ type: 'toolbar.found', name: '_Loft', turnId: 'turn-0', places: [place] });

  store.dismissLocation('turn-0', '_Loft');
  assert.equal(store.locations().size, 0);

  store.apply({ type: 'toolbar.found', name: '_Loft', turnId: 'turn-0', places: [place] });
  store.apply({
    type: 'conversation',
    snapshot: { sessionId: 's', agent: 'Owly', docTitle: 'd', startedAt: '2026-01-01T00:00:00Z', turns: [] },
  });
  assert.equal(store.locations().size, 0);
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
