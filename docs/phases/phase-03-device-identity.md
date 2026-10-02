# Phase 03: Device Identity and Local Desktop Configuration

## 1. Executive Summary

Phase 03 implements persistent device identity management and strongly-typed local desktop configuration. This ensures that every Desktop client installation is uniquely identifiable across application restarts and OS reboots without relying on fragile hardware hostnames, while establishing validated configuration models for runtime collection parameters.

---

## 2. Device Identity Lifecycle

```
[ Application First Start ]
           |
           v
  Does `device-id.txt` exist
      and contain a valid GUID?
       /                 \
     NO                   YES
     /                     \
Generate fresh GUID        Read & validate
Write atomically to        existing DeviceId
`device-id.txt`            from `device-id.txt`
     \                     /
      \                   /
       v                 v
   Use DeviceId for Session & Telemetry
```

### Why Hostname is NOT Machine Identity
1. **Mutable**: End users and network administrators routinely modify machine hostnames.
2. **Non-Unique**: Multiple computers across distinct local networks frequently share identical names (e.g. `macbook-pro`, `DESKTOP-12345`).
3. **Cloning Collisions**: Cloned disk images or virtual machine templates inherit identical hostnames, causing severe identity collisions in backend data pipelines.

---

## 3. Persistent Identity Storage & OS Differences

### Storage Location
The device identity is persisted in `device-id.txt` within the agent's local application data directory (`FileDeviceIdentityStore.GetDefaultIdentityDirectory()`):
- **Windows**: `%LOCALAPPDATA%\RemoteWork\Agent\device-id.txt`
- **macOS**: `~/Library/Application Support/RemoteWork/Agent/device-id.txt` (or `~/.local/share/RemoteWork/Agent/device-id.txt`)
- **Linux**: `~/.local/share/RemoteWork/Agent/device-id.txt`

### Platform-Specific Provider Implementations
- `MacOsDeviceInfoProvider`: Reports `OperatingSystem = "macOS"`, system version, hostname, and persistent `DeviceId`.
- `WindowsDeviceInfoProvider`: Reports `OperatingSystem = "Windows"`, system version, hostname, and persistent `DeviceId`.
- `LinuxDeviceInfoProvider`: Reports `OperatingSystem = "Linux"`, system version, hostname, and persistent `DeviceId`.

---

## 4. Configuration Architecture & Separation of Concerns

The architecture strictly separates three configuration concepts:

1. **Static Application Configuration (`DesktopConfiguration`)**:
   - Loaded from `appsettings.json`, environment variables, or CLI arguments at host startup.
   - Strongly-typed properties: `ApplicationVersion`, `BackendBaseUrl`, `LocalStoragePath`, `HeartbeatIntervalSeconds`, `ActivitySamplingIntervalSeconds`, `ActivityBatchIntervalSeconds`, `Logging`, `ScreenshotDefaults`.
   - Validated via `DesktopConfigurationValidator` implementing `IValidateOptions<DesktopConfiguration>`.
2. **Persistent Local Device Identity (`IDeviceIdentityStore`)**:
   - Manages non-volatile installation identity independently of application configuration files.
3. **Server-Managed Monitoring Policy (`MonitoringPolicy`)**:
   - Domain policy fetched or updated dynamically from the backend server at runtime.

---

## 5. Security & Privacy Considerations

- **Atomic File Operations**: `FileDeviceIdentityStore` uses temporary file staging and atomic replacement (`File.Move`) to prevent identity file corruption on unexpected process shutdown or power failure.
- **Validation**: If `device-id.txt` is corrupted or empty, `FileDeviceIdentityStore` safely generates a new valid GUID without crashing.
- **Privacy Enforcement**: `DeviceId` contains no PII, hardware MAC addresses, serial numbers, or sensitive telemetry.

---

## 6. Migration Notes from Previous `DeviceIdentityStore`

- The `IDeviceIdentityStore` interface contract (`GetOrCreateDeviceId()`) was preserved to maintain 100% backward compatibility with `DeviceCollector` and session services.
- `FileDeviceIdentityStore` added GUID validation, atomic write staging, and cross-platform folder detection.
- Existing installations with valid persisted GUIDs in `device-id.txt` will continue to read their established `DeviceId` seamlessly without identity churn.

---

## 7. Verification Results

- **Build**: `dotnet build RemoteWork.Desktop.sln` succeeded with 0 warnings and 0 errors.
- **Tests**: `dotnet test RemoteWork.Desktop.sln` passed all 61 Unit Tests and 9 Integration Tests.
- **Tested Scenarios**:
  - First-run `DeviceId` creation & format validation.
  - Repeated loads returning identical `DeviceId`.
  - Corrupted file recovery.
  - Configuration defaults and JSON serialization.
  - `DesktopConfigurationValidator` validation of invalid URLs, sampling rates, and quality bounds.
  - Platform device info generation for macOS and Windows.

---

## 8. Definition of Done Compliance

- [x] Persistent DeviceId
- [x] macOS works
- [x] Windows works
- [x] Config works
- [x] Tests pass
- [x] Docs updated (`docs/phases/phase-03-device-identity.md`)
- [x] Git commit created
