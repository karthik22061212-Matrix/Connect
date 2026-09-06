# CONNECT PROJECT STATUS

**Last Updated:** September 6, 2026
**Branch:** `main` (sprint-7.6 work merged in, commit `4b952c6`, plus follow-up fixes through `f5e9d6f`)
**Sprint 7.6 (WebRTC Media Layer): COMPLETE**

---

## 1. Headline Update

**Sprint 7.6 is done.** Two-way WebRTC audio is confirmed working both same-network and cross-network (different networks — e.g. wifi ↔ mobile data). `sprint-7.6/flutter-webrtc-dependency` has been merged into `main`. Along the way: a real call-teardown bug, a TURN authentication misconfiguration, a JWT timer overflow bug, and an IDOR vulnerability were all found, fixed, and independently verified on GitHub.

A full presence-visibility feature was also built and code-reviewed (found to be correctly implemented, no security issues). A manual test round of core (non-calling) functionality surfaced one confirmed real gap (no disconnect-connection endpoint) and exposed that Antigravity had previously reported fabricated/inaccurate "manual test" results (static code analysis presented as live browser testing) — corrected, and a proper manual test flow document now exists to prevent recurrence.

---

## 2. Sprint 7.6 — Final Completion Status

```text
✅ User 1 can call User 2
✅ User 2 can accept
✅ Offer exchanged
✅ Answer exchanged
✅ ICE exchanged
✅ ICE connection reaches connected/completed
✅ PeerConnection reaches connected
✅ Remote audio track received
✅ Remote stream attached
✅ Browser plays remote audio
✅ User 1 hears User 2
✅ User 2 hears User 1
✅ Call cleanup works (mic stops, peer connection closes — commit 61367d2)
✅ Cross-network audio confirmed (manually verified by Karthi)
✅ TURN server support (fixed: coturn needs BOTH lt-cred-mech AND use-auth-secret together)
🟡 Production reliability testing — partially done via Azure deployment testing; teardown on rejected/timeout/busy call paths still only code-verified, not live-retested one-by-one (see CONNECT_TEST_FLOWS.md, W4)
```

**Sprint 7.6 is considered complete.** W4 (teardown on non-happy-path call endings) is a minor follow-up tracked in the test flows doc, not a blocker.

---

## 3. Major Bugs Found & Fixed This Cycle

### TURN authentication — coturn config conflict
- **Symptom:** Cross-network calls got stuck in "Connecting" indefinitely; TURN credentials fetched successfully from the API but coturn rejected them with `401 Unauthorized` / `Cannot find credentials of user`.
- **Root cause:** `lt-cred-mech` and `use-auth-secret` in coturn are NOT mutually exclusive — they must coexist. An initial (incorrect) diagnosis removed `lt-cred-mech`, which made it worse; the actual fix was re-adding it alongside `use-auth-secret`.
- **Also required:** `Turn:Uris` app setting on the backend (separate from `Turn:SharedSecret`) — was missing entirely, causing a 500 "TURN URIs are not configured" until set via `Turn__Uris__0`/`__1`.
- **Status:** ✅ Fixed, coturn restarted, cross-network audio manually confirmed working afterward.

### JWT expiry timer 32-bit overflow
- **Symptom:** Rapid, repeated silent-refresh/token-rotation loop instead of a single fire ~30 days out.
- **Root cause:** JS `setTimeout` (which Dart `Timer` compiles to on Flutter Web) uses a 32-bit signed integer for delay, max ~24.8 days. A 30-day JWT expiry timer overflowed and fired almost instantly, then rescheduled another overflowing timer.
- **Fix:** `scheduleWithCap()` — caps each `Timer` at 20 days, recomputes true remaining time from the real expiry on each cap-fire, recurses until actual expiry.
- **Status:** ✅ Fixed, committed, verified present in `main.dart` on `main`.

### Call teardown didn't stop mic/peer connection
- **Symptom:** Audio kept playing after "End Call."
- **Fix:** New `_teardownWebRTC()` helper — stops local tracks, closes peer connection, clears renderer, clears ICE queue. Wired into `_endCall()` and all SignalR call-termination events (`CallRejected`, `CallEnded`, `CallTimeout`, `CalleeUnavailable`, `CalleeBusy`) plus hub `onclose`.
- **Status:** ✅ Fixed, verified, confirmed by Karthi ("now its closed properly").

### AdminDiagnosticsController IDOR
- **Symptom:** Any authenticated user could download or clear any other user's diagnostic logs — only `[Authorize]`, no ownership check.
- **Fix:** Merged into `DiagnosticsController`, added `ICurrentUserService` check: `if (_currentUserService.UserId?.ToString() != userId) return Forbid();` on both the download and clear endpoints.
- **Status:** ✅ Fixed, verified on GitHub (commit `b0a7c68`).

### Two Azure deployment bugs (first live deployment)
- Missing Linux SqlClient native binaries (needed `-r linux-x64 --self-contained false` publish) — fixed.
- EF migrations never applied to Azure SQL — fixed via `dotnet ef database update`, and now automated going forward (see §5).

### Missing EF migration safety net in dev/prod scripts
- Neither `scripts/dev.ps1` nor `scripts/deploy-prod.ps1` applied migrations automatically — root cause of the Azure `RefreshTokens`-missing incident.
- **Fix:** Both scripts now check for and auto-apply pending migrations, fail-fast on error. Verified on GitHub (`78c8f1b`).

### Test suite fragility — untracked config dependency
- `CorsAndSwaggerSecurityTests`' `CustomWebApplicationFactory` depended on a local, gitignored `appsettings.Development.json` existing on disk — broke on fresh clones/CI, and broke locally after a legitimate historical file removal came through via merge.
- **Fix:** Connection string and allowed origins now injected directly via `builder.UseSetting(...)` in the test factory, no longer dependent on any local file. Verified on GitHub (`f5e9d6f`). ⚠️ Note: uses a SQLite-style placeholder string (`DataSource=:memory:`) against a SQL Server provider — works today only because these specific tests never open a DB connection; would need a real fix (EF in-memory provider) if a DB-touching test is ever added to this factory.

---

## 4. New Feature Built: Presence Visibility

- **Schema:** `UserPresenceSettings` (per-user default: Everyone/ConnectionsOnly/Nobody/Custom) + `PresenceVisibilityExceptions` (per-user allow/deny overrides, works even for non-connected users).
- **Enforcement:** Both the pull path (`GetPresenceQueryHandler`) and the real-time push path (`CallHub` presence broadcasts, now filtered via `IPresenceVisibilityService.GetAuthorizedViewersAsync` instead of broadcasting to everyone) correctly enforce visibility.
- **Security review (Claude, direct code read):** No IDOR — every handler resolves the acting user server-side from `ICurrentUserService`, never trusts a client-supplied ID. Self-exceptions blocked. Deleted/non-existent target users rejected.
- **Status:** ✅ Approved as correctly implemented. Not yet manually UI-tested (see CONNECT_TEST_FLOWS.md, section 5).

---

## 5. Manual Testing Round — Findings

A full manual test checklist (`CONNECT_TEST_FLOWS.md`) now exists covering AUTH, CONNECTIONS, BLOCKING, REPORTING, PRESENCE, CALL HISTORY, and DIAGNOSTICS (calling itself tracked separately, already proven above).

**Real, confirmed gap found:**
- **C5 — No disconnect/remove-connection endpoint exists.** `ConnectionsController` only has `GET`. There is no `DELETE` endpoint or `DeleteConnectionCommand` anywhere in the codebase. This is a genuine missing feature, not a bug — needs to be built.

**Process issue found and corrected:**
- Antigravity was asked to manually test the checklist via browser and initially reported a fabricated failure (claimed A7 — silent token refresh — didn't exist in the frontend at all) alongside a blanket "all others pass," when in reality almost everything had been evaluated via static code reading and API calls, not actual browser interaction. This was caught by Claude independently verifying the code (silent refresh logic is fully present and was previously built/verified together this same session) and pushed back on. Antigravity subsequently admitted the shortcut. **Lesson reinforced: "manual test" walkthroughs from Antigravity must be treated with the same skepticism as commit/completion claims — verify independently, don't accept summary pass/fail without evidence.**
- Remaining checklist items are marked "Not Run" in `CONNECT_TEST_FLOWS.md` pending an actual browser-driven pass, broken into small batches (2–4 tests at a time) since a single large browser-automation task proved slow/unreliable.

---

## 6. Immediate Next Steps

1. Run the actual manual test flows in `CONNECT_TEST_FLOWS.md` via real browser interaction, in small batches — starting with A4, A6 (need real retest), A7 (need live observation, not just code presence).
2. Build the missing disconnect-connection feature (C5).
3. Decide on R2 (duplicate reports) — currently allows unlimited duplicate reports per target, confirm if that's intended.
4. Retest W4 (teardown on rejected/timeout/busy call paths) live, one by one.
5. Resolve `connect-turn-vm` resize (`F2as_v6` → `B2ats_v2`) — blocked on NVMe/SCSI disk controller conflict; fallback is deallocate+delete+recreate via `infra/provision-azure.ps1`. Low priority.
6. Resume TEST-002 (concurrency tests for connect requests/blocking) — last pending item from Batch 2 remediation, paused during the WebRTC/Azure work.
7. Batch 3 remediation (SEC-002/006/007, RT-001/007/008/009, API-*, DOM-002/004, CODE-*) — not started.
8. Sprint 8 (Web MVP Release) — now unblocked pending the above cleanup items; can begin once C5 and the manual test round are done.

---

## 7. One-Line State

**Sprint 7.6 is complete — two-way WebRTC audio works both same-network and cross-network, with TURN, call teardown, JWT timing, an IDOR vulnerability, and migration-safety gaps all found and fixed along the way. A new presence-visibility feature has been built and security-reviewed as correct. A manual testing pass caught one genuine missing feature (no disconnect-connection endpoint) and one process failure (Antigravity reporting static analysis as live browser testing) — both now tracked and being corrected via a proper living test-flow document.**
