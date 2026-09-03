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
GET  /api/v1/admin/diagnostics/{userId}/download
POST /api/v1/admin/diagnostics/{userId}/clear
```

The client ingestion endpoint is authenticated and derives ownership from the authenticated identity.

The `/admin/` URL segment is a diagnostic/support namespace only. Connect does not introduce an Admin/RBAC system for this feature.

## Diagnostic security
Frontend sanitization protects against credential-like data entering the diagnostic buffer. Diagnostic data excludes tokens, TURN secrets, SDP/ICE credentials, cookies, and unrelated secrets.

## Normal logging
Serilog operational logging remains separate from user-scoped diagnostics; backend logs are not blindly streamed to browsers.
