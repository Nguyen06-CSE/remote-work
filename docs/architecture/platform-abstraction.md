# Platform Abstraction Architecture

## 1. Overview & Abstraction Strategy

The **Platform Abstraction Layer** (`RemoteWork.Desktop.Platform.Abstractions`) defines clean, platform-independent C# interfaces that invert dependencies between business logic (`RemoteWork.Desktop.Application` / `Core`) and platform-specific OS implementations (`RemoteWork.Desktop.Platform.Windows`, `MacOS`, `Linux`).

### Key Principles

1. **Dependency Inversion**: High-level application modules depend on interfaces in `Platform.Abstractions`, not on concrete OS implementations.
2. **Zero Platform Types in Abstractions**: Interfaces MUST NOT reference OS-specific types (e.g. `HWND`, `CGWindowID`, `X11 Display`, `Win32 MSG`). All signatures use standard .NET primitives or Core domain models.
3. **Pluggable Architecture**: Platform-specific projects implement these interfaces and are registered via Dependency Injection during application host startup based on `OperatingSystem.IsWindows()`, `IsMacOS()`, or `IsLinux()`.

---

## 2. Core Abstraction Interfaces

### `IDeviceProvider`
- **Purpose**: Provides hardware and operating system information for device identification.
- **Contract**:
  ```csharp
  public interface IDeviceProvider
  {
      Device GetDevice(string deviceId, string agentVersion);
  }
  ```

### `IIdleProvider`
- **Purpose**: Queries system-wide user idle duration since the last hardware input event.
- **Contract**:
  ```csharp
  public interface IIdleProvider
  {
      TimeSpan GetIdleTime();
  }
  ```

### `IInputActivityProvider`
- **Purpose**: Monitors low-level keyboard and mouse interaction counters without capturing sensitive typed key contents or mouse position paths.
- **Contract**:
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

### `IApplicationActivityProvider`
- **Purpose**: Detects currently active foreground window title, process name, and application display name.
- **Contract**:
  ```csharp
  public interface IApplicationActivityProvider
  {
      ApplicationActivity? GetActiveApplicationActivity();
  }
  ```

### `IScreenshotProvider`
- **Purpose**: Captures desktop screen snapshots according to policy configuration.
- **Contract**:
  ```csharp
  public interface IScreenshotProvider
  {
      ScreenshotResult CaptureScreen(string outputPath);
  }
  ```

---

## 3. Gauzy Architectural Lessons & Decisions

### Lessons Applied from Gauzy
- **Strict OS Capability Isolation**: Native OS hooks and platform APIs are encapsulated entirely inside dedicated platform projects (`Platform.Windows`, `Platform.MacOS`, `Platform.Linux`).
- **Offline-First Data Pipeline**: Platform abstractions return lightweight domain data structures that can be seamlessly persisted locally to SQLite or sent over sync queues.
- **Privacy Controls**: Platform input providers only increment numerical event counts. No text keyloggers or sensitive contents are ever retrieved.

### Gauzy Concepts Intentionally NOT Copied
- **Monolithic Node.js/Electron Wrapper**: Gauzy mixes native Node C++ bindings directly within Electron processes. RemoteWork uses native .NET C# P/Invoke and Native AOT compatibility without Electron overhead.
- **Leaky OS Types in Core**: Gauzy occasionally leaks platform-specific shell handles into application logic. RemoteWork enforces complete isolation in `Platform.Abstractions`.
- **Client-side Productivity Calculations**: Gauzy calculates employee productivity scores inside the client. RemoteWork delegates all performance scoring to the Backend.
