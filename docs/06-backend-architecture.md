# Backend Architecture: Connect API

Stack: ASP.NET Core Web API (.NET 8) · Clean Architecture · CQRS via MediatR · Repository + Unit of Work over EF Core

---

## 1. Solution / Folder Structure

```
backend/
  src/
    Connect.Domain/                    # Enterprise-wide business rules, no dependencies on anything
      Entities/                        # User, ConnectRequest, Connection, Call, CallHistory, Block, Report
      Enums/                           # PresenceStatus, CallStatus, ConnectRequestStatus, SubscriptionTier
      Exceptions/                      # DomainException and subtypes
      Common/                          # BaseEntity, AuditableEntity (CreatedAt, UpdatedAt)

    Connect.Application/               # Use cases (CQRS), depends only on Domain
      Common/
        Behaviors/                     # MediatR pipeline behaviors (Validation, Logging, UnhandledException)
        Interfaces/                    # IUnitOfWork, IRepository<T>, ICurrentUserService, IDateTimeProvider,
                                        # IJwtTokenGenerator, IPushNotificationService
        Exceptions/                    # ValidationException, NotFoundException, ForbiddenAccessException,
                                        # ConflictException (e.g. duplicate User ID)
        Mappings/                      # Mapping profiles (AutoMapper or manual extension methods)
      Features/                        # Vertical grouping by feature, CQRS inside Clean Architecture
        Auth/
          Commands/RegisterUser/
          Commands/Login/
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
        Calls/
          Commands/InitiateCall/
          Commands/EndCall/
          Queries/GetCallHistory/
        Blocking/
          Commands/BlockUser/
          Commands/UnblockUser/
          Queries/GetBlockedUsers/
        Reports/
          Commands/ReportUser/
        Account/
          Commands/SoftDeleteAccount/
          Commands/ReactivateAccount/
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
        CallHub.cs                      # SignalR hub
        PresenceTracker.cs              # Tracks connected users (in-memory or Redis later)
      Notifications/
        FcmPushNotificationService.cs   # Firebase Cloud Messaging integration
      DependencyInjection.cs            # AddInfrastructure() extension

    Connect.Api/                        # Presentation layer, depends on Application + Infrastructure
      Controllers/                      # Thin controllers — just dispatch to MediatR
      Middleware/
        GlobalExceptionHandlerMiddleware.cs
      Hubs/                             # Hub route mapping (or hub lives in Infrastructure, mapped here)
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
- Controllers never contain business logic — they just build a Command/Query and call `_mediator.Send(...)`.

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
4. CORS (restrict to Flutter Web origin)
5. Authentication (JWT bearer)
6. Authorization
7. Endpoint routing → Controllers + SignalR Hub (/hubs/call)
```

### Global Exception Handler
Catches all unhandled exceptions and maps them to consistent `ProblemDetails` JSON responses:

| Exception Type | HTTP Status | Example |
|---|---|---|
| `ValidationException` (FluentValidation) | 400 Bad Request | Invalid email format on register |
| `NotFoundException` | 404 Not Found | Calling a User ID that doesn't exist |
| `UnauthorizedAccessException` | 401 Unauthorized | Missing/expired JWT |
| `ForbiddenAccessException` | 403 Forbidden | Trying to call a user you're not connected to |
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

## 5. API Endpoints (MVP)

| Method | Endpoint | Purpose |
|---|---|---|
| POST | `/api/auth/register` | Email + password + desired User ID signup |
| POST | `/api/auth/login` | Returns JWT |
| GET | `/api/users/check-userid?value=xyz` | Live User ID availability check |
| GET | `/api/users/search?query=xyz` | Search by User ID or phone number |
| POST | `/api/connect-requests` | Send a Connect Request |
| POST | `/api/connect-requests/{id}/accept` | Accept a request |
| POST | `/api/connect-requests/{id}/decline` | Decline a request |
| GET | `/api/connect-requests/pending` | List incoming pending requests |
| GET | `/api/connections` | List connected users (callable contacts) |
| POST | `/api/calls/{connectionId}/initiate` | Start a call attempt (presence-checked; also triggers SignalR invite) |
| POST | `/api/calls/{callId}/end` | End an active call, logs duration |
| GET | `/api/calls/history` | Paginated call history (90-day retention applied) |
| POST | `/api/users/{userId}/block` | Block a user |
| DELETE | `/api/users/{userId}/block` | Unblock a user |
| GET | `/api/users/blocked` | List blocked users |
| POST | `/api/reports` | Report a user (reason + note) |
| DELETE | `/api/account` | Soft-delete own account (60-day window) |
| POST | `/api/account/reactivate` | Reactivate within 60-day window |

### SignalR Hub — `/hubs/call`
Real-time, low-latency signaling that doesn't fit REST request/response:

| Hub Method (client → server) | Purpose |
|---|---|
| `UpdatePresence(status)` | Broadcast online/offline/busy state |
| `SendCallInvite(toUserId)` | Notify callee of incoming call |
| `RespondToCall(callId, accepted)` | Accept/reject |
| `SendWebRtcOffer/Answer/IceCandidate` | WebRTC signaling exchange (SDP + ICE candidates) |
| `EndCall(callId)` | Broadcast hang-up to the other party |

| Hub Event (server → client) | Purpose |
|---|---|
| `IncomingCall` | Push the ringing UI to callee |
| `CallAccepted` / `CallRejected` | Update caller's UI |
| `CalleeUnavailable` / `CalleeBusy` | Presence-based rejection (per Sprint 3 rules) |
| `NetworkReconnecting` / `CallEnded` | Reliability + teardown events |

---

## 6. Cross-Cutting Notes
- **Auth:** `ICurrentUserService` (Infrastructure) reads the JWT claims to expose `UserId` to any handler without touching `HttpContext` directly in Application layer.
- **Validation:** FluentValidation validators live next to each Command/Query, auto-discovered and run by `ValidationBehavior`.
- **Testing:** Because handlers depend on `IUnitOfWork` interfaces, Application layer unit tests mock everything — no real DB needed. Integration tests in `Connect.Api.IntegrationTests` spin up the real pipeline against an in-memory or test SQLite DB.
- **Tiering hook:** `ICurrentUserService` or the `User` entity exposes `SubscriptionTier` — handlers for premium-gated features (post-MVP) check this without any structural change needed later.
