# CONNECT PROJECT STATUS

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

## 1. Headline Update

Connect's direct and cross-network WebRTC audio paths are proven. A real two-device Wi-Fi ↔ mobile-data test successfully established a call with clear two-way audio. TURN fallback was required, and the frontend dynamically integrated with the backend secure TURN credentials to successfully authenticate, allocate, and utilize a `typ relay` candidate via Azure Coturn.

The DEV/PROD configuration baseline is complete. The Azure App Service backend, Static Web Apps frontend, and Azure SQL database are online and tested. The production deployment script includes the latest fixes. The TURN VM is correctly configured with public/private NAT mapping and is currently deallocated to control costs. The next priority is remaining UX and stability items.

---

## 2. WebRTC Audio Status

| Item | Status |
|---|---|
| Same-network two-way audio | ✅ Confirmed |
| Call-end teardown | ✅ Fixed and verified |
| Teardown wiring on reject/timeout/busy/unavailable/hub disconnect | ✅ Code-wired; individual live verification remains |
| P0-1 audio quality | ✅ Complete — Audio quality validation from the successful real call |
| P0-2 faster setup / cancellation safety | ✅ Complete based on existing implementation/verification |
| P0-3 microphone permission UX | ⬜ Not started |
| P0-4 network recovery | ⬜ Not proven |
| P0-5 TURN fallback | ✅ Complete — implemented and manually verified cross-network |
| P0-6 exit-path regression | 🟠 Partially verified unless all exit paths were individually tested |

---

## 3. Cross-Network Test — VERIFIED WITH TURN

The latest major test used two real devices on different networks:

```text
User 1 → home Wi-Fi
User 2 → mobile carrier data
```

Observed:
- SignalR call signaling succeeded.
- Call was accepted.
- SDP offer/answer succeeded.
- ICE candidates were exchanged, including a browser-generated relay candidate (`typ relay`).
- TURN authentication and allocation passed.
- Media path established via Azure Coturn relay.
- Audio was successfully heard across networks.

Conclusion: The signaling system and TURN relay infrastructure are fully working. Cross-network audio is verified.

---

## 4. Secure TURN Credential Backend — COMPLETE AND VERIFIED

Implemented and verified:

- `TurnCredentialsDto`, `ITurnCredentialService`, `TurnSettings`, `TurnCredentialService`
- authenticated `GET /api/v1/turn/credentials`
- Frontend TURN credentials are dynamically fetched from the backend.
- Frontend/backend TURN integration is verified.

TURN infrastructure details (Azure):
- VM: `connect-turn-vm` (Standard_B2ats_v2, B1s rejected by capacity restrictions)
- Public IP: 52.172.234.96
- Private IP: 10.0.0.4
- Coturn service is working.
- Config uses: `external-ip=PUBLIC_IP/PRIVATE_IP`, `relay-ip=PRIVATE_IP`, `static-auth-secret` directly in `turnserver.conf`.
- Unsupported coturn include mechanism was removed.
- TLS listening port 5349 is disabled (no TURN TLS certificates configured).
- **Note:** TURN VM is currently DEALLOCATED after testing to control cost.

TURN stages:

```text
Backend credential generator ✅
        ↓
Coturn shared-secret REST auth ✅
        ↓
Secure Flutter credential retrieval ✅
        ↓
TURN relay verification ✅
        ↓
Wi-Fi ↔ mobile-data audio verification ✅
```

---

## 5. Configuration Architecture — BASELINE COMPLETE

The predictable environment configuration baseline is now implemented and committed.

### Development

```text
UI  → localhost:8080
API → localhost:5234
```

Local development secrets/configuration must come from an ignored local source or supported local secret store.

### Production

```text
UI  → deployment-provided URL
API → deployment-provided URL
TURN → deployment-provided endpoint
```

Production secrets belong in Azure App Service Configuration and/or Azure Key Vault.

Current baseline:
- checked-in `.example` templates show safe configuration names/values
- local `appsettings.Development.json` is ignored and no longer tracked
- DEV entry point: `scripts/dev.ps1`
- PROD entry-point structure: `scripts/deploy-prod.ps1` (contains latest fixes)
- production secrets belong in Azure App Service Configuration and/or Azure Key Vault
- no production secrets are committed to source control
- TURN endpoint and secret are not hard-coded in Flutter
- Backend is deployed on Azure App Service. Frontend is deployed on Azure Static Web Apps. Azure SQL database is online.

The model centralizes environment-specific values such as API URLs, ports, CORS origins, database connection strings, JWT settings, TURN endpoint and TURN shared secret.

---

## 6. Dual-Session Development Mechanism — COMPLETE

The frontend dual-session development mechanism has been removed and pushed in commit `40ea0f8`.

Removed:
- `_user1Session`
- `_user2Session`
- `_activeSessionIndex`
- slot-based refresh/login/register handling
- `connect_u2_*` storage
- User 1 / User 2 switching UI

Current model:
- one `UserSession` per browser/device
- one refresh pipeline
- one SignalR identity for the authenticated browser session

Two-user testing now uses separate browsers/profiles/devices. Authentication, refresh, logout, SignalR and WebRTC behavior were preserved during the refactor.

---

## 7. Azure State / Security

Known deployment fixes are complete:
- Linux SqlClient deployment issue resolved.
- Azure SQL migrations applied.
- JWT timer overflow fixed and pushed.
- Server/client diagnostic logging was used successfully, download endpoint verified, and diagnostic data cleared after testing.

Security items still open:
- Azure SQL admin password was previously exposed during debugging and should be rotated.
- The previously exposed static TURN credential must be rotated after the coturn shared-secret migration.

Azure services can remain stopped/deallocated when not actively testing to control cost. (TURN VM is currently deallocated).

---

## 8. Immediate Work Order

```text
1. P0-3 microphone permission UX
        ↓
2. P0-4 network recovery / reconnection
        ↓
3. P0-6 call-exit regression
        ↓
4. Security credential rotation
        ↓
5. Sprint 7.6 final regression
        ↓
6. Merge verification
```

---

## 9. Sprint 7.6 Completion Snapshot

```text
✅ User 1 can call User 2
✅ User 2 can accept
✅ Offer exchanged
✅ Answer exchanged
✅ ICE exchanged
✅ Direct ICE can connect on same-network calls
✅ PeerConnection can connect on direct calls
✅ Remote audio track received
✅ Remote stream attached
✅ Browser playback confirmed
✅ Two-way audio confirmed on direct calls
✅ Call cleanup confirmed
✅ Cross-network failure resolved
✅ Root cause narrowed to missing relay path
✅ Secure TURN credential backend foundation implemented
✅ Coturn REST/shared-secret authentication configured
✅ Secure Flutter dynamic TURN integration implemented
✅ Cross-network audio using TURN — manually verified
✅ DEV/PROD configuration baseline established
✅ Single-session frontend cleanup completed
⬜ P0-3 microphone permission UX
⬜ P0-4 network recovery
🟠 P0-6 exit-path regression
⬜ Production reliability testing
⬜ Production deployment automation finalization (needs final verification)
```

**One-line state:** Connect's WebRTC audio path is fully proven across different networks (Wi-Fi ↔ mobile data). Configuration, single-session authentication, secure Azure TURN infrastructure, and dynamic TURN integration are complete, pushed, and verified. The next implementation priority is P0-3 microphone permission handling, followed by P0-4 recovery and P0-6 exit-path regression.
