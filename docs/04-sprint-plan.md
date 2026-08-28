# Agile Sprint Plan: Connect (Web-first MVP)

Assuming 2-week sprints; adjust pace as needed. Each sprint ends with a working, demoable increment.
Reference `01-project-brief.md` for full feature detail behind each sprint's scope.

| Sprint | Focus | Deliverables |
|---|---|---|
| **Sprint 0** | Local Scaffolding | Local dev environment set up (see below); empty ASP.NET Core API + Flutter Web project structure; local DB running; projects confirmed talking to each other |
| **Sprint 1** | Auth & Identity | Signup (email + password), login (JWT), User ID selection with live availability check, ASP.NET Core Identity wired to local DB |
| **Sprint 2** | Directory & Connect Requests | Search by User ID/phone number, send/accept/decline Connect Request, Connection records, contact list of connected users |
| **Sprint 3** | Presence & Signaling | Online/offline presence tracking, SignalR hub setup, "Unavailable"/"Busy" states, real-time missed-call notification for busy users |
| **Sprint 4** | Core Calling (WebRTC) | Call initiation, ringing (15-sec timeout), accept/reject/end call, STUN integration (public STUN, TURN deferred until Azure VM is up), P2P audio stream |
| **Sprint 5** | Call Reliability | Auto-reconnect on network drop (10-sec window), "Network is low" indicator, retry-once on connection failure, "Call failed" handling |
| **Sprint 6** | History, Push, Trust & Safety | Call history (90-day retention), Firebase push notifications (incoming + missed calls) — Firebase project has no card/approval delay, can be set up any time, block user, report abuse |
| **Sprint 7** | Account Lifecycle & Polish | Soft-delete account flow (60-day reactivation window, reactivated silently via login), background purge jobs (90-day call history, 60-day expired accounts), end-to-end testing on Web (local) |
| **Sprint 7.4** | **Hardening & CI/CD (pre-Azure, no cloud dependency)** | GitHub Actions CI (build + test on push), password policy rules, rate limiting on auth endpoints, tightened CORS, environment-based configuration + User Secrets for local dev, Swagger/API versioning conventions |
| **Sprint 7.5** | **Azure Migration** | Once Azure subscription is active: provision resources (App Service, Azure SQL, Static Web App, TURN VM), migrate from local DB to Azure SQL, deploy backend + web app, wire up coturn TURN server |
| **Sprint 8** | Web MVP Release | Final QA on live Azure deployment, deploy stable Web release, gather feedback |
| **Sprint 9+** | Mobile Conversion | Extend Flutter Web app to Android/iOS — FCM mobile push, mic permissions, mobile UI adjustments, APK/TestFlight distribution |

Sprint scope can shift based on velocity — this is a starting structure, not a fixed contract. Backlog grooming before each sprint should confirm scope against what's actually ready.

## Local Development Stack (Sprints 0–7.4, before Azure is active)
| Azure Resource (later) | Local Substitute (now) |
|---|---|
| Azure SQL Database | **SQL Server LocalDB** (Windows dev environment). Same T-SQL dialect as Azure SQL — schema, queries, and EF Core migrations transfer with zero changes in Sprint 7.5 |
| Azure App Service | Run ASP.NET Core API locally (`dotnet run`) |
| Azure Static Web Apps | Run Flutter Web locally (`flutter run -d chrome --web-port=8080` or local web server) |
| coturn TURN server (Azure VM) | Skip for now — use public STUN only for local testing; most local/dev network paths connect fine without TURN. Add TURN when Azure VM is provisioned (Sprint 7.5), since real-world NAT traversal needs it |
| Firebase Cloud Messaging | No substitute needed — Firebase project setup is free and instant (no card/approval wait like Azure), so this can be created any time from Sprint 0 onward |

This means real coding can start immediately — nothing above is blocked by the Azure subscription.

## Notes for Agentic Sprint Execution (BMAD / Antigravity)
- Each sprint row above maps roughly to one epic; features listed can be broken into individual stories/tasks by the agent running the sprint.
- **Azure subscription is NOT required to start.** Sprint 0 through Sprint 7.4 run entirely on the local dev stack (see above); Azure comes in at Sprint 7.5 once the subscription is active. Sprint 7.4 (hardening/CI-CD) was specifically inserted to give productive, non-blocked work while waiting on Azure account activation.
- Sprints 0–8 are strictly Web-first (Flutter Web target only); do not pull in mobile-specific work until Sprint 9+.
- Cross-reference `05-data-privacy-and-tiering.md` for the Free/Paid tier field — it should be scaffolded in Sprint 1 (user entity) even though no paid feature gating exists yet.
- Use EF Core migrations from the start (even against LocalDB) so the schema transfers cleanly to Azure SQL in Sprint 7.5 with no rework.
- **Architectural rule established during Sprints 3–5:** call initiation (`InitiateCallAttempt`) and call failure (`NotifyCallFailed`) live only in the SignalR hub, never as REST endpoints — this avoids maintaining two parallel implementations of the same real-time business logic. Hub methods delegate to MediatR commands internally rather than containing logic directly (see `06-backend-architecture.md` Section 2). Any new call-flow feature should follow this same pattern unless deliberately decided otherwise.
- **Account reactivation is login-only** — there is no separate reactivate command/endpoint. A soft-deleted account is silently reactivated when the user logs back in within the 60-day window (handled in `LoginCommandHandler`).
