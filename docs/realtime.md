# Realtime synchronization

AtlasHub separates append-only agent events from authorized view synchronization. Both use authenticated server-sent events. Domain has no realtime infrastructure dependencies.

| Location | Responsibility |
|---|---|
| `Hub.Application/Realtime` | Authorized projections, canonical fingerprints, bounded patches and counters |
| `Hub.Application/Abstractions/Realtime` | Notification subscription and projection cache ports |
| `Hub.Infrastructure/Realtime` | Coalesced signals, Redis notifications, scoped cache and outbox dispatch |
| `Hub.Infrastructure/Persistence` | Transactional notification staging and outbox records |
| `Hub.Web/Endpoints` | SSE transport, session renewal, cursors and revocation frames |
| `Hub.Web/wwwroot/realtime.js` | Browser connections and held-state lifecycle |
| `Hub.Web/wwwroot/projection-protocol.js` | Atomic patch validation and application |

## Projection protocol

`GET /api/realtime/{view}` supports `documents`, `conversations` and `overview`. A client can echo a 64-character hexadecimal fingerprint through `Last-Event-ID`, or the `fingerprint` query parameter on a new connection. The header takes precedence so native EventSource reconnects use the latest acknowledged state rather than an older URL parameter.

Before every read, `ProjectionService` reloads the enabled account, checks the session version and executes existing authorized queries. Cache scope includes user, session version, role, team, view and schema. Cache entries never replace authorization or hydration. SHA-256 fingerprints cover schema identity and canonical JSON with ordinal object-key ordering and deterministic array ordering. They are cache validators, not access credentials.

| Frame | Meaning |
|---|---|
| `snapshot` | Full authorized `data` on a cold subscription or safe fallback |
| `patch` | Bounded operations transforming `baseFingerprint` into `fingerprint` |
| `resumed` | Current authorized state matches the held fingerprint; no data retransmission |
| `subscription_closed` | Session access lost; discard held state |

Data frames carry schema `1` and use their fingerprint as the SSE event ID. Unused envelope fields serialize as null. No arbitrary SQL subscriptions or administrator data subscriptions are exposed.

Patches support JSON Pointer escaping and add, remove and replace operations. Diff generation stops at 256 operations and falls back to snapshots. Selection compares UTF-8 encoded frame sizes, including SSE IDs and delimiters. Views exceeding 2 MB use the existing authenticated REST fallback in the browser.

The client checks schema and base fingerprint, applies operations to a clone, and commits state only after the entire patch succeeds. Missing parents, invalid array indices, malformed pointers and prototype-related paths are rejected. A mismatch discards the base and requests a new snapshot. Repeated failures stop after bounded recovery.

Held state remains in browser memory across transient reconnects and in-app navigation. It is cleared on logout or revocation. Render callbacks receive copies. Browser reloads start cold; private views are never persisted in localStorage. The server keeps the most recent scoped snapshot for five minutes in Redis, with a bounded 16 MB memory fallback. A missing reconnect base requires a snapshot; a matching held/current fingerprint still resumes without a cache hit.

## Durable notifications

`RealtimeOutbox` contains an ID, topic and creation time, without prompts, file bytes, grants or provider credentials. Relevant tracked writes enqueue rows in the same save transaction. Atomic repositories enqueue explicitly. Rolling back a mutation rolls back its notification.

Topics cover document invalidation, user-scoped conversations, usage and session changes, and run-scoped events. Notifications request recomputation and never contain authorized view data.

The web dispatcher reads up to 128 committed rows every 500 ms, coalesces topics and publishes to Redis. Acknowledged rows are removed after publication. Failed publication retains rows for retry. Delivery is at least once; duplicate signals are safe and unchanged projections emit nothing. Subscriptions are registered before hydration, with one pending signal per subscriber to bound buffering.

Streams also recheck after five idle seconds. This handles missed signals, outages and restarts without permanent staleness. Ordinary worker heartbeat changes do not trigger view notifications.

## Agent events and authorization

`GET /api/runs/{id}/events` retains numeric event cursors and durable PostgreSQL replay. Streams wait on run/session notifications instead of polling every 250 ms. Backlogs drain in pages of 300 without waiting between full pages.

Each page rechecks enabled account, session version and ownership before loading events. Account disablement and password changes wake affected subscribers. Invalid sessions receive `subscription_closed` with reason `session_revoked`; missing runs close with `unavailable`. The browser discards private state and ignores queued frames from disposed connections. Active agent leases stop renewing for disabled accounts.

Revocation is observed on the next notification-driven evaluation or bounded fallback, rather than with a zero-latency guarantee. Already delivered bytes cannot be withdrawn. Connections renew within fifteen minutes, capped by the authentication ticket's expiry. Six concurrent streams per account bound resource use.

## Measurement and tests

`atlas_realtime_*` counters record evaluations, suppression, snapshots, patches, resumes, emitted frame bytes and a hypothetical full-snapshot baseline. Counts include SSE framing and exclude heartbeat/control frames, HTTP headers, TLS and compression. The baseline assumes a snapshot at every evaluation, including fallback evaluations. Counters reset with the web process; no production saving percentage is asserted.

Backend tests exercise authorized scopes, hidden changes, canonicalization, size selection, rollback, committed updates, resume, notification loss and existing-stream revocation. Browser tests cover atomic patches, unsafe paths, stale bases, reconnect, state isolation, bounded recovery and revocation cleanup.

```sh
dotnet test AtlasHub.slnx -c Release
node --test tests/Hub.Browser.Tests/*.test.js
```
