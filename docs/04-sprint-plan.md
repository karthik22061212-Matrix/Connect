# Agile Sprint Plan: Connect

## Current status

| Sprint | Focus | Status |
|---|---|---|
| Sprint 0 | Local scaffolding | ✅ Complete |
| Sprint 1 | Auth & identity | ✅ Complete |
| Sprint 2 | Directory & connect requests | ✅ Complete |
| Sprint 3 | Presence & signaling | ✅ Complete |
| Sprint 4 | Core calling | ✅ Implemented |
| Sprint 5 | Call reliability | ✅ Implemented |
| Sprint 6 | History, push, trust & safety | ✅ Complete |
| Sprint 7 | Account lifecycle & polish | ✅ Complete |
| Sprint 7.4 | Hardening & CI/CD | ✅ Complete |
| Sprint 7.5 | Azure migration | ✅ Infrastructure complete |
| Sprint 7.6 | WebRTC media/reliability | ✅ Implementation complete |
| Diagnostic logging | Production-oriented diagnostics | ✅ Implementation complete |
| Sprint 8 | Final Web release | ⏳ Pending QA |

## Sprint 7.6 implementation completed
- `flutter_webrtc`
- microphone capture and permissions
- WebRTC offer/answer/ICE
- dynamic TURN credentials
- bounded ICE restart recovery
- teardown hardening
- stale signaling/call-ID protection
- production-oriented diagnostics

## Verification still pending
- physical cross-network TURN test
- physical P0-4 recovery test
- full manual UI regression
- final live deployment QA

## Remaining engineering backlog
- Azure SQL credential rotation
- Azure NSG SSH cleanup
- operational production logging review
- complete operational `scripts/deploy-prod.ps1`
- final Sprint 7.6 regression and merge verification
