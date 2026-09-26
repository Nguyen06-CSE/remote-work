# Project Boundaries & Dependency Rules

## 1. Overview

To ensure maintainability, testability, and portability across operating systems, RemoteWork Desktop enforces strict boundary and dependency rules across all solution projects.

---

## 2. Dependency Matrix

```
┌────────────────────────────────────────────────────────┐
│               RemoteWork.Desktop.Host                  │
└───────┬──────────┬──────────┬──────────┬───────┬───────┘
        │          │          │          │       │
        ▼          ▼          ▼          ▼       │
   Persistence   Infra   Platform.*  Application │
        │          │          │          │       │
        │          │          ▼          │       │
        │          │    Platform.Abstr.  │       │
        │          │          │          │       │
        ▼          ▼          ▼          ▼       ▼
       └───────────┴──────────┴──────────┴──► Core
```

---

## 3. Strict Boundary Rules

### 1. `RemoteWork.Desktop.Core`
- **Rule 1.1:** Must NEVER reference any other project in the solution.
- **Rule 1.2:** Must NEVER depend on platform-specific APIs (`System.Runtime.InteropServices` P/Invoke, Win32, macOS AppKit/Carbon/CoreGraphics, Linux X11/Wayland).
- **Rule 1.3:** Must NEVER reference Entity Framework Core, SQLite, HTTP client libraries, or UI frameworks.
- **Rule 1.4:** Contains only pure domain logic, entities, value objects, domain enums, and domain interfaces.

### 2. `RemoteWork.Desktop.Platform.Abstractions`
- **Rule 2.1:** Defines OS-neutral capability contracts (`IDeviceInfoProvider`, `IInputActivityProvider`, `IIdleTimeProvider`).
- **Rule 2.2:** May reference `RemoteWork.Desktop.Core`.
- **Rule 2.3:** Must not contain platform-specific P/Invoke code or third-party native binaries.

### 3. `RemoteWork.Desktop.Platform.*` (Windows / MacOS / Linux)
- **Rule 3.1:** References `RemoteWork.Desktop.Platform.Abstractions` and `RemoteWork.Desktop.Core`.
- **Rule 3.2:** Must never reference `Application`, `Infrastructure`, `Persistence`, or other sibling `Platform.*` projects.
- **Rule 3.3:** Encapsulates OS-specific API calls (P/Invoke, message pumps, platform permissions, C API interop).

### 4. `RemoteWork.Desktop.Application`
- **Rule 4.1:** References `RemoteWork.Desktop.Core` and `RemoteWork.Desktop.Platform.Abstractions`.
- **Rule 4.2:** Must never reference `Platform.Windows`, `Platform.MacOS`, `Platform.Linux`, `Infrastructure`, or `Persistence`.
- **Rule 4.3:** Contains use case logic, batching algorithms, tracking workflow orchestration.

### 5. `RemoteWork.Desktop.Infrastructure`
- **Rule 5.1:** References `RemoteWork.Desktop.Core` and `RemoteWork.Desktop.Application`.
- **Rule 5.2:** Handles technical configuration binding (`AgentOptions`), logging, external communication infrastructure.

### 6. `RemoteWork.Desktop.Persistence`
- **Rule 6.1:** References `RemoteWork.Desktop.Core` and `RemoteWork.Desktop.Application`.
- **Rule 6.2:** Implements storage abstractions (`IDeviceIdentityStore`, future SQLite `DbContext`, repositories).

### 7. `RemoteWork.Desktop.Host`
- **Rule 7.1:** References all layers to perform runtime dependency injection composition.
- **Rule 7.2:** Must contain minimal logic: bootstrap, configuration loading, OS detection, and host lifecycle.
