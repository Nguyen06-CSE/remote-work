# Troubleshooting: SQLite Error 19 'FOREIGN KEY constraint failed'

## 1. Problem Description

During host execution (`RemoteWork.Desktop.Host`), when the first `ActivityBatch` was emitted by `MonitoringService` and processed by `TrackingPersistenceCoordinator.PersistAndEnqueueBatchAsync`, an unhandled database exception was logged:

```text
Microsoft.Data.Sqlite.SqliteException (0x80004005): SQLite Error 19: 'FOREIGN KEY constraint failed'.
   at Microsoft.Data.Sqlite.SqliteCommand.ExecuteReader(CommandBehavior behavior)
   at Microsoft.EntityFrameworkCore.Storage.RelationalCommand.ExecuteReader(RelationalCommandParameterObject parameterObject)
   at Microsoft.EntityFrameworkCore.Update.ReaderModificationCommandBatch.Execute(IRelationalConnection connection)
   at Microsoft.EntityFrameworkCore.Update.Internal.BatchExecutor.Execute(IEnumerable`1 commandBatches, IRelationalConnection connection)
   at Microsoft.EntityFrameworkCore.ChangeTracking.Internal.StateManager.SaveChanges(IList`1 entriesToSave)
   at Microsoft.EntityFrameworkCore.DbContext.SaveChanges(Boolean acceptAllChangesOnSuccess)
   at RemoteWork.Desktop.Persistence.Repositories.ActivityBatchRepository.SaveAsync(ActivityBatch batch, CancellationToken ct)
   at RemoteWork.Desktop.Application.Sync.TrackingPersistenceCoordinator.PersistAndEnqueueBatchAsync(ActivityBatch batch, CancellationToken ct)
```

## 2. Root Cause Analysis

### SQLite Schema Relationship
In `RemoteWorkDbContext`, `ActivityBatchEntityConfiguration` enforces two foreign keys with `DeleteBehavior.Restrict`:
- `ActivityBatches.DeviceId` -> `Devices.DeviceId`
- `ActivityBatches.SessionId` -> `Sessions.SessionId`

SQLite enforces foreign key checks (`PRAGMA foreign_keys = ON`).

### The Bug
In `RemoteWork.Desktop.Host.Worker.ExecuteAsync`:
1. `_deviceCollector.Collect(_options.AgentVersion)` detected the host device in memory.
2. `_sessionCollector.StartSession(device.DeviceId)` created an active `SessionInfo` in memory.
3. Neither `device` nor `session` was saved into the SQLite database.
4. `_monitoringService.StartAsync()` started emitting activity batches.
5. When `TrackingPersistenceCoordinator.PersistAndEnqueueBatchAsync(batch)` ran, EF Core attempted to insert `ActivityBatchEntity` pointing to `DeviceId` and `SessionId`.
6. Since neither row existed in the SQLite `Devices` or `Sessions` tables, SQLite rejected the insert with Error 19 (`FOREIGN KEY constraint failed`).

Why was this not caught in previous unit/integration tests?
- In `ActivityBatchPersistenceTests.cs`, the test class had an explicit helper `SeedDeviceAndSession()` that directly seeded `Devices` and `Sessions` in its fixture before inserting batches.
- In `SyncEngineIntegrationTests.cs`, queue items were enqueued directly without going through `ActivityBatchRepository.SaveAsync()`.
- The missing link was in the runtime host wiring in `Worker.cs`.

## 3. The Solution

In `Worker.cs`:
1. **Startup Persistence**: Immediately after creating `device` and `session`, create an `IServiceScope` from `IServiceScopeFactory`, resolve `IDeviceRepository` and `ISessionRepository`, and persist them:
   ```csharp
   // Persist Device and Session to SQLite so that foreign keys in ActivityBatches are satisfied
   using (var scope = _scopeFactory.CreateScope())
   {
       var deviceRepo = scope.ServiceProvider.GetRequiredService<IDeviceRepository>();
       var sessionRepo = scope.ServiceProvider.GetRequiredService<ISessionRepository>();
       await deviceRepo.UpsertAsync(device, stoppingToken);
       await sessionRepo.SaveAsync(session, stoppingToken);
       _logger.LogInformation("Persisted initial Device {DeviceId} and Session {SessionId} to SQLite.", device.DeviceId, session.SessionId);
   }
   ```
2. **Shutdown Persistence**: In the `finally` block of `Worker.ExecuteAsync`, when `_sessionCollector.EndSession()` returns the ended session, update it in SQLite so `Status = "Ended"` and `EndedAt` are persisted:
   ```csharp
   var endedSession = _sessionCollector.EndSession();
   if (endedSession != null)
   {
       try
       {
           using var scope = _scopeFactory.CreateScope();
           var sessionRepo = scope.ServiceProvider.GetRequiredService<ISessionRepository>();
           await sessionRepo.UpdateAsync(endedSession, CancellationToken.None);
           _logger.LogInformation("Updated ended Session {SessionId} in SQLite.", endedSession.SessionId);
       }
       catch (Exception ex)
       {
           _logger.LogError(ex, "Failed to update ended Session {SessionId} in SQLite.", endedSession.SessionId);
       }
   }
   ```

## 4. Verification

### Automated Integration Tests
Added `ForeignKeyConstraintTests.cs` in `tests/RemoteWork.Desktop.IntegrationTests/Persistence/`:
1. `Device_Session_ActivityBatch_Insertion_Succeeds_When_ForeignKeys_Exist`: Proves batch insertion succeeds when parents exist.
2. `ActivityBatch_Insertion_Throws_SqliteException_When_Session_Does_Not_Exist`: Proves SQLite error 19 occurs if session is missing.
3. `ActivityBatch_Insertion_Throws_SqliteException_When_Device_Does_Not_Exist`: Proves SQLite error 19 occurs if device is missing.
4. `TrackingPersistenceCoordinator_With_Startup_Persisted_Device_And_Session_Succeeds`: Proves end-to-end coordinator persists batch and queues sync item.
5. `Session_Shutdown_Update_Persists_EndedAt_And_Status_Across_Restart`: Proves shutdown update persists `EndedAt` and `Status = Ended` across database reopening.

All 5 tests pass cleanly alongside the existing 160 unit/integration tests (165 total).

### Live Production Host Run
Ran `RemoteWork.Desktop.Host` in `Development` environment on macOS.
Inspection of `~/Library/Application Support/RemoteWork/Agent/remotework.db` via `sqlite3`:
```bash
sqlite3 ~/Library/Application\ Support/RemoteWork/Agent/remotework.db \
  "SELECT 'Devices', COUNT(*) FROM Devices UNION ALL \
   SELECT 'Sessions', COUNT(*) FROM Sessions UNION ALL \
   SELECT 'ActivityBatches', COUNT(*) FROM ActivityBatches UNION ALL \
   SELECT 'SyncQueue', COUNT(*) FROM SyncQueue;"
```
Output:
```
Devices|1
Sessions|2
ActivityBatches|3
SyncQueue|3
```
All batches and sync queue items completed successfully with zero foreign key exceptions.

## 5. Prevention Guidelines
- When adding new entities with foreign keys in EF Core, ensure that both repository tests and end-to-end host runtime tests assert that parent records are saved before child records are dispatched.
- Do not disable foreign key constraints (`PRAGMA foreign_keys = OFF`) to mask lifecycle bugs.
