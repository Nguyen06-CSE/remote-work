# ADR-002: Native Platform Activity Providers Implementation Strategy

## Status
Accepted

## Context
The RemoteWork Desktop Agent requires access to OS-level telemetry on Windows and macOS:
1. User idle time (since last hardware input event).
2. Active foreground application and window title.
3. Aggregate keyboard event counts and mouse event counts (without keylogging or capturing raw user input data).
4. System privacy permission detection (Accessibility, Screen Recording).
5. Screen capture capability PoC.

To maintain cross-platform buildability, security compliance, and privacy safety, we evaluated multiple implementation techniques for native interaction in .NET 10.

## Decision
We adopt a unified P/Invoke & Native Interop architecture with strict privacy constraints:
1. **Windows**:
   - Idle Time: `user32.dll` `GetLastInputInfo()` + `kernel32.dll` `GetTickCount64()`.
   - Active Application: `user32.dll` `GetForegroundWindow()` + `GetWindowThreadProcessId()` + `GetWindowText()` + `Process.GetProcessById()`.
   - Input Activity Hook: `SetWindowsHookEx` with `WH_KEYBOARD_LL` (13) and `WH_MOUSE_LL` (14) with a dedicated background Win32 message pump thread (`GetMessage`).
   - Permissions: Inherently granted / Not required for standard user desktop session telemetry on Windows.
   - Screenshot: `Graphics.CopyFromScreen` via GDI+ (`user32.dll` / `gdi32.dll` device contexts).

2. **macOS**:
   - Idle Time: `IOKit.framework` matching `IOHIDSystem` via `IOServiceGetMatchingService` and querying `HIDIdleTime` property.
   - Active Application: Objective-C runtime P/Invoke (`libobjc.dylib` via `objc_getClass` / `objc_msgSend`) accessing `NSWorkspace.sharedWorkspace.frontmostApplication`.
   - Input Activity Hook: `ApplicationServices.framework` `CGEventTapCreate` (`kCGSessionEventTap`, `kCGEventTapOptionListenOnly`) attached to a background `CFRunLoop`.
   - Permissions: `ApplicationServices` `AXIsProcessTrusted()` for Accessibility; `CoreGraphics` `CGPreflightScreenCaptureAccess()` for Screen Recording.
   - Screenshot: `/usr/sbin/screencapture -x` (native OS utility wrapper) ensuring proper color space management and hardware acceleration.

3. **Privacy & Security Constraints**:
   - Key codes, character sequences, passwords, clipboard contents, and mouse cursor trajectories are **NEVER** captured or retained.
   - Native hooks increment only volatile numeric counters (`_keyboardCount++`, `_mouseCount++`) and immediately discard all event payloads.
   - Graceful degradation: If permissions are denied or native APIs fail, providers return safe default/null values without crashing the host.

## Alternatives Considered
- **Third-party native hook NuGet packages (e.g., SharpHook, H.Hooks)**:
  - *Rejected*: Incur external binary dependencies, heavier memory footprint, and unpredictable platform compatibility across newer macOS/Windows versions.
- **AppleScript execution (`osascript`) for active app / idle detection**:
  - *Rejected*: High process creation overhead (~100-200ms per call), fragile text parsing, and prone to user prompts for Apple Events permissions.
- **Continuous screen polling / Optical Character Recognition (OCR)**:
  - *Rejected*: Massive CPU/battery drain, severe privacy concerns, and out of scope for the agent.

## Consequences
### Positive
- Zero external native dependencies: Entirely relies on OS-bundled frameworks (`IOKit`, `CoreGraphics`, `ApplicationServices`, `user32.dll`, `gdi32.dll`).
- Ultra-low latency and minimal resource utilization (< 0.1% CPU when idle).
- Privacy-by-design compliance: Impossible to recover user keystrokes from memory dumps or logs.
- Clean isolation: Platform-specific logic is entirely contained within `RemoteWork.Desktop.Platform.Windows` and `RemoteWork.Desktop.Platform.MacOS`.

### Negative / Trade-offs
- macOS requires explicit user approval in **System Settings → Privacy & Security** for Accessibility and Screen Recording permissions.
- Win32 hooks require an active message pump thread on Windows.
