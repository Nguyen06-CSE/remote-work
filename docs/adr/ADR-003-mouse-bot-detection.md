# ADR-003: Ephemeral In-Memory Mouse Coordinates for Fraud and Bot Detection

## Status
Accepted

## Context
In `ADR-002` (Native Platform Activity Providers Implementation Strategy), the system established strict privacy constraints:
> *"Key codes, character sequences, passwords, clipboard contents, and mouse cursor trajectories are NEVER captured or retained."*

Additionally, `docs/ai-development/master-agent-instruction.md` states:
> *"Do not store mouse coordinates unless a future phase explicitly requires them."*

However, in real-world remote work scenarios, employees may employ automated clickers ("mouse auto-clickers", macros, or anti-idle scripts) to fabricate activity counts and artificially inflate active duration metrics without performing actual work.

Detecting automated mouse click patterns algorithmically (such as fixed-interval periodicity or spatial clustering) requires analyzing:
1. Inter-click temporal intervals ($\Delta t$).
2. Relative screen spatial positions $(X, Y)$ of click events over a sliding window.

## Problem
Relying solely on aggregate click counts (`_mouseCount++`) makes it mathematically impossible to distinguish genuine human input from automated bot scripts. An employee running a script that clicks every 15 seconds will register as continuously active with high activity percentage, undermining the integrity of the telemetry platform.

## Current Design
In the current implementation (`ADR-002`):
- Native hooks (`CGEventTap` on macOS, `WH_MOUSE_LL` on Windows) increment an atomic integer `_mouseCount++`.
- The native event payload containing coordinates (`CGPoint`, `MSLLHOOKSTRUCT.pt`) is discarded immediately within the native callback.
- No temporal spacing between individual clicks is recorded within the sampling cycle.

## Why It Is Insufficient
Counting clicks without timing intervals or spatial variance cannot detect:
1. **Periodic Auto-Clickers:** Scripts triggering clicks at exact, unvarying intervals (e.g., exactly every 30.00 seconds).
2. **Fixed-Area Macros:** Scripts clicking randomly or repetitively inside a tiny fixed rectangle on screen to keep the OS from entering idle state.
3. **Cyclic Multi-Zone Bots:** Scripts cycling between a few fixed coordinate clusters.

## Proposed Change
We introduce a strictly constrained, privacy-preserving **Ephemeral In-Memory Ring Buffer** pattern:

1. **Short-Lived Ring Buffer in RAM:**
   - Platform providers (`MacOsInputActivityProvider`, `WindowsInputActivityProvider`) capture only `(TimestampMs, X, Y)` for mouse down events into a thread-safe ring buffer capped at 200 samples (`MouseClickSample`).
   - The buffer resides exclusively in volatile memory (RAM).
   - Oldest samples are dropped automatically if capacity is exceeded.

2. **Immediate Purge upon Extraction (`Drain`):**
   - At each monitoring tick, `IInputActivityProvider.DrainMouseSamples()` drains and empties the ring buffer atomically.
   - The samples are handed to `IMouseBotDetector.Analyze(samples)`.

3. **Zero Raw Coordinate Persistence & Zero Exfiltration:**
   - Raw coordinates are **NEVER** persisted to SQLite, **NEVER** written to log files, and **NEVER** serialized into network payloads (`ActivityBatch`).
   - `MouseClickSample` is a `readonly record struct` without a custom `ToString()` to prevent accidental string logging.
   - The detector outputs only a binary flag: `bool IsSuspicious` and timestamp.
   - Once analysis finishes, coordinate objects are released for immediate garbage collection.

4. **Algorithmic Secrecy:**
   - The backend and frontend receive only `{ "hasSuspiciousMouseActivity": true }`.
   - The detection logic runs entirely client-side, withholding internal scores, thresholds, and heuristic breakdowns from logs to resist reverse-engineering.

5. **Configurable & Remotely Deactivatable:**
   - Controlled via `TrackingOptions.MouseBotDetectionEnabled` so it can be disabled via backend policy if required by local labor regulations.

## Alternatives Considered
1. **Pure Rate-Based / Frequency Heuristic (Count only):**
   - *Rejected*: Human gaming or fast typing/clicking produces burst counts indistinguishable from bot bursts. Yields unacceptably high false-positive and false-negative rates.
2. **Continuous Full Cursor Trajectory Logging to SQLite/Backend:**
   - *Rejected*: Severe violation of employee privacy, high CPU/RAM overhead, massive storage consumption, and violation of project GDPR boundaries.
3. **Client-Side Deep Learning / ML Model:**
   - *Rejected*: Incurs heavy native dependencies (ONNX Runtime / Torch), increases agent binary size, and consumes excessive CPU on employee laptops. Statistical heuristics (Coefficient of Variation, Bounding Box, Grid Clustering) suffice for 99% of commercial auto-clickers.

## Consequences
### Positive
- Enables reliable detection of automated clickers without storing or exfiltrating private trajectory data.
- Negligible resource footprint: 200 samples $\times$ 16 bytes $\approx$ 3.2 KB RAM overhead.
- Strictly adheres to Privacy-by-Design: raw coordinates are destroyed within milliseconds of collection.
- Cross-platform parity across macOS and Windows.

### Negative / Trade-offs
- Memory dump forensics could theoretically expose up to the last 200 click coordinates prior to garbage collection.
- Legitimate repetitive workflows (e.g., intensive data entry in spreadsheet cells, CAD tools) may trigger false positives if thresholds are overly strict; requires application whitelisting in future iterations.
- Hardware-based "mouse wigglers" (physical USB dongles simulating mouse movement) cannot be detected by coordinate heuristics alone.

## Migration Impact
- **Platform Abstractions**: Add `IReadOnlyList<MouseClickSample> DrainMouseSamples()` to `IInputActivityProvider`.
- **Platform Implementations**: Update `MacOsInputActivityProvider` (using `CGEventGetLocation`) and `WindowsInputActivityProvider` (using `MSLLHOOKSTRUCT.pt`) to populate the ring buffer. `LinuxInputActivityProvider` returns an empty array.
- **Core Layer**: Add `MouseClickSample`, `BotDetectionResult`, `IMouseBotDetector`, and extend `ActivityEventType.SuspiciousActivityDetected`.
- **Application Layer**: Implement `MouseBotDetector`, update `ActivityCollector`, `ActivityAccumulator`, and `TrackingOptions`.
- **Zero Breaking Schema Changes**: Backward compatible with existing session and device stores.