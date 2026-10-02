# Phase 09 — Application Activity Tracking

**Status:** Complete  
**Date:** 2026-10-02  
**Depends on:** Phase 07 (Local SQLite Persistence), Phase 08 (Offline-First Sync Engine)

---

## 1. Objective

Track which application is currently active in the foreground and how long it remains active without collecting detailed application content, file content, browser URLs, or search queries.

Key principles:
- **Transition Detection**: Record app transitions (e.g., VS Code -> Chrome -> Postman) rather than emitting duplicate records on every poll.
- **Wall-Clock Timing**: Compute durations using wall-clock intervals (`EndTime - StartTime`) to eliminate cumulative timer drift.
- **Privacy by Design**: Strictly exclude window titles, URLs, document content, and search queries.
- **Offline-First Pipeline**: Persist directly to local SQLite (`ApplicationActivities`) and enqueue to `SyncQueue`.

---

## 2. What Was Implemented

### 2.1 Core & Platform Abstraction Layer
- `IApplicationActivityProvider`: Updated interface in `RemoteWork.Desktop.Platform.Abstractions` providing `ActiveApplicationInfo? GetActiveApplication()`.
- `IActiveApplicationProvider`: Inherits `IApplicationActivityProvider` for backwards compatibility.
- `IApplicationActivityCollector`: New interface in `RemoteWork.Desktop.Core.Interfaces` defining `Sample(deviceId, sessionId, now)` and `Flush(deviceId, sessionId, now)`.

### 2.2 Platform Implementations
- **macOS (`MacOsActiveApplicationProvider`)**:
  - Uses `NSWorkspace.sharedWorkspace.frontmostApplication` via Objective-C runtime P/Invoke.
  - Returns `ApplicationName`, `ProcessName`, `ProcessId`, and sets `WindowTitle = string.Empty`.
  - Registered as `IApplicationActivityProvider` and `IActiveApplicationProvider` in `MacOsServiceCollectionExtensions`.
- **Windows (`WindowsActiveApplicationProvider`)**:
  - Uses `GetForegroundWindow` and `GetWindowThreadProcessId` via Win32 P/Invoke.
  - Uses `Process.GetProcessById(pid).ProcessName` for clean process names.
  - Deliberately sets `WindowTitle = string.Empty` to prevent leaking open document paths or web URLs.
  - Registered in `WindowsServiceCollectionExtensions`.
- **Linux (`LinuxActiveApplicationProvider`)**:
  - Abstraction stub returning `null`.
  - Registered in `LinuxServiceCollectionExtensions`.

### 2.3 Application Layer
- `ApplicationActivityCollector`:
  - Maintains state machine for the current application and start timestamp.
  - Suppresses redundant records when the same application remains active.
  - Computes exact duration using wall-clock timestamps (`now - startedAt`).
  - Emits `ApplicationActivity` with `WindowTitle = null` upon application focus switch.
  - Safely handles missing application metadata (fallbacks to process name or `"Unknown Application"`).
  - Handles platform provider exceptions gracefully without crashing.
  - Implements `Flush` to capture the final active application span on session shutdown.
- `MonitoringService`:
  - Injects `IApplicationActivityCollector?`.
  - On each background tick, calls `_appActivityCollector.Sample(session.DeviceId, session.SessionId)`.
  - Raises `public event Action<ApplicationActivity>? OnApplicationActivityGenerated`.
  - On `StopAsync()`, calls `_appActivityCollector.Flush(...)` and emits the final application activity.
- `TrackingPersistenceCoordinator`:
  - Added `PersistAndEnqueueApplicationActivityAsync(ApplicationActivity activity, CancellationToken ct)`.
  - Saves activity to local SQLite via `IApplicationActivityRepository` and enqueues to `SyncQueue`.

### 2.4 Host Runtime Integration
- In `Worker.cs`:
  - Subscribed `_monitoringService.OnApplicationActivityGenerated` to persist and enqueue into SQLite and `SyncQueue` asynchronously via scoped `TrackingPersistenceCoordinator`.

---

## 3. Files Created & Modified

### Created (3 files):
1. `src/RemoteWork.Desktop.Core/Interfaces/IApplicationActivityCollector.cs`
2. `src/RemoteWork.Desktop.Application/Collectors/ApplicationActivityCollector.cs`
3. `tests/RemoteWork.Desktop.UnitTests/Application/ApplicationActivityCollectorTests.cs`
4. `tests/RemoteWork.Desktop.IntegrationTests/Sync/ApplicationActivitySyncIntegrationTests.cs`
5. `docs/architecture/application-tracking.md`
6. `docs/phases/phase-09-application-tracking.md`

### Modified (7 files):
1. `src/RemoteWork.Desktop.Platform.Abstractions/IApplicationActivityProvider.cs`
2. `src/RemoteWork.Desktop.Platform.Abstractions/IActiveApplicationProvider.cs`
3. `src/RemoteWork.Desktop.Platform.MacOS/MacOsServiceCollectionExtensions.cs`
4. `src/RemoteWork.Desktop.Platform.Windows/WindowsActiveApplicationProvider.cs`
5. `src/RemoteWork.Desktop.Platform.Windows/WindowsServiceCollectionExtensions.cs`
6. `src/RemoteWork.Desktop.Platform.Linux/LinuxServiceCollectionExtensions.cs`
7. `src/RemoteWork.Desktop.Application/ApplicationServiceCollectionExtensions.cs`
8. `src/RemoteWork.Desktop.Application/Monitoring/MonitoringService.cs`
9. `src/RemoteWork.Desktop.Application/Sync/TrackingPersistenceCoordinator.cs`
10. `src/RemoteWork.Desktop.Host/Worker.cs`
11. `tests/RemoteWork.Desktop.UnitTests/Application/MonitoringServiceTests.cs`
12. `tests/RemoteWork.Desktop.IntegrationTests/Platform/WindowsPlatformIntegrationTests.cs`
13. `tests/RemoteWork.Desktop.IntegrationTests/Persistence/ApplicationActivityPersistenceTests.cs`

---

## 4. Test Verification Results

### Unit Tests (`ApplicationActivityCollectorTests.cs` & `MonitoringServiceTests.cs`):
- `Initial_Sample_Should_Not_Emit_Activity`: PASS
- `Same_Application_Remains_Active_Should_Not_Generate_Redundant_Records`: PASS
- `Application_Change_Should_Emit_Previous_Application_With_Accurate_WallClock_Duration`: PASS
- `Subsequent_Application_Transition_Should_Track_Next_Application_Duration`: PASS
- `Focus_Lost_To_Null_Should_Finalize_Active_Application`: PASS
- `Flush_On_Shutdown_Should_Emit_In_Progress_Application_And_Reset_State`: PASS
- `Missing_Application_Metadata_Should_Fallback_Safely`: PASS
- `Provider_Failure_Should_Be_Handled_Gracefully_Without_Crashing`: PASS
- `GracefulShutdown_FlushesApplicationActivity_WhenAppCollectorProvided`: PASS

### Integration Tests (`ApplicationActivitySyncIntegrationTests.cs`, `PlatformIntegrationTests.cs`):
- `ApplicationTransition_Persists_To_SQLite_And_Enqueues_To_SyncQueue`: PASS
- `MacOsActiveApplicationProvider_ReturnsActiveApplicationMetadata`: PASS
- `WindowsActiveApplicationProvider_OperatesWithoutCrash`: PASS
- `PersistAndEnqueueApplicationActivity_Should_Save_To_SQLite_And_SyncQueue`: PASS

### Full Test Suite Execution Summary:
- **Unit Tests:** 104 passed (0 failed, 0 skipped)
- **Integration Tests:** 73 passed (0 failed, 0 skipped)
- **Total:** 177 passed (100% green, ~2s runtime)

---

## 5. Definition of Done Checklist

- [x] Windows works (`WindowsActiveApplicationProvider` implemented, tested without crash, clean process name, zero window title)
- [x] macOS works (`MacOsActiveApplicationProvider` tested, verified on macOS host)
- [x] session association works (verified in unit and integration tests)
- [x] durations are correct (verified wall-clock calculation without timer drift)
- [x] no application content collected (`WindowTitle = null`, no URLs, files, or queries)
- [x] tests pass (177/177 passing)
- [x] docs complete (`application-tracking.md`, `phase-09-application-tracking.md`)
- [x] commit created
