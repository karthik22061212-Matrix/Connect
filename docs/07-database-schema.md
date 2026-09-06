# Database Schema: Connect

EF Core Code-First using SQL Server-compatible persistence.

## Core entities
```text
User
 ├── ConnectRequest
 ├── Connection
 ├── Call
 ├── Block
 ├── Report
 └── DeviceToken
```

## User
Application identity/state includes the user identifier, email/phone where applicable, password/identity-managed credentials, presence, subscription tier, soft-delete state, and audit timestamps.

There is no `IsAdmin` or application-wide role field added for diagnostics.

## Call
Stores connection, caller, callee, status, missed reason, start/answer/end times, and duration.

## Diagnostic storage
Diagnostic events are deliberately not stored in SQL.

They are stored in the bounded singleton in-memory diagnostic service:
- keyed by user ID;
- maximum 1000 events per user;
- cleared on backend restart.

## Retention
Call history and account-retention policies are enforced by the existing background processing.
