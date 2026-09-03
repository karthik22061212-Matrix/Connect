# Architecture & Core Flows: Connect

## High-level architecture
```text
Flutter Web
   |-- HTTPS REST --> ASP.NET Core API --> Azure SQL
   |-- SignalR ------> CallHub
   |-- WebRTC audio <--> remote client
          |
          +--> STUN
          +--> TURN/coturn when relay is required
```

## Calling flow
1. Caller initiates through SignalR.
2. Backend checks connection/presence/call state.
3. Callee receives the incoming call.
4. Acceptance starts WebRTC negotiation.
5. SignalR carries offer, answer, and ICE candidates.
6. WebRTC carries actual audio.
7. Either party can end the call.

## WebRTC setup
The client fetches TURN credentials, creates its ICE-server configuration, obtains microphone permission, creates an `RTCPeerConnection`, adds the local audio track, exchanges SDP, and handles remote audio.

## P0-4 recovery
- `disconnected` -> 3-second grace period.
- `failed` -> bounded recovery starts immediately.
- Recovery uses ICE restart.
- Maximum 3 recovery attempts.
- 15 seconds per attempt.
- Successful reconnection clears recovery state.
- Exhaustion terminates the call.

## Teardown
Explicit termination paths clean up media tracks, peer connection state, recovery timers, and stale WebRTC setup generations.

## Diagnostics
```text
Frontend + SignalR + WebRTC + Backend
              |
              v
 User / Session / Correlation / Call scoped events
              |
              v
 bounded diagnostic storage
              |
              v
 diagnostic download / clear API
```
