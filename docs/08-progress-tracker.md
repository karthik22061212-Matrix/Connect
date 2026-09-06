# Progress Tracker: Connect

**Updated:** 2026-09-03  
**Branch:** `sprint-7.6/flutter-webrtc-dependency`  
**Latest commit:** `3583bfa`

## Prerequisites
- [x] Local tooling
- [x] Git repository
- [x] Local database development
- [x] Firebase/FCM foundation

## Core sprints
- [x] Sprint 0
- [x] Sprint 1
- [x] Sprint 2
- [x] Sprint 3
- [x] Sprint 4
- [x] Sprint 5
- [x] Sprint 6
- [x] Sprint 7
- [x] Sprint 7.4

## Sprint 7.5 — Azure
- [x] Azure resources provisioned
- [x] Azure SQL deployment path
- [x] App Service deployment path
- [x] Static Web App deployment path
- [x] coturn VM
- [x] dynamic TURN credential backend
- [ ] Complete operational production deployment script

## Sprint 7.6 — WebRTC
- [x] `flutter_webrtc`
- [x] microphone capture
- [x] microphone permission/error/retry
- [x] PeerConnection
- [x] SignalR offer/answer/ICE integration
- [x] STUN + dynamic TURN configuration
- [x] teardown and generation protection
- [x] bounded ICE restart recovery
- [x] exit-path hardening
- [ ] physical cross-network TURN verification
- [ ] physical P0-4 recovery verification
- [ ] full manual regression

## Diagnostics
- [x] structured frontend events
- [x] bounded frontend buffer
- [x] client-log ingestion
- [x] bounded backend per-user storage
- [x] SignalR/call diagnostics
- [x] combined diagnostic download
- [x] diagnostic clear
- [x] production developer UI hidden
- [x] sensitive-data sanitization
- [ ] live diagnostic verification

## Remaining engineering/security work
- [ ] Azure SQL credential rotation
- [ ] Azure NSG SSH cleanup
- [ ] operational logging review
- [ ] final deployment automation
- [ ] final Sprint 7.6 regression/merge

## Current checkpoint
Implementation work for the major P0 calling/reliability/logging items is complete. Live physical verification remains intentionally deferred to the manual test session.
