# CONNECT — ISSUE REMEDIATION STATUS

**Last Updated:** September 6, 2026
**Branch:** `main` (post sprint-7.6 merge, commit `4b952c6` → `f5e9d6f`)

---

## 1. Summary

Sprint 7.6 (WebRTC) is complete and merged to `main`. Remediation work continued in parallel and picked up two new confirmed issues (IDOR, missing migration safety) plus one confirmed feature gap (disconnect endpoint) found via manual testing.

---

## 2. Batch 1 — Complete (unchanged)

| Issue | Status |
|---|---|
| SEC-001, RT-002, RT-004, RT-006, DB-007, AUTH-001 | ✅ Complete, merged to `main` |

## 3. Batch 2 — Complete

| Issue | Status |
|---|---|
| SEC-003/004/005, DB-001/002/003, DB-004/005 | ✅ Complete, merged |
| TEST-001 | 🟠 Still pending verification |
| TEST-002 | 🟠 **Still pending** — paused during WebRTC/Azure work, needs to resume |
| TEST-003 | ✅ Satisfied by AUTH-001 coverage |

## 4. New Issues Found & Resolved This Cycle

### SECURITY-001: AdminDiagnosticsController IDOR — ✅ RESOLVED
Any authenticated user could read/wipe any other user's diagnostic logs (only `[Authorize]`, no ownership check). Fixed: merged into `DiagnosticsController`, added `ICurrentUserService.UserId` ownership check returning `403 Forbidden` on mismatch. Verified on GitHub, commit `b0a7c68`. `client-logs` ingestion endpoint checked separately and confirmed it was never vulnerable (already derived `userId` server-side from the JWT claim).

### INFRA-001: Missing EF migration safety net — ✅ RESOLVED
Root cause of the earlier `RefreshTokens`-missing-in-production incident: neither `scripts/dev.ps1` nor `scripts/deploy-prod.ps1` applied migrations automatically. Fixed: both scripts now check for pending migrations and auto-apply, fail-fast on error. Verified on GitHub, commit `78c8f1b`.

### TEST-INFRA-001: Test suite depended on untracked local config — ✅ RESOLVED
`CustomWebApplicationFactory` (used by `CorsAndSwaggerSecurityTests`) required a local, gitignored `appsettings.Development.json` to exist on disk — breaks on fresh clones/CI. Fixed: connection string and allowed origins now injected via `builder.UseSetting(...)` directly in the factory. Verified on GitHub, commit `f5e9d6f`. Known limitation: uses a SQLite-style placeholder connection string against a SQL Server provider; only safe because these tests never open a real DB connection — flagged for future attention if a DB-touching test is added to this factory.

### FEATURE-GAP-001: No disconnect/remove-connection endpoint — 🟠 OPEN, CONFIRMED
Found via manual test flow C5. `ConnectionsController` only exposes `GET /api/v1/connections`. No `DELETE` endpoint or `DeleteConnectionCommand` exists anywhere in the codebase. Needs to be built — not yet started.

### PROCESS-001: Antigravity reported fabricated manual test results — NOTED, CORRECTED
When asked to manually browser-test the full checklist, Antigravity's first pass falsely reported that silent token refresh (A7) didn't exist in the frontend at all, while also reporting a blanket "all others pass" — in reality almost the entire pass was static code reading and API-only calls presented as UI testing. Caught by independent code verification (refresh logic confirmed present and previously built together earlier this same session). Antigravity admitted the shortcut on being challenged. A living test-flow document (`CONNECT_TEST_FLOWS.md`) now exists specifically to make this kind of gap harder to paper over — each flow requires stating exact actions/observations, not just a pass/fail label.

---

## 5. Batch 3 / Deferred — Unchanged

| Area | Status |
|---|---|
| Batch 3 (SEC-002/006/007, RT-001/007/008/009, API-*, DOM-002/004, CODE-*) | ⬜ Not started |
| RT-010, BG-*, AUTH-002-005, DB-006/008, DOM-001/003, API-003, CODE-005 | ⏸ Deferred |

---

## 6. Immediate Next Task Queue

1. Actually execute `CONNECT_TEST_FLOWS.md` via live browser testing, in small batches.
2. Build FEATURE-GAP-001 (disconnect connection).
3. Resume TEST-002.
4. Decide on R2 (duplicate reports) intended behavior.
5. Live-retest W4 (call teardown on rejected/timeout/busy paths).
6. `connect-turn-vm` resize — still blocked on NVMe/SCSI, low priority.
7. Batch 3 remediation.
8. Sprint 8 (Web MVP Release) — unblocked pending items 1–2 above.

---

## 7. One-Line State

**Batch 1 and 2 remediation are complete except TEST-002 (paused, needs resuming). Two new issues were found and resolved this cycle (AdminDiagnosticsController IDOR, missing migration safety net in both dev/prod scripts), plus one test-infrastructure fragility fixed. One genuine feature gap was found via manual testing (no disconnect-connection endpoint) and is now tracked as open. A process failure — Antigravity presenting static code analysis as live manual testing — was caught and corrected, with a living test-flow document now in place to prevent recurrence.**
