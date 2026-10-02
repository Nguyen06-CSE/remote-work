# Application Activity Tracking Architecture

## 1. Objective & Scope

The Application Activity Tracking system tracks which application is active in the user's foreground and how long it remains active. It operates strictly within privacy boundaries to provide organizational visibility into application usage without collecting sensitive personal or proprietary user data.

---

## 2. High-Level Architecture

The tracking pipeline is fully decoupled and platform-independent:

```
Platform Abstraction
  └─ IApplicationActivityProvider (macOS / Windows / Linux)
           ↓ (ActiveApplicationInfo: AppName, ProcName, PID)
Application Collector
  └─ ApplicationActivityCollector (State machine: Transition detection & Wall-clock duration)
           ↓ (ApplicationActivity: start time, duration, session, device)
Monitoring Service
  └─ MonitoringService (Periodic background sampling tick & graceful shutdown flush)
           ↓ (OnApplicationActivityGenerated event)
Tracking Coordinator
  └─ TrackingPersistenceCoordinator
       ├──> SQLite: ApplicationActivities Table (Local persistence)
       └──> SQLite: SyncQueue Table (Offline-first sync engine)
```

---

## 3. Platform Implementations

### 3.1 macOS (`MacOsActiveApplicationProvider`)
- **Primary Mechanism:** Direct WindowServer query via `CoreGraphics.framework` (`CGWindowListCopyWindowInfo`).
  - Queries on-screen windows at layer 0 (`kCGNormalWindowLevel`).
  - Extracts `kCGWindowOwnerName` and `kCGWindowOwnerPID`.
  - Operates synchronously over Mach IPC from any thread without depending on a Cocoa event loop (`NSRunLoop.mainRunLoop` / `NSApplication`).
- **Secondary Fallback:** Native Objective-C runtime via `libobjc.dylib` P/Invoke (`objc_getClass`, `sel_registerName`, `objc_msgSend`).
  - Explicitly initializes `AppKit.framework` via `dlopen` so that `NSWorkspace` is registered in .NET console hosts.
  - Calls `[NSWorkspace sharedWorkspace] frontmostApplication`.
- **Permission Requirements:** None required for reading the application name and PID. Screen Recording permission is NOT needed because window titles and contents are strictly ignored.
- **Polling Strategy:** Polled synchronously on each monitoring tick (default 10s in production, 2s in dev).
- **Performance:** Sub-millisecond execution time (< 0.1ms per invocation, minimal CPU overhead).

### 3.2 Windows (`WindowsActiveApplicationProvider`)
- **API / Library:** Uses Win32 user32 APIs via P/Invoke:
  - `GetForegroundWindow()` retrieves the foreground window handle (`HWND`).
  - `GetWindowThreadProcessId(hwnd, out pid)` retrieves the associated process ID.
  - `Process.GetProcessById(pid).ProcessName` resolves the clean executable process name.
- **Permission Requirements:** Standard user permissions (no administrator or UAC elevation required).
- **Polling Strategy:** Polled synchronously on each monitoring tick.
- **Limitations:** Some modern UWP / Windows Store applications run inside generic host wrappers (`ApplicationFrameHost.exe`).
- **Performance:** Sub-millisecond execution time, zero perceptible impact on system responsiveness.

### 3.3 Linux (`LinuxActiveApplicationProvider`)
- **API / Library:** Abstraction stub (`GetActiveApplication() => null`).
- **Permission Requirements:** N/A.
- **Strategy:** Reserved for future X11 (`_NET_ACTIVE_WINDOW`) / Wayland implementation.

---

## 4. Active Application Transition & Timing Model

### 4.1 Transition Detection
Rather than emitting redundant telemetry records for every sampling poll, `ApplicationActivityCollector` maintains internal state:
- When the user remains on the same application across consecutive polls, `Sample()` returns `null` (no redundant records generated).
- When a change in active application is detected (e.g., `VS Code` -> `Google Chrome` -> `Postman`):
  1. The previous application's span is finalized.
  2. Wall-clock duration is computed: `duration = currentTime - startTime`.
  3. An `ApplicationActivity` record is emitted for the ended application.
  4. The newly focused application begins a new active span starting at `currentTime`.
- When focus is lost (e.g., screen locked or desktop focused without active window), the active application span is closed.

### 4.2 Wall-Clock Timing (Avoiding Drift)
- Spans use absolute UTC timestamps (`DateTimeOffset.UtcNow`).
- Duration is calculated strictly as `EndTime - StartTime`.
- This eliminates cumulative timer drift that occurs when summing discrete polling intervals.

### 4.3 Lifecycle & Graceful Shutdown
When the agent terminates (`MonitoringService.StopAsync()`):
- `ApplicationActivityCollector.Flush()` finalizes the currently active application span up to the exact moment of shutdown.
- Ensures no tracked time is lost when the application closes.

---

## 5. Privacy Guarantees & Deliberately Excluded Data

To ensure complete employee privacy and security compliance, the Application Tracking system adheres to strict data minimization:

| Data Element | Collected? | Rationale / Mitigation |
|---|---|---|
| **Application Name** | **YES** | Identifies which tool is active (e.g. "Visual Studio Code", "Google Chrome"). |
| **Process Name** | **YES** | Identifies executable name (e.g. "Code", "chrome"). |
| **Process ID** | **YES** | Distinguishes process instances. |
| **Start Time & Duration** | **YES** | Measures engagement duration. |
| **Window Title** | **NO (`null`)** | **Deliberately excluded.** Window titles often contain sensitive file paths, document names, or search queries. |
| **Application Content** | **NO** | Zero document text, code, or editor content collected. |
| **File Content & Paths** | **NO** | No file system or open file paths inspected. |
| **Browser URLs** | **NO** | Zero URLs, domains, query parameters, or browsing history collected. |
| **Search Queries** | **NO** | Zero input keystrokes or query text collected. |

---

## 6. Offline-First Persistence & Synchronization

All emitted `ApplicationActivity` records follow the established offline architecture:
1. Written directly to SQLite table `ApplicationActivities`.
2. Enqueued into SQLite table `SyncQueue` with `EntityType = "ApplicationActivity"`.
3. Transported asynchronously by `SyncEngine` when connectivity is available.
