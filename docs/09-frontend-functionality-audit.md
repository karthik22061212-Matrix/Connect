# Frontend Functionality Audit — Connect Web App

**Updated:** 2026-09-03  
**Purpose:** Track real UI/manual verification separately from code implementation.

## Manual verification status
Manual regression is intentionally deferred to the planned test session.

A code/build pass does not count as manual verification.

## Authentication
- [ ] Registration through UI
- [ ] Validation errors
- [ ] Login
- [ ] Session persistence/restore
- [ ] User ID availability feedback

## Directory / connections
- [ ] User search
- [ ] Zero-result handling
- [ ] Send request
- [ ] Accept/decline request
- [ ] Connected-user list

## Calling
- [ ] SignalR connected state
- [ ] Ringing/incoming call
- [ ] Accept/reject
- [ ] 15-second timeout
- [ ] Caller/callee hangup
- [ ] Two-way audio
- [ ] Microphone permission denied
- [ ] Microphone retry
- [ ] Microphone disconnect/error

## WebRTC/network
- [ ] Same-network two-way audio
- [ ] Cross-network Wi-Fi/mobile call
- [ ] TURN credentials retrieved
- [ ] Relay candidate observed when required
- [ ] Network interruption recovery
- [ ] ICE restart
- [ ] Maximum three recovery attempts
- [ ] Recovery exhaustion
- [ ] Hangup during recovery
- [ ] New call after recovery

## History / trust / account
- [ ] Call history
- [ ] Block/unblock
- [ ] Blocked list
- [ ] Report
- [ ] Soft delete
- [ ] Login-based reactivation

## Production diagnostics
- [ ] Developer diagnostic UI hidden in release build
- [ ] Download Logs hidden in release build
- [ ] Client logs sync correctly
- [ ] User ownership cannot be spoofed
- [ ] Combined diagnostics are chronological
- [ ] Clear endpoint works
- [ ] Sensitive values absent

## Current status
**Manual verification: pending.**
