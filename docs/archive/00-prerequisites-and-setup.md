# 00 - Prerequisites & Setup: Connect

**Updated:** 2026-09-03  
**Current branch:** `sprint-7.6/flutter-webrtc-dependency`  
**Latest implementation commit:** `3583bfa`

## Current stack
- Flutter Web frontend
- ASP.NET Core Web API (.NET 8)
- SignalR for realtime signaling
- WebRTC (`flutter_webrtc`) for browser voice media
- SQL Server/Azure SQL with EF Core
- Azure App Service backend
- Azure Static Web App frontend
- Azure VM running coturn for TURN

## Local development
```text
Flutter Web: flutter run -d chrome --web-port=8080
```

The Flutter API base URL is configuration-driven through `API_BASE_URL`. Production API URLs must not be hardcoded in application source.

## Azure resources currently provisioned
- Resource group: `connect-rg`
- API App Service: `connect-api-5633`
- Static Web App: `connect-web-5633`
- Azure SQL server/database: `connect-sql-5633` / `connect-db`
- TURN VM: `connect-turn-vm`
- TURN public IP: `52.172.234.96`

Production secrets are configuration-managed and must not be committed.

## WebRTC prerequisites
The frontend now:
- requests microphone access;
- applies echo cancellation, noise suppression, and auto gain control;
- obtains short-lived TURN credentials from `GET /api/v1/turn/credentials`;
- configures STUN + TURN;
- creates and manages `RTCPeerConnection`;
- exchanges offer/answer/ICE through SignalR.

## Current verification state
Implementation is complete for the current P0 WebRTC work. Live verification is still pending for:
- cross-network Wi-Fi/mobile calling;
- TURN relay selection;
- real network-change recovery;
- full manual regression.
