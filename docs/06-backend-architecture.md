# Backend Architecture: Connect API

Stack: ASP.NET Core Web API (.NET 8) · Clean Architecture · CQRS via MediatR · Repository + Unit of Work over EF Core

---

## 1. Solution / Folder Structure

```
backend/
  src/
    Connect.Domain/                    # Enterprise-wide business rules, no dependencies on anything
      Entities/                        # User, ConnectRequest, Connection, Call, Block, Report, DeviceToken
      Enums/                           # PresenceStatus, CallStatus, ConnectRequestStatus, SubscriptionTier,
                                        # MissedReason, ReportStatus, DevicePlatform
      Exceptions/                      # DomainException and subtypes
      Common/                          # BaseEntity, AuditableEntity (CreatedAt, UpdatedAt)

    Connect.Application/               # Use cases (CQRS), depends only on Domain
      Common/
        Behaviors/                     # MediatR pipeline behaviors (Validation, Logging, UnhandledException)
        Interfaces/                    # IUnitOfWork, IRepository<T>, ICurrentUserService, IDateTimeProvider,
                                        # IJwtTokenGenerator, IPushNotificationService, IPresenceTracker
        Exceptions/                    # ValidationException, NotFoundException, ForbiddenAccessException,
                                        # ConflictException (e.g. duplicate User ID)
        Mappings/                      # Mapping profiles (AutoMapper or manual extension methods)
      Features/                        # Vertical grouping by feature, CQRS inside Clean Architecture
        Auth/
          Commands/RegisterUser/
          Commands/Login/               # Also handles silent account reactivation within 60-day window
        Users/
          Queries/CheckUserIdAvailability/
          Queries/SearchUsers/
        ConnectRequests/
          Commands/SendConnectRequest/
          Commands/AcceptConnectRequest/
          Commands/DeclineConnectRequest/
          Queries/GetPendingRequests/
          Queries/GetConnections/
        Presence/
          Commands/UpdatePresence/
          Queries/GetPresence/          # Access-restricted: only for users you're connected to (or self)
        Calls/
          Commands/InitiateCall/        # Called via SignalR hub only, never REST
          Commands/EndCall/
          Commands/FailCall/            # Handles connection failures and network-drop timeouts
          Queries/GetCallHistory/       # Paginated, 90-day retention cutoff
        Notifications/
          Commands/RegisterDeviceToken/ # Registers/updates FCM token for push notifications
        Blocking/
          Commands/BlockUser/
          Commands/UnblockUser/
          Queries/GetBlockedUsers/
        Reports/
          Commands/ReportUser/
        Account/
          Commands/SoftDeleteAccount/
          Commands/PurgeOldCallHistory/       # Invoked by background service, 90-day cutoff
          Commands/PurgeExpiredAccounts/      # Invoked by background service, 60-day cutoff
      DependencyInjection.cs            # AddApplication() extension — registers MediatR, FluentValidation, mappings

    Connect.Infrastructure/             # Implementation details, depends on Application + Domain
      Persistence/
        ApplicationDbContext.cs
        Configurations/                 # IEntityTypeConfiguration<T> per entity (Fluent API)
        Repositories/                   # Generic Repository<T>, specific repos (UserRepository, CallRepository)
        UnitOfWork.cs
        Migrations/
      Identity/                         # ASP.NET Core Identity setup, JwtTokenGenerator
      Realtime/
        CallHub.cs                      # SignalR hub — sole entry point for call initiation and real-time state
        PresenceTracker.cs              # Tracks connected users (in-memory now; Redis if scaled beyond one instance)
      Notifications/
        FcmPushNotificationService.cs   # Firebase Cloud Messaging integration; gracefully no-ops if unconfigured (local dev)
      Services/
        CallHistoryPurgeBackgroundService.cs      # IHostedService, periodic — invokes PurgeOldCallHistoryCommand
        ExpiredAccountsPurgeBackgroundService.cs  # IHostedService, periodic — invokes PurgeExpiredAccountsCommand
      DependencyInjection.cs            # AddInfrastructure() extension

    Connect.Api/                        # Presentation layer, depends on Application + Infrastructure
      Controllers/                      # Thin controllers — just dispatch to MediatR
      Middleware/
        GlobalExceptionHandlerMiddleware.cs
      Program.cs
      appsettings.json

  tests/
    Connect.Application.UnitTests/
    Connect.Domain.UnitTests/
    Connect.Api.IntegrationTests/
```

**Dependency rule:** `Domain` has zero dependencies. `Application` depends only on `Domain`. `Infrastructure` depends on `Application` (implements its interfaces). `Api` depends on `Application` + `Infrastructure` (composition root — wires everything in `Program.cs`).

---

## 2. CQRS with MediatR

- Every use case is either a **Command** (write, changes state) or a **Query** (read, returns a DTO).
- Each Command/Query has three files in its folder: `XyzCommand.cs` (or `XyzQuery.cs`), `XyzHandler.cs`, `XyzValidator.cs` (FluentValidation).
- Controllers and the SignalR hub never contain business logic — they build a Command/Query and call `_mediator.Send(...)`. This applies equally to `CallHub`: hub methods (`InitiateCallAttempt`, `EndCall`, `NotifyCallFailed`) delegate to the same command handlers as any REST endpoint would, then relay the appropriate real-time event based on the result. This keeps one source of truth for business rules regardless of entry point.

**Example flow — Send Connect Request:**
```
POST /api/connect-requests
  → SendConnectRequestController
    → new SendConnectRequestCommand(fromUserId, toUserId)
      → MediatR pipeline: ValidationBehavior → LoggingBehavior → Handler
        → SendConnectRequestCommandHandler
          → checks via IUnitOfWork.Users (not blocked, not already connected)
          → creates ConnectRequest entity
          → IUnitOfWork.ConnectRequests.Add(...)
          → IUnitOfWork.SaveChangesAsync()
      → returns Result<ConnectRequestDto>
```

**Example flow — Call initiation (hub-only, no REST):**
```
Client calls SignalR hub method: InitiateCallAttempt(calleeId)
  → CallHub.InitiateCallAttempt
    → _mediator.Send(new InitiateCallCommand(callerId, calleeId))
      → InitiateCallCommandHandler
        → validates Connection exists, not blocked, not self
        → checks IPresenceTracker for callee's presence
        → Offline/Busy: creates Call (Status=Missed), triggers push notification
        → Online: creates Call (Status=Ringing)
        → returns CallResultDto
    → Hub relays the correct event based on CallResultDto:
        IncomingCall / CalleeUnavailable / CalleeBusy / MissedCallNotification
```

### MediatR Pipeline Behaviors (in order)
1. **UnhandledExceptionBehavior** — logs any unexpected exception before it bubbles to the global handler
2. **ValidationBehavior** — runs FluentValidation validators registered for the request; throws `ValidationException` on failure (caught by global exception handler → 400)
3. **LoggingBehavior** — logs request name + key parameters (never logs password/JWT)
4. Handler executes

---

## 3. DB Layer: Repository + Unit of Work over EF Core

```csharp
// Application layer interfaces (implemented in Infrastructure)
public interface IRepository<T> where T : BaseEntity
{
    Task<T?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<T>> ListAsync(CancellationToken ct);
    void Add(T entity);
    void Remove(T entity);
}

public interface IUnitOfWork
{
    IRepository<User> Users { get; }
    IRepository<ConnectRequest> ConnectRequests { get; }
    IRepository<Connection> Connections { get; }
    IRepository<Call> Calls { get; }
    IRepository<Block> Blocks { get; }
    IRepository<Report> Reports { get; }
    IRepository<DeviceToken> DeviceTokens { get; }
    Task<int> SaveChangesAsync(CancellationToken ct);
}
```

- Handlers depend only on `IUnitOfWork` (from `Connect.Application.Common.Interfaces`) — never on `DbContext` directly. Keeps Application layer fully persistence-agnostic and testable (mock `IUnitOfWork` in unit tests).
- Complex queries (e.g. searching users, paginated call history) live in a dedicated **Query Service** or use `IApplicationDbContext` read-only projection directly with `.AsNoTracking()` for performance — CQRS convention: reads can bypass the repository abstraction since they don't mutate state, but writes always go through the Unit of Work.
- Repository implementations live in `Connect.Infrastructure.Persistence.Repositories`, backed by `ApplicationDbContext` (EF Core, SQL Server LocalDB now → Azure SQL later, same provider family, so no rewrite needed).

---

## 4. Middleware Pipeline (`Program.cs` order)

```
1. GlobalExceptionHandlerMiddleware   (custom — must be first, catches everything downstream)
2. Serilog request logging middleware
3. HTTPS redirection
4. CORS (restrict to explicit allowed origins — Flutter Web dev + production origin)
5. Authentication (JWT bearer; also reads token from `access_token` query param for SignalR handshake)
6. Authorization
7. Endpoint routing → Controllers + SignalR Hub (/hubs/call)
```

### Global Exception Handler
Catches all unhandled exceptions and maps them to consistent `ProblemDetails` JSON responses:

| Exception Type | HTTP Status | Example |
|---|---|---|
| `ValidationException` (FluentValidation) | 400 Bad Request | Invalid email format on register |
| `NotFoundException` | 404 Not Found | Calling a User ID that doesn't exist |
| `UnauthorizedAccessException` | 401 Unauthorized | Missing/expired JWT, or login attempt past the 60-day reactivation deadline |
| `ForbiddenAccessException` | 403 Forbidden | Trying to call a user you're not connected to, or viewing presence of a non-connected user |
| `ConflictException` | 409 Conflict | User ID already taken |
| Anything else (`Exception`) | 500 Internal Server Error | Unexpected — logged with full stack trace, generic message returned to client (never leak internals) |

```csharp
public class GlobalExceptionHandlerMiddleware
{
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        try { await next(context); }
        catch (ValidationException ex) { await WriteProblem(context, 400, ex.Errors); }
        catch (NotFoundException ex) { await WriteProblem(context, 404, ex.Message); }
        catch (ForbiddenAccessException ex) { await WriteProblem(context, 403, ex.Message); }
        catch (ConflictException ex) { await WriteProblem(context, 409, ex.Message); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception");
            await WriteProblem(context, 500, "An unexpected error occurred.");
        }
    }
}
```

---

## 5. API Endpoints (v1)

All REST endpoints follow explicit versioning under the `/api/v1` base route prefix. Auth endpoints (`/register`, `/login`) are rate-limited (`AuthRateLimit` policy: max 10 req/min per IP). Registration enforces password complexity rules (min 8 chars, 1 upper, 1 lower, 1 digit, 1 special char).

| Method | Endpoint | Purpose |
|---|---|---|
| POST | `/api/v1/auth/register` | Email + password + desired User ID signup (enforces length & complexity rules; rate limited) |
| POST | `/api/v1/auth/login` | Returns JWT. Silently reactivates soft-deleted account within 60-day window; rate limited |
| GET | `/api/v1/users/check-userid?value=xyz` | Live User ID availability check |
| GET | `/api/v1/users/search?query=xyz` | Search by User ID or phone number |
| PUT | `/api/v1/presence` | Update own presence status |
| GET | `/api/v1/presence/{userId}` | Get presence of a specific user — restricted to connected users or self (403 otherwise) |
| POST | `/api/v1/connect-requests` | Send a Connect Request |
| POST | `/api/v1/connect-requests/{id}/accept` | Accept a request |
| POST | `/api/v1/connect-requests/{id}/decline` | Decline a request |
| GET | `/api/v1/connect-requests/pending` | List incoming pending requests |
| GET | `/api/v1/connections` | List connected users (callable contacts) |
| POST | `/api/v1/calls/{callId}/end` | End an active call, logs duration. Shares `EndCallCommandHandler` with hub method |
| GET | `/api/v1/calls/history` | Paginated call history (90-day retention applied) |
| POST | `/api/v1/notifications/device-token` | Register/update an FCM device token for push notifications |
| POST | `/api/v1/users/{userId}/block` | Block a user |
| DELETE | `/api/v1/users/{userId}/block` | Unblock a user |
| GET | `/api/v1/users/blocked` | List blocked users |
| POST | `/api/v1/reports` | Report a user (reason + note) |
| DELETE | `/api/v1/account` | Soft-delete own account (60-day window). Reactivation happens automatically on next login |

**Deliberately not REST:** call initiation (`InitiateCallAttempt`) and call failure reporting (`NotifyCallFailed`) live only in the SignalR hub (`/hubs/call`).

### SignalR Hub — `/hubs/call`
Real-time, low-latency signaling that doesn't fit REST request/response. Every hub method that changes state delegates to a MediatR command internally (see Section 2) rather than containing business logic directly:

| Hub Method (client → server) | Purpose |
|---|---|
| `UpdatePresence(status)` | Broadcast online/offline/busy state |
| `InitiateCallAttempt(calleeId)` | **Call initiation lives here, not REST** — delegates to `InitiateCallCommand`, then relays `IncomingCall` / `CalleeUnavailable` / `CalleeBusy` based on the result |
| `RespondToCall(callId, accepted)` | Accept/reject |
| `SendWebRtcOffer` / `SendWebRtcAnswer` / `SendIceCandidate` | WebRTC signaling exchange (SDP + ICE candidates) |
| `EndCall(callId)` | Delegates to `EndCallCommand` (same handler as the REST `/end` endpoint), then broadcasts `CallEnded` |
| `NotifyCallFailed(callId, reason)` | Delegates to `FailCallCommand` (e.g. connection could not be established), then broadcasts `CallFailed` |
| `NotifyNetworkDrop(callId)` | Starts the 10-second auto-reconnect window, broadcasts `NetworkReconnecting` to the peer; auto-invokes `FailCallCommand` if not restored in time |
| `NotifyNetworkRestored(callId)` | Relays network recovery to the peer (`NetworkRestored`) |

| Hub Event (server → client) | Purpose |
|---|---|
| `IncomingCall` | Push the ringing UI to callee |
| `CallAccepted` / `CallRejected` | Update caller's UI |
| `CalleeUnavailable` / `CalleeBusy` | Presence-based rejection (per Sprint 3 rules) |
| `MissedCallNotification` | Real-time missed-call push to a busy/timed-out callee |
| `CallTimeout` | 15-second unanswered-ring timeout reached |
| `NetworkReconnecting` / `NetworkRestored` | Call reliability state changes during network drop |
| `CallFailed` | Call could not be established or recovered |
| `CallEnded` | Either party hung up |

---

## 6. Cross-Cutting Notes
- **Auth:** `ICurrentUserService` (Infrastructure) reads the JWT claims to expose `UserId` to any handler without touching `HttpContext` directly in Application layer.
- **Validation:** FluentValidation validators live next to each Command/Query, auto-discovered and run by `ValidationBehavior`.
- **Testing:** Because handlers depend on `IUnitOfWork` interfaces, Application layer unit tests mock everything — no real DB needed. Integration tests in `Connect.Api.IntegrationTests` spin up the real pipeline against an in-memory or test SQLite DB.
- **Tiering hook:** `ICurrentUserService` or the `User` entity exposes `SubscriptionTier` — handlers for premium-gated features (post-MVP) check this without any structural change needed later.
- **Background jobs:** `CallHistoryPurgeBackgroundService` and `ExpiredAccountsPurgeBackgroundService` are `IHostedService` implementations running on periodic intervals, each invoking a corresponding MediatR command (`PurgeOldCallHistoryCommand`, `PurgeExpiredAccountsCommand`) — the same commands can also be invoked directly for deterministic testing.
