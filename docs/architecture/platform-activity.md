# Platform Activity Architecture

## 1. Overview & Architectural Philosophy

The **Activity Engine** is responsible for collecting user input intensity and idle state metrics across operating systems. It enables RemoteWork to determine whether an employee is actively working, in a rest/idle period, and the relative intensity of their interactions (keyboard and mouse events) over time.

### Core Architectural Separation

The tracking pipeline strictly separates three distinct operational phases:

```
[ OS Native Events ] ──> (1. Raw Collection) ──> [ Atomic Platform Providers ]
                                                          │
                                                          v (2. Periodic Sampling)
                                                 [ ActivityCollector ]
                                                          │
                                                          v (3. Aggregation)
                                                 [ ActivityAccumulator ]
                                                          │
                                                          v (Flush)
                                                 [ ActivityBatch ] (Ready for Persistence/Sync)
```

1. **Raw Collection (Platform Layer)**:
   - Platform providers (`WindowsInputActivityProvider`, `MacOsInputActivityProvider`, `WindowsIdleTimeProvider`, `MacOsIdleTimeProvider`) intercept OS-level input signals and query system idle timers.
   - Operates in real-time on dedicated background threads or on-demand.
   - **Privacy Boundary**: Callbacks immediately discard key characters, virtual key codes, scan codes, and mouse coordinates, incrementing only primitive thread-safe numerical counters.
2. **Periodic Sampling (Application Layer)**:
   - Orchestrated by `ActivityCollector.Collect()` on a configurable interval (default: 10s).
   - Atomically reads and resets counters from `IInputActivityProvider`.
   - Queries system idle duration via `IIdleProvider` and compares against `IdleThresholdSeconds`.
   - Emits fine-grained in-memory domain events (`ActivityEvent`: `ActivityStateChanged`, `KeyboardActivity`, `MouseActivity`) only when transitions or non-zero activity occur.
   - Suppresses redundant state events: `ActivityStateChanged` is emitted only when transitioning between `Active` and `Idle`.
3. **Aggregation (Application Layer)**:
   - Aggregates sampled counts and time intervals in `ActivityAccumulator`.
   - Periodically flushes aggregated batches (`ActivityBatch`) representing time blocks (e.g. 60s).
   - Only aggregated batches are sent forward for persistence and remote synchronization — raw OS events are never transmitted to the network.

---

## 2. Abstraction Boundaries & Inversion of Control

The Application and Core layers contain **zero platform-specific API calls, Win32 imports, or macOS frameworks**. All interaction routes through contracts defined in `RemoteWork.Desktop.Platform.Abstractions`:

### Key Contracts

- **`IIdleProvider`**:
  ```csharp
  public interface IIdleProvider
  {
      TimeSpan GetIdleTime();
  }
  ```
  Provides the elapsed duration since the last system-wide hardware input.

- **`IInputActivityProvider`**:
  ```csharp
  public interface IInputActivityProvider : IDisposable
  {
      int GetKeyboardCount();
      int GetMouseCount();
      IReadOnlyList<MouseClickSample> DrainMouseSamples();
      void Start();
      void Stop();
  }
  ```
  Exposes explicit lifecycle controls (`Start`, `Stop`, `Dispose`) and atomic drain methods (`GetKeyboardCount`, `GetMouseCount`) that return the number of events captured since the last sample and reset internal counters to zero.

---

## 3. Platform Implementations

### 3.1. macOS (`RemoteWork.Desktop.Platform.MacOS`)

#### Idle Time Detection (`MacOsIdleTimeProvider`)
- **API / Framework**: `IOKit.framework` matching `IOHIDSystem`, extracting `HIDIdleTime`.
- **Function Call**: `IOServiceGetMatchingService(kIOMasterPortDefault, IOServiceMatching("IOHIDSystem"))` and `IORegistryEntryCreateCFProperty(service, "HIDIdleTime", ...)`.
- **Unit of Measurement**: Nanoseconds (converted to `TimeSpan`).
- **Why Selected**: Direct kernel-level HID property access; avoids spawning external processes (`ioreg` or AppleScript); microsecond execution time.
- **Permissions Required**: None. Available to any user process.
- **Limitations**: Older 32-bit tick registers can roll over after ~49 days of continuous uptime.
- **Fallback Behavior**: Wrapped in `try-catch`; returns `TimeSpan.Zero` if IOKit service matching fails or registry property is unavailable.

#### Input Monitoring (`MacOsInputActivityProvider`)
- **API / Framework**: `ApplicationServices.framework` (`CGEventTapCreate`) and `CoreFoundation.framework` (`CFRunLoop`).
- **Mechanism**:
  - Creates a passive listen-only event tap (`kCGSessionEventTap`, `kCGHeadInsertEventTap`, `kCGEventTapOptionListenOnly`).
  - Event mask filters: `kCGEventKeyDown` (10), `kCGEventLeftMouseDown` (1), `kCGEventRightMouseDown` (3), `kCGEventOtherMouseDown` (25), `kCGEventScrollWheel` (22).
  - Explicitly excludes `kCGEventMouseMoved` and `kCGEventLeftMouseDragged` to avoid processing high-frequency mouse movements.
  - Dedicated background thread runs `CFRunLoopGetCurrent()` with `CFMachPortCreateRunLoopSource`.
- **Why Selected**: Official Apple Quartz Event Services API for system-wide input observation; low latency; zero external dynamic libraries.
- **Permissions Required**: **Accessibility** (`AXIsProcessTrusted`).
- **Known Limitations**:
  - Requires user to grant Accessibility in *System Settings → Privacy & Security → Accessibility*.
  - Cannot tap events when the screen is locked or in non-GUI / SSH-only terminal sessions.
- **Fallback & Error Behavior**:
  - If Accessibility is missing, `CGEventTapCreate` returns `IntPtr.Zero`.
  - The provider logs a descriptive warning via `ILogger`, flags `_started = false`, and returns without crashing.
  - `GetKeyboardCount()` and `GetMouseCount()` safely return 0.
- **Performance Considerations**: Listen-only tap does not block the event stream; CPU overhead is negligible (<0.1%).

---

### 3.2. Windows (`RemoteWork.Desktop.Platform.Windows`)

#### Idle Time Detection (`WindowsIdleTimeProvider`)
- **API**: Win32 `user32.dll!GetLastInputInfo` + `Environment.TickCount`.
- **Calculation**: `idleMs = (uint)Environment.TickCount - lastInputInfo.dwTime`.
- **Why Selected**: Standard Win32 API measuring system-wide idle time across all physical input devices connected to the desktop session.
- **Permissions Required**: None for interactive user desktop sessions.
- **Tick Rollover Handling**: `Environment.TickCount` and `LASTINPUTINFO.dwTime` are 32-bit unsigned integers; unsigned subtraction `(uint)Environment.TickCount - dwTime` correctly handles the 49.7-day rollover arithmetic.
- **Fallback Behavior**: Returns `TimeSpan.Zero` if `GetLastInputInfo` returns false, avoiding unhandled runtime exceptions.

#### Input Monitoring (`WindowsInputActivityProvider`)
- **API**: Win32 Low-Level Hooks via `user32.dll!SetWindowsHookEx` (`WH_KEYBOARD_LL = 13`, `WH_MOUSE_LL = 14`).
- **Mechanism**:
  - Dedicated background thread configured with `ApartmentState.STA` runs a standard Win32 message loop (`GetMessage`, `TranslateMessage`, `DispatchMessage`).
  - Thread lifecycle is synchronized with `ManualResetEventSlim _hookReady` and terminated via `PostThreadMessage(threadId, WM_QUIT, 0, 0)`.
  - Keyboard callback: intercepts `WM_KEYDOWN` and `WM_SYSKEYDOWN`, filters out synthetic/injected events (`LLKHF_INJECTED`), and increments `_keyboardCount`.
  - Mouse callback: intercepts `WM_LBUTTONDOWN`, `WM_RBUTTONDOWN`, `WM_MBUTTONDOWN`, and `WM_MOUSEWHEEL`, incrementing `_mouseCount`. Mouse movements (`WM_MOUSEMOVE`) are ignored.
- **Why Selected**: Standard Windows mechanism for global low-level input hooks without injecting DLLs into third-party processes.
- **Permissions Required**: None for interactive user desktop sessions.
- **Known Limitations**:
  - Low-level hooks must maintain a responsive message loop or Windows will remove them (the hook timeout registry setting).
  - Session 0 Isolation: Cannot capture input if executed as a non-interactive Windows Service. Must run in the user's interactive desktop session.
- **Fallback & Error Behavior**:
  - If `SetWindowsHookEx` fails, the provider logs the Win32 error code via `Marshal.GetLastWin32Error()`, sets `_started = false`, and returns safely without throwing.
  - Counter queries return 0.
- **Performance Considerations**: Fast in-memory counter increment; hook callback delegates immediately forward to `CallNextHookEx`.

---

### 3.3. Linux (`RemoteWork.Desktop.Platform.Linux`)

- **Current State**: Scaffolded stubs (`LinuxIdleTimeProvider`, `LinuxInputActivityProvider`) implementing `IIdleProvider` and `IInputActivityProvider`.
- **Behavior**: Safe no-op implementations returning `TimeSpan.Zero` and 0 counts.
- **Future Roadmap**: Will implement X11 (`libXss` / `libX11` / `XRecord`) and Wayland idle protocol / portal input tracking during the dedicated Linux platform phase.

---

## 4. Privacy by Design Constraints

RemoteWork enforces strict privacy guarantees at the hardware interop layer:

| Data Type | Policy | Implementation Guarantee |
|---|---|---|
| **Keyboard Key Values** | **NEVER COLLECTED** | Keydown callbacks only increment `_keyboardCount++`. No virtual key code, scan code, or character is persisted or stored. |
| **Passwords / Text** | **NEVER COLLECTED** | No buffer or text reconstruction exists in the application. |
| **Mouse Coordinates** | **NEVER COLLECTED** | Cursor locations (X/Y coordinates) are not captured or saved in any provider buffer. |
| **Mouse Movement** | **IGNORED** | High-frequency mouse movements (`WM_MOUSEMOVE`, `kCGEventMouseMoved`) do not trigger event counts. |
| **Clipboard / Screen**| **EXCLUDED FROM ACTIVITY** | Activity Engine does not inspect clipboard or capture screens. |

---

## 5. Architectural Comparison with Gauzy

The RemoteWork Activity Engine was designed after an extensive technical study of Ever Gauzy Desktop's architecture:

| Architectural Dimension | Ever Gauzy Approach | RemoteWork Approach | RemoteWork Rationale |
|---|---|---|---|
| **Component Separation** | `desktop-activity` package mixed with Electron main process and `uiohook-napi` Node bindings. | Pure C# Clean Architecture with `RemoteWork.Desktop.Platform.Abstractions` and concrete platform assemblies. | Decouples business logic completely from OS dependencies; enables testability without Electron or native bindings installed. |
| **Input Capture Mechanism** | Third-party npm package (`uiohook-napi`) or polling `powerMonitor.getSystemIdleTime()`. | Native platform P/Invoke (`CGEventTap` on macOS, `SetWindowsHookEx` on Windows). | Eliminates heavy native Node addons and architecture mismatch issues (e.g. ARM64 vs x64 dylibs); strictly controls memory and privacy. |
| **Idle Detection** | Electron `powerMonitor.getSystemIdleTime()` with AFK dialog prompts. | Dedicated `IIdleProvider` consuming native OS timers (`IOKit` / `GetLastInputInfo`) against configurable `IdleThresholdSeconds`. | Allows headless background execution and deterministic idle state transitions without Electron GUI dependencies. |
| **Data Aggregation** | Periodic slot generation (`prepare_activities_screenshot`) coupled with screenshot capture and window title. | Three-tier pipeline: Raw collection $\rightarrow$ Periodic Sampling $\rightarrow$ Aggregated `ActivityBatch` flushing. | Prevents sending high-volume raw input events to the network; guarantees batch boundary consistency with unique GUIDs and UTC timestamps. |
| **Privacy Safeguards** | Flags exist in uiohook, but coordinate hooks and event types are bundled in third-party C bindings. | Hardware interop callbacks discard key/coordinate details immediately; zero coordinate persistence. | Guarantees compliance with strict privacy standards (GDPR, employee privacy policies). |
