# Database Schema: Connect

EF Core Code-First. SQL Server LocalDB now (Windows dev) → Azure SQL Database in Sprint 7.5, same provider family, no rewrite needed.

---

## 1. Entity-Relationship Overview

```
User ─────────< ConnectRequest >───────── User
  │            (FromUserId, ToUserId)
  │
  ├────────< Connection >──────── User
  │         (UserAId, UserBId)
  │
  ├────────< Call >──────── User
  │         (CallerId, CalleeId)
  │
  ├────────< Block >──────── User
  │         (BlockerUserId, BlockedUserId)
  │
  ├────────< Report >──────── User
  │         (ReporterUserId, ReportedUserId)
  │
  └────────< DeviceToken (many per user, for FCM push)
```

---

## 2. Tables

### User
| Column | Type | Constraints |
|---|---|---|
| Id | uniqueidentifier | PK |
| UserId | nvarchar(30) | **Unique**, indexed — user-chosen handle |
| Email | nvarchar(256) | **Unique**, indexed |
| PasswordHash | nvarchar(max) | Not null (ASP.NET Core Identity managed) |
| PhoneNumber | nvarchar(20) | **Unique**, indexed, nullable (needed for phone-based search) |
| PresenceStatus | tinyint (enum: Offline=0, Online=1, Busy=2) | Default Offline |
| SubscriptionTier | tinyint (enum: Free=0, Paid=1) | Default Free |
| IsDeleted | bit | Default false — soft delete flag |
| DeletedAt | datetime2 | Nullable — set when soft-deleted |
| ReactivationDeadline | datetime2 | Nullable — `DeletedAt + 60 days`, computed on delete |
| CreatedAt | datetime2 | Not null |
| UpdatedAt | datetime2 | Not null |

### ConnectRequest
| Column | Type | Constraints |
|---|---|---|
| Id | uniqueidentifier | PK |
| FromUserId | uniqueidentifier | FK → User.Id |
| ToUserId | uniqueidentifier | FK → User.Id |
| Status | tinyint (enum: Pending=0, Accepted=1, Declined=2) | Default Pending |
| CreatedAt | datetime2 | Not null |
| RespondedAt | datetime2 | Nullable |

**Index:** unique composite index on `(FromUserId, ToUserId)` **where Status = Pending** (filtered index) — prevents duplicate pending requests between the same pair.

### Connection
| Column | Type | Constraints |
|---|---|---|
| Id | uniqueidentifier | PK |
| UserAId | uniqueidentifier | FK → User.Id |
| UserBId | uniqueidentifier | FK → User.Id |
| CreatedAt | datetime2 | Not null (when Connect Request was accepted) |

**Rule:** always store `UserAId < UserBId` (ordered by GUID comparison) so a pair only ever has one row regardless of who initiated. **Unique composite index** on `(UserAId, UserBId)`.

### Call
| Column | Type | Constraints |
|---|---|---|
| Id | uniqueidentifier | PK |
| ConnectionId | uniqueidentifier | FK → Connection.Id |
| CallerId | uniqueidentifier | FK → User.Id |
| CalleeId | uniqueidentifier | FK → User.Id |
| Status | tinyint (enum: Ringing=0, Accepted=1, Completed=2, Missed=3, Rejected=4, Failed=5) | Not null |
| MissedReason | tinyint (enum: None=0, Offline=1, Busy=2, NoAnswer=3, ConnectionFailed=4) | Nullable, only set when Status = Missed/Failed |
| StartedAt | datetime2 | Not null — when call was initiated |
| AnsweredAt | datetime2 | Nullable — when callee accepted |
| EndedAt | datetime2 | Nullable |
| DurationSeconds | int | Nullable — computed as `EndedAt - AnsweredAt` on call end |

**Index:** composite index on `(CallerId, StartedAt DESC)` and `(CalleeId, StartedAt DESC)` — for fast call history queries (paginated, sorted by recency). This table implements the "Call History" feature directly — no separate CallHistory table needed.

### Block
| Column | Type | Constraints |
|---|---|---|
| Id | uniqueidentifier | PK |
| BlockerUserId | uniqueidentifier | FK → User.Id |
| BlockedUserId | uniqueidentifier | FK → User.Id |
| CreatedAt | datetime2 | Not null |

**Index:** unique composite index on `(BlockerUserId, BlockedUserId)`.

### Report
| Column | Type | Constraints |
|---|---|---|
| Id | uniqueidentifier | PK |
| ReporterUserId | uniqueidentifier | FK → User.Id |
| ReportedUserId | uniqueidentifier | FK → User.Id |
| Reason | nvarchar(100) | Not null (e.g. "Harassment", "Spam", "Other") |
| Note | nvarchar(1000) | Nullable — free-text detail |
| Status | tinyint (enum: Open=0, Reviewed=1) | Default Open — for future admin review tooling |
| CreatedAt | datetime2 | Not null |

### DeviceToken
| Column | Type | Constraints |
|---|---|---|
| Id | uniqueidentifier | PK |
| UserId | uniqueidentifier | FK → User.Id |
| Token | nvarchar(500) | Not null — FCM registration token |
| Platform | tinyint (enum: Web=0, Android=1, iOS=2) | Not null |
| CreatedAt | datetime2 | Not null |
| UpdatedAt | datetime2 | Not null — refreshed when token rotates |

**Index:** index on `UserId` (a user may have multiple device tokens — e.g. web + mobile later); unique index on `Token`.

---

## 3. Enums Reference
```csharp
public enum PresenceStatus { Offline = 0, Online = 1, Busy = 2 }
public enum SubscriptionTier { Free = 0, Paid = 1 }
public enum ConnectRequestStatus { Pending = 0, Accepted = 1, Declined = 2 }
public enum CallStatus { Ringing = 0, Accepted = 1, Completed = 2, Missed = 3, Rejected = 4, Failed = 5 }
public enum MissedReason { None = 0, Offline = 1, Busy = 2, NoAnswer = 3, ConnectionFailed = 4 }
public enum ReportStatus { Open = 0, Reviewed = 1 }
public enum DevicePlatform { Web = 0, Android = 1, iOS = 2 }
```

---

## 4. Notes for Implementation
- All tables inherit `Id`, `CreatedAt`, `UpdatedAt` from a shared `AuditableEntity` base class in `Connect.Domain.Common`.
- Soft delete is only implemented on `User` (per the locked 60-day reactivation rule). Other entities (Call, ConnectRequest, etc.) belonging to a soft-deleted user are **not** deleted or hidden — they remain intact so the other party's history/records stay accurate; only the deleted user's own login/visibility is blocked.
- Retention (call history 90 days, permanent purge after 60-day soft-delete window) is enforced by background jobs — `CallHistoryPurgeBackgroundService` and `ExpiredAccountsPurgeBackgroundService`, implemented in Sprint 7 (see `04-sprint-plan.md` and `06-backend-architecture.md` Section 6).
- `Connection.UserAId < UserBId` ordering must be enforced in the command handler (`SendConnectRequestCommandHandler` / accept logic), not just the DB constraint — compare GUIDs before insert.
- EF Core configurations for each entity (Fluent API, not data annotations) live in `Connect.Infrastructure.Persistence.Configurations`, one file per entity, implementing `IEntityTypeConfiguration<T>`.
