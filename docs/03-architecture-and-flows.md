# Architecture & Core Flows: Connect

See `02-tech-stack.md` for the full technology rationale.

## High-Level Architecture
```
[Flutter App: Web/Android/iOS]
        |
        |-- HTTPS (REST) --> [ASP.NET Core API on Azure App Service] --> [Azure SQL Database]
        |                         (signup, login, user ID availability check, user directory, call history)
        |
        |-- WebSocket (SignalR Hub) --> [Signaling]
        |                         (call invite, accept/reject, hang up)
        |
        |-- WebRTC P2P audio stream (via STUN/TURN) directly between two users
```

---

## Core Flows

### Signup / Login
1. User registers with email + password + desired User ID → API checks User ID availability in real time (like a username check); if taken, prompts for another.
2. ASP.NET Core Identity hashes the password and stores the user in Azure SQL.
3. Login returns a JWT used for all subsequent authenticated REST requests and the SignalR connection.

### Connect Request
1. User A searches by User ID or phone number.
2. If a match is found, User A sends a **Connect Request** to that user.
3. User B sees the pending request and can **Accept** or **Decline**.
4. On Accept, a **Connection** record is created between A and B.
5. Only connected users appear as callable in each other's contact list.

### Calling
1. User A opens their connected contacts and taps "Call" on User B (only possible if a Connection exists).
2. Backend checks User B's presence:
   - **Offline** → A sees "Unavailable" instantly, no ring; a missed-call record is created for B to see on next login.
   - **Busy (on another call)** → A sees "User is in another call"; a missed-call record is created and pushed to B in real time via SignalR (since B is online).
   - **Available** → SignalR hub notifies User B ("incoming call").
3. If accepted, both clients exchange WebRTC connection info (via the SignalR hub) and establish a direct P2P audio stream (using STUN, falling back to TURN if needed).
4. Either user can end the call, which is broadcast via the SignalR hub.
5. API logs the call (caller, callee, start/end time, duration, status) to the call history table.

### Call Reliability Edge Cases
- **Network drop mid-call** → auto-reconnect attempt while call window stays open, show "Network is low / reconnecting" to both users, auto-end call if not reconnected within 10 seconds.
- **Connection fails at call start** (no STUN/TURN path found) → auto-retry once → if still fails, log a missed call for the callee, caller sees "Call failed. Try again?"
- **Unanswered call** → rings for 15 seconds (MVP default), then times out and logs as a missed call.
