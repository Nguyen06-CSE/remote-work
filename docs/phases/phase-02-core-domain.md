# Phase 02: Core Domain & Platform Abstraction Layer

## 1. Executive Summary

Phase 02 established the platform-independent Core Domain (`RemoteWork.Desktop.Core`) and Platform Abstraction layer (`RemoteWork.Desktop.Platform.Abstractions`). This phase defines all domain models, state machines, monitoring policies, and event definitions required by future desktop tracking modules, while enforcing strict architectural boundaries.

---

## 2. Implemented Scope

### Core Domain Models (`RemoteWork.Desktop.Core.Models`)
- `Device`: Represents machine hardware/OS identity.
- `Session`: Manages employee session state machine (`Starting`, `Active`, `Ending`, `Ended`, `Error`) and duration calculation.
- `ActivitySample`: Captures raw point-in-time input metrics (keyboard/mouse counts, active/idle time).
- `ActivityBatch`: Aggregates activity metrics over session time windows.
- `ApplicationActivity`: Represents foreground active window and process metadata.
- `ScreenshotMetadata`: Tracks capture metadata (file path, resolution, file size, timestamp).
- `MonitoringPolicy`: Centralized policy for sampling intervals, idle thresholds, screenshot intervals, and feature toggles.
- `AgentRuntimeState`: Manages agent runtime lifecycle status (`Starting`, `Running`, `Stopping`, `Stopped`, `Error`).

### Domain Event Model (`RemoteWork.Desktop.Core.Events`)
- `TrackingEvent`: Base abstract event containing `EventId`, `DeviceId`, `Timestamp`, `Type`, and `SessionId`.
- `SessionEvent`: Emitted during session status transitions.
- `ActivityTrackingEvent`: Emitted during raw activity collection.
- `ApplicationActivityEvent`: Emitted on active application window changes.
- `ScreenshotEvent`: Emitted when a screenshot capture completes.

### Platform Service Abstractions (`RemoteWork.Desktop.Platform.Abstractions`)
- `IDeviceProvider`: Interface for retrieving device info.
- `IIdleProvider`: Interface for system idle time detection.
- `IInputActivityProvider`: Interface for keyboard/mouse interaction metrics.
- `IApplicationActivityProvider`: Interface for active application tracking.
- `IScreenshotProvider`: Interface for desktop screen captures.

---

## 3. Architecture & Data Flow

```
+-----------------------------------------------------------+
|               Platform Implementations                    |
| (Platform.Windows / Platform.MacOS / Platform.Linux)       |
+-----------------------------------------------------------+
                              | Implements
                              v
+-----------------------------------------------------------+
|              Platform Abstractions Layer                  |
|  (IDeviceProvider, IIdleProvider, IScreenshotProvider...) |
+-----------------------------------------------------------+
                              ^ Consumes
                              |
+-----------------------------------------------------------+
|                   Application Layer                       |
|         (Collectors, Services, Orchestration)             |
+-----------------------------------------------------------+
                              | Operates on
                              v
+-----------------------------------------------------------+
|                      Core Domain                          |
|  (Models, Events, Policies, Runtime States - NO OS deps)  |
+-----------------------------------------------------------+
```

---

## 4. Files Created & Modified

### Core Domain (`src/RemoteWork.Desktop.Core/`)
- `Models/Device.cs` (Created)
- `Models/DeviceInfo.cs` (Updated to inherit `Device`)
- `Models/Session.cs` (Created)
- `Models/SessionInfo.cs` (Updated to inherit `Session`)
- `Models/ActivitySample.cs` (Created)
- `Models/ActivityBatch.cs` (Created)
- `Models/ApplicationActivity.cs` (Created)
- `Models/ScreenshotMetadata.cs` (Created)
- `Models/MonitoringPolicy.cs` (Created)
- `Events/TrackingEvent.cs` (Created)
- `Events/SessionEvent.cs` (Created)
- `Events/ActivityTrackingEvent.cs` (Created)
- `Events/ApplicationActivityEvent.cs` (Created)
- `Events/ScreenshotEvent.cs` (Created)

### Platform Abstractions (`src/RemoteWork.Desktop.Platform.Abstractions/`)
- `IDeviceProvider.cs` (Created)
- `IDeviceInfoProvider.cs` (Updated to inherit `IDeviceProvider`)
- `IIdleProvider.cs` (Created)
- `IIdleTimeProvider.cs` (Updated to inherit `IIdleProvider`)
- `IApplicationActivityProvider.cs` (Created)
- `IActiveApplicationProvider.cs` (Updated to inherit `IApplicationActivityProvider`)

### Documentation (`docs/`)
- `docs/architecture/core-domain.md` (Created)
- `docs/architecture/platform-abstraction.md` (Created)
- `docs/phases/phase-02-core-domain.md` (Created)

### Tests (`tests/RemoteWork.Desktop.UnitTests/Core/`)
- `DeviceTests.cs` (Created)
- `SessionTests.cs` (Created)
- `ActivitySampleTests.cs` (Created)
- `ActivityBatchTests.cs` (Created)
- `ApplicationActivityTests.cs` (Created)
- `ScreenshotMetadataTests.cs` (Created)
- `MonitoringPolicyTests.cs` (Created)
- `EventModelTests.cs` (Created)

---

## 5. Verification & Testing

- **Build Verification**: `dotnet build RemoteWork.Desktop.sln` succeeded with 0 warnings and 0 errors.
- **Unit Test Execution**: `dotnet test RemoteWork.Desktop.sln` passed all 49 unit tests and 6 integration tests.

---

## 6. Definition of Done Compliance

- [x] Core builds independently
- [x] Core has no platform dependency (`user32.dll`, macOS APIs, Linux APIs, EF Core, SQLite, HttpClient, Electron, Avalonia)
- [x] Abstractions compile
- [x] Unit tests pass (49 unit tests passed)
- [x] Documentation written (`docs/architecture/core-domain.md`, `docs/architecture/platform-abstraction.md`, `docs/phases/phase-02-core-domain.md`)
- [x] No OS-specific implementation added
- [x] No database/API/UI code added
