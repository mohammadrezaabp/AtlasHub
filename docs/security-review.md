# Publication security review

Review date: 2026-10-27. Scope: repository contents, application trust boundaries, dependency advisories and local publication controls. This is a source and regression-test review, not a penetration-test certification.

## Checks performed

| Check | Result |
|---|---|
| Source snapshot scanned with Gitleaks 8.30.1, default rules and an additional Deepgram credential rule | No detected secrets |
| Indexed file contents compared with configured local credential values | No matches; credential values are never printed |
| Publication paths | Environment secrets, runtime data, seed documents, backups, credentials, build outputs and local tooling excluded |
| Direct and transitive NuGet advisory audit against nuget.org | No known vulnerable packages reported at review time |
| Release regression suite | 55 backend tests and 14 browser tests passing |
| Authentication and sessions | Unauthorized API reads rejected; CSRF, lockout, account disablement and password-change revocation covered |
| Document, run and voice ownership | Private/team retrieval, protected downloads, image evidence and owner-scoped voice playback covered |
| Administrator privileges | Approval, system/model and team-management permissions enforced by the server |
| Browser boundary | Defensive headers verified; shell does not expose credentials or local data paths |
| Mutation boundary | Multipart upload and POST/PATCH/PUT/DELETE operations require antiforgery validation |
| Storage and transaction boundaries | Traversal rejection, approval replay, rollback, idempotent completion, competing claims and durable cleanup covered |
| Runtime isolation | Non-root app containers; database, object API, cache, vectors, gateway and tools have no published host ports |

## Publication safeguards

Realtime checks additionally cover revocation of already-open streams, user-scoped cache state, hidden-view suppression, unsafe patch paths, stale-base recovery and missed notifications. [Realtime design](realtime.md) documents the bounded recheck and the limits of withdrawing already delivered information.

The repository has explicit private-path exclusions, an index-based publication check, local pre-commit and pre-push hooks, and CI secret scans across all reachable Git history. The local index check also compares contents with credentials in `.env` when that file exists; CI performs pattern-based scanning without loading user secrets. GitHub Actions use read-only repository permissions and immutable action commit references. Dependabot proposes dependency updates.

Source documents and historic local diagnostics were retained privately rather than included in this repository. No application data reset is part of publication.

## Remaining deployment considerations

- Credentials that have been shared outside a secret store should be rotated with their providers. This review does not revoke or replace local provider keys.
- The default Compose deployment is loopback-only HTTP. Remote operation requires TLS, explicit reverse-proxy trust and network policies.
- The local setup uses an administrative MinIO credential. Use a restricted bucket-scoped identity and managed secret delivery for a hardened deployment.
- Prometheus metrics contain aggregate operational counters and are reachable from the local application endpoint; protect that endpoint in a remote deployment.
- No content malware scanning, external penetration test or container OS vulnerability assessment was performed as part of this publication check. NuGet auditing does not cover container image packages or provider security.
- Prompt injection defenses include server-enforced permissions, bounded tools and approval checks; model instructions alone do not eliminate prompt injection risk.
- Budget admission is process-local. Scale-out requires shared atomic reservations. Database upgrades need an explicit migration process beyond development startup initialization.

Read [the security policy](../SECURITY.md) before deploying outside a local evaluation environment.
