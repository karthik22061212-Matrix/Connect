# Tech Stack: Connect

| Layer | Technology | Current state |
|---|---|---|
| Frontend | Flutter | Web-first |
| Backend | ASP.NET Core Web API (.NET 8) | Implemented |
| Realtime | SignalR | Implemented |
| Voice media | WebRTC / `flutter_webrtc` | Implemented |
| NAT traversal | STUN + coturn TURN | Implemented; physical verification pending |
| Auth | JWT | Implemented |
| Database | SQL Server / Azure SQL | Implemented |
| ORM | EF Core | Implemented |
| Architecture | Clean Architecture | Implemented |
| CQRS | MediatR | Implemented |
| Validation | FluentValidation | Implemented |
| Push foundation | Firebase Cloud Messaging | Implemented |
| Backend hosting | Azure App Service | Provisioned |
| Web hosting | Azure Static Web Apps | Provisioned |
| TURN | Azure VM + coturn | Provisioned |

## Configuration
Flutter production API configuration uses `API_BASE_URL` via compile-time environment configuration.

TURN credentials are dynamically requested from the authenticated backend and are not embedded in frontend source.

## Logging
Development diagnostics remain developer-facing. Production normal-user UI does not expose developer diagnostics.

Backend diagnostic storage is bounded in memory per user.
