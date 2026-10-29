# Security policy

AtlasHub is a local development and evaluation application. The default Docker deployment binds the app and management consoles to loopback. Do not expose it directly to the Internet.

## Report a vulnerability

Use **Security → Report a vulnerability** on this repository to contact the maintainer privately. Do not include API keys, passwords, private documents, recordings, database exports or customer data in public issues or pull requests. For ordinary bugs that contain no sensitive information, open an issue with minimal reproduction steps.

## Credentials and private data

- Configure provider keys and bootstrap credentials in a local `.env`. Only the empty-key `.env.example` belongs in Git.
- `data/`, `knowledge/` contents, `.tools/`, `artifacts/`, database files, backups, private keys and generated output are excluded from publication.
- Enable local checks with `pwsh ./scripts/Test-PublishSafety.ps1 -InstallHook`. Install Gitleaks and put it on `PATH` before pushing. The pre-push hook refuses to proceed without a history scan.
- CI scans the full Git history with Gitleaks and audits direct and transitive NuGet packages. Secret scanners are safeguards, not a guarantee that arbitrary private content is safe to publish.
- If a real secret enters Git history, revoke it at its provider immediately. Removing the current file does not remove the secret from history.

## Trust boundaries

The browser receives an encrypted session cookie and antiforgery token. Provider credentials and the internal service token stay on the server. Accounts are provisioned by administrators; logout, disabled accounts and password changes revoke every session of the account. Agent deletion proposals must target a document the user named in that request, and still need administrator approval. Documents are authorized before listing, download, retrieval, tool use and image processing. File and voice bytes stay in a private MinIO bucket, with downloads brokered by the application.

Retrieved content and tool results are untrusted model input. Prompts instruct the model to ignore embedded instructions, while server-side permissions, tool allowlists, bounded loops, spending controls and human deletion approvals enforce the actual boundaries. Prompt instructions alone cannot guarantee resistance to every prompt injection.

## Deployment limits

Local HTTP cookies become Secure when served over HTTPS. Remote deployments need a trusted TLS reverse proxy, explicit forwarded-header trust, network restrictions, protected secrets, backup and restore procedures, malware scanning appropriate to uploaded content, and an upgrade plan. Database, Redis, Qdrant, gateway, MCP and object API ports should remain private. MinIO credentials in the default Compose setup are administrative credentials; provision a bucket-scoped service identity for a hardened deployment.

The gateway budget admission lock is process-local; do not scale it horizontally without shared atomic reservations. Startup schema initialization is intended for a dedicated development database, rather than as a general production migration system. `/metrics` exposes aggregate operational counters only to requests carrying the dedicated `METRICS_TOKEN` bearer token. Container OS vulnerability scanning and penetration testing are separate deployment responsibilities.
