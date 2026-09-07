# Backend Architecture: Connect API

Stack: ASP.NET Core Web API (.NET 8), Clean Architecture, CQRS/MediatR, EF Core.

## Structure
```text
Connect.Domain
Connect.Application
  Common/
    Diagnostics/
  Features/
Connect.Infrastructure
  Persistence/
  Identity/
  Realtime/
  Notifications/
  Diagnostics/
Connect.Api
  Controllers/
  Middleware/
```

## SignalR
`CallHub` remains the realtime entry point for call-related operations and WebRTC signaling.

## Diagnostic components
```text
Connect.Application/Common/Diagnostics/
    DiagnosticEvent.cs
    IDiagnosticLogService.cs

Connect.Infrastructure/Diagnostics/
    InMemoryDiagnosticLogService.cs
```

The service is registered as a singleton and keeps bounded per-user queues.

## Diagnostic endpoints
```text
POST /api/v1/diagnostics/client-logs
GET  /api/v1/diagnostics/{userId}/download
POST /api/v1/diagnostics/{userId}/clear
```

The client ingestion endpoint is authenticated and derives ownership from the authenticated identity.

## Diagnostic security
Frontend sanitization protects against credential-like data entering the diagnostic buffer. Diagnostic data excludes tokens, TURN secrets, SDP/ICE credentials, cookies, and unrelated secrets.

## Normal logging
Serilog operational logging remains separate from user-scoped diagnostics; backend logs are not blindly streamed to browsers.


## Authentication Architecture

Email/password:
Client → API → credential validation → JWT + refresh token

Google (PLANNED / FUTURE):
Client → Google authentication → server-side validation → Connect account resolution → JWT + refresh token

Apple (PLANNED / FUTURE):
Client → Apple authentication → server-side validation → Connect account resolution → JWT + refresh token

Important Authentication Rules:
- Provider identity must be validated by the server.
- Do not trust arbitrary provider IDs sent from the client.
- Account linking must be deterministic.
- Duplicate-account creation must be prevented.
- Email matching alone should not blindly merge accounts unless product/security rules explicitly allow it.
- Apple private relay email behavior must be considered.
- Provider subject IDs should be treated as stable identity identifiers.
- Logout and refresh token handling should remain consistent with the normal Connect authentication model.
