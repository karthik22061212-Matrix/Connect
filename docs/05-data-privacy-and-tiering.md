# Data, Privacy & Tiering Model: Connect

## Data & Privacy (MVP scope)
- **Call history retention:** 90 days by default. Records include caller, callee, timestamp, duration, and status (missed/completed).
- **Blocking:** In MVP. A blocked user cannot send Connect Requests or place calls to the blocker.
- **Reporting abuse:** In MVP. Users can report another user with a reason/note; stored for admin review.
- **Account deletion:** Soft delete. Account is deactivated and data retained for 60 days. The user can reactivate by re-initiating login within that window. After 60 days with no reactivation, data is permanently purged.

---

## Tiering Model (Free vs Paid)

**Current phase:** All users are treated as **Free tier** — no paid features are active yet.
The system should still be **designed with a tier field on the user account** from the
start, so paid features can be switched on later without a schema rework.

| Tier | Status | Included |
|---|---|---|
| **Free** | Active now | All MVP features: voice calling, Connect Requests, presence-aware calling, 90-day call history, blocking, reporting, push notifications, 15-sec fixed ring timeout |
| **Paid (Premium)** | Planned, not built yet | Custom/adjustable ring timeout (up to 1 min), extended/custom call history retention, and any future premium features (e.g. priority call routing, ad-free, extra storage) |

**Implementation note for later:** Add a `SubscriptionTier` (Free/Paid) field on the user entity in Azure SQL now, defaulted to `Free`, so premium feature checks can be added later via a simple conditional — no migration needed when billing is introduced.
