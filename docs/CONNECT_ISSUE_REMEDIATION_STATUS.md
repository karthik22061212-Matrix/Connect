# CONNECT — ISSUE REMEDIATION STATUS

**Last Updated:** September 4, 2026 (TURN implementation complete and verified cross-network)
**Branch:** `sprint-7.6/flutter-webrtc-dependency`
**Latest verified commit on origin:** `6429e1a` (`fix: finalize TURN and production deployment configuration`)

---

## Production Diagnostic Logging Architecture — FINAL DEVELOPMENT REQUIREMENT

This is the final logging architecture to complete the development workstream.

### Development
- Developer Tools diagnostic logging may be enabled.
- Developer Tools may provide local inspection and log download for development/testing.
- Client logs remain user/session scoped.

### Production
- End users must not have access to diagnostic logs through the UI or Developer Tools.
- Developer diagnostic UI/log download is disabled in production builds.
- Frontend and backend diagnostic events must be associated with the relevant user/session and correlation context.
- An admin/support-only diagnostic mechanism provides the complete technical history for a specific user/case.
- The admin download combines relevant frontend, backend, SignalR and WebRTC diagnostic events into one chronological downloadable report.
- No user can retrieve another user’s diagnostic logs.
- Diagnostic endpoints are not exposed as normal user-facing UI functionality.

### Internal Diagnostic Endpoints
The planned support endpoints are:
- `GET /api/v1/admin/diagnostics/{userId}/download` — retrieve one combined user-scoped diagnostic report.
- `POST /api/v1/admin/diagnostics/{userId}/clear` — clear the selected user’s diagnostic data.

These endpoints must be admin/support authorized and must never expose secrets, passwords, JWTs, TURN shared secrets/credentials, SDP, or ICE credentials.

### Lifecycle
- A user’s diagnostics begin with their authenticated session/context.
- Refresh continues the same user/session diagnostic context where appropriate.
- Logout clears the user’s client-side diagnostic context.
- A new user must never inherit the previous user’s diagnostic data.
- Diagnostic data remains scoped to the correct user/session and correlation context.

### Final Development Boundary
The combined admin diagnostic download is the final logging-related development task. After this requirement is implemented and verified, the logging architecture is considered complete for the current development workstream. Future changes are production operations/observability enhancements rather than part of the current feature-development sequence.

---

## 1. Current State

Sprint 7.6 direct WebRTC audio is proven on the same network and the call-teardown bug was fixed. The JWT timer overflow was fixed and pushed. A real Wi-Fi ↔ mobile-data test successfully established a call with clear two-way audio using an Azure TURN relay. TURN fallback was required, and the frontend dynamically integrated with the backend secure TURN credentials to successfully authenticate, allocate, and utilize a `typ relay` candidate.

The DEV/PROD configuration baseline is complete, the dual-user frontend session-slot mechanism has been removed, and both changes are committed and pushed. The secure TURN credential backend foundation, Coturn shared-secret REST authentication, and Flutter TURN retrieval are complete and pushed. The Azure deployment script includes the latest fixes. The next tasks involve completing the remaining P0 UX and stability items.

---

## 2. WebRTC / P0 Status

| Item | Status |
|---|---|
| P0-1 Audio quality | ✅ Complete — echo cancellation, noise suppression and auto gain control verified at runtime. Audio quality validated from successful real call |
| P0-2 Call connection speed / setup race | ✅ Implementation complete; normal accepted-call regression passed; generation-counter cancellation protection is in place |
| P0-3 Microphone permissions | ⬜ Not started |
| P0-4 Network recovery / reconnection | ⬜ Not proven |
| P0-5 TURN fallback | ✅ Complete — implemented and manually verified cross-network |
| P0-6 Call-exit regression | 🟠 Shared teardown wired; individual live verification of all exit paths remains |

### P0-2 Evidence

Callee WebRTC setup starts immediately after Accept. `_webRTCSetupFuture` prevents duplicate setup and `_webRTCSetupGeneration` invalidates stale asynchronous setup after teardown. Same-network calls achieved fast remote playback and clean teardown.

### P0-5 Cross-network Success

The live test used one device on home Wi-Fi and another on mobile carrier data. SignalR, SDP offer/answer and ICE candidate exchange all succeeded, including a browser-generated relay candidate (`typ relay`). TURN authentication and allocation passed. The media path was successfully established via Azure Coturn relay, and two-way audio was clearly heard.

### P0-5 Backend & TURN Infrastructure

Implemented and tested:
- `TurnCredentialsDto`, `ITurnCredentialService`, `TurnSettings`, `TurnCredentialService`
- authenticated `GET /api/v1/turn/credentials`
- Frontend dynamically retrieves short-lived TURN credentials.

Azure TURN Infrastructure details:
- VM: `connect-turn-vm` (Standard_B2ats_v2, B1s rejected by capacity restrictions)
- Public IP: 52.172.234.96
- Private IP: 10.0.0.4
- Coturn service is working.
- Config uses: `external-ip=PUBLIC_IP/PRIVATE_IP`, `relay-ip=PRIVATE_IP`, `static-auth-secret` directly in `turnserver.conf`. Unsupported include mechanism removed. TLS listening port disabled.
- **Note:** TURN VM is currently DEALLOCATED after testing to control cost.

---

## 3. Azure / Deployment Issues

### AZURE-DEPLOY-001 — RESOLVED
Missing Linux SqlClient native binaries were fixed by publishing with the Linux RID. Azure health subsequently reported a healthy database connection.

### AZURE-DEPLOY-002 — RESOLVED
Azure SQL migrations were applied and live registration succeeded.

### FRONTEND-001 — RESOLVED
JWT expiry timer overflow was fixed by capping individual timers at 20 days and recomputing actual remaining expiry time. Fix is committed as `0a6ffc6` and pushed.

### SECURITY-NOTE-001 — OPEN
Azure SQL admin password was previously entered in plaintext during debugging. It still needs rotation.

---

## 4. Configuration Architecture — BASELINE COMPLETE

The project needs one clear environment model before more deployment work.

### Development
- Frontend fixed port: `8080`
- Backend API fixed port: `5234`
- Local secrets/configuration stored outside Git
- One simple development entry point

### Production
- Frontend URL supplied by deployment configuration
- Backend URL supplied by deployment configuration
- TURN endpoint supplied by deployment configuration
- Production secrets stored in Azure App Service Configuration and/or Azure Key Vault
- No production secrets in Dart, checked-in JSON, `.env` files, scripts, or documentation
- Checked-in `.example` templates show required configuration names without real secret values
- One simple production deployment entry point

This configuration model should own API URLs, ports, CORS, JWT settings, database connection strings, TURN settings and other environment-specific values.

---

## 5. Dual-Session Development Code — COMPLETE

The development-only dual-session mechanism has been removed from `frontend/lib/main.dart`.

Removed:
- `_user1Session`
- `_user2Session`
- `_activeSessionIndex`
- slot-specific refresh/login/registration handling
- `connect_u2_*` local-storage keys
- User 1 / User 2 session switching UI

Current model:
- one `UserSession` per browser/device
- one refresh operation at a time
- one SignalR identity per authenticated browser session

Two-user testing now uses separate browsers/profiles/devices. The refactor was committed as `40ea0f8` and pushed.

---

## 6. Immediate Next Task Queue

1. Complete P0-3 microphone permission UX, P0-4 network recovery and remaining P0-6 exit-path live tests.
2. Rotate the exposed Azure SQL admin password and previously exposed static TURN secret.
3. Finalize production deployment automation and configuration verification.
4. Complete Sprint 7.6 regression, independently verify completion criteria, then merge the sprint branch.
5. Resume TEST-002 and later Batch 3 work.

---

## 7. One-Line Remediation State

**Direct WebRTC is proven and same-network audio works; cross-network audio is now verified via Azure Coturn relay; backend TURN credential generator, configuration baseline, Coturn infrastructure, and single-session frontend refactor are complete and pushed; the immediate blocker is remaining P0 items (microphone UX, network recovery, exit-path testing) and final credential rotation.**
