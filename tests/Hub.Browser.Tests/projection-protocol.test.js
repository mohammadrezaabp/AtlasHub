import test from 'node:test';
import assert from 'node:assert/strict';
import { applyPatch, acceptFrame } from '../../src/Hub.Web/wwwroot/projection-protocol.js';

const first = 'a'.repeat(64);
const second = 'b'.repeat(64);

test('patches update fields, remove items and decode escaped JSON pointers', () => {
  const before = { documents: [{ title: 'Old' }, { title: 'Removed' }], 'a/b~c': 1 };
  const after = applyPatch(before, [
    { op: 'replace', path: '/documents/0/title', value: 'New' },
    { op: 'remove', path: '/documents/1' },
    { op: 'add', path: '/documents/-', value: { title: 'Added' } },
    { op: 'replace', path: '/a~1b~0c', value: 2 }
  ]);
  assert.deepEqual(after, { documents: [{ title: 'New' }, { title: 'Added' }], 'a/b~c': 2 });
  assert.equal(before.documents[0].title, 'Old');
});

test('unsafe paths, malformed pointers and invalid array indices are rejected', () => {
  for (const path of ['/__proto__/polluted', '/constructor/prototype/x', '/x/~2', '/items/01', '/items/4', '/missing/title']) {
    assert.throws(() => applyPatch({ items: [1] }, [{ op: 'replace', path, value: 'bad' }]));
  }
  assert.equal({}.polluted, undefined);
});

test('a failed multi-operation patch leaves the original state intact', () => {
  const original = { title: 'Original' };
  assert.throws(() => applyPatch(original, [{ op: 'replace', path: '/title', value: 'Changed' }, { op: 'remove', path: '/missing' }]));
  assert.deepEqual(original, { title: 'Original' });
});

test('root replacement works and oversized patches are refused', () => {
  assert.deepEqual(applyPatch([1], [{ op: 'replace', path: '', value: [2, 3] }]), [2, 3]);
  assert.throws(() => applyPatch({}, Array.from({ length: 257 }, () => ({ op: 'add', path: '/x', value: 1 }))));
});

test('snapshots are copied and matching resumed frames retain state', () => {
  const frame = { type: 'snapshot', schema: 1, fingerprint: first, data: { title: 'Held' } };
  const held = acceptFrame(null, frame);
  frame.data.title = 'Changed outside';
  assert.equal(held.data.title, 'Held');
  assert.equal(acceptFrame(held, { type: 'resumed', schema: 1, fingerprint: first }), held);
});

test('base mismatches, missing held state and unknown schemas trigger recovery', () => {
  const held = { fingerprint: first, data: { title: 'Held' } };
  assert.throws(() => acceptFrame(held, { type: 'patch', schema: 1, fingerprint: second, baseFingerprint: second, patch: [] }));
  assert.throws(() => acceptFrame(null, { type: 'resumed', schema: 1, fingerprint: first }));
  assert.throws(() => acceptFrame(held, { type: 'snapshot', schema: 2, fingerprint: second, data: [] }));
});
