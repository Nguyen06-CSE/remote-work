using Microsoft.Extensions.Logging.Abstractions;
using RemoteWork.Desktop.Application.Collectors;
using RemoteWork.Desktop.Application.Sync;
using RemoteWork.Desktop.Core.Models;
using RemoteWork.Desktop.IntegrationTests.Persistence;
using RemoteWork.Desktop.Persistence.Entities;
using RemoteWork.Desktop.Persistence.Repositories;
using RemoteWork.Desktop.Platform.Abstractions;
using Xunit;

namespace RemoteWork.Desktop.IntegrationTests.Sync;

public sealed class ApplicationActivitySyncIntegrationTests : IDisposable
{
    private readonly DbContextFixture _fixture = new();
    private const string DeviceId = "sync-device-app-01";
    private const string SessionId = "sync-session-app-01";

    public ApplicationActivitySyncIntegrationTests()
    {
        SeedDeviceAndSession().GetAwaiter().GetResult();
    }

    private async Task SeedDeviceAndSession()
    {
        _fixture.Context.Devices.Add(new DeviceEntity
        {
            DeviceId = DeviceId,
            Hostname = "test-host",
            OperatingSystem = "macOS",
            OsVersion = "15.0",
            AgentVersion = "1.0.0",
            RegisteredAt = DateTimeOffset.UtcNow,
            LastSeenAt = DateTimeOffset.UtcNow
        });
        _fixture.Context.Sessions.Add(new SessionEntity
        {
            SessionId = SessionId,
            DeviceId = DeviceId,
            StartedAt = DateTimeOffset.UtcNow.AddHours(-1),
            Status = "Active"
        });
        await _fixture.Context.SaveChangesAsync();
    }

    private sealed class TestAppProvider : IApplicationActivityProvider
    {
        public ActiveApplicationInfo? ActiveApp { get; set; }
        public ActiveApplicationInfo? GetActiveApplication() => ActiveApp;
    }

    [Fact]
    public async Task ApplicationTransition_Persists_To_SQLite_And_Enqueues_To_SyncQueue()
    {
        var provider = new TestAppProvider();
        var collector = new ApplicationActivityCollector(provider, NullLogger<ApplicationActivityCollector>.Instance);

        var appRepo = new ApplicationActivityRepository(_fixture.Context);
        var batchRepo = new ActivityBatchRepository(_fixture.Context);
        var syncRepo = new SyncQueueRepository(_fixture.Context);

        var coordinator = new TrackingPersistenceCoordinator(
            batchRepo,
            syncRepo,
            appRepo,
            NullLogger<TrackingPersistenceCoordinator>.Instance);

        var t0 = DateTimeOffset.UtcNow.AddMinutes(-10);
        var t1 = t0.AddMinutes(4); // 4 minutes of VS Code
        var t2 = t1.AddMinutes(6); // 6 minutes of Chrome

        // 1. VS Code starts at t0
        provider.ActiveApp = new ActiveApplicationInfo { ApplicationName = "Code", ProcessName = "Code", ProcessId = 100 };
        var sample1 = collector.Sample(DeviceId, SessionId, t0);
        Assert.Null(sample1);

        // 2. Switches to Chrome at t1
        provider.ActiveApp = new ActiveApplicationInfo { ApplicationName = "Google Chrome", ProcessName = "chrome", ProcessId = 200 };
        var codeActivity = collector.Sample(DeviceId, SessionId, t1);
        Assert.NotNull(codeActivity);
        Assert.Equal("Code", codeActivity.ApplicationName);
        Assert.Equal(TimeSpan.FromMinutes(4), codeActivity.Duration);

        // Persist Code activity
        await coordinator.PersistAndEnqueueApplicationActivityAsync(codeActivity);

        // 3. Flushes Chrome at t2 on shutdown
        var chromeActivity = collector.Flush(DeviceId, SessionId, t2);
        Assert.NotNull(chromeActivity);
        Assert.Equal("Google Chrome", chromeActivity.ApplicationName);
        Assert.Equal(TimeSpan.FromMinutes(6), chromeActivity.Duration);

        // Persist Chrome activity
        await coordinator.PersistAndEnqueueApplicationActivityAsync(chromeActivity);

        // 4. Verify in SQLite database
        var storedActivities = await appRepo.GetBySessionIdAsync(SessionId);
        Assert.Equal(2, storedActivities.Count);
        Assert.Equal("Code", storedActivities[0].ApplicationName);
        Assert.Equal(TimeSpan.FromMinutes(4), storedActivities[0].Duration);
        Assert.Equal("Google Chrome", storedActivities[1].ApplicationName);
        Assert.Equal(TimeSpan.FromMinutes(6), storedActivities[1].Duration);

        // 5. Verify in SyncQueue
        var pendingQueue = await syncRepo.GetPendingAsync();
        Assert.Equal(2, pendingQueue.Count);
        Assert.All(pendingQueue, item => Assert.Equal("ApplicationActivity", item.EntityType));
    }

    public void Dispose() => _fixture.Dispose();
}
