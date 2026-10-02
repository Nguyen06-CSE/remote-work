using RemoteWork.Desktop.Core.Enums;
using RemoteWork.Desktop.Core.Models;
using RemoteWork.Desktop.Persistence.Entities;
using RemoteWork.Desktop.Persistence.Repositories;

namespace RemoteWork.Desktop.IntegrationTests.Persistence;

/// <summary>
/// Tests for session persistence: save, query by id, query by device, update status.
/// </summary>
public sealed class SessionPersistenceTests : IDisposable
{
    private readonly DbContextFixture _fixture = new();

    private static DeviceEntity MakeDeviceEntity(string deviceId = "device-001") => new()
    {
        DeviceId = deviceId,
        Hostname = "test-host",
        OperatingSystem = "macOS",
        OsVersion = "14.0",
        AgentVersion = "1.0.0",
        RegisteredAt = DateTimeOffset.UtcNow,
        LastSeenAt = DateTimeOffset.UtcNow
    };

    [Fact]
    public async Task SaveAsync_Should_Persist_Session()
    {
        // Arrange — device must exist first (FK constraint)
        _fixture.Context.Devices.Add(MakeDeviceEntity());
        await _fixture.Context.SaveChangesAsync();

        var repo = new SessionRepository(_fixture.Context);
        var session = new SessionInfo
        {
            SessionId = Guid.NewGuid().ToString(),
            DeviceId = "device-001",
            StartedAt = DateTimeOffset.UtcNow,
        };
        session.MarkActive();

        // Act
        await repo.SaveAsync(session);

        // Assert
        var stored = await _fixture.Context.Sessions.FindAsync(session.SessionId);
        Assert.NotNull(stored);
        Assert.Equal(session.SessionId, stored.SessionId);
        Assert.Equal("device-001", stored.DeviceId);
        Assert.Equal("Active", stored.Status);
    }

    [Fact]
    public async Task GetBySessionIdAsync_Should_Return_Correct_Session()
    {
        _fixture.Context.Devices.Add(MakeDeviceEntity());
        await _fixture.Context.SaveChangesAsync();

        var repo = new SessionRepository(_fixture.Context);
        var id = Guid.NewGuid().ToString();
        var session = new SessionInfo { SessionId = id, DeviceId = "device-001", StartedAt = DateTimeOffset.UtcNow };
        session.MarkActive();
        await repo.SaveAsync(session);

        var loaded = await repo.GetBySessionIdAsync(id);

        Assert.NotNull(loaded);
        Assert.Equal(id, loaded.SessionId);
        Assert.Equal("device-001", loaded.DeviceId);
        Assert.Equal(SessionStatus.Active, loaded.Status);
    }

    [Fact]
    public async Task GetBySessionIdAsync_Should_Return_Null_For_Unknown_Id()
    {
        var repo = new SessionRepository(_fixture.Context);

        var result = await repo.GetBySessionIdAsync("nonexistent-id");

        Assert.Null(result);
    }

    [Fact]
    public async Task GetByDeviceIdAsync_Should_Return_All_Sessions_For_Device()
    {
        _fixture.Context.Devices.Add(MakeDeviceEntity());
        await _fixture.Context.SaveChangesAsync();

        var repo = new SessionRepository(_fixture.Context);
        for (var i = 0; i < 3; i++)
        {
            var s = new SessionInfo { SessionId = Guid.NewGuid().ToString(), DeviceId = "device-001", StartedAt = DateTimeOffset.UtcNow.AddMinutes(-i) };
            s.MarkActive();
            await repo.SaveAsync(s);
        }

        var sessions = await repo.GetByDeviceIdAsync("device-001");

        Assert.Equal(3, sessions.Count);
        // Should be ordered newest first
        Assert.True(sessions[0].StartedAt >= sessions[1].StartedAt);
    }

    [Fact]
    public async Task GetByDeviceIdAsync_Should_Return_Empty_For_Unknown_Device()
    {
        var repo = new SessionRepository(_fixture.Context);

        var sessions = await repo.GetByDeviceIdAsync("ghost-device");

        Assert.Empty(sessions);
    }

    [Fact]
    public async Task UpdateAsync_Should_Persist_Status_Change()
    {
        _fixture.Context.Devices.Add(MakeDeviceEntity());
        await _fixture.Context.SaveChangesAsync();

        var repo = new SessionRepository(_fixture.Context);
        var session = new SessionInfo { SessionId = Guid.NewGuid().ToString(), DeviceId = "device-001", StartedAt = DateTimeOffset.UtcNow };
        session.MarkActive();
        await repo.SaveAsync(session);

        // Transition to Ended
        session.MarkEnding();
        session.MarkEnded();
        await repo.UpdateAsync(session);

        var updated = await repo.GetBySessionIdAsync(session.SessionId);
        Assert.NotNull(updated);
        Assert.Equal(SessionStatus.Ended, updated.Status);
        Assert.NotNull(updated.EndedAt);
    }

    [Fact]
    public async Task Session_Domain_State_Should_Survive_Roundtrip()
    {
        _fixture.Context.Devices.Add(MakeDeviceEntity());
        await _fixture.Context.SaveChangesAsync();

        var repo = new SessionRepository(_fixture.Context);
        var session = new SessionInfo { SessionId = Guid.NewGuid().ToString(), DeviceId = "device-001", StartedAt = DateTimeOffset.UtcNow };
        session.MarkActive();
        session.MarkEnding();
        var endTime = DateTimeOffset.UtcNow;
        session.MarkEnded(endTime);
        await repo.SaveAsync(session);

        var loaded = await repo.GetBySessionIdAsync(session.SessionId);

        Assert.NotNull(loaded);
        Assert.Equal(SessionStatus.Ended, loaded.Status);
        // EndedAt roundtrip — SQLite stores as text; verify within 1 second precision
        Assert.NotNull(loaded.EndedAt);
        Assert.True(Math.Abs((loaded.EndedAt!.Value - endTime).TotalSeconds) < 1.0);
    }

    public void Dispose() => _fixture.Dispose();
}
