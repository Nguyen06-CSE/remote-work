```
RemoteWork/
│
├── src/
│   │
│   ├── RemoteWork.Desktop.Host/
│   │   └── Entry point / lifecycle / DI
│   │
│   ├── RemoteWork.Desktop.Core/
│   │   └── Domain / Models / Interfaces / Events
│   │
│   ├── RemoteWork.Desktop.Application/
│   │   └── Use cases / orchestration
│   │
│   ├── RemoteWork.Desktop.Infrastructure/
│   │   └── Configuration / Logging / HTTP / common infrastructure
│   │
│   ├── RemoteWork.Desktop.Persistence/
│   │   └── EF Core / SQLite / repositories / migrations
│   │
│   ├── RemoteWork.Desktop.Platform.Abstractions/
│   │   └── OS-independent contracts
│   │
│   ├── RemoteWork.Desktop.Platform.Windows/
│   │   └── Windows implementation
│   │
│   ├── RemoteWork.Desktop.Platform.MacOS/
│   │   └── macOS implementation
│   │
│   └── RemoteWork.Desktop.Platform.Linux/
│       └── Linux implementation sau
│
├── tests/
│   ├── RemoteWork.Desktop.UnitTests/
│   └── RemoteWork.Desktop.IntegrationTests/
│
├── docs/
│   ├── architecture/
│   ├── adr/
│   ├── phases/
│   ├── troubleshooting/
│   └── reference/
│       └── gauzy/
│
└── README.md
```


```
Directory structure:
└── nguyen06-cse-remote-work/
    ├── RemoteWork.Desktop.sln
    ├── RemoteWork.Desktop.slnx
    ├── docs/
    │   ├── native-capability-spike.md
    │   ├── adr/
    │   │   ├── ADR-001-cross-platform-desktop-architecture.md
    │   │   └── ADR-002-native-activity-providers.md
    │   ├── ai-development/
    │   │   └── master-agent-instruction.md
    │   ├── api/
    │   │   └── agent-api-contract.md
    │   ├── architecture/
    │   │   ├── DESKTOP_APP_PLATFORM_DECISION.md
    │   │   ├── project-boundaries.md
    │   │   └── target-desktop-architecture.md
    │   ├── monitoring/
    │   │   ├── monitoring-policy.md
    │   │   └── monitoring-scope.md
    │   ├── phases/
    │   │   └── phase-01-rearchitecture.md
    │   ├── reference/
    │   │   ├── desktop-app-architecture-roadmap.md
    │   │   └── gauzy/
    │   │       ├── gauzy-desktop-app-architecture.md
    │   │       └── gauzy-desktop-architecture-detail.md
    │   ├── spike-runs/
    │   │   └── phase-01-rearchitecture/
    │   │       ├── README.md
    │   │       └── run-2026-09-20-1430-macos/
    │   │           └── summary.md  
    │   └── troubleshooting/
    │       └── cross-platform-build.md
    ├── src/
    │   ├── RemoteWork.Desktop.Application/
    │   │   ├── ApplicationServiceCollectionExtensions.cs
    │   │   ├── RemoteWork.Desktop.Application.csproj
    │   │   ├── Collectors/
    │   │   │   ├── ActivityCollector.cs
    │   │   │   ├── DeviceCollector.cs
    │   │   │   ├── IdleActivityCollector.cs
    │   │   │   ├── KeyboardActivityCollector.cs
    │   │   │   ├── MouseActivityCollector.cs
    │   │   │   └── SessionCollector.cs
    │   │   ├── Monitoring/
    │   │   │   └── MonitoringService.cs
    │   │   └── Options/
    │   │       └── TrackingOptions.cs
    │   ├── RemoteWork.Desktop.Core/
    │   │   ├── RemoteWork.Desktop.Core.csproj
    │   │   ├── Enums/
    │   │   │   ├── ActivityEventType.cs
    │   │   │   ├── AgentStatus.cs
    │   │   │   └── SessionStatus.cs
    │   │   ├── Interfaces/
    │   │   │   ├── IActivityCollector.cs
    │   │   │   ├── IDeviceIdentityStore.cs
    │   │   │   ├── IIdleActivityCollector.cs
    │   │   │   ├── IKeyboardActivityCollector.cs
    │   │   │   ├── IMonitoringService.cs
    │   │   │   ├── IMouseActivityCollector.cs
    │   │   │   └── ISessionCollector.cs
    │   │   └── Models/
    │   │       ├── AgentRuntimeState.cs
    │   │       ├── DeviceInfo.cs
    │   │       ├── SessionInfo.cs
    │   │       └── Activity/
    │   │           ├── ActivityAccumulator.cs
    │   │           ├── ActivityBatch.cs
    │   │           ├── ActivityEvent.cs
    │   │           └── ActivityState.cs
    │   ├── RemoteWork.Desktop.Host/
    │   │   ├── appsettings.Development.json
    │   │   ├── appsettings.json
    │   │   ├── Program.cs
    │   │   ├── RemoteWork.Desktop.Host.csproj
    │   │   ├── Worker.cs
    │   │   └── Properties/
    │   │       └── launchSettings.json
    │   ├── RemoteWork.Desktop.Infrastructure/
    │   │   ├── InfrastructureServiceCollectionExtensions.cs
    │   │   ├── RemoteWork.Desktop.Infrastructure.csproj
    │   │   └── Configuration/
    │   │       └── AgentOptions.cs
    │   ├── RemoteWork.Desktop.Persistence/
    │   │   ├── FileDeviceIdentityStore.cs
    │   │   ├── PersistenceServiceCollectionExtensions.cs
    │   │   └── RemoteWork.Desktop.Persistence.csproj
    │   ├── RemoteWork.Desktop.Platform.Abstractions/
    │   │   ├── IActiveApplicationProvider.cs
    │   │   ├── IDeviceInfoProvider.cs
    │   │   ├── IIdleTimeProvider.cs
    │   │   ├── IInputActivityProvider.cs
    │   │   ├── IPlatformPermissionProvider.cs
    │   │   ├── IScreenshotProvider.cs
    │   │   └── RemoteWork.Desktop.Platform.Abstractions.csproj
    │   ├── RemoteWork.Desktop.Platform.Linux/
    │   │   ├── LinuxActiveApplicationProvider.cs
    │   │   ├── LinuxDeviceInfoProvider.cs
    │   │   ├── LinuxIdleTimeProvider.cs
    │   │   ├── LinuxInputActivityProvider.cs
    │   │   ├── LinuxPlatformPermissionProvider.cs
    │   │   ├── LinuxScreenshotProvider.cs
    │   │   ├── LinuxServiceCollectionExtensions.cs
    │   │   └── RemoteWork.Desktop.Platform.Linux.csproj
    │   ├── RemoteWork.Desktop.Platform.MacOS/
    │   │   ├── MacOsActiveApplicationProvider.cs
    │   │   ├── MacOsDeviceInfoProvider.cs
    │   │   ├── MacOsIdleTimeProvider.cs
    │   │   ├── MacOsInputActivityProvider.cs
    │   │   ├── MacOsPlatformPermissionProvider.cs
    │   │   ├── MacOsScreenshotProvider.cs
    │   │   ├── MacOsServiceCollectionExtensions.cs
    │   │   └── RemoteWork.Desktop.Platform.MacOS.csproj
    │   └── RemoteWork.Desktop.Platform.Windows/
    │       ├── RemoteWork.Desktop.Platform.Windows.csproj
    │       ├── WindowsActiveApplicationProvider.cs
    │       ├── WindowsDeviceInfoProvider.cs
    │       ├── WindowsIdleTimeProvider.cs
    │       ├── WindowsInputActivityProvider.cs
    │       ├── WindowsPlatformPermissionProvider.cs
    │       ├── WindowsScreenshotProvider.cs
    │       └── WindowsServiceCollectionExtensions.cs
    └── tests/
        ├── RemoteWork.Desktop.IntegrationTests/
        │   ├── RemoteWork.Desktop.IntegrationTests.csproj
        │   ├── Persistence/
        │   │   └── FileDeviceIdentityStoreTests.cs
        │   └── Platform/
        │       └── MacOsPlatformIntegrationTests.cs
        └── RemoteWork.Desktop.UnitTests/
            ├── RemoteWork.Desktop.UnitTests.csproj
            ├── Application/
            │   ├── ActivityAccumulatorTests.cs
            │   ├── ActivityCollectorTests.cs
            │   ├── IdleActivityCollectorTests.cs
            │   └── SessionCollectorTests.cs
            ├── Core/
            │   ├── AgentRuntimeStateTests.cs
            │   ├── DeviceInfoTests.cs
            │   └── SessionInfoTests.cs
            └── Platform/
                ├── LinuxPlatformProviderTests.cs
                ├── PlatformLifecycleContractTests.cs
                └── PlatformModelTests.cs
```