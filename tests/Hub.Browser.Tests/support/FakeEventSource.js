export class FakeEventSource {
  static instances = [];
  constructor(url) { this.url = url; this.readyState = 1; this.closed = false; FakeEventSource.instances.push(this); }
  close() { this.closed = true; this.readyState = 2; }
  send(frame) { this.onmessage?.({ data: JSON.stringify(frame) }); }
}
