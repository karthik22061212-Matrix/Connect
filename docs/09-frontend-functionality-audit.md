# Frontend Functionality Audit — Connect Web App
Started: 2026-08-26
URL under test: https://delightful-ocean-0ada35700.7.azurestaticapps.net
Purpose: Confirm what actually works when a real user clicks through the UI, tab by tab.
Testing method: ONE ITEM AT A TIME — test it, update its line in this file with the result, then move to the next item. Never batch.

For each item, record Status as one of: ✅ Works / ⚠️ Partially works (explain exactly what breaks or is missing) / ❌ Broken or does nothing. Include what you clicked, what you expected, and what actually happened.

## Tab 1 — Auth & Users
- [x] Can register a new user through the form (not curl/Postman — the actual UI form) — ✅ Works. Clicked "Register as User 2" button after entering User ID 'audit_user_101', email 'audit101@test.com', phone '+1112223334'. Expected registration success and session update; actual HTTP 200 returned, SignalR connected as 'audit_user_101', UI updated User 2 slot.
- [ ] Form shows validation errors for bad input (empty fields, duplicate handle, etc.)
- [ ] Can log in with a registered user through the UI
- [ ] UI reflects logged-in state after login (session persists, shows correct user)
- [ ] Check user ID availability field gives live feedback in the UI

## Tab 2 — Directory & Search
- [ ] Search box returns real results when searching for another registered user
- [ ] Search UI handles zero results gracefully (not a blank/broken screen)
- [ ] Results are clickable/actionable from the UI (e.g. can send a request from here)

## Tab 3 — Connect Requests
- [ ] Can send a connect request to another user via the UI
- [ ] Pending requests list updates and displays correctly
- [ ] Can accept a request from the UI and connection actually forms
- [ ] Can decline a request from the UI

## Tab 4 — Calling & SignalR
- [ ] SignalR connection status indicator shows "Connected" correctly
- [ ] Initiating a call from the UI triggers a visible "ringing" state
- [ ] The other logged-in session sees an incoming call notification/UI
- [ ] Accept call updates UI state on both sides
- [ ] Reject call updates UI state correctly
- [ ] Call timeout (15s) behaves correctly in the UI if not answered
- [ ] End call button works and updates UI
- [ ] CONFIRM: does any actual audio play or microphone get requested? (expected: NO — flag clearly if the UI implies otherwise, since no WebRTC media layer exists)

## Tab 5 — Call History
- [ ] Call history list displays past calls with correct details (duration, participant, timestamp)
- [ ] Pagination or scroll works if there are many entries

## Tab 6 — Trust, Safety & Account
- [ ] Block user works from the UI and reflects immediately
- [ ] Unblock user works from the UI
- [ ] Blocked users list displays correctly
- [ ] Report user form works and submits
- [ ] Account deletion/soft-delete flow works from the UI (test carefully — don't destroy your only test account without a backup one)

## Overall UI/UX Observations (fill in only after all above items are individually tested)
- Is this a UI you'd hand to a real end user today, or does it read as a developer test harness? Be blunt.
- Note anything confusing, unlabeled, or that would make no sense to someone who didn't build this backend.

## Notes
(running log of anything discovered that contradicts prior claims in the progress tracker or sprint docs, added AS YOU FIND EACH ONE — not compiled at the end)
