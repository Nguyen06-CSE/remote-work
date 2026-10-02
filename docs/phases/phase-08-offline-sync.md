# Phase 08 — Offline-First Sync Engine

**Status:** Complete  
**Date:** 2026-10-02  
**Depends on:** Phase 07 (Local SQLite Persistence)

---

## 1. Objective

Implement an offline-first synchronization engine so that the Desktop application continues collecting data without interruption when the backend is unavailable.

Architecture:
```
Collector -> SQLite -> Sync Queue -> Transport -> Backend
```

The tracking system must never depend on an active network connection.

---

## 2. What Was Implemented

### 2.1 Core Domain Updates
- `SyncStatus` enum: `Pending`, `InProgress`, `Synced`, `Failed`.
- `SyncQueueItem` domain model with rich state machine and explicit state transitions:
  - `MarkInProgress(attemptTime)`
  - `MarkSynced(syncedTime)`
  - `MarkFailed(error, attemptTime, retryDelay, isPermanent)`
  - `ResetToPending()`
  - Transition validation throwing `InvalidOperationException` on illegal paths.
- `SyncResult`, `SyncItemResult`, and `SyncBatchResult` transport return models with support for partial batch success and permanent vs. transient error discrimination.
- `ISyncTransport` abstraction interface.
- `ISyncEngine` abstraction interface.
- `ISyncQueueRepository` interface updated with idempotency check (`EnqueueIfNotExistsAsync`), eligibility query with backoff calculation (`GetEligibleForSyncAsync`), and restart recovery (`ResetInProgressToPendingAsync`).

### 2.2 Persistence Layer Updates
- `SyncQueueItemEntity` updated with `QueueItemId`, `AttemptCount`, `LastAttemptAt`, `ErrorMessage`, `IsPermanentFailure`, and `NextAttemptAt`, while retaining backward-compatibility aliases.
- `SyncQueueItemEntityConfiguration` configured with UTC tick value conversions, indexes on `Status`, `(Status, CreatedAt)`, and `(EntityType, EntityId)`.
- `SyncQueueRepository` fully implemented with idempotency, state transitions, and restart recovery.
- EF Core migration `AddOfflineSyncQueueFields` generated and verified.

### 2.3 Infrastructure Layer
- `InMemorySyncTransport` created, implementing `ISyncTransport` with rich testing hooks: online/offline toggling, transient network errors, permanent validation errors, latency simulation, and sent item tracking.
- Registered `ISyncTransport` in `InfrastructureServiceCollectionExtensions`.

### 2.4 Application Layer
- `SyncOptions` configuration options with configurable retry delay, backoff multiplier, max interval, and max retry attempts.
- `SyncEngine` implemented:
  - Background polling loop with non-busy waiting.
  - On-demand `SyncPendingAsync` method for deterministic testing.
  - State machine coordination (`Pending -> InProgress -> Synced | Failed`).
  - Exponential backoff calculation capping at `MaxRetryIntervalSeconds`.
  - Permanent failure handling (terminates retries for invalid payloads or schema errors).
  - Transport-driven offline detection: failed communication marks offline and throttles background loop; success restores online status.
  - Restart recovery: resets stale `InProgress` items to `Pending`.
  - Clean `CancellationToken` support.
- `TrackingPersistenceCoordinator`: connects `Collector -> SQLite -> SyncQueue`, ensuring data is saved locally and enqueued without touching the network.
- Registered `SyncOptions`, `SyncEngine`, `ISyncEngine`, and `TrackingPersistenceCoordinator` in `ApplicationServiceCollectionExtensions`.

### 2.5 Host Worker Integration
- `Worker.cs` updated to inject `MonitoringService`, `ISyncEngine`, and `IServiceScopeFactory`.
- Subscribed `_monitoringService.OnBatchGenerated` to `TrackingPersistenceCoordinator.PersistAndEnqueueBatchAsync`.
- Lifecycle-managed `_syncEngine.StartAsync` and `StopAsync`.

### 2.6 Production Wiring Fix (SQLite Foreign Key Constraints)
During live runtime verification of the Host, inserting an `ActivityBatch` failed with SQLite Error 19 (`FOREIGN KEY constraint failed`) because `Device` and `Session` were created in memory but never persisted before batches were emitted.
- In `Worker.cs`, added startup persistence of `Device` via `IDeviceRepository.UpsertAsync` and `Session` via `ISessionRepository.SaveAsync` prior to starting `MonitoringService`.
- Added shutdown persistence in `Worker.cs` finally block to persist `EndedAt` and `Status = Ended` via `ISessionRepository.UpdateAsync`.
- Added `ForeignKeyConstraintTests.cs` verifying foreign key enforcement and end-to-end persistence.
- Documented root cause and solution in `docs/troubleshooting/sqlite-fk-constraint-failed.md`.

---

## 3. Architecture Decisions

### 3.1 Unifying Tracking Persistence and Sync Queue
Tracking data writes exclusively to local SQLite tables (`ActivityBatches`, `Sessions`, etc.) and simultaneously enqueues an item into `SyncQueue`. The tracking engine does not wait for or check network connectivity. This completely decouples tracking performance and reliability from network conditions.

### 3.2 Transient vs. Permanent Error Classification
Not all sync failures should be retried:
- **Transient failures** (HTTP 502/503/504, connection timeouts, DNS failures) trigger exponential backoff retry.
- **Permanent failures** (HTTP 400 Bad Request, schema validation failure, unauthorized entity) are marked `IsPermanentFailure = true` and quarantined. They are never retried, preventing queue blockages.

### 3.3 Restart Recovery Pattern
If the host process is killed while an upload is in flight, the items remain in `InProgress` state in SQLite. Upon startup, `SyncEngine.RecoverStaleInProgressItemsAsync()` automatically resets all `InProgress` items back to `Pending`, ensuring no collected telemetry is lost.

---

## 4. Files Created & Modified

### Created (9 files):
1. `src/RemoteWork.Desktop.Core/Enums/SyncStatus.cs`
2. `src/RemoteWork.Desktop.Core/Models/SyncResult.cs`
3. `src/RemoteWork.Desktop.Core/Interfaces/ISyncTransport.cs`
4. `src/RemoteWork.Desktop.Core/Interfaces/ISyncEngine.cs`
5. `src/RemoteWork.Desktop.Infrastructure/Transport/InMemorySyncTransport.cs`
6. `src/RemoteWork.Desktop.Application/Options/SyncOptions.cs`
7. `src/RemoteWork.Desktop.Application/Sync/TrackingPersistenceCoordinator.cs`
8. `src/RemoteWork.Desktop.Application/Sync/SyncEngine.cs`
9. `src/RemoteWork.Desktop.Persistence/Data/Migrations/20261002140553_AddOfflineSyncQueueFields.cs`
10. `tests/RemoteWork.Desktop.UnitTests/Core/SyncQueueItemStateTransitionTests.cs`
11. `tests/RemoteWork.Desktop.IntegrationTests/Sync/SyncEngineIntegrationTests.cs`
12. `tests/RemoteWork.Desktop.IntegrationTests/Persistence/ForeignKeyConstraintTests.cs`
13. `docs/architecture/offline-sync.md`
14. `docs/phases/phase-08-offline-sync.md`
15. `docs/troubleshooting/sync-failures.md`
16. `docs/troubleshooting/sqlite-fk-constraint-failed.md`

### Modified (7 files):
1. `src/RemoteWork.Desktop.Core/Models/SyncQueueItem.cs`
2. `src/RemoteWork.Desktop.Core/Interfaces/ISyncQueueRepository.cs`
3. `src/RemoteWork.Desktop.Persistence/Entities/SyncQueueItemEntity.cs`
4. `src/RemoteWork.Desktop.Persistence/Data/Configurations/SyncQueueItemEntityConfiguration.cs`
5. `src/RemoteWork.Desktop.Persistence/Repositories/SyncQueueRepository.cs`
6. `src/RemoteWork.Desktop.Persistence/Repositories/ActivityBatchRepository.cs`
7. `src/RemoteWork.Desktop.Infrastructure/InfrastructureServiceCollectionExtensions.cs`
8. `src/RemoteWork.Desktop.Application/ApplicationServiceCollectionExtensions.cs`
9. `src/RemoteWork.Desktop.Host/Worker.cs`

---

## 5. Simulation & Test Verification

All 9 required simulations and the offline tracking pipeline were implemented in `SyncEngineIntegrationTests.cs`:

| Test Name | Scenario Tested | Result |
|---|---|---|
| `Scenario1_Backend_Available` | Items synced and transitioned to `Synced` status | PASS |
| `Scenario2_Backend_Unavailable` | Offline state detected, items marked `Failed` with backoff | PASS |
| `Scenario3_Failure_During_Upload` | Transport exception handled safely without crashing | PASS |
| `Scenario4_Retry` | Exponential backoff delay respected, succeeds on retry | PASS |
| `Scenario5_Restart_Application` | Stale `InProgress` items reset to `Pending` on restart | PASS |
| `Scenario6_Queue_Survives_Restart` | SQLite queue persists across context close and reopen | PASS |
| `Scenario7_Duplicate_Upload_Prevention` | Idempotent enqueue prevents duplicate queue items | PASS |
| `Scenario8_Partial_Batch_Success` | Mixed batch: success items synced, permanent failures quarantined | PASS |
| `Scenario9_Cancellation_During_Sync` | Graceful abort on `CancellationToken` cancellation | PASS |
| `Scenario10_Tracking_And_Persistence_Pipeline_Works_Offline` | Full pipeline collects and stores data while transport is offline | PASS |

### Foreign Key & Pipeline Constraint Tests (`ForeignKeyConstraintTests.cs`):
| Test Name | Scenario Tested | Result |
|---|---|---|
| `Device_Session_ActivityBatch_Insertion_Succeeds_When_ForeignKeys_Exist` | Verifies parent entity creation satisfies FKs | PASS |
| `ActivityBatch_Insertion_Throws_SqliteException_When_Session_Does_Not_Exist` | Verifies SQLite Error 19 on missing Session | PASS |
| `ActivityBatch_Insertion_Throws_SqliteException_When_Device_Does_Not_Exist` | Verifies SQLite Error 19 on missing Device | PASS |
| `TrackingPersistenceCoordinator_With_Startup_Persisted_Device_And_Session_Succeeds` | Verifies end-to-end coordinator batch saving & queuing | PASS |
| `Session_Shutdown_Update_Persists_EndedAt_And_Status_Across_Restart` | Verifies session end time & status persist across restart | PASS |

### Test Suite Execution Summary:
- **Unit Tests:** 95 passed (0 failed, 0 skipped)
- **Integration Tests:** 70 passed (0 failed, 0 skipped)
- **Total:** 165 passed (100% green, ~2s runtime)

---

## 6. Gauzy Reference Reflection

| Aspect | Gauzy Desktop | RemoteWork Desktop | Rationale |
|---|---|---|---|
| **Storage** | Knex + SQLite (`gauzy.sqlite3`) | EF Core 9 + SQLite (`remotework.db`) | Local persistence as single source of truth |
| **Offline Detection** | Periodic HTTP pinging (`DesktopOfflineModeHandler`) | Transport error detection + connectivity state in `SyncEngine` | Decoupled from OS network adapter state |
| **Queue Organization** | Multiple queues (`SequenceQueue`, `TimeSlotQueue`, `ScreenshotQueue`) | Single polymorphic queue (`SyncQueueItem` with `EntityType`) | Eliminates coordination overhead across multiple queue workers |
| **Architecture** | Electron Main <-> IPC <-> Angular UI (Akita Store) | Clean Architecture .NET Headless Worker | Avoids IPC serialization overhead and memory footprint |

---

## 7. Known Limitations

- Production HTTP/FastAPI transport not implemented (reserved for Phase 12 per roadmap).
- Queue cleanup policy for `Synced` items: `DeleteSentAsync` is available but not scheduled on a timer yet.
- Large screenshot payloads are stored as JSON references/metadata rather than raw binary in SQLite.

---

## 8. Next Phase Dependencies

- **Phase 09: Application Tracking**: Native tracking of foreground applications and window titles, persisting to `ApplicationActivity` and enqueuing to `SyncQueue`.
- **Phase 10: Screenshot Engine**: Periodic screenshots saved locally and queued for offline sync.
- **Phase 12: Backend API & Transport**: Implementation of HTTP `FastApiSyncTransport` consuming `ISyncTransport`.

---

## 9. Definition of Done Checklist

- [x] Tracking works offline (verified in `Scenario10`)
- [x] Queue persists (verified in `Scenario6`)
- [x] Retry works (verified in `Scenario4`)
- [x] Duplicate prevention exists (verified in `Scenario7`)
- [x] Restart recovery works (verified in `Scenario5`)
- [x] Tests pass (165/165 passing)
- [x] No FastAPI implementation yet (`InMemorySyncTransport` used)
- [x] Docs complete (`offline-sync.md`, `phase-08-offline-sync.md`, `sync-failures.md`, `sqlite-fk-constraint-failed.md`)
- [x] Commit created
