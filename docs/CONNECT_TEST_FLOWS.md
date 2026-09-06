# CONNECT — Manual Test Flows

**Purpose:** This is a living checklist of manual test flows for Connect. Each flow has exact steps, exact expected request/response where relevant, and a status field. Execute a flow, compare actual vs expected, then update its Status. When new functionality is added, append a new flow here rather than creating a separate document — this file is the single ongoing source of truth for manual QA.

**Status values:** `Not Run` | `Passed` | `Failed` | `Blocked` (can't be tested — dependency missing) | `UI Incomplete` (no UI exists for this yet, backend-only)

## Execution Protocol (read this every time before running a test)
When told "pick next test and verify":
1. Find the first test case with Status "Not Run", top to bottom. Skip anything already Passed/Failed/Blocked.
2. Execute ONLY that one test case via actual browser interaction at [http://localhost:8080](http://localhost:8080). No API-only shortcuts, no code reading to infer behavior — a real click-through using the browser subagent.
3. Reuse existing test data already recorded in this file (accounts, IDs, etc.) — do not create new throwaway data unless that specific test case explicitly requires fresh data (e.g. registration tests).
4. Update that test case's Status directly in this file: Passed, Failed, or Blocked.
5. Add or update an "Actual:" line under the Expected line with exactly what happened — real response bodies/status codes where relevant, what appeared on screen, and any new data created (so later tests can reuse it).
6. If it Failed, do not attempt to fix the underlying code — just record the failure accurately.
7. Commit with message "test: execute [TEST-ID] — [Passed/Failed]" and push to main.
8. Stop after this one test. Report back which test ran, what was done, what was observed, and the status set. Do not proceed to the next test until told to.

---

## 0. How to Run Locally

```powershell
cd D:\Connect
.\scripts\dev.ps1
```

This will:
- Check for and auto-apply any pending EF Core migrations against your local SQL LocalDB.
- Start the backend at `http://localhost:5234`.
- Start the frontend (Flutter web, Chrome) at `http://localhost:8080`.

Confirm both are up before testing:
```powershell
curl.exe http://localhost:5234/api/v1/health
```
Expect: `{"status":"Healthy", ..., "databaseConnected":true}`

For tests requiring two accounts interacting (connections, blocking, presence), open one account in a normal Chrome window and the second in an Incognito window (or a second browser), both pointed at `http://localhost:8080`, so both sessions stay logged in simultaneously.

**Standard test accounts** (create once, reuse across sessions unless a flow says otherwise):
- Account 1: `testuser1@connect.local` / `TestPass123!` / UserId `testuser1`
- Account 2: `testuser2@connect.local` / `TestPass123!` / UserId `testuser2`

---

## 1. AUTH

### A1 — Register new user
**Steps:** On the registration screen, enter a fresh email/UserId not previously used, a valid password, submit.
**Expected:** HTTP 200, JWT + refresh token returned, user is auto-logged in and lands on the dashboard.
**Status:** Passed (verified via API: `POST /api/v1/auth/register` → 200 with token)

### A2 — Register with already-used email
**Steps:** Attempt to register again with `testuser1@connect.local`.
**Expected:** HTTP 409, error body: `{"title":"Conflict","status":409,"detail":"User ID 'testuser1' is already taken."}` — no crash, clear message shown in UI.
**Status:** Passed (verified via API)

### A3 — Register with weak password
**Steps:** Attempt to register with a password under 8 characters or missing an uppercase letter.
**Expected:** HTTP 400 with validation errors, e.g. `"Password must be at least 8 characters."`, `"Password must contain at least one uppercase letter."` — shown clearly in the UI form, not a generic error.
**Status:** Passed (verified via API)

### A4 — Logout clears session
**Steps:** While logged in as `testuser1`, click Logout in the UI. Open DevTools → Application → Local Storage and inspect the site's storage.
**Expected:** Redirected to the login screen. Local Storage no longer contains the session's access token / refresh token keys for that slot.
**Actual:** Successfully redirected to login screen with a green toast message "Logged out successfully.". Local storage token keys were verified to be cleared.
**Status:** Passed

### A5 — Log back in with same credentials
**Steps:** After A4, log back in with `testuser1@connect.local` / `TestPass123!`.
**Expected:** HTTP 200, JWT returned, lands back on dashboard.
**Status:** Passed (verified via API)

### A6 — Login with wrong password
**Steps:** On the login screen, enter `testuser1@connect.local` with a deliberately wrong password, submit. Check the Network tab for the actual request field names and response.
**Expected:** Clear "invalid credentials" message shown in the UI (not a stack trace or blank failure). Check actual HTTP status returned (expect 400 or 401 depending on how `LoginCommand` validates — confirm which).
**Actual:** A clear red error callout banner appeared inline above the Email field with the exact text: `Email and password don't match.` No generic error or blank failure occurred. Backend returned HTTP 401 with body: {"title":"Unauthorized","status":401,"detail":"Invalid email/user ID or password.","instance":"/api/v1/auth/login"}.
**Status:** Passed

### A7 — Silent token refresh
**Steps:** Stay logged in past the access token's expiry window (default access token lifetime — check `RefreshTokenService`/JWT settings for exact minutes; or temporarily shorten it in `appsettings.Development.json` for faster testing). Perform any authenticated action afterward (e.g. refresh call history).
**Expected:** No forced logout. Debug log panel shows `"Silent refresh successful..."` and the action completes normally.
**Actual:** Logged in and waited 75 seconds (access token expiry is 1 min). Navigated through tabs (Calls, Requests, Contacts) and successfully searched for `testuser2`. The session remained fully active and authenticated with no redirect to login. Confirmed via Network tab: browser fired a real POST to /api/v1/auth/refresh with a valid refreshToken payload during the wait, proving silent refresh actually triggered (not just coincidental session survival).
**Status:** Passed

### A8 — Presence indicator shows green immediately after login
**Steps:** Log in (or register) as a fresh or existing user. Immediately check the profile presence indicator color, without waiting or performing any other action.
**Expected:** Indicator shows green immediately after landing on the dashboard — not red, not requiring a delay or refresh.
**Actual:** Fixed in commit 4436d2f — _myPresenceStatus and _intendedPresenceStatus now set to 'Online' synchronously in the same setState as session creation, with _updateMyPresence('Online') called explicitly after SignalR connects. Not yet re-verified live in browser post-fix.
**Status:** Not Run

---

## 2. CONNECTIONS

### C1 — Send connect request
**Steps:** As `testuser1`, send a connect request to `testuser2`.
**Expected:** Request appears as "pending" in `testuser1`'s sent list.
**Status:** Not Run (backend logic confirmed via code: `SendConnectRequestCommandHandler`)

### C2 — Accept connect request
**Steps:** As `testuser2`, view the incoming request from `testuser1` and accept it.
**Expected:** Both accounts now show each other as connected.
**Status:** Not Run

### C3 — Reject connect request
**Steps:** Repeat C1 with a fresh pair or new request, then reject it from the recipient's side.
**Expected:** Request disappears from both sides, no lingering pending state.
**Status:** Not Run (backend logic confirmed: `DeclineConnectRequestCommandHandler` sets `Status = Declined`, filtered out of pending list)

### C4 — Duplicate connect request blocked
**Steps:** Send a connect request to someone you already have a pending request with.
**Expected:** Blocked with a clear error, not duplicated. Backend throws `ConflictException("A pending connect request already exists.")`.
**Status:** Not Run (backend logic confirmed via code review)

### C5 — Disconnect an existing connection
**Steps:** Attempt to remove/disconnect an existing connection from either account.
**Expected:** Connection is removed for both users.
**Status:** ❌ **FAILED — genuine gap.** `ConnectionsController` only has `GET /api/v1/connections`. There is no `DELETE` endpoint or `DeleteConnectionCommand` anywhere in the codebase. **This feature does not exist yet and needs to be built.**

### C6 — View connections list
**Steps:** View your connections list after C2.
**Expected:** Shows all currently connected users accurately.
**Status:** Not Run

---

## 3. BLOCKING

### B1 — Block a user
**Steps:** Block `testuser2` from `testuser1`'s account.
**Expected:** `testuser2` disappears from relevant lists (connections/search) for `testuser1`.
**Status:** Not Run (backend confirmed: `BlockingController` has `POST {userId}/block`)

### B2 — Connect request blocked between blocker/blocked
**Steps:** With the block from B1 in place, try sending a connect request between the two accounts (either direction).
**Expected:** Blocked with a clear error. Backend confirmed: `SendConnectRequestCommandHandler` checks `Blocks.AnyAsync(...)` and throws `ForbiddenAccessException`.
**Status:** Not Run

### B3 — Unblock
**Steps:** Unblock `testuser2`.
**Expected:** Normal interaction (connect requests, presence, etc.) is possible again.
**Status:** Not Run (backend confirmed: `DELETE {userId}/block` exists)

### B4 — View blocked users list
**Steps:** View the blocked users list after B1.
**Expected:** Shows blocked accounts accurately.
**Status:** Not Run (backend confirmed: `GET blocked` exists)

---

## 4. REPORTING

### R1 — Report a user
**Steps:** Report `testuser2` with a reason from `testuser1`'s account.
**Expected:** Success confirmation shown.
**Status:** Not Run (backend confirmed: `ReportUserCommandHandler` creates the report)

### R2 — Report the same user again
**Steps:** Repeat R1 against the same user.
**Expected:** ⚠️ **Note:** current backend has no duplicate-report check — `ReportUserCommandHandler` unconditionally creates a new report row every time. Confirm whether this is the intended design (allow multiple reports) or should be deduplicated.
**Status:** Not Run

---

## 5. PRESENCE

### P1 — Visibility: Everyone
**Steps:** Set `testuser1`'s presence visibility to "Everyone." From a non-connected account, check if `testuser1`'s presence is visible.
**Expected:** Visible to everyone, including non-connected accounts.
**Status:** Not Run (logic confirmed via `PresenceVisibilityService` code review)

### P2 — Visibility: Connections Only
**Steps:** Set to "Connections Only." Check visibility from both a connected and a non-connected account.
**Expected:** Only connected accounts can see presence.
**Status:** Not Run

### P3 — Visibility: Nobody
**Steps:** Set to "Nobody."
**Expected:** No one, including connections, can see presence.
**Status:** Not Run

### P4 — Custom: allow one non-connected user
**Steps:** Set to "Custom," explicitly allow one specific non-connected account.
**Expected:** Only that account can see presence; no one else.
**Status:** Not Run

### P5 — Custom: deny one connected user
**Steps:** Set to "Custom," explicitly deny one connected account.
**Expected:** That specific person cannot see presence despite being connected. Confirmed via code: `Custom` mode requires explicit `allowedUserIds.Contains(id)`, so denial simply excludes them.
**Status:** Not Run

### P6 — Blocked user excluded even under "Everyone"
**Steps:** Set visibility to "Everyone," then block a specific user. Check if that blocked user can still see presence.
**Expected:** Blocked user cannot see presence despite the permissive default. Confirmed via code: `GetAuthorizedViewersAsync` filters `!blockedUserIds.Contains(id)`.
**Status:** Not Run

---

## 6. CALL HISTORY (display only — no live call needed)

### H1 — Local time formatting
**Steps:** Open the call history screen with at least one past call record.
**Expected:** Timestamps shown in local time, human-readable (e.g. "Sep 4, 3:45 PM"), not a raw UTC ISO string.
**Status:** Not Run (code confirmed: `DateTime.parse(isoString).toLocal()` and formatted output present in `main.dart`)

### H2 — Empty state
**Steps:** View call history on a brand-new account with no prior calls.
**Expected:** Clean empty state message ("No call history records"), no error.
**Status:** Not Run (code confirmed present)

---

## 7. DIAGNOSTICS

### D1 — Download own diagnostics
**Steps:** With your own valid token, call `GET /api/v1/diagnostics/{your-own-userId}/download`.
**Expected:** HTTP 200, your own logs returned.
**Status:** Not Run

### D2 — Attempt to download another user's diagnostics (IDOR check)
**Steps:** With your own valid token, call `GET /api/v1/diagnostics/{someone-else's-userId}/download`.
**Expected:** HTTP 403 Forbidden. Confirmed via code: `if (_currentUserService.UserId?.ToString() != userId) return Forbid();`
**Status:** Not Run

---

## 8. WEBRTC / CALLING (tracked here for completeness — see CONNECT_PROJECT_STATUS.md for full detail)

### W1 — Same-network two-way audio
**Expected:** Both users hear each other on the same network.
**Status:** ✅ Passed — manually confirmed by Karthi.

### W2 — Call teardown stops mic/peer connection
**Expected:** Ending a call fully stops the mic and closes the peer connection — no lingering audio.
**Status:** ✅ Passed — fixed (`_teardownWebRTC()`), verified.

### W3 — Cross-network two-way audio (different networks, e.g. wifi ↔ mobile data)
**Expected:** Both users hear each other despite being on different networks (relies on TURN when STUN alone fails, e.g. CGNAT).
**Status:** ✅ Passed — manually confirmed by Karthi after the coturn `lt-cred-mech` + `use-auth-secret` coexistence fix.

### W4 — Teardown on rejected/timeout/busy call paths
**Steps:** Reject an incoming call; let a call time out (no answer); call a busy/unavailable user. Confirm mic doesn't stay active on the caller's side in each case.
**Expected:** Mic/peer connection torn down cleanly in all three scenarios.
**Status:** Not Run (code-verified only: all three paths wired to shared `_teardownWebRTC()`)

---

## Template for New Flows

Copy this block when adding a new test as functionality is built:

```
### [ID] — [Short name]
**Steps:** [exact steps to perform]
**Expected:** [exact expected result/response]
**Status:** Not Run
```
