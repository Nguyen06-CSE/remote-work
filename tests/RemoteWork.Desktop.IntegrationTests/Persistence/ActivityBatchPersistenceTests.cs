using RemoteWork.Desktop.Core.Models;
using ActivityBatch = RemoteWork.Desktop.Core.Models.Activity.ActivityBatch;
using RemoteWork.Desktop.Persistence.Entities;
using RemoteWork.Desktop.Persistence.Repositories;

namespace RemoteWork.Desktop.IntegrationTests.Persistence;

/// <summary>
/// Tests for ActivityBatch persistence: save, query by session/device, TimeSpan roundtrip.
/// </summary>
public sealed class ActivityBatchPersistenceTests : IDisposable
{
    private readonly DbContextFixture _fixture = new();
    private const string DeviceId = "device-batch-001";
    private const string SessionId = "session-batch-001";

    public ActivityBatchPersistenceTests()
    {
        SeedDeviceAndSession().GetAwaiter().GetResult();
    }

    private async Task SeedDeviceAndSession()
    {
        _fixture.Context.Devices.Add(new DeviceEntity
        {
            DeviceId = DeviceId,
            Hostname = "batch-host",
            OperatingSystem = "Windows",
            OsVersion = "11",
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

    private static ActivityBatch MakeBatch(string batchId, string sessionId = SessionId, string deviceId = DeviceId)
        => new()
        {
            BatchId = batchId,
            DeviceId = deviceId,
            SessionId = sessionId,
            StartedAt = DateTimeOffset.UtcNow.AddMinutes(-1),
            EndedAt = DateTimeOffset.UtcNow,
            KeyboardCount = 42,
            MouseCount = 17,
            ActiveDuration = TimeSpan.FromSeconds(45),
            IdleDuration = TimeSpan.FromSeconds(15),
            HasSuspiciousMouseActivity = false
        };

    [Fact]
    public async Task SaveAsync_Should_Persist_ActivityBatch()
    {
        var repo = new ActivityBatchRepository(_fixture.Context);
        var batch = MakeBatch(Guid.NewGuid().ToString());

        await repo.SaveAsync(batch);

        var stored = await _fixture.Context.ActivityBatches.FindAsync(batch.BatchId);
        Assert.NotNull(stored);
        Assert.Equal(batch.BatchId, stored.BatchId);
        Assert.Equal(42, stored.KeyboardCount);
        Assert.Equal(17, stored.MouseCount);
    }

    [Fact]
    public async Task GetBySessionIdAsync_Should_Return_Batches_For_Session()
    {
        var repo = new ActivityBatchRepository(_fixture.Context);
        await repo.SaveAsync(MakeBatch(Guid.NewGuid().ToString()));
        await repo.SaveAsync(MakeBatch(Guid.NewGuid().ToString()));

        var batches = await repo.GetBySessionIdAsync(SessionId);

        Assert.Equal(2, batches.Count);
    }

    [Fact]
    public async Task GetByDeviceIdAsync_Should_Return_Batches_For_Device()
    {
        var repo = new ActivityBatchRepository(_fixture.Context);
        await repo.SaveAsync(MakeBatch(Guid.NewGuid().ToString()));

        var batches = await repo.GetByDeviceIdAsync(DeviceId);

        Assert.NotEmpty(batches);
        Assert.All(batches, b => Assert.Equal(DeviceId, b.DeviceId));
    }

    [Fact]
    public async Task GetBySessionIdAsync_Should_Return_Empty_For_Unknown_Session()
    {
        var repo = new ActivityBatchRepository(_fixture.Context);

        var batches = await repo.GetBySessionIdAsync("nonexistent-session");

        Assert.Empty(batches);
    }

    [Fact]
    public async Task TimeSpan_Should_Roundtrip_Correctly_Via_Ticks()
    {
        var repo = new ActivityBatchRepository(_fixture.Context);
        var activeDuration = TimeSpan.FromMinutes(3).Add(TimeSpan.FromSeconds(14)).Add(TimeSpan.FromMilliseconds(567));
        var idleDuration = TimeSpan.FromSeconds(45).Add(TimeSpan.FromMilliseconds(123));

        var batch = new ActivityBatch
        {
            BatchId = Guid.NewGuid().ToString(),
            DeviceId = DeviceId,
            SessionId = SessionId,
            StartedAt = DateTimeOffset.UtcNow.AddMinutes(-5),
            EndedAt = DateTimeOffset.UtcNow,
            ActiveDuration = activeDuration,
            IdleDuration = idleDuration
        };
        await repo.SaveAsync(batch);

        var loaded = await repo.GetBySessionIdAsync(SessionId);
        var found = loaded.First(b => b.BatchId == batch.BatchId);

        Assert.Equal(activeDuration, found.ActiveDuration);
        Assert.Equal(idleDuration, found.IdleDuration);
    }

    [Fact]
    public async Task SuspiciousMouseActivity_Flag_Should_Persist()
    {
        var repo = new ActivityBatchRepository(_fixture.Context);
        var batchId = Guid.NewGuid().ToString();
        var batch = new ActivityBatch
        {
            BatchId = batchId,
            DeviceId = DeviceId,
            SessionId = SessionId,
            StartedAt = DateTimeOffset.UtcNow.AddMinutes(-1),
            EndedAt = DateTimeOffset.UtcNow,
            KeyboardCount = 42,
            MouseCount = 17,
            ActiveDuration = TimeSpan.FromSeconds(45),
            IdleDuration = TimeSpan.FromSeconds(15),
            HasSuspiciousMouseActivity = true
        };
        await repo.SaveAsync(batch);

        var loaded = await repo.GetBySessionIdAsync(SessionId);
        var found = loaded.First(b => b.BatchId == batch.BatchId);

        Assert.True(found.HasSuspiciousMouseActivity);
    }

    [Fact]
    public async Task Batches_Should_Be_Ordered_By_StartedAt_When_Queried_By_Session()
    {
        var repo = new ActivityBatchRepository(_fixture.Context);
        var t = DateTimeOffset.UtcNow;

        await repo.SaveAsync(new ActivityBatch { BatchId = "b3", DeviceId = DeviceId, SessionId = SessionId, StartedAt = t.AddMinutes(2), EndedAt = t.AddMinutes(3) });
        await repo.SaveAsync(new ActivityBatch { BatchId = "b1", DeviceId = DeviceId, SessionId = SessionId, StartedAt = t, EndedAt = t.AddMinutes(1) });
        await repo.SaveAsync(new ActivityBatch { BatchId = "b2", DeviceId = DeviceId, SessionId = SessionId, StartedAt = t.AddMinutes(1), EndedAt = t.AddMinutes(2) });

        var batches = await repo.GetBySessionIdAsync(SessionId);
        var ids = batches.Select(b => b.BatchId).ToList();

        Assert.Equal(["b1", "b2", "b3"], ids);
    }

    public void Dispose() => _fixture.Dispose();
}
