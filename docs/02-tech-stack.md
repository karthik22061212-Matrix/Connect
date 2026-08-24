# Tech Stack: Connect (Azure Free-Tier Friendly)

Single best-fit stack, locked in for the MVP.

| Layer | Choice | Why |
|---|---|---|
| Frontend (Web + Mobile) | **Flutter** | One codebase compiles to Web, Android, and iOS — avoids maintaining separate React + React Native apps |
| Backend / API | **ASP.NET Core Web API (.NET 8)** | Mature, high-performance, first-class Azure integration |
| Real-time signaling | **SignalR** (built into ASP.NET Core) | .NET's native equivalent of Socket.io — handles call invite, ringing, accept/reject over WebSockets, no extra library needed |
| Push notifications | **Firebase Cloud Messaging (FCM)** | Free, cross-platform (Android/iOS/Web), triggers call-ringing even when the app is closed/backgrounded; called from the .NET backend via FCM Admin SDK |
| Voice calling | **WebRTC** (peer-to-peer audio) | Industry standard, free, low-latency; SignalR only handles signaling — actual audio flows P2P between the two Flutter clients |
| NAT traversal | **STUN** (free, e.g. Google's public STUN) + **TURN** (coturn, self-hosted, or a free-tier TURN provider) | Needed so calls connect even behind routers/firewalls |
| Auth | **ASP.NET Core Identity + JWT** | Native to .NET, handles email/password hashing, user ID uniqueness checks, and token issuance out of the box |
| Database | **Azure SQL Database (Free tier)** | Natural fit with .NET/Entity Framework Core; Azure now offers an always-free SQL DB tier |
| Backend hosting | **Azure App Service (Linux, F1 Free tier)** | Hosts the ASP.NET Core API + SignalR hub; Linux is cheaper and the standard choice for .NET 8 |
| Web app hosting | **Azure Static Web Apps (Free tier)** | Hosts the Flutter web build |
| Mobile app distribution | Direct APK / TestFlight initially (no cost) | Play Store/App Store listing can come later |
| TURN server hosting | Azure free-tier B1s VM (12-month free trial) running **coturn** — set up alongside MVP build | Ensures reliable NAT traversal from day one, avoiding connection gaps during testing |

## Azure Free-Tier Resources Needed (Sprint 0 checklist)
- [ ] Azure App Service (Linux, F1 Free tier) — for ASP.NET Core API + SignalR
- [ ] Azure SQL Database (Free tier)
- [ ] Azure Static Web Apps (Free tier) — for Flutter Web build
- [ ] Azure free-tier B1s VM — for coturn TURN server
- [ ] Firebase project — for Cloud Messaging (FCM)
