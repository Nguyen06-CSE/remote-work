# ADR-004: Dynamic System Framework Loading and Activity Sampling Lifecycle

## Status
Accepted

## Context
During the Phase 09 Application Tracking investigation and verification, several critical platform runtime behaviors were identified on macOS:

1. **macOS Framework Linkage in .NET Console Runtimes:**
   In ADR-002, macOS active application detection was specified via Objective-C runtime P/Invoke (`libobjc.dylib` calling `objc_getClass("NSWorkspace")`). In a managed .NET 10 console application, system GUI frameworks like `AppKit.framework` are not pre-linked or loaded into the process address space. Consequently, `objc_getClass("NSWorkspace")` returns `0` unless `AppKit` is loaded explicitly via `dlopen`.

2. **Cocoa NSRunLoop Coupling in CLI Daemons:**
   In standard macOS AppKit architecture, `NSWorkspace.sharedWorkspace.frontmostApplication` caches foreground state and relies on WindowServer distributed notifications delivered strictly to the main thread's Cocoa event loop (`NSRunLoop.mainRunLoop` / `[NSApplication run]`).
   In a .NET CLI worker daemon running on thread pool worker threads without `NSApplication`, `NSWorkspace.frontmostApplication` remains stuck on the initial application and never updates when the user switches windows.
   Direct Mach IPC querying via `CoreGraphics.framework` (`CGWindowListCopyWindowInfo` filtering for on-screen layer 0 windows) provides immediate, thread-independent foreground window owner metadata without requiring any run loop or screen recording permissions.

3. **Transition-Only Sampling vs. Steady-State Telemetry:**
   `ApplicationActivityCollector` was implemented purely as a transition detector (emitting records only on app focus change or shutdown). When a user works in a single application for an extended period without switching, zero records are emitted or persisted, creating an observability gap and leaving SQLite with 0 records until process exit.

## Decision

1. **Dual Query Architecture on macOS (`MacOsActiveApplicationProvider`):**
   - **Explicit Dynamic Framework Initialization:** `MacOsActiveApplicationProvider` loads `AppKit.framework` via `dlopen("/System/Library/Frameworks/AppKit.framework/AppKit", RTLD_LAZY)` to ensure Objective-C runtime registration.
   - **Primary WindowServer Query (`CoreGraphics`):** Uses `CGWindowListCopyWindowInfo(kCGWindowListOptionOnScreenOnly | kCGWindowListExcludeDesktopElements, 0)` to inspect the top-level window at normal level (layer 0). Extracts `kCGWindowOwnerName` and `kCGWindowOwnerPID` synchronously without run loop dependency.
   - **Secondary Fallback (`AppKit` / `NSWorkspace`):** If WindowServer query returns null, falls back to `NSWorkspace.sharedWorkspace.frontmostApplication`.

2. **Strict Privacy by Design:**
   - Window titles, document names, URLs, and search queries MUST NOT be collected.
   - `ActiveApplicationInfo.WindowTitle` is strictly assigned to `string.Empty` or `null`. No accessibility APIs or title extraction are called.

3. **Integration Test Integrity Mandate:**
   - Platform integration tests MUST NOT hide assertions behind conditional null checks (`if (result is not null)`).
   - In desktop session environments where a GUI is active, platform providers MUST assert non-null metadata (`Assert.NotNull(result)`). In headless CI environments where no GUI session exists, tests MUST explicitly assert the documented fallback behavior.

4. **Collector Sampling Lifecycle & Async Shutdown Guarantee:**
   - `ApplicationActivityCollector` prevents spamming duplicate records on every poll.
   - On daemon shutdown, `Worker.StopAsync` flushes the active application span and explicitly awaits pending persistence tasks (`Task.WhenAll(_pendingTasks)`) to guarantee all final records are written to SQLite before host termination.

## Consequences

### Positive
- Fully resolves the macOS `NSWorkspace` resolution failure across all .NET process hosting models.
- Provides instantaneous, thread-safe active window tracking in CLI background daemons.
- Eliminates false-positive test passes in integration suites.
- Ensures zero data loss on graceful host shutdown.

### Negative / Trade-offs
- CoreGraphics window iteration inspects top on-screen windows (takes <0.1ms per sample, negligible CPU impact).
- Calling `dlopen` on AppKit loads framework images (~2-4 MB working set, well within the ~100 MB budget).
