# Progress Tracker: Connect

This file is the single source of truth for what's done. Every new Antigravity/BMAD session
MUST read this file first, continue from the first unchecked item, and check off items
as they're completed — updating this file is part of finishing a task, not optional.

**Rule: update this file in the same commit as the code that completes each item.**

---

## Prerequisites
- [x] Firebase project created, FCM enabled
- [x] Local tooling installed (.NET 8 SDK, Flutter SDK, Git)
- [x] Docs `00`–`07` committed to `docs/`
- [x] SQL Server LocalDB confirmed working

## Sprint 0 — Local Scaffolding
- [x] `Connect.Domain` project created (empty, base entities/enums stubbed)
- [x] `Connect.Application` project created (MediatR, FluentValidation, AutoMapper registered)
- [x] `Connect.Infrastructure` project created (EF Core, LocalDB connection wired)
- [x] `Connect.Api` project created (Program.cs, appsettings.json, Swagger enabled)
- [x] Solution builds and runs locally (`dotnet run` succeeds, Swagger UI loads)
- [x] Flutter Web project created, runs locally (`flutter run -d chrome`)
- [x] Flutter project successfully calls a test "health check" endpoint on the API
- [x] Global exception handler middleware wired and tested
- [x] Initial EF Core migration created from `07-database-schema.md`, applied to LocalDB

## Sprint 1 — Auth & Identity
- [x] `RegisterUser` command + handler + validator
- [x] `Login` command + handler (JWT issuance)
- [x] `CheckUserIdAvailability` query
- [x] ASP.NET Core Identity wired to `User` entity
- [x] JWT authentication middleware configured
- [x] Endpoints tested via Swagger

## Sprint 2 — Directory & Connect Requests
- [x] `SearchUsers` query (by User ID or phone number)
- [x] `SendConnectRequest` command
- [x] `AcceptConnectRequest` / `DeclineConnectRequest` commands
- [x] `GetPendingRequests` / `GetConnections` queries
- [x] Connection uniqueness/ordering rule enforced (per `07-database-schema.md`)

## Sprint 3 — Presence & Signaling
- [x] SignalR `CallHub` scaffolded, mapped in `Program.cs`
- [x] `UpdatePresence` hub method + presence tracking
- [x] "Unavailable" / "Busy" logic implemented per `01-project-brief.md` rules
- [x] Real-time missed-call notification to busy user

## Sprint 4 — Core Calling (WebRTC)
- [x] `InitiateCall` command (presence check, creates `Call` row)
- [x] SignalR WebRTC signaling methods (offer/answer/ICE candidate relay)
- [x] Ring timeout (15 sec) implemented
- [x] `EndCall` command, call duration calculated
- [ ] **CORRECTION (found 2026-08-26, during Sprint 7.5):** actual WebRTC audio layer is NOT implemented. No `flutter_webrtc` dependency, no `RTCPeerConnection`, `getUserMedia`, or `MediaStream` anywhere in `frontend/lib/main.dart`. Only SignalR signaling (offer/answer/ICE relay messages) exists — no audio has ever actually been transmitted between callers. STUN/TURN has no client-side consumer yet.

## Sprint 5 — Call Reliability
- [x] Auto-reconnect logic on network drop (10-sec window)
- [x] "Network is low / reconnecting" state broadcast
- [x] Retry-once on initial connection failure
- [x] "Call failed" handling + missed-call logging

## Sprint 6 — History, Push, Trust & Safety
- [x] `GetCallHistory` query (paginated)
- [x] FCM push notification service implemented
- [x] Push triggered on incoming call + missed call
- [x] `BlockUser` / `UnblockUser` / `GetBlockedUsers`
- [x] `ReportUser` command

## Sprint 7 — Account Lifecycle & Polish
- [x] `SoftDeleteAccount` command (60-day window logic)
- [x] Silent account auto-reactivation via `LoginCommandHandler` (within 60-day window)
- [x] Background job: purge call history older than 90 days (`CallHistoryPurgeBackgroundService`)
- [x] Background job: permanently purge accounts past 60-day reactivation deadline (`ExpiredAccountsPurgeBackgroundService`)
- [x] End-to-end testing pass on Web (local) (all 9 user flows verified through Flutter Web client + local API, all 54 unit tests passing)

## Sprint 7.4 — Hardening & CI/CD (pre-Azure, no cloud dependency)
- [x] GitHub Actions workflow: build + run tests on every push/PR
- [x] Password policy rules enforced (min length, complexity) on registration
- [x] Rate limiting added on auth endpoints (register, login)
- [x] CORS policy tightened to explicit allowed origins
- [x] Environment-based configuration structure (appsettings.Development.json vs appsettings.Production.json)
- [x] JWT signing key and other secrets moved to .NET User Secrets for local dev (not hardcoded)
- [x] Swagger/OpenAPI reviewed for completeness; API versioning convention decided (even if just "v1" prefix for now)

## Sprint 7.5 — Azure Migration
- [ ] Azure subscription active
- [ ] Azure CLI provisioning script run (App Service, Azure SQL, Static Web App, TURN VM)
- [ ] Connection strings/config switched from LocalDB to Azure SQL
- [ ] Backend deployed to Azure App Service
- [ ] Flutter Web deployed to Azure Static Web Apps
- [ ] coturn TURN server configured and reachable

## Sprint 7.6 — WebRTC Media Layer (added 2026-08-26, addresses Sprint 4 correction above)
- [ ] Add `flutter_webrtc` dependency to `frontend/pubspec.yaml`
- [ ] Implement `getUserMedia` mic capture with permission handling (Web)
- [ ] Implement `RTCPeerConnection` wired to existing SignalR offer/answer/ICE events
- [ ] Wire `iceServers` config: STUN (public) + TURN (`turn:52.172.234.96:3478`, see `azure-deployment-info.env`)
- [ ] Verify real two-way audio on an actual call (not just signaling state)
- [ ] Handle renegotiation / cleanup on call end

## Sprint 8 — Web MVP Release
- [ ] Final QA on live Azure deployment
- [ ] Stable Web release tagged/deployed
- [ ] Feedback collection mechanism in place

## Sprint 9+ — Mobile Conversion
- [ ] Not started — out of scope until Sprint 8 is complete

---

## Session Log
*(Each Antigravity session should append one line here when it stops, noting where it left off.)*

- Session 1: Completed Sprint 0 local scaffolding — created Connect.Domain, Connect.Application, Connect.Infrastructure, and Connect.Api projects, wired Serilog request logging and Global Exception Handler, created and applied InitialCreate EF Core migration to SQL Server LocalDB, scaffolded Flutter Web application, integrated health check endpoint, verified local build and Swagger UI. Sprint 0 complete.
- Session 2: Completed Sprint 1 Auth & Identity — implemented RegisterUser command, Login command, CheckUserIdAvailability query, wired ASP.NET Core Identity password hashing to User entity, configured JWT authentication middleware and Swagger UI Bearer authorization, added unit tests covering Auth & Users features. Sprint 1 complete.
- Session 3: Confirmed server-side RegisterUserCommandHandler uniqueness enforcement (explicit pre-checks + DbUpdateException handling for unique DB indexes returning ConflictException 409). Completed Sprint 2 Directory & Connect Requests — implemented SearchUsers query, SendConnectRequest command, AcceptConnectRequest & DeclineConnectRequest commands, GetPendingRequests query, GetConnections query, and enforced Connection UserAId < UserBId ordering rule with unit test coverage. Sprint 2 complete.
- Session 4: Confirmed Sprint 2 filtered unique index and handler checks for duplicate Connect Requests. Completed Sprint 3 Presence & Signaling — implemented in-memory PresenceTracker, CallHub mapped at /hubs/call, JWT query parameter authentication, UpdatePresence command and REST controller, call attempt presence logic (Unavailable/Busy/Ringing), and real-time missed call notifications for busy users. All 17 unit tests passing. Sprint 3 complete.
- Session 5: Confirmed and enforced Sprint 3 GET /api/presence/{userId} connection check (403 Forbidden for non-connected users). Completed Sprint 4 Core Calling (WebRTC) — implemented CQRS InitiateCallCommand and EndCallCommand with MediatR delegation in CallHub and CallsController, 15-second ring timeout, WebRTC signaling relay methods, unit test coverage (34 passing tests), and Flutter Web voice calling overlay UI. Sprint 4 complete.
- Session 6: Completed Sprint 5 Call Reliability — implemented CQRS FailCallCommand and FailCallCommandHandler, 10-second auto-reconnect window timer for network drops, "Network is low / reconnecting" state broadcast via SignalR (NetworkReconnecting/NetworkRestored), initial connection single-retry logic, REST endpoint POST /api/calls/{callId}/fail in CallsController, unit test coverage (37 passing tests), and updated Flutter Web UI for reconnecting and call failed states. Sprint 5 complete.
- Session 7: Confirmed CallHub.NotifyCallFailed delegates to FailCallCommand via MediatR. Removed redundant REST endpoint POST /api/calls/{callId}/fail from CallsController (keeping real-time call transitions hub-only). All 37 unit tests passing. Ready to start Sprint 6.
- Session 8: Completed Sprint 6 History, Push, Trust & Safety — implemented paginated GetCallHistoryQuery with 90-day retention filter, RegisterDeviceTokenCommand, FcmPushNotificationService with FCM push triggers for incoming and missed calls (Offline, Busy, Timeout), BlockUserCommand, UnblockUserCommand, GetBlockedUsersQuery, ReportUserCommand, REST endpoints in CallsController, NotificationsController, BlockingController, ReportsController, and unit tests (42 passing tests). Sprint 6 complete.
- Session 9: Answered Sprint 5 questions with code proof. Completed Sprint 7 Account Lifecycle & Polish — implemented SoftDeleteAccountCommand (`DELETE /api/account`), silent account auto-reactivation on login within 60-day window in LoginCommandHandler, background purge services for 90-day call history and 60-day expired accounts, and 12 new unit tests (54 passing unit tests total). Sprint 7 complete.
- Session 10: Completed full End-to-end testing pass on Web (local) — built interactive functional test UI in Flutter Web app (`frontend/lib/main.dart`) supporting SignalR hub connection and dual session switcher. Executed complete 9-step E2E flow against running local API server (`http://localhost:5200`): handle availability check, registration, search, send/accept connect request, voice calling, call history, ring timeout missed call logging, block/unblock enforcement (verified 403 Forbidden when blocked), report user, and soft-delete account with silent reactivation upon login. Fixed DTO JSON property key mappings and CallsController SignalR imports. All 54 backend unit tests passing. Sprint 7 fully complete.
- Session 11: Sprint 7.4 (Hardening & CI/CD) inserted into the plan — Azure subscription not yet active, so this sprint provides productive work with no cloud dependency while waiting.
- Session 12: Completed Sprint 7.4 Hardening & CI/CD — created GitHub Actions CI workflow (`.github/workflows/ci.yml`), enforced registration password complexity rules (length + uppercase, lowercase, digit, special char) with unit tests (`RegisterUserCommandValidatorTests.cs`), added .NET 8 fixed-window rate limiting on `/api/v1/auth/register` and `/api/v1/auth/login`, tightened CORS policy to explicit configured allowed origins, separated `appsettings.Development.json` and `appsettings.Production.json`, configured `<UserSecretsId>` in `Connect.Api.csproj`, updated all REST controller routes to `/api/v1/...`, updated OpenAPI/Swagger documentation, and updated Flutter Web client HTTP API requests to `/api/v1/...`. All 60 unit tests passing. Sprint 7.4 fully complete.

- Session 13: Discovered during Sprint 7.5 TURN verification that no WebRTC media/audio implementation exists in the Flutter client — only SignalR signaling was ever built. Corrected Sprint 4 tracker entry and added Sprint 7.6 (WebRTC Media Layer) before Sprint 8. Azure infra provisioning (Sprint 7.5) confirmed complete: SQL, App Service, Static Web App, and TURN VM all live; coturn installed and verified relaying via trickle-ice test (turn:52.172.234.96:3478).