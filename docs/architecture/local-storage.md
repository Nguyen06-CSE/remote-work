# Local Storage Architecture

## Overview

The RemoteWork Desktop agent persists tracking data locally using **SQLite** via **Entity Framework Core 9**. The local database is the primary data store for the agent — all collected tracking data is written here first, enabling offline-first operation. A future sync layer will read from this store and push records to the Backend.

---

## Database Location

The database path is resolved at runtime using the platform standard application data directory. **No path is hard-coded.**

| Platform | Default Path |
|----------|-------------|
| **macOS** | `~/Library/Application Support/RemoteWork/Agent/remotework.db` |
| **Windows** | `C:\Users\<user>\AppData\Local\RemoteWork\Agent\remotework.db` |
| **Linux** | `~/.local/share/RemoteWork/Agent/remotework.db` |

Resolution is done by `DatabasePathResolver` using `Environment.SpecialFolder.LocalApplicationData`. The directory is created automatically on first run.

The path can be overridden via configuration:

```json
{
  "Database": {
    "DatabaseDirectory": "/custom/path",
    "DatabaseFileName": "remotework.db"
  }
}
```

---

## Schema

### Entity Relationship

```
Devices (PK: DeviceId)
  │
  ├─── Sessions (FK: DeviceId → Devices, PK: SessionId)
  │       │
  │       ├─── ActivityBatches (FK: DeviceId, SessionId, PK: BatchId)
  │       │
  │       └─── ApplicationActivities (FK: DeviceId, SessionId nullable, PK: ActivityId)
  │
  └─── SyncQueue (standalone, PK: ItemId)
```

### Tables

#### `Devices`
| Column | Type | Notes |
|--------|------|-------|
| `DeviceId` | TEXT (PK) | GUID string |
| `Hostname` | TEXT | Required |
| `OperatingSystem` | TEXT | Required |
| `OsVersion` | TEXT | Required |
| `AgentVersion` | TEXT | Required |
| `RegisteredAt` | INTEGER | UTC ticks |
| `LastSeenAt` | INTEGER | UTC ticks, updated on each upsert |

#### `Sessions`
| Column | Type | Notes |
|--------|------|-------|
| `SessionId` | TEXT (PK) | GUID string |
| `DeviceId` | TEXT (FK) | → Devices, Restrict delete |
| `StartedAt` | INTEGER | UTC ticks |
| `EndedAt` | INTEGER? | UTC ticks, nullable |
| `Status` | TEXT | `Starting`, `Active`, `Ending`, `Ended`, `Error` |

Indexes: `DeviceId`, `(DeviceId, StartedAt)`

#### `ActivityBatches`
| Column | Type | Notes |
|--------|------|-------|
| `BatchId` | TEXT (PK) | GUID string |
| `DeviceId` | TEXT (FK) | → Devices, Restrict |
| `SessionId` | TEXT (FK) | → Sessions, Restrict |
| `StartedAt` | INTEGER | UTC ticks |
| `EndedAt` | INTEGER | UTC ticks |
| `KeyboardCount` | INTEGER | Cumulative count for interval |
| `MouseCount` | INTEGER | Cumulative count for interval |
| `ActiveDurationTicks` | INTEGER | TimeSpan stored as ticks |
| `IdleDurationTicks` | INTEGER | TimeSpan stored as ticks |
| `HasSuspiciousMouseActivity` | INTEGER | Boolean as 0/1 |

Indexes: `(SessionId, StartedAt)`, `(DeviceId, StartedAt)`

#### `ApplicationActivities`
| Column | Type | Notes |
|--------|------|-------|
| `ActivityId` | TEXT (PK) | GUID string |
| `DeviceId` | TEXT (FK) | → Devices, Restrict |
| `SessionId` | TEXT (FK)? | → Sessions, SetNull on delete |
| `Timestamp` | INTEGER | UTC ticks |
| `ApplicationName` | TEXT | Required |
| `ProcessName` | TEXT | Required |
| `ProcessId` | INTEGER | OS process ID |
| `WindowTitle` | TEXT? | Nullable |
| `DurationTicks` | INTEGER | TimeSpan as ticks |

Indexes: `(DeviceId, Timestamp DESC)`, `SessionId`

#### `SyncQueue`
| Column | Type | Notes |
|--------|------|-------|
| `ItemId` | TEXT (PK) | GUID string |
| `EntityType` | TEXT | `Session`, `ActivityBatch`, `ApplicationActivity` |
| `EntityId` | TEXT | ID of the referenced entity |
| `PayloadJson` | TEXT | Serialized payload for sync |
| `Status` | TEXT | `Pending`, `Sent`, `Failed` |
| `CreatedAt` | INTEGER | UTC ticks |
| `SentAt` | INTEGER? | UTC ticks, nullable |
| `FailureReason` | TEXT? | Nullable |
| `RetryCount` | INTEGER | Incremented on each failure |

Indexes: `Status`, `(Status, CreatedAt)`

---

## EF Core Decisions

### DateTimeOffset Stored as UTC Ticks (INTEGER)

**Problem:** EF Core 9 + SQLite cannot sort `DateTimeOffset` values in `ORDER BY` when stored as TEXT (the default). This causes `NotSupportedException` at runtime.

**Decision:** All `DateTimeOffset` properties use a `HasConversion` value converter that stores and reads values as `long` (UTC ticks). This makes timestamps sortable and indexable as integers in SQLite.

**Trade-off:** Values lose timezone offset information (always stored as UTC). This is acceptable since all tracking data uses `DateTimeOffset.UtcNow` consistently.

### TimeSpan Stored as Ticks (INTEGER)

EF Core has no built-in SQLite mapping for `TimeSpan`. Storing as `long` ticks gives full precision and enables arithmetic comparisons.

### Core Domain Models NOT Modified

Persistence entities (`DeviceEntity`, `SessionEntity`, etc.) are separate from domain models (`Device`, `Session`, etc.). Each repository maps between the two layers. This keeps the domain free of EF attributes and infrastructure concerns.

### Session State Reconstruction

`Session.Status` and `Session.EndedAt` have `protected set` (enforced by the domain state machine). When loading from the database, `SessionRepository.MapToSession()` uses `SessionInfo` (the public sealed subclass) and drives it through the domain state machine methods (`MarkActive()`, `MarkEnded()`, etc.) to reconstruct the correct state.

### Design-Time Factory

`RemoteWorkDbContextFactory` enables `dotnet ef` tooling without requiring the Host startup project. It uses an in-memory SQLite connection for migration scaffolding only.

### Scoped DbContext Lifetime

`RemoteWorkDbContext` is registered as **scoped** (default for `AddDbContext`). The Host creates a scope per logical operation. This is appropriate for a Worker Service where each background task cycle creates a new scope.

---

## Migration Strategy

Migrations are stored in `src/RemoteWork.Desktop.Persistence/Data/Migrations/`.

**On startup**, `DatabaseInitializer.InitializeAsync()` calls `MigrateAsync()` which:
1. Creates the database file if it does not exist.
2. Applies any pending migrations in order.
3. Is idempotent — safe to call on every startup.

**Adding a new migration:**
```bash
dotnet ef migrations add <MigrationName> \
  --project src/RemoteWork.Desktop.Persistence \
  --output-dir Data/Migrations
```

**Rolling back:**
```bash
dotnet ef migrations remove --project src/RemoteWork.Desktop.Persistence
```

**Schema changes** require a new migration. The migration file must be committed alongside the code change.

---

## Performance Considerations

- **Timestamps as integers**: Allows SQLite B-tree indexing on timestamp columns. Composite indexes `(SessionId, StartedAt)` and `(DeviceId, StartedAt)` support the primary query patterns.
- **`AsNoTracking()`**: All read queries use `AsNoTracking()` to avoid EF change tracking overhead for read-only operations.
- **Batch writes**: Activity data is accumulated in-memory and flushed as `ActivityBatch` records (one row per interval, not one row per event). This dramatically reduces write frequency.
- **Limit parameter**: `GetByDeviceIdAsync` on `ApplicationActivityRepository` accepts a `limit` parameter (default 100) to prevent unbounded result sets.
- **SQLite WAL mode**: Not yet explicitly enabled. Consider enabling WAL mode for production via `PRAGMA journal_mode=WAL` for better concurrent read performance.

---

## Data Retention Considerations

- **No automatic purge** is implemented in Phase 07. A future phase will add configurable retention policies.
- **SyncQueue cleanup**: `DeleteSentAsync()` removes sent items and should be called periodically after successful sync.
- **ActivityBatch volume**: At 1 batch per minute per session, a full 8-hour workday generates ~480 rows per session. Long-term, a retention policy should archive or delete old batches.
- **ApplicationActivities**: Volume depends on how frequently the foreground application changes. A daily limit or rolling window purge should be considered.

---

## Project Structure

```
src/RemoteWork.Desktop.Persistence/
├── Data/
│   ├── Configurations/
│   │   ├── ActivityBatchEntityConfiguration.cs
│   │   ├── ApplicationActivityEntityConfiguration.cs
│   │   ├── DeviceEntityConfiguration.cs        ← DateTimeOffset converters defined here
│   │   ├── SessionEntityConfiguration.cs
│   │   └── SyncQueueItemEntityConfiguration.cs
│   ├── Migrations/
│   │   └── <timestamp>_InitialSchema.cs
│   ├── DatabaseInitializer.cs
│   ├── DatabaseOptions.cs
│   ├── DatabasePathResolver.cs
│   ├── RemoteWorkDbContext.cs
│   └── RemoteWorkDbContextFactory.cs           ← Design-time only
├── Entities/
│   ├── ActivityBatchEntity.cs
│   ├── ApplicationActivityEntity.cs
│   ├── DeviceEntity.cs
│   ├── SessionEntity.cs
│   └── SyncQueueItemEntity.cs
├── Repositories/
│   ├── ActivityBatchRepository.cs
│   ├── ApplicationActivityRepository.cs
│   ├── DeviceRepository.cs
│   ├── SessionRepository.cs
│   └── SyncQueueRepository.cs
├── FileDeviceIdentityStore.cs
└── PersistenceServiceCollectionExtensions.cs

src/RemoteWork.Desktop.Core/
├── Interfaces/
│   ├── IActivityBatchRepository.cs
│   ├── IApplicationActivityRepository.cs
│   ├── IDeviceRepository.cs
│   ├── ISessionRepository.cs
│   └── ISyncQueueRepository.cs
└── Models/
    └── SyncQueueItem.cs
```
