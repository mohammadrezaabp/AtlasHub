import test, { beforeEach } from 'node:test';
import assert from 'node:assert/strict';
import { ProjectionClient } from '../../src/Hub.Web/wwwroot/realtime.js';
import { FakeEventSource } from './support/FakeEventSource.js';

const first = 'a'.repeat(64);
const second = 'b'.repeat(64);
const snapshot = () => ({ type: 'snapshot', schema: 1, fingerprint: first, data: [{ title: 'Original' }] });
const client = overrides => new ProjectionClient('documents', { EventSourceType: FakeEventSource, onData: () => {}, onRevoked: () => {}, ...overrides });
beforeEach(() => { FakeEventSource.instances = []; });

test('cold subscriptions have no fingerprint and repeated starts keep one connection', async () => {
  const projection = client();
  projection.start(); projection.start();
  assert.equal(FakeEventSource.instances.length, 1);
  assert.equal(projection.source.url, '/api/realtime/documents');
  projection.source.send(snapshot());
  assert.equal(await projection.ready, true);
  projection.dispose();
});

test('reopening a held view sends its latest fingerprint and resumes without duplicate rendering', () => {
  let renders = 0;
  const projection = client({ onData: () => { renders++; } });
  projection.start(); projection.source.send(snapshot());
  projection.pause(); projection.start();
  assert.equal(projection.source.url, '/api/realtime/documents?fingerprint=' + first);
  projection.source.send({ type: 'resumed', schema: 1, fingerprint: first });
  projection.source.send({ type: 'resumed', schema: 1, fingerprint: first });
  assert.equal(renders, 2);
  projection.dispose();
});

test('a mismatched patch restarts with a fresh snapshot request', () => {
  const projection = client();
  projection.start(); const previous = projection.source;
  previous.send(snapshot());
  previous.send({ type: 'patch', schema: 1, fingerprint: second, baseFingerprint: second, patch: [] });
  assert.equal(previous.closed, true);
  assert.equal(projection.state, null);
  assert.equal(projection.source.url, '/api/realtime/documents');
  projection.source.send(snapshot());
  assert.equal(projection.state.fingerprint, first);
  projection.dispose();
});

test('revocation closes the connection, clears held state and ignores queued old frames', () => {
  let revoked = 0;
  const projection = client({ onRevoked: () => { revoked++; } });
  projection.start(); const source = projection.source;
  source.send(snapshot());
  source.send({ type: 'subscription_closed', reason: 'session_revoked' });
  source.send(snapshot());
  assert.equal(revoked, 1);
  assert.equal(source.closed, true);
  assert.equal(projection.state, null);
});

test('an expired session discovered after a disconnect clears cached state', async () => {
  let revoked = 0;
  const projection = client({ verifySession: async () => false, onRevoked: () => { revoked++; } });
  projection.start(); projection.source.send(snapshot());
  await projection.source.onerror();
  assert.equal(revoked, 1);
  assert.equal(projection.state, null);
});

test('an old asynchronous session check cannot revoke a later login', async () => {
  let answer, revoked = 0;
  const projection = client({ verifySession: () => new Promise(resolve => { answer = resolve; }), onRevoked: () => { revoked++; } });
  projection.start();
  const pending = projection.source.onerror();
  projection.dispose();
  answer(false); await pending;
  assert.equal(revoked, 0);
});

test('render callbacks cannot mutate held private state', () => {
  const projection = client({ onData: rows => { rows[0].title = 'Mutated by renderer'; } });
  projection.start(); projection.source.send(snapshot());
  assert.equal(projection.state.data[0].title, 'Original');
  projection.dispose();
});

test('invalid frames have bounded recovery and do not create an endless reconnect loop', async () => {
  let errors = 0;
  const projection = client({ onError: () => { errors++; } });
  projection.start();
  for (let index = 0; index < 3; index++) projection.source.send({ type: 'snapshot', schema: 99 });
  assert.equal(projection.source, null);
  assert.equal(errors, 1);
  assert.equal(await projection.ready, false);
});
