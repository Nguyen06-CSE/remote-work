# Phase 07 — Local SQLite Persistence

**Status:** Complete  
**Date:** 2026-10-02  
**Depends on:** Phase 06 (Activity Monitoring Runtime)

---

## Objective

Implement local SQLite persistence using Entity Framework Core 9 so that all tracking data collected by the Desktop agent is durably stored on the local machine before any backend sync occurs.

---

## What Was Implemented

### 1. NuGet Packages Added
- `Microsoft.EntityFrameworkCore.Sqlite` 9.0.4 — SQLite EF provider
- `Microsoft.EntityFrameworkCore.Design` 9.0.4 — migration tooling (private assets)
- `Microsoft.Extensions.Options.ConfigurationExtensions` 10.0.12 — options binding

### 2. Core Domain Extensions
- `SyncQueueItem` domain model added to `Core.Models`
- Repository interfaces added to `Core.Interfaces`:
  - `IDeviceRepository`
  - `ISessionRepository`
  - `IActivityBatchRepository`
  - `IApplicationActivityRepository`
  - `ISyncQueueRepository`

### 3. Persistence Entities
Five separate persistence entities (not domain models) in `Persistence.Entities`:
- `DeviceEntity`
- `SessionEntity`
- `ActivityBatchEntity`
- `ApplicationActivityEntity`
- `SyncQueueItemEntity`

### 4. DbContext and Configuration
- `RemoteWorkDbContext` — EF Core DbContext with 5 DbSets
- Entity configurations via `IEntityTypeConfiguration<T>` for each entity:
  - Primary keys, required constraints, FK relationships, indexes
  - `DateTimeOffset` properties converted to `long` (UTC ticks) via `HasConversion`
- `RemoteWorkDbContextFactory` — design-time factory for `dotnet ef` tooling
- `DatabaseOptions` — configurable directory and file name
- `DatabasePathResolver` — resolves platform-appropriate path at runtime
- `DatabaseInitializer` — calls `MigrateAsync()` on startup

### 5. Repositories
One repository per entity, implementing the Core interface:
- `DeviceRepository` — upsert with LastSeenAt tracking
- `SessionRepository` — save, update status, query by session/device
- `ActivityBatchRepository` — save, query by session/device
- `ApplicationActivityRepository` — save, query with limit
- `SyncQueueRepository` — enqueue, get pending, mark sent/failed, delete sent

### 6. Service Registration
`PersistenceServiceCollectionExtensions.AddPersistenceServices(IServiceCollection, IConfiguration)` wires:
- `DatabaseOptions` from configuration
- `DatabasePathResolver` as singleton
- `RemoteWorkDbContext` with runtime-resolved SQLite connection string
- All repositories as scoped
- `DatabaseInitializer` as scoped
- Existing `FileDeviceIdentityStore` (unchanged)

### 7. Host Startup
`Program.cs` updated to:
- Pass `builder.Configuration` to `AddPersistenceServices`
- Create a DI scope and call `DatabaseInitializer.InitializeAsync()` before `host.RunAsync()`

### 8. EF Core Migration
Migration `InitialSchema` generated in `src/RemoteWork.Desktop.Persistence/Data/Migrations/`:
- Creates 5 tables with correct columns, types, PKs, FKs, and indexes
- `Up()` and `Down()` methods both present

### 9. Integration Tests (43 new tests)
Added to `tests/RemoteWork.Desktop.IntegrationTests/Persistence/`:

| Test File | Tests | Covers |
|-----------|-------|--------|
| `DbContextFixture.cs` | (fixture) | Isolated file-based SQLite per test |
| `DatabaseCreationTests.cs` | 4 | DB creation, table existence, idempotent init, MigrateAsync |
| `DevicePersistenceTests.cs` | 6 | Insert, upsert, LastSeenAt, null return, domain mapping, no duplicates |
| `SessionPersistenceTests.cs` | 6 | Save, query by id/device, update, state roundtrip, EndedAt |
| `ActivityBatchPersistenceTests.cs` | 7 | Save, query, TimeSpan ticks roundtrip, suspicious flag, ordering |
| `ApplicationActivityPersistenceTests.cs` | 6 | Save, ordering, null sessionId/title, duration ticks, limit |
| `SyncQueuePersistenceTests.cs` | 8 | Enqueue, pending, limit, ordering, mark sent/failed, delete sent, empty queue |
| `PersistenceRestartTests.cs` | 5 | Data after restart, PK constraint violations |

---

## Architecture Decisions

### DateTimeOffset → long (UTC Ticks)

**Problem:** EF Core 9 + SQLite throws `NotSupportedException` when attempting `ORDER BY` on `DateTimeOffset` columns (stored as TEXT by default).

**Solution:** A `ValueConverter<DateTimeOffset, long>` is defined in `DeviceEntityConfiguration` and applied to all timestamp properties across all entities. Timestamps are stored as `long` UTC ticks (INTEGER), which SQLite can sort natively.

**Trade-off:** Timezone offset information is discarded. All agent timestamps use `DateTimeOffset.UtcNow`, so this is acceptable.

### Domain / Persistence Separation

Core domain models (`Device`, `Session`, `ActivityBatch`, `ApplicationActivity`) are **not modified** to include EF attributes or navigation properties. Separate persistence entities carry the EF-specific structure. Each repository handles the mapping.

### Session State Reconstruction

`Session.Status` and `Session.EndedAt` have `protected set` (domain invariant enforcement). `SessionRepository.MapToSession()` uses `SessionInfo` (the public sealed subclass) and calls domain methods (`MarkActive()`, `MarkEnded()`, etc.) to restore the persisted state correctly.

### DbContext Lifetime: Scoped

`AddDbContext<RemoteWorkDbContext>()` registers as scoped (default). This is appropriate for Worker Service: each background cycle creates a new scope and gets a fresh DbContext, avoiding stale change-tracking issues.

---

## Files Created

### src/RemoteWork.Desktop.Core/
- `Interfaces/IDeviceRepository.cs`
- `Interfaces/ISessionRepository.cs`
- `Interfaces/IActivityBatchRepository.cs`
- `Interfaces/IApplicationActivityRepository.cs`
- `Interfaces/ISyncQueueRepository.cs`
- `Models/SyncQueueItem.cs`

### src/RemoteWork.Desktop.Persistence/
- `Data/RemoteWorkDbContext.cs`
- `Data/RemoteWorkDbContextFactory.cs`
- `Data/DatabaseOptions.cs`
- `Data/DatabasePathResolver.cs`
- `Data/DatabaseInitializer.cs`
- `Data/Configurations/DeviceEntityConfiguration.cs`
- `Data/Configurations/SessionEntityConfiguration.cs`
- `Data/Configurations/ActivityBatchEntityConfiguration.cs`
- `Data/Configurations/ApplicationActivityEntityConfiguration.cs`
- `Data/Configurations/SyncQueueItemEntityConfiguration.cs`
- `Data/Migrations/<timestamp>_InitialSchema.cs`
- `Data/Migrations/RemoteWorkDbContextModelSnapshot.cs`
- `Entities/DeviceEntity.cs`
- `Entities/SessionEntity.cs`
- `Entities/ActivityBatchEntity.cs`
- `Entities/ApplicationActivityEntity.cs`
- `Entities/SyncQueueItemEntity.cs`
- `Repositories/DeviceRepository.cs`
- `Repositories/SessionRepository.cs`
- `Repositories/ActivityBatchRepository.cs`
- `Repositories/ApplicationActivityRepository.cs`
- `Repositories/SyncQueueRepository.cs`

### tests/RemoteWork.Desktop.IntegrationTests/Persistence/
- `DbContextFixture.cs`
- `DatabaseCreationTests.cs`
- `DevicePersistenceTests.cs`
- `SessionPersistenceTests.cs`
- `ActivityBatchPersistenceTests.cs`
- `ApplicationActivityPersistenceTests.cs`
- `SyncQueuePersistenceTests.cs`
- `PersistenceRestartTests.cs`

### docs/
- `docs/architecture/local-storage.md`
- `docs/phases/phase-07-local-persistence.md`

## Files Modified

- `src/RemoteWork.Desktop.Persistence/RemoteWork.Desktop.Persistence.csproj` — added EF Core packages
- `src/RemoteWork.Desktop.Persistence/PersistenceServiceCollectionExtensions.cs` — accepts IConfiguration, registers EF
- `src/RemoteWork.Desktop.Host/Program.cs` — passes configuration, runs DatabaseInitializer

---

## Migration Behavior

`DatabaseInitializer.InitializeAsync()` calls `MigrateAsync()` which is **always safe to call on startup**:
- First run: creates the database file + all tables + records migration history
- Subsequent runs: checks `__EFMigrationsHistory` table, applies only pending migrations
- Already up-to-date: returns immediately, no-op

To add schema changes:
```bash
# 1. Edit entities / configurations
# 2. Generate migration
dotnet ef migrations add <Name> --project src/RemoteWork.Desktop.Persistence --output-dir Data/Migrations
# 3. Commit migration file alongside code
```

---

## Tests Performed

```
Passed!  - Failed: 0, Passed: 55, Skipped: 0, Total: 55 — IntegrationTests
Passed!  - Failed: 0, Passed: 86, Skipped: 0, Total: 86 — UnitTests
Total: 141 tests, 0 failures
```

All 86 pre-existing unit tests continue to pass unchanged.

---

## Problems Encountered and Solutions

| Problem | Root Cause | Solution |
|---------|-----------|----------|
| `Session.EndedAt` / `Session.Status` inaccessible | Domain model uses `protected set` | Use `SessionInfo` subclass + domain state methods in `MapToSession()` |
| `Configure<T>(IConfigurationSection)` compile error | Missing `Microsoft.Extensions.Options.ConfigurationExtensions` package | Added package to Persistence csproj |
| `NotSupportedException` on `ORDER BY DateTimeOffset` | EF Core 9 SQLite stores `DateTimeOffset` as TEXT; cannot sort | Added `HasConversion` to store all timestamps as `long` UTC ticks; regenerated migration |
| `CS8858` — `with` expression on sealed class | `ActivityBatch` and `ApplicationActivity` are sealed classes, not records | Replaced `with { }` syntax with explicit object initializers in tests |
| `CS0104` — ambiguous `ActivityBatch` | Two `ActivityBatch` classes in different Core namespaces | Added `using ActivityBatch = RemoteWork.Desktop.Core.Models.Activity.ActivityBatch;` alias |

---

## Known Limitations

- No automatic data retention / purge policy (planned for a future phase)
- SQLite WAL mode not explicitly enabled (consider for production performance)
- `ScreenshotMetadata` not persisted (out of scope — not yet needed by completed modules)
- `SyncQueueItem` persistence exists but no sync service consumes it yet

---

## Next Phase Dependencies

- **Sync layer**: Must implement a background service that reads `SyncQueue` pending items and pushes to the Backend API
- **Retention policy**: Add configurable max-age or row-count limits for ActivityBatches and ApplicationActivities
- **MonitoringService integration**: Wire `IActivityBatchRepository` and `IApplicationActivityRepository` into `MonitoringService` to persist each emitted batch
