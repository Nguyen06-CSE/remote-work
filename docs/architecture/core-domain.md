# Core Domain Architecture

## 1. Overview & Architectural Role

The **Core Domain** (`RemoteWork.Desktop.Core`) forms the stable, platform-independent foundation of the RemoteWork Desktop client tracking system. It defines the core domain models, state machines, domain events, and policy structures used by all higher layers (Application, Infrastructure, Persistence, and Host).

Following Clean Architecture principles, the Core assembly has **zero dependencies** on external packages, OS APIs (`user32.dll`, macOS Cocoa/AppKit, Linux X11/Wayland), ORMs (EF Core, SQLite), HTTP clients, or UI frameworks.

---

## 2. Domain Models

| Model | Purpose | Ownership | Creator Layer | Consumer Layer |
|---|---|---|---|---|
| **Device** | Represents the physical machine running the agent, including OS metadata and agent version. | Domain Model | Platform Provider / Application | Persistence / Sync Service / Backend |
| **Session** | Represents an employee work tracking session with lifecycle state (`Starting`, `Active`, `Ending`, `Ended`, `Error`). | Domain Model | Application (`SessionCollector`) | Persistence / Activity Collectors / UI |
| **ActivitySample** | Represents a point-in-time sample of raw user input metrics (keyboard counts, mouse counts, active/idle status). | Domain Model | Application (`ActivityCollector`) | Aggregator / Persistence |
| **ActivityBatch** | Represents an aggregated time window of activity metrics over a session interval. | Domain Model | Application (`ActivityAccumulator`) | Persistence / Sync Queue / Backend |
| **ApplicationActivity** | Represents active window and process foreground tracking data. | Domain Model | Platform Provider / Application | Persistence / Sync Queue / Backend |
| **ScreenshotMetadata** | Captures metadata for a taken screen capture (path, resolution, file size, timestamp). | Domain Model | Platform Provider / Application | Persistence / Upload Queue / Backend |
| **MonitoringPolicy** | Represents remotely or locally configured monitoring intervals, thresholds, and feature flags. | Domain Model | Application / Configuration | Activity Collectors / Monitoring Service |
| **AgentRuntimeState** | Tracks the lifecycle state of the background tracking agent (`Starting`, `Running`, `Stopping`, `Stopped`, `Error`). | Domain Model | Host / Application | Monitoring Service / System Tray / Host |

---

## 3. Relationships Between Device, Session, and Activity

```
+-------------------------------------------------------------------+
|                            Device                                 |
|  (DeviceId, Hostname, OperatingSystem, OsVersion, AgentVersion)   |
+-------------------------------------------------------------------+
                                  | 1
                                  |
                                  | *
+-------------------------------------------------------------------+
|                            Session                                |
|    (SessionId, DeviceId, StartedAt, EndedAt, Status, Duration)    |
+-------------------------------------------------------------------+
       | 1                                 | 1
       |                                   |
       | *                                 | *
+-----------------------+   +-------------------------------+   +-----------------------+
|    ActivitySample     |   |      ApplicationActivity      |   |  ScreenshotMetadata   |
| (SampleId, DeviceId,  |   |   (ActivityId, DeviceId,      |   | (ScreenshotId,        |
|  SessionId, Keyboard, |   |    SessionId, ProcessName,    |   |  DeviceId, SessionId, |
|  Mouse, Active/Idle)  |   |    WindowTitle, Duration)     |   |  FilePath, Width, H)  |
+-----------------------+   +-------------------------------+   +-----------------------+
       |
       v (aggregated into)
+-------------------------------------------------------------------+
|                            ActivityBatch                          |
|  (BatchId, DeviceId, SessionId, KeyboardCount, MouseCount, ...)   |
+-------------------------------------------------------------------+
```

1. **Device -> Session**: A single `Device` can have multiple tracking `Session`s over time. Every `Session` MUST be tied to a specific `DeviceId`.
2. **Session -> Work Tracking Data**: All raw activity samples (`ActivitySample`), application switches (`ApplicationActivity`), screenshots (`ScreenshotMetadata`), and activity batches (`ActivityBatch`) reference `SessionId`.
3. **Decoupled Life Cycle**: If a session ends, new raw events are suspended until a new session starts.

---

## 4. Separation of Raw Data vs. Derived Metrics

The domain explicitly distinguishes between **Raw Data** and **Derived Metrics**:

- **Raw Data** (`ActivitySample`, `ApplicationActivity`, `ScreenshotMetadata`, `TrackingEvent`):
  - Collected directly from platform providers and input hooks.
  - Granular, immutable event data capturing high-resolution events.
  - Stored locally for offline durability and auditability.
- **Derived Metrics** (`ActivityBatch`, session totals, active/idle time summaries):
  - Aggregated over fixed time windows (e.g. 1-minute or 10-minute intervals).
  - Computed by domain accumulators to minimize payload bandwidth before synchronization.
  - The Desktop agent collects raw and derived metrics; employee performance scoring and analytical derived indicators are **never** calculated inside the client agent—they are left strictly for Backend processing.

---

## 5. Event Model

All tracking events inherit from the unified base `TrackingEvent`:

```
                    +------------------------+
                    |     TrackingEvent      |
                    |  (EventId, DeviceId,   |
                    |   Timestamp, Type,     |
                    |   SessionId?)          |
                    +------------------------+
                                |
     +------------------+-------+-------+--------------------+
     |                  |               |                    |
+----+-------------+ +--+------------+ ++-----------------+ ++----------------+
|  SessionEvent    | |  Activity     | |  Application     | | ScreenshotEvent |
| (SessionStatus)  | |  TrackingEvent| |  ActivityEvent   | | (FilePath,      |
+------------------+ +---------------+ +------------------+ |  Width, Height, |
                                                            |  FileSize)      |
                                                            +-----------------+
```

Every event supports:
- `EventId`: Unique identifier (string/Guid) for deduplication.
- `DeviceId`: Identifies the originating device.
- `Timestamp`: UTC timestamp of event creation.
- `Type`: String event classification (e.g., `session.started`, `activity.sample`, `application.changed`, `screenshot.captured`).
- `SessionId`: Optional reference to the active session for work-tracking contextualization.
