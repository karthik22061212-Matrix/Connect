# Project Brief: Connect (Real-time Calling App, MVP)

## 1. Overview
A SaaS communication application — users sign up, choose a unique User ID, log in,
and can voice-call any other registered user directly from the app. Starting with
**voice calling only**; chat and video are planned for later phases.

**Scale target:** Small business / early users (10s–100s of concurrent users)
**Platforms:** Web app + Mobile app (Android/iOS) from a single codebase
**Build sequence:** **Web app first** — complete and stable release on Web before converting the same Flutter flow to Mobile (Android/iOS)
**Hosting:** Azure, free tier to start

Related docs: see `02-tech-stack.md`, `03-architecture-and-flows.md`, `04-sprint-plan.md`, `05-data-privacy-and-tiering.md`

---

## 2. MVP Feature Scope
- [ ] User signup with **Email + Password**
- [ ] **User-chosen User ID** (custom handle) with live availability check — if taken, prompt user to pick another
- [ ] Login / session handling (JWT-based auth)
- [ ] **Search by User ID or phone number** — show matched user(s) if found
- [ ] **Connect Request flow** — requester sends a request, other user must Accept before a connection exists
- [ ] **Calls restricted to connected users only** — no connection, no call
- [ ] Online/offline presence status
- [ ] **Presence-aware calling**:
  - If callee is **offline** → caller sees "Unavailable", call doesn't ring, logged as missed call (visible when callee next opens the app)
  - If callee is **busy** (on another call) → caller sees "User is in another call", logged as missed call, **callee gets a real-time notification** immediately (not just on next open)
- [ ] **Voice calling** (1:1) between connected users
- [ ] Call ringing, accept/reject, end call
- [ ] **Call reliability handling**:
  - Network drop mid-call → auto-reconnect attempt, show "Network is low / reconnecting" to both users, auto-end call if not reconnected within 10 seconds
  - Connection fails at call start → auto-retry once → if still fails, log a missed call for callee, caller sees "Call failed. Try again?"
  - Unanswered call → rings for 15 seconds (MVP default), then times out and logs as missed call
- [ ] **Push notifications (Firebase Cloud Messaging)** — incoming calls ring/notify even when app is backgrounded or closed; missed calls also trigger a push notification
- [ ] **Call history** (caller, callee, timestamp, duration, missed/completed) — retained 90 days by default
- [ ] **Block user** — blocked users cannot send Connect Requests or call
- [ ] **Report abuse** — users can report another user with a reason/note, stored for admin review
- [ ] **Account deletion (soft delete)** — account deactivated, data retained 60 days, user can reactivate by re-initiating login within that window; permanently purged after 60 days

### Out of scope for MVP (future phases)
- Chat/messaging
- Video calling
- Group calls
- Custom/extended call history retention (Premium feature)

---

## 3. Distribution
- **MVP:** Direct APK install (Android), TestFlight (iOS), and the Static Web Apps URL (Web) — fastest to iterate on, no store review delays
- **Post-MVP:** Play Store / App Store listing once the app is stable and ready for public/organic growth

---

## 4. Build Sequence
1. **Phase 1 — Web app first:** Build and release the complete MVP as a Flutter Web app, backed by the ASP.NET Core API/SignalR/Azure infrastructure. Validate the full flow (signup, Connect Requests, presence, calling, history, blocking/reporting) on Web.
2. **Phase 2 — Mobile conversion:** Once the Web release is stable, reuse the same Flutter codebase and adapt/extend it for Android and iOS (platform-specific concerns: push notifications via FCM, mobile permissions for mic access, mobile-specific UI adjustments).

This lets the backend, signaling, and calling logic get proven on one platform before multiplying the surface area to mobile.

---

## 5. Roadmap (Post-MVP)
1. Phase 2: Custom/adjustable ring timeout (user-configurable, up to 1 minute) — Premium feature
2. Phase 3: Text chat
3. Phase 4: Video calling
4. Phase 5: Group calls
5. Phase 6: Play Store / App Store listing
