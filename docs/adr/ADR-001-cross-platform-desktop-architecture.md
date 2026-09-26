# ADR-001: Modular Cross-Platform Desktop Architecture

## Status
Accepted

## Context
The initial prototype of RemoteWork Desktop Agent was implemented as a single monolithic C# console project (`RemoteWork.Agent`). This project had tightly coupled dependencies between business logic (activity collecting, session management) and Windows Win32 native APIs (P/Invoke hooks, `GetLastInputInfo`).

The requirements for RemoteWork Desktop have evolved:
1. Target both macOS and Windows as first-class desktop environments, with future Linux support.
2. Maintain offline-first tracking capabilities with local persistence (SQLite / ORM).
3. Prepare for future FastAPI / PostgreSQL backend integration.
4. Keep the domain core isolated from OS, HTTP, and UI frameworks.

## Decision
We restructure the codebase into a modular Clean Architecture solution:
- `RemoteWork.Desktop.Core`: Pure domain models, enums, interfaces. Zero external dependencies.
- `RemoteWork.Desktop.Platform.Abstractions`: OS-neutral contracts (`IDeviceInfoProvider`, `IInputActivityProvider`, `IIdleTimeProvider`).
- `RemoteWork.Desktop.Platform.Windows`: Windows Win32 P/Invoke implementations.
- `RemoteWork.Desktop.Platform.MacOS`: macOS platform implementations and runtime detection.
- `RemoteWork.Desktop.Platform.Linux`: Linux platform scaffold.
- `RemoteWork.Desktop.Application`: Tracking workflow orchestration and use cases.
- `RemoteWork.Desktop.Infrastructure`: Configuration binding (`AgentOptions`), logging, external communication infrastructure.
- `RemoteWork.Desktop.Persistence`: Local storage implementations (identity store, future SQLite/EF Core).
- `RemoteWork.Desktop.Host`: Generic Host composition root with dynamic runtime platform detection.

## Consequences
### Positive
- Cross-platform buildability: The solution compiles and runs on macOS, Windows, and Linux without platform-specific build failures.
- Decoupled testing: Domain and application logic are tested with fast, in-memory unit tests using mocks/stubs.
- Clean boundaries: Clear separation between platform code, business logic, persistence, and host lifecycle.

### Negative / Trade-offs
- More projects in the solution, requiring proper project reference management and DI registration.
