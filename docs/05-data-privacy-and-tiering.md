# Data, Privacy & Tiering Model: Connect

## Application data
Connect handles authentication, connections, presence, calls/call history, blocking, reporting, and device notification registration.

## Retention
- Call history: 90 days by default.
- Soft-deleted account reactivation window: 60 days.
- Background jobs enforce related retention/purge rules.

## Diagnostic data
Frontend diagnostics use a bounded in-memory buffer (500 events).

Backend diagnostics use bounded per-user in-memory storage (1000 events per user). Backend restart can remove these diagnostics; this is intentional for the current troubleshooting design.

Client diagnostic ingestion derives ownership from the authenticated identity.

## Sensitive data exclusions
Diagnostic events must not contain:
- JWT/access token
- Authorization header values
- TURN password/shared secret
- SDP
- ICE credentials
- cookies
- database credentials
- SMTP credentials

## Tiering
All current users are treated as Free tier. No paid feature gating is active.
