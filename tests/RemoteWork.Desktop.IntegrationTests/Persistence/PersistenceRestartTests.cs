using Microsoft.EntityFrameworkCore;
using RemoteWork.Desktop.Core.Models;
using RemoteWork.Desktop.Persistence.Entities;
using RemoteWork.Desktop.Persistence.Repositories;

namespace RemoteWork.Desktop.IntegrationTests.Persistence;

/// <summary>
/// Tests that data survives a DbContext lifecycle boundary (simulating application restart),
/// and that primary key constraints are enforced.
/// </summary>
public sealed class PersistenceRestartTests : IDisposable
{
    private readonly DbContextFixture _fixture = new();

    [Fact]
    public async Task Device_Data_Should_Persist_After_Context_Restart()
    {
        var repo1 = new DeviceRepository(_fixture.Context);
        var device = new Device
        {
            DeviceId = "restart-device-001",
            Hostname = "restart-host",
            OperatingSystem = "macOS",
            OsVersion = "14.0",
            AgentVersion = "1.0.0"
        };
        await repo1.UpsertAsync(device);

        // Simulate restart by reopening the same database file
        await using var ctx2 = _fixture.ReopenContext();
        var repo2 = new DeviceRepository(ctx2);
        var loaded = await repo2.GetByDeviceIdAsync("restart-device-001");

        Assert.NotNull(loaded);
        Assert.Equal("restart-host", loaded.Hostname);
        Assert.Equal("macOS", loaded.OperatingSystem);
    }

    [Fact]
    public async Task Session_Data_Should_Persist_After_Context_Restart()
    {
        _fixture.Context.Devices.Add(new DeviceEntity
        {
            DeviceId = "restart-device-002",
            Hostname = "h",
            OperatingSystem = "macOS",
            OsVersion = "14.0",
            AgentVersion = "1.0",
            RegisteredAt = DateTimeOffset.UtcNow,
            LastSeenAt = DateTimeOffset.UtcNow
        });
        await _fixture.Context.SaveChangesAsync();

        var sessionRepo1 = new SessionRepository(_fixture.Context);
        var session = new SessionInfo
        {
            SessionId = "restart-session-001",
            DeviceId = "restart-device-002",
            StartedAt = DateTimeOffset.UtcNow
        };
        session.MarkActive();
        await sessionRepo1.SaveAsync(session);

        // Simulate restart
        await using var ctx2 = _fixture.ReopenContext();
        var sessionRepo2 = new SessionRepository(ctx2);
        var loaded = await sessionRepo2.GetBySessionIdAsync("restart-session-001");

        Assert.NotNull(loaded);
        Assert.Equal("restart-device-002", loaded.DeviceId);
    }

    [Fact]
    public async Task Duplicate_Session_Id_Should_Throw_On_Constraint_Violation()
    {
        _fixture.Context.Devices.Add(new DeviceEntity
        {
            DeviceId = "dup-device-001",
            Hostname = "h",
            OperatingSystem = "macOS",
            OsVersion = "14",
            AgentVersion = "1.0",
            RegisteredAt = DateTimeOffset.UtcNow,
            LastSeenAt = DateTimeOffset.UtcNow
        });
        await _fixture.Context.SaveChangesAsync();

        var repo = new SessionRepository(_fixture.Context);
        var session = new SessionInfo
        {
            SessionId = "dup-session-001",
            DeviceId = "dup-device-001",
            StartedAt = DateTimeOffset.UtcNow
        };
        session.MarkActive();
        await repo.SaveAsync(session);

        // Try inserting a duplicate session id via a fresh context
        await using var ctx2 = _fixture.ReopenContext();
        ctx2.Sessions.Add(new SessionEntity
        {
            SessionId = "dup-session-001",
            DeviceId = "dup-device-001",
            StartedAt = DateTimeOffset.UtcNow,
            Status = "Active"
        });

        await Assert.ThrowsAsync<DbUpdateException>(
            async () => await ctx2.SaveChangesAsync());
    }

    [Fact]
    public async Task Duplicate_Device_Id_Should_Throw_On_Constraint_Violation()
    {
        _fixture.Context.Devices.Add(new DeviceEntity
        {
            DeviceId = "dup-dev-unique",
            Hostname = "h",
            OperatingSystem = "macOS",
            OsVersion = "14",
            AgentVersion = "1.0",
            RegisteredAt = DateTimeOffset.UtcNow,
            LastSeenAt = DateTimeOffset.UtcNow
        });
        await _fixture.Context.SaveChangesAsync();

        await using var ctx2 = _fixture.ReopenContext();
        ctx2.Devices.Add(new DeviceEntity
        {
            DeviceId = "dup-dev-unique",
            Hostname = "other",
            OperatingSystem = "Windows",
            OsVersion = "11",
            AgentVersion = "1.0",
            RegisteredAt = DateTimeOffset.UtcNow,
            LastSeenAt = DateTimeOffset.UtcNow
        });

        await Assert.ThrowsAsync<DbUpdateException>(
            async () => await ctx2.SaveChangesAsync());
    }

    [Fact]
    public async Task SyncQueue_Items_Persist_After_Context_Restart()
    {
        var repo1 = new SyncQueueRepository(_fixture.Context);
        var item = new SyncQueueItem
        {
            ItemId = "restart-sync-001",
            EntityType = "Session",
            EntityId = "some-session",
            PayloadJson = """{"sessionId":"some-session"}""",
            CreatedAt = DateTimeOffset.UtcNow
        };
        await repo1.EnqueueAsync(item);

        await using var ctx2 = _fixture.ReopenContext();
        var repo2 = new SyncQueueRepository(ctx2);
        var pending = await repo2.GetPendingAsync();

        Assert.Single(pending);
        Assert.Equal("restart-sync-001", pending[0].ItemId);
    }

    public void Dispose() => _fixture.Dispose();
}
