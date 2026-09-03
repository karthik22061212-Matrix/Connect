# Project Brief: Connect

## Overview
Connect is a real-time communication application focused on reliable one-to-one voice calling.

The implementation is Web-first. Flutter provides the client, ASP.NET Core provides the API and SignalR hub, and WebRTC carries the actual audio.

## Implemented MVP capabilities
- User registration/login and JWT authentication
- User ID availability and search
- Connect request/accept/decline flow
- Connected-user directory
- Presence and busy/unavailable handling
- SignalR call signaling
- 15-second ringing timeout
- Call history
- Blocking and reporting
- Soft-delete/account reactivation flow
- FCM service foundation
- Browser microphone capture using `flutter_webrtc`
- Dynamic TURN credential retrieval
- Microphone permission/error/retry handling
- Bounded ICE restart recovery
- Hardened WebRTC teardown and stale-call protection
- Structured diagnostic logging architecture

## Current WebRTC reliability
The frontend now contains generation protection, microphone error classification, retry handling, ICE state monitoring, bounded ICE restart recovery, and explicit teardown for hangup/session termination/disposal.

## Diagnostic architecture
Development builds retain developer diagnostics.

Release builds hide the developer diagnostic UI from normal users.

Structured diagnostics combine frontend, backend, SignalR, and WebRTC events using user/session/correlation/call context. Diagnostic data must not contain authentication tokens, TURN credentials, SDP/ICE credentials, cookies, database credentials, or SMTP credentials.

Connect does not require an application-wide Admin/RBAC system for this diagnostic feature.

## Later scope
- Mobile conversion
- Group calling
- Video
- Chat
- Premium billing/features
