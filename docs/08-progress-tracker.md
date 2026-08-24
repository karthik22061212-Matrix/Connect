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
- [ ] `RegisterUser` command + handler + validator
- [ ] `Login` command + handler (JWT issuance)
- [ ] `CheckUserIdAvailability` query
- [ ] ASP.NET Core Identity wired to `User` entity
- [ ] JWT authentication middleware configured
- [ ] Endpoints tested via Swagger

## Sprint 2 — Directory & Connect Requests
- [ ] `SearchUsers` query (by User ID or phone number)
- [ ] `SendConnectRequest` command
- [ ] `AcceptConnectRequest` / `DeclineConnectRequest` commands
- [ ] `GetPendingRequests` / `GetConnections` queries
- [ ] Connection uniqueness/ordering rule enforced (per `07-database-schema.md`)

## Sprint 3 — Presence & Signaling
- [ ] SignalR `CallHub` scaffolded, mapped in `Program.cs`
- [ ] `UpdatePresence` hub method + presence tracking
- [ ] "Unavailable" / "Busy" logic implemented per `01-project-brief.md` rules
- [ ] Real-time missed-call notification to busy user

## Sprint 4 — Core Calling (WebRTC)
- [ ] `InitiateCall` command (presence check, creates `Call` row)
- [ ] SignalR WebRTC signaling methods (offer/answer/ICE candidate relay)
- [ ] Ring timeout (15 sec) implemented
- [ ] `EndCall` command, call duration calculated

## Sprint 5 — Call Reliability
- [ ] Auto-reconnect logic on network drop (10-sec window)
- [ ] "Network is low / reconnecting" state broadcast
- [ ] Retry-once on initial connection failure
- [ ] "Call failed" handling + missed-call logging

## Sprint 6 — History, Push, Trust & Safety
- [ ] `GetCallHistory` query (paginated)
- [ ] FCM push notification service implemented
- [ ] Push triggered on incoming call + missed call
- [ ] `BlockUser` / `UnblockUser` / `GetBlockedUsers`
- [ ] `ReportUser` command

## Sprint 7 — Account Lifecycle & Polish
- [ ] `SoftDeleteAccount` command (60-day window logic)
- [ ] `ReactivateAccount` command
- [ ] Background job: purge call history older than 90 days
- [ ] Background job: permanently purge accounts past 60-day reactivation deadline
- [ ] End-to-end testing pass on Web (local)

## Sprint 7.5 — Azure Migration
- [ ] Azure subscription active
- [ ] Azure CLI provisioning script run (App Service, Azure SQL, Static Web App, TURN VM)
- [ ] Connection strings/config switched from LocalDB to Azure SQL
- [ ] Backend deployed to Azure App Service
- [ ] Flutter Web deployed to Azure Static Web Apps
- [ ] coturn TURN server configured and reachable

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
