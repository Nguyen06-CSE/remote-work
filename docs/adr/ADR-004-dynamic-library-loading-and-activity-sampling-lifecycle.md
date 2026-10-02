# ADR-004: Dynamic System Framework Loading and Activity Sampling Lifecycle

## Status
Accepted

## Context
During the Phase 09 Application Tracking investigation, two systemic issues were identified:

1. **macOS Framework Linkage in .NET Console Runtimes:**
   In ADR-002, macOS active application detection was specified via Objective-C runtime P/Invoke (`libobjc.dylib` calling `objc_getClass("NSWorkspace")`). In a managed .NET 10 console application, system GUI frameworks like `AppKit.framework` are not pre-linked or loaded into the process address space. Consequently, `objc_getClass("NSWorkspace")` returns `0` unless `AppKit` is loaded explicitly via `dlopen`.

2. **Transition-Only Sampling vs. Steady-State Telemetry:**
   `ApplicationActivityCollector` was implemented purely as a transition detector (emitting records only on app focus change or shutdown). When a user works in a single application for an extended period without switching, zero records are emitted or persisted, creating an observability gap and leaving SQLite with 0 records until process exit.

## Decision

1. **Explicit Dynamic Framework Initialization on macOS:**
   - Platform providers interacting with Objective-C frameworks outside `libSystem.dylib` (such as `AppKit` for `NSWorkspace`) MUST explicitly load their framework binary via `dlopen("/System/Library/Frameworks/<Framework>.framework/<Framework>", RTLD_LAZY)` in the static constructor or initialization phase.
   - All Objective-C class lookups (`objc_getClass`) must be validated against `IntPtr.Zero` with appropriate diagnostic warning logs rather than silent failure.

2. **Integration Test Integrity Mandate:**
   - Platform integration tests MUST NOT hide assertions behind conditional null checks (`if (result is not null)`).
   - In desktop session environments where a GUI is active, platform providers MUST assert non-null metadata (`Assert.NotNull(result)`). In headless CI environments where no GUI session exists, tests MUST explicitly assert the documented fallback behavior.

3. **Collector Sampling Lifecycle & Steady-State Heartbeat:**
   - `ApplicationActivityCollector` continues to prevent spamming duplicate records on every poll.
   - However, to prevent data loss in ungraceful process terminations and ensure consistent backend visibility, the collector will support an activity span flush if the active duration exceeds a configurable threshold (e.g. 5 minutes) or emit diagnostic trace telemetry on every sample.

## Consequences

### Positive
- Fully resolves the macOS `NSWorkspace` resolution failure across all .NET process hosting models.
- Eliminates false-positive test passes in integration suites.
- Restores real-time observability and database persistence for desktop application tracking.

### Negative / Trade-offs
- Calling `dlopen` loads the AppKit framework image into process memory (~2-4 MB working set impact, fully acceptable within runtime budget).
