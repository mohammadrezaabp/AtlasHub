const forbidden = new Set(['__proto__', 'constructor', 'prototype']);
const own = (value, key) => Object.prototype.hasOwnProperty.call(value, key);
const fingerprint = value => typeof value === 'string' && /^[a-f0-9]{64}$/.test(value);

export function applyPatch(data, operations) {
  if (!Array.isArray(operations) || operations.length > 256) throw Error('Invalid patch operations.');
  let result = structuredClone(data);
  for (const operation of operations) {
    if (!['add', 'remove', 'replace'].includes(operation.op) || typeof operation.path !== 'string') throw Error('Invalid patch.');
    if (operation.op !== 'remove' && !own(operation, 'value')) throw Error('Missing patch value.');
    if (operation.path === '') {
      if (operation.op !== 'replace') throw Error('Invalid root operation.');
      result = structuredClone(operation.value);
      continue;
    }
    if (!operation.path.startsWith('/')) throw Error('Invalid patch path.');
    const keys = operation.path.slice(1).split('/').map(part => {
      if (/~(?:[^01]|$)/.test(part)) throw Error('Invalid JSON pointer escape.');
      const key = part.replaceAll('~1', '/').replaceAll('~0', '~');
      if (forbidden.has(key)) throw Error('Unsafe patch path.');
      return key;
    });
    let target = result;
    for (const key of keys.slice(0, -1)) {
      if (target === null || typeof target !== 'object' || !own(target, key)) throw Error('Missing patch parent.');
      target = target[key];
    }
    const key = keys.at(-1);
    if (target === null || typeof target !== 'object') throw Error('Invalid patch target.');
    if (Array.isArray(target)) {
      if (key !== '-' && !/^(0|[1-9]\d*)$/.test(key)) throw Error('Invalid array index.');
      const index = key === '-' ? target.length : Number(key);
      if (!Number.isSafeInteger(index) || index > target.length || (operation.op !== 'add' && index >= target.length)) throw Error('Patch index out of bounds.');
      if (operation.op === 'add') target.splice(index, 0, structuredClone(operation.value));
      else if (operation.op === 'remove') target.splice(index, 1);
      else target[index] = structuredClone(operation.value);
    } else {
      if (operation.op !== 'add' && !own(target, key)) throw Error('Missing patch property.');
      if (operation.op === 'remove') delete target[key];
      else target[key] = structuredClone(operation.value);
    }
  }
  return result;
}

export function acceptFrame(state, frame) {
  if (frame.schema !== 1 || !fingerprint(frame.fingerprint)) throw Error('Unknown projection schema or fingerprint.');
  if (frame.type === 'snapshot') {
    if (frame.data === null || typeof frame.data !== 'object') throw Error('Missing snapshot.');
    return { fingerprint: frame.fingerprint, data: structuredClone(frame.data) };
  }
  if (!state || state.data == null) throw Error('No held projection.');
  if (frame.type === 'resumed') {
    if (state.fingerprint !== frame.fingerprint) throw Error('Resume fingerprint mismatch.');
    return state;
  }
  if (frame.type !== 'patch' || frame.baseFingerprint !== state.fingerprint) throw Error('Patch base mismatch.');
  return { fingerprint: frame.fingerprint, data: applyPatch(state.data, frame.patch) };
}
