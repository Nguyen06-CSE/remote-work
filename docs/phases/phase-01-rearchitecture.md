# Phase 01: Repository Audit & Architecture Migration

## 1. Original Architecture

The initial prototype consisted of a single monolith project (`RemoteWork.Agent`) and an associated test project (`RemoteWork.Agent.Tests`) nested under a subfolder `RemoteWork.AgentDesktop/`.

The monolithic project contained several subdirectories (`Core`, `Collectors`, `Platform/Windows`, `Storage`, `Monitoring`, `Configuration`) with direct dependencies across boundaries:
- `IdleActivityCollector` directly referenced `Platform/Windows/Input/WindowsIdleTimeProvider`.
- `Program.cs` directly registered Windows-specific services into DI without platform checks, failing on non-Windows platforms.
- Windows P/Invoke calls and low-level message pumps were intertwined with agent startup.
- Tests were restricted to in-memory model validation and lacked integration tests for persistence and decoupled collectors.

---

## 2. Problems Discovered During Audit

1. **Tight Platform Coupling:** Hardcoded Windows Win32 API dependencies inside collector classes prevented multi-platform execution (macOS/Linux).
2. **Monolithic Project Organization:** All technical layers (domain entities, background worker, Win32 P/Invoke, file storage, options) lived inside one single assembly.
3. **DI Duplication:** `Program.cs` had duplicated singleton registrations for providers and collectors.
4. **No Clean OS Abstraction Layer:** Missing generic contracts for idle time querying and OS metadata retrieval across platforms.
5. **Lack of Persistence Layering:** File-based identity storage was lumped into storage folders inside the worker project rather than an isolated persistence layer ready for SQLite/EF Core.

---

## 3. Target Architecture

The repository has been restructured into a standard, clean .NET solution (`RemoteWork.Desktop.sln` / `RemoteWork.Desktop.slnx`):

```
src/
├── RemoteWork.Desktop.Host/                   # Generic Host, DI composition, Worker BackgroundService
├── RemoteWork.Desktop.Core/                   # Domain entities, enums, domain interfaces
├── RemoteWork.Desktop.Application/            # Tracking workflows, activity aggregation, orchestration
├── RemoteWork.Desktop.Infrastructure/         # Configuration binding, options, logging
├── RemoteWork.Desktop.Persistence/            # Device identity storage, SQLite/EF Core foundations
├── RemoteWork.Desktop.Platform.Abstractions/  # OS-neutral capability contracts (IDeviceInfoProvider, etc.)
├── RemoteWork.Desktop.Platform.Windows/       # Windows Win32 LL hooks, GetLastInputInfo
├── RemoteWork.Desktop.Platform.MacOS/         # macOS native providers & runtime detection
└── RemoteWork.Desktop.Platform.Linux/         # Linux scaffolded providers

tests/
├── RemoteWork.Desktop.UnitTests/              # Domain & application unit tests (20 tests)
└── RemoteWork.Desktop.IntegrationTests/       # Persistence & filesystem integration tests (1 test)

docs/
├── architecture/                              # Architecture overviews & project boundaries
├── adr/                                       # Architectural Decision Records
├── phases/                                    # Phase documentation
├── troubleshooting/                           # Cross-platform troubleshooting guides
└── reference/                                 # Gauzy references and architecture roadmaps
```

---

## 4. Migration Mapping

| Old Location (Prototype) | New Project & Namespace | Rationale |
| :--- | :--- | :--- |
| `Core/Models/*` | `RemoteWork.Desktop.Core.Models` | Pure domain entities (`DeviceInfo`, `SessionInfo`, `AgentRuntimeState`, `ActivityBatch`, etc.). |
| `Core/Enums/*` | `RemoteWork.Desktop.Core.Enums` | Domain enums (`SessionStatus`, `AgentStatus`, `ActivityEventType`). |
| `Core/Interfaces/*` | `RemoteWork.Desktop.Core.Interfaces` & `Platform.Abstractions` | Segregated into pure domain interfaces vs. platform capability abstractions. |
| `Collectors/Device/*` | `RemoteWork.Desktop.Application.Collectors` | Orchestrates device collection without depending on concrete OS types. |
| `Collectors/Session/*` | `RemoteWork.Desktop.Application.Collectors` | Orchestrates session lifecycle (`ISessionCollector`). |
| `Collectors/Activity/*` | `RemoteWork.Desktop.Application.Collectors` | Orchestrates activity event generation, duration calculation, and batch flushing. |
| `Platform/Windows/*` | `RemoteWork.Desktop.Platform.Windows` | Isolated Win32 P/Invoke code, hooks, and message pump. |
| `Storage/DeviceIdentityStore.cs` | `RemoteWork.Desktop.Persistence.FileDeviceIdentityStore` | Isolated in persistence layer with parameterizable storage path for testing. |
| `Configuration/AgentOptions.cs` | `RemoteWork.Desktop.Infrastructure.Configuration` | Bound via `Microsoft.Extensions.Options`. |
| `Monitoring/MonitoringService.cs` | `RemoteWork.Desktop.Application.Monitoring` | Async activity sampling loop orchestrated via interfaces. |
| `Program.cs`, `Worker.cs` | `RemoteWork.Desktop.Host` | Clean Generic Host composition with runtime OS platform branching. |

---

## 5. Dependency Direction

```
Host ──► (Application, Infrastructure, Persistence, Platform.*)
Application ──► (Core, Platform.Abstractions)
Platform.Windows ──► (Platform.Abstractions, Core)
Platform.MacOS ──► (Platform.Abstractions, Core)
Platform.Linux ──► (Platform.Abstractions, Core)
Persistence ──► (Core, Application)
Infrastructure ──► (Core, Application)
Platform.Abstractions ──► Core
Core ──► (Zero dependencies)
```

---

## 6. Key Design Decisions

### 6.1. Why C# / .NET Remains the Chosen Platform
- **Native OS Interop:** Modern .NET 10 provides first-class, high-performance P/Invoke (`DllImport`, `LibraryImport`), low-level threading (STA message pumps on Windows, GCD / CoreGraphics integration on macOS), and memory safety.
- **Low Footprint Background Runtime:** A compiled .NET Generic Host background worker runs with minimal CPU and memory overhead compared to bundling heavyweight runtimes for background services.
- **Robust Cross-Platform Generic Host:** Built-in dependency injection, structured logging, configuration management, and hosting lifecycle works uniformly across Windows, macOS, and Linux.

### 6.2. Why Platform-Specific Implementations are Isolated
- **Compile-Time Safety & Portability:** Isolating Win32 P/Invoke code in `RemoteWork.Desktop.Platform.Windows` ensures that non-Windows builds (e.g. running on macOS development machines or Linux CI pipelines) do not fail with runtime interop exceptions.
- **Unit Testability:** Application and domain logic can be thoroughly unit tested with mock/stub platform providers without needing access to specific OS hooks.

---

## 7. What Was Preserved From the Old Prototype
- Full state transition logic for `SessionInfo` and `AgentRuntimeState`.
- Win32 low-level keyboard/mouse hook algorithms with dedicated STA thread and message pump (`GetMessage`/`DispatchMessage`).
- Device identity persistence strategy using local application data storage.
- Activity batch aggregation mathematics and active/idle duration tracking.
- All original unit test cases.

---

## 8. What Was Intentionally Not Implemented
- **UI Framework / Windows:** UI development is deferred until Desktop tracking and Backend services are stabilized.
- **FastAPI / PostgreSQL Network Synchronization:** HTTP synchronization client is not yet added in Phase 01 to keep focus on repository architecture.
- **SQLite / EF Core Migration Scripts:** SQLite entities and migrations will be implemented in the dedicated persistence phase.
- **Full macOS / Linux Native Hook Implementations:** macOS and Linux projects provide valid, working scaffolds with runtime platform resolution; full native hook capturing (e.g., macOS CoreGraphics event taps) will be implemented in their respective platform phases.

---

## 9. Migration Risks & Mitigation

| Risk | Mitigation |
| :--- | :--- |
| **Breaking Existing Functionality:** Migrating classes across projects could break existing behavior. | Preserved exact method logic, migrated all 11 unit tests, and added 10 new unit/integration tests (total 21 passing tests). |
| **Cross-Platform Compilation Failures:** Win32 P/Invoke APIs failing on macOS/Linux environments. | Separated platform assemblies and guarded OS-specific runtime calls using `RuntimeInformation.IsOSPlatform`. |
| **Circular Dependencies:** Application and Infrastructure creating circular dependencies. | Enforced strict inward dependency flow towards `Core` and `Platform.Abstractions`. |
