# CONNECT PROJECT STATUS

**Last Updated:** September 6, 2026
**Branch:** `main` (sprint-7.6 work merged in, commit `4b952c6`, plus follow-up fixes through `f5e9d6f`)

## 1. Current Architecture / Platform
- **Frontend:** Flutter Web
- **Backend:** ASP.NET Core Web API
- **Framework:** .NET 8
- **Architecture:** Clean Architecture / CQRS / MediatR
- **Realtime:** SignalR
- **Media:** WebRTC
- **Database:** SQL Server / Azure SQL
- **Deployment:** Azure App Service, Azure Static Web Apps
- **Network Traversal:** TURN infrastructure (coturn on Azure VM)

## 2. Current Completed Functionality
The following features are actually implemented and present in the current repository:
- **Authentication:** Registration, Login, JWT authentication, Refresh token flow
- **Directory:** User ID availability and search
- **Connections:** Connection requests (send, accept, decline), Connected-user list
- **Calling:** SignalR signaling, 15-second ringing timeout, Caller/callee hangup, Two-way audio (WebRTC), Cross-network calling (TURN), Call teardown hardening
- **History:** Call history
- **Presence:** Realtime presence, Presence visibility/privacy (Everyone, Connections Only, Nobody, Custom)
- **Trust & Safety:** Blocking, Reporting
- **Diagnostics:** Structured frontend and backend diagnostic event ingestion and export
- **Account:** Soft-delete and login-based reactivation
- **Frontend/Web Functionality:** Microphone capture, permission handling, error retry, bounded ICE restart recovery

## 3. Authentication Roadmap / Flows

### Implemented
1. **Email / Password Login:** Client → API → credential validation → JWT + refresh token
2. **Email / Password Registration:** Client → API → account creation → JWT + refresh token
3. **JWT Access Token Flow:** Used for all authenticated endpoints.
4. **Refresh Token Flow:** Silent refresh when access token expires.
5. **Logout Flow:** Client clears local storage session, user is redirected.

### Planned / Future
6. **Google Login (Planned)**
   *Flow:* User → Google OAuth/Identity Provider → backend identity validation → existing/new Connect account → Connect JWT/session → application

7. **Apple Login (Planned)**
   *Flow:* User → Apple Sign In → backend identity/token validation → existing/new Connect account → Connect JWT/session → application

**Important Authentication Rules (for Google & Apple integration):**
- Provider identity must be validated by the server.
- Do not trust arbitrary provider IDs sent from the client.
- Account linking must be deterministic.
- Duplicate-account creation must be prevented.
- Email matching alone should not blindly merge accounts unless product/security rules explicitly allow it.
- Apple private relay email behavior must be considered.
- Provider subject IDs should be treated as stable identity identifiers.
- Logout and refresh token handling should remain consistent with the normal Connect authentication model.

## 4. Current Blockers / Open Issues (Priority Queue)
1. **C5** — Disconnect existing connection (Feature gap)
2. **S3** — Clear search results when search query becomes empty
3. **Continue manual QA** using CONNECT_TEST_FLOWS.md, starting with the next unexecuted test
4. **Presence visibility manual tests** P1–P6
5. **W4** call exit/teardown live regression
6. **TEST-002** concurrency testing
7. **R2** duplicate-report product decision
8. **Production cleanup / deployment hardening** (e.g., Azure SQL credential rotation, Azure NSG SSH cleanup)
9. **Batch 3 remediation** (SEC-002/006/007, RT-001/007/008/009, API-*, DOM-002/004, CODE-*)
10. **Sprint 8** Web MVP release (Unblocked pending C5 and manual QA)

## 5. Recent Fixes (Sprint 7.6 & Remediation)
- **WebRTC:** coturn `lt-cred-mech` and `use-auth-secret` conflict resolved.
- **Calling:** Call teardown properly stops mic and peer connection on rejection/timeout/busy.
- **Frontend:** JWT expiry timer 32-bit overflow capped at 20 days.
- **Security:** DiagnosticsController IDOR fixed (user-ownership validated).
- **Deployment:** EF Core migrations now auto-apply in dev and prod scripts.
