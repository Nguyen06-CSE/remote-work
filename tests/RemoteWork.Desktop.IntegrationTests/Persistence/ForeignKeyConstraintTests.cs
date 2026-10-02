using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using RemoteWork.Desktop.Application.Sync;
using RemoteWork.Desktop.Core.Enums;
using RemoteWork.Desktop.Core.Models;
using RemoteWork.Desktop.Persistence.Entities;
using RemoteWork.Desktop.Persistence.Repositories;
using ActivityBatch = RemoteWork.Desktop.Core.Models.Activity.ActivityBatch;

namespace RemoteWork.Desktop.IntegrationTests.Persistence;

/// <summary>
/// Verifies foreign key constraint enforcement in SQLite for the pipeline:
/// Device -> Session -> ActivityBatch -> SyncQueue.
/// Proves that foreign keys are strictly enforced (Error 19) and that the startup
/// persistence of Device and Session allows ActivityBatch inserts to succeed cleanly.
/// </summary>
public sealed class ForeignKeyConstraintTests : IDisposable
{
    private readonly DbContextFixture _fixture = new();

    [Fact]
    public async Task Device_Session_ActivityBatch_Insertion_Succeeds_When_ForeignKeys_Exist()
    {
        var deviceRepo = new DeviceRepository(_fixture.Context);
        var sessionRepo = new SessionRepository(_fixture.Context);
        var batchRepo = new ActivityBatchRepository(_fixture.Context);

        var device = new Device
        {
            DeviceId = "fk-test-device-1",
            Hostname = "host-1",
            OperatingSystem = "macOS",
            OsVersion = "14.4",
            AgentVersion = "1.0.0"
        };
        await deviceRepo.UpsertAsync(device);

        var session = new SessionInfo
        {
            SessionId = "fk-test-session-1",
            DeviceId = device.DeviceId,
            StartedAt = DateTimeOffset.UtcNow
        };
        session.MarkActive();
        await sessionRepo.SaveAsync(session);

        var batch = new ActivityBatch
        {
            BatchId = "fk-test-batch-1",
            DeviceId = device.DeviceId,
            SessionId = session.SessionId,
            StartedAt = DateTimeOffset.UtcNow.AddMinutes(-1),
            EndedAt = DateTimeOffset.UtcNow,
            KeyboardCount = 10,
            MouseCount = 5,
            ActiveDuration = TimeSpan.FromSeconds(30),
            IdleDuration = TimeSpan.FromSeconds(30),
            HasSuspiciousMouseActivity = false
        };

        // Act & Assert - must not throw foreign key constraint violation
        await batchRepo.SaveAsync(batch);

        var savedBatch = await _fixture.Context.ActivityBatches.FindAsync(batch.BatchId);
        Assert.NotNull(savedBatch);
        Assert.Equal(session.SessionId, savedBatch.SessionId);
        Assert.Equal(device.DeviceId, savedBatch.DeviceId);
    }

    [Fact]
    public async Task ActivityBatch_Insertion_Throws_SqliteException_When_Session_Does_Not_Exist()
    {
        var deviceRepo = new DeviceRepository(_fixture.Context);
        var batchRepo = new ActivityBatchRepository(_fixture.Context);

        // Only persist Device, but NOT Session
        var device = new Device
        {
            DeviceId = "fk-test-device-orphan",
            Hostname = "orphan-host",
            OperatingSystem = "macOS",
            OsVersion = "14.4",
            AgentVersion = "1.0.0"
        };
        await deviceRepo.UpsertAsync(device);

        var batch = new ActivityBatch
        {
            BatchId = "fk-test-batch-orphan",
            DeviceId = device.DeviceId,
            SessionId = "non-existent-session-id",
            StartedAt = DateTimeOffset.UtcNow.AddMinutes(-1),
            EndedAt = DateTimeOffset.UtcNow,
            KeyboardCount = 10,
            MouseCount = 5,
            ActiveDuration = TimeSpan.FromSeconds(30),
            IdleDuration = TimeSpan.FromSeconds(30),
            HasSuspiciousMouseActivity = false
        };

        // Act & Assert - MUST fail with SQLite Error 19: FOREIGN KEY constraint failed
        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => batchRepo.SaveAsync(batch));
        var sqliteEx = Assert.IsType<SqliteException>(ex.GetBaseException());
        Assert.Equal(19, sqliteEx.SqliteErrorCode);
    }

    [Fact]
    public async Task ActivityBatch_Insertion_Throws_SqliteException_When_Device_Does_Not_Exist()
    {
        var batchRepo = new ActivityBatchRepository(_fixture.Context);

        var batch = new ActivityBatch
        {
            BatchId = "fk-test-batch-nodevice",
            DeviceId = "non-existent-device-id",
            SessionId = "non-existent-session-id",
            StartedAt = DateTimeOffset.UtcNow.AddMinutes(-1),
            EndedAt = DateTimeOffset.UtcNow,
            KeyboardCount = 10,
            MouseCount = 5,
            ActiveDuration = TimeSpan.FromSeconds(30),
            IdleDuration = TimeSpan.FromSeconds(30),
            HasSuspiciousMouseActivity = false
        };

        // Act & Assert - MUST fail with SQLite Error 19: FOREIGN KEY constraint failed
        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => batchRepo.SaveAsync(batch));
        var sqliteEx = Assert.IsType<SqliteException>(ex.GetBaseException());
        Assert.Equal(19, sqliteEx.SqliteErrorCode);
    }

    [Fact]
    public async Task TrackingPersistenceCoordinator_With_Startup_Persisted_Device_And_Session_Succeeds()
    {
        var deviceRepo = new DeviceRepository(_fixture.Context);
        var sessionRepo = new SessionRepository(_fixture.Context);
        var batchRepo = new ActivityBatchRepository(_fixture.Context);
        var syncRepo = new SyncQueueRepository(_fixture.Context);

        // Simulate Worker Startup persistence
        var device = new Device
        {
            DeviceId = "coordinator-device-1",
            Hostname = "coord-host",
            OperatingSystem = "macOS",
            OsVersion = "15.0",
            AgentVersion = "1.0.0"
        };
        await deviceRepo.UpsertAsync(device);

        var session = new SessionInfo
        {
            SessionId = "coordinator-session-1",
            DeviceId = device.DeviceId,
            StartedAt = DateTimeOffset.UtcNow
        };
        session.MarkActive();
        await sessionRepo.SaveAsync(session);

        // Coordinate batch persistence and sync queuing
        var coordinator = new TrackingPersistenceCoordinator(
            batchRepo,
            syncRepo,
            NullLogger<TrackingPersistenceCoordinator>.Instance);

        var batch = new ActivityBatch
        {
            BatchId = "coordinator-batch-1",
            DeviceId = device.DeviceId,
            SessionId = session.SessionId,
            StartedAt = DateTimeOffset.UtcNow.AddMinutes(-1),
            EndedAt = DateTimeOffset.UtcNow,
            KeyboardCount = 15,
            MouseCount = 25,
            ActiveDuration = TimeSpan.FromSeconds(40),
            IdleDuration = TimeSpan.FromSeconds(20),
            HasSuspiciousMouseActivity = false
        };

        var enqueued = await coordinator.PersistAndEnqueueBatchAsync(batch);
        Assert.True(enqueued);

        // Verify in SQLite
        var savedBatch = await _fixture.Context.ActivityBatches.FindAsync(batch.BatchId);
        Assert.NotNull(savedBatch);

        var queueItems = await syncRepo.GetPendingAsync();
        var item = Assert.Single(queueItems);
        Assert.Equal("ActivityBatch", item.EntityType);
        Assert.Equal(batch.BatchId, item.EntityId);
        Assert.Equal(SyncStatus.Pending, item.Status);
    }

    [Fact]
    public async Task Session_Shutdown_Update_Persists_EndedAt_And_Status_Across_Restart()
    {
        var deviceRepo = new DeviceRepository(_fixture.Context);
        var sessionRepo = new SessionRepository(_fixture.Context);

        var device = new Device
        {
            DeviceId = "shutdown-dev-1",
            Hostname = "shutdown-host",
            OperatingSystem = "macOS",
            OsVersion = "15.0",
            AgentVersion = "1.0.0"
        };
        await deviceRepo.UpsertAsync(device);

        var session = new SessionInfo
        {
            SessionId = "shutdown-sess-1",
            DeviceId = device.DeviceId,
            StartedAt = DateTimeOffset.UtcNow.AddHours(-1)
        };
        session.MarkActive();
        await sessionRepo.SaveAsync(session);

        // Simulate shutdown: MarkEnded and UpdateAsync
        var endedAt = DateTimeOffset.UtcNow;
        session.MarkEnded(endedAt);
        await sessionRepo.UpdateAsync(session);

        // Reopen DB context to simulate application restart
        await using var ctx2 = _fixture.ReopenContext();
        var sessionRepo2 = new SessionRepository(ctx2);
        var loaded = await sessionRepo2.GetBySessionIdAsync(session.SessionId);

        Assert.NotNull(loaded);
        Assert.Equal(SessionStatus.Ended, loaded.Status);
        Assert.NotNull(loaded.EndedAt);
        Assert.NotNull(loaded.Duration);
        Assert.True(loaded.Duration > TimeSpan.Zero);
    }

    public void Dispose() => _fixture.Dispose();
}
