import { acceptFrame } from './projection-protocol.js';

export class ProjectionClient {
  constructor(name, { onData, onRevoked, onError = () => {}, EventSourceType = globalThis.EventSource, verifySession = async () => true }) {
    if (!['documents', 'conversations', 'overview'].includes(name)) throw Error('Unknown projection.');
    this.name = name;
    this.onData = onData;
    this.onRevoked = onRevoked;
    this.onError = onError;
    this.verifySession = verifySession;
    this.EventSourceType = EventSourceType;
    this.state = null;
    this.source = null;
    this.disposed = false;
    this.checkingSession = false;
    this.recoveries = 0;
    this.ready = new Promise(resolve => { this.resolveReady = resolve; });
  }

  start() {
    if (this.disposed || this.source) return;
    const suffix = this.state ? '?fingerprint=' + encodeURIComponent(this.state.fingerprint) : '';
    const source = new this.EventSourceType('/api/realtime/' + this.name + suffix);
    this.source = source;
    let notifyResume = true;
    source.onmessage = event => {
      if (this.source !== source || this.disposed) return;
      let frame;
      try {
        frame = JSON.parse(event.data);
        if (frame.type === 'subscription_closed') {
          this.dispose();
          this.onRevoked();
          return;
        }
        this.state = acceptFrame(this.state, frame);
      } catch {
        this.pause();
        this.state = null;
        if (++this.recoveries <= 2) this.start();
        else { this.resolveReady?.(false); this.resolveReady = null; this.onError('Live updates could not be synchronized. Refresh to retry.'); }
        return;
      }
      this.recoveries = 0;
      this.resolveReady?.(true);
      this.resolveReady = null;
      if (frame.type !== 'resumed' || notifyResume) this.onData(structuredClone(this.state.data));
      notifyResume = false;
    };
    source.onerror = async () => {
      if (this.disposed || this.source !== source || this.checkingSession) return;
      this.checkingSession = true;
      try {
        const valid = await this.verifySession();
        if (this.disposed || this.source !== source) return;
        if (!valid) { this.dispose(); this.onRevoked(); }
        else if (source.readyState === 2) { this.pause(); this.resolveReady?.(false); this.resolveReady = null; this.onError('Live updates paused. Reopen this page to retry.'); }
      } catch { /* EventSource retries transient network failures with its last event fingerprint. */ }
      finally { this.checkingSession = false; }
    };
  }

  pause() { this.source?.close(); this.source = null; }
  dispose() { this.disposed = true; this.pause(); this.state = null; this.resolveReady?.(false); this.resolveReady = null; }
}
