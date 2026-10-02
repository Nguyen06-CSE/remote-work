using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RemoteWork.Desktop.Application.Options;
using RemoteWork.Desktop.Application.Sync;
using RemoteWork.Desktop.Core.Enums;
using RemoteWork.Desktop.Core.Models;
using ActivityBatch = RemoteWork.Desktop.Core.Models.Activity.ActivityBatch;
using RemoteWork.Desktop.Infrastructure.Transport;
using RemoteWork.Desktop.IntegrationTests.Persistence;
using RemoteWork.Desktop.Persistence.Entities;
using RemoteWork.Desktop.Persistence.Repositories;
using Xunit;

namespace RemoteWork.Desktop.IntegrationTests.Sync;

public sealed class SyncEngineIntegrationTests : IDisposable
{
    private readonly DbContextFixture _fixture = new();
    private readonly InMemorySyncTransport _transport = new();
    private readonly SyncOptions _options = new()
    {
        SyncIntervalSeconds = 1,
        BatchSize = 10,
        InitialRetryDelaySeconds = 1,
        MaxRetryIntervalSeconds = 10,
        BackoffMultiplier = 2.0,
        MaxRetryAttempts = 3
    };

    private SyncEngine CreateEngine(SyncQueueRepository repository) => new(
        repository,
        _transport,
        Microsoft.Extensions.Options.Options.Create(_options),
        NullLogger<SyncEngine>.Instance);

    private static SyncQueueItem CreateItem(string? entityId = null, string entityType = "ActivityBatch") => new()
    {
        QueueItemId = Guid.NewGuid().ToString("D"),
        EntityType = entityType,
        EntityId = entityId ?? Guid.NewGuid().ToString("D"),
        PayloadJson = """{"count": 42}""",
        CreatedAt = DateTimeOffset.UtcNow
    };

    [Fact]
    public async Task Scenario1_Backend_Available_Should_Sync_Items_And_Transition_To_Synced()
    {
        var repo = new SyncQueueRepository(_fixture.Context);
        var engine = CreateEngine(repo);

        var item1 = CreateItem("batch-1");
        var item2 = CreateItem("batch-2");

        await repo.EnqueueAsync(item1);
        await repo.EnqueueAsync(item2);

        // Act
        var syncedCount = await engine.SyncPendingAsync();

        // Assert
        Assert.Equal(2, syncedCount);
        Assert.True(engine.IsOnline);

        var stored1 = await repo.GetByIdAsync(item1.QueueItemId);
        var stored2 = await repo.GetByIdAsync(item2.QueueItemId);

        Assert.NotNull(stored1);
        Assert.NotNull(stored2);
        Assert.Equal(SyncStatus.Synced, stored1.Status);
        Assert.Equal(SyncStatus.Synced, stored2.Status);
        Assert.Equal(1, stored1.AttemptCount);
        Assert.Equal(1, stored2.AttemptCount);

        Assert.Equal(2, _transport.SentItems.Count);
    }

    [Fact]
    public async Task Scenario2_Backend_Unavailable_Should_Mark_Offline_And_Transition_To_Failed_With_Backoff()
    {
        var repo = new SyncQueueRepository(_fixture.Context);
        var engine = CreateEngine(repo);

        var item = CreateItem("batch-offline");
        await repo.EnqueueAsync(item);

        _transport.IsOnline = false;

        bool connectivityChangedEventFired = false;
        engine.ConnectivityChanged += isOnline =>
        {
            if (!isOnline) connectivityChangedEventFired = true;
        };

        // Act
        var syncedCount = await engine.SyncPendingAsync();

        // Assert
        Assert.Equal(0, syncedCount);
        Assert.False(engine.IsOnline);
        Assert.True(connectivityChangedEventFired);

        var stored = await repo.GetByIdAsync(item.QueueItemId);
        Assert.NotNull(stored);
        Assert.Equal(SyncStatus.Failed, stored.Status);
        Assert.Equal(1, stored.AttemptCount);
        Assert.NotNull(stored.NextAttemptAt);
        Assert.False(stored.IsPermanentFailure);
        Assert.Contains("Connection refused", stored.ErrorMessage);
    }

    [Fact]
    public async Task Scenario3_Failure_During_Upload_Should_Handle_Exception_Safely_And_Mark_Failed()
    {
        var repo = new SyncQueueRepository(_fixture.Context);
        var engine = CreateEngine(repo);

        var item = CreateItem("batch-fail");
        await repo.EnqueueAsync(item);

        _transport.SimulateNetworkFailure = true;
        _transport.NetworkErrorMessage = "503 Service Unavailable: Gateway Timeout";

        // Act
        var syncedCount = await engine.SyncPendingAsync();

        // Assert
        Assert.Equal(0, syncedCount);
        Assert.False(engine.IsOnline);

        var stored = await repo.GetByIdAsync(item.QueueItemId);
        Assert.NotNull(stored);
        Assert.Equal(SyncStatus.Failed, stored.Status);
        Assert.Equal(1, stored.AttemptCount);
        Assert.Contains("503 Service Unavailable", stored.ErrorMessage);
    }

    [Fact]
    public async Task Scenario4_Retry_Should_Use_Exponential_Backoff_And_Succeed_On_Subsequent_Attempt()
    {
        var repo = new SyncQueueRepository(_fixture.Context);
        var engine = CreateEngine(repo);

        var item = CreateItem("batch-retry");
        await repo.EnqueueAsync(item);

        // Attempt 1: Offline -> Failed
        _transport.IsOnline = false;
        await engine.SyncPendingAsync();

        var failedItem = await repo.GetByIdAsync(item.QueueItemId);
        Assert.NotNull(failedItem);
        Assert.Equal(SyncStatus.Failed, failedItem.Status);
        Assert.Equal(1, failedItem.AttemptCount);
        var nextAttempt = failedItem.NextAttemptAt;
        Assert.NotNull(nextAttempt);

        // Immediate retry before backoff delay has arrived: should NOT be eligible
        _transport.IsOnline = true;
        var immediateSynced = await engine.SyncPendingAsync();
        Assert.Equal(0, immediateSynced); // Not yet eligible

        // Fast-forward time past backoff delay
        var futureContext = _fixture.ReopenContext();
        var futureRepo = new SyncQueueRepository(futureContext);
        var futureEngine = CreateEngine(futureRepo);

        // Update NextAttemptAt to past to simulate elapsed backoff time
        var entity = await futureContext.SyncQueue.FindAsync(item.QueueItemId);
        Assert.NotNull(entity);
        entity.NextAttemptAt = DateTimeOffset.UtcNow.AddSeconds(-1);
        await futureContext.SaveChangesAsync();

        // Attempt 2: Online -> Synced
        var retrySynced = await futureEngine.SyncPendingAsync();
        Assert.Equal(1, retrySynced);

        var syncedItem = await futureRepo.GetByIdAsync(item.QueueItemId);
        Assert.NotNull(syncedItem);
        Assert.Equal(SyncStatus.Synced, syncedItem.Status);
        Assert.Equal(2, syncedItem.AttemptCount);
        Assert.Null(syncedItem.ErrorMessage);
    }

    [Fact]
    public async Task Scenario5_Restart_Application_Should_Recover_Stale_InProgress_Items_To_Pending()
    {
        var repo1 = new SyncQueueRepository(_fixture.Context);

        // Simulate crash: an item was left InProgress in SQLite
        var item = CreateItem("batch-crash");
        item.MarkInProgress(DateTimeOffset.UtcNow);
        await repo1.EnqueueAsync(item);

        var rawItem = await _fixture.Context.SyncQueue.FindAsync(item.QueueItemId);
        Assert.NotNull(rawItem);
        rawItem.Status = nameof(SyncStatus.InProgress);
        await _fixture.Context.SaveChangesAsync();

        // Simulate application restart: new context, new engine
        await using var ctx2 = _fixture.ReopenContext();
        var repo2 = new SyncQueueRepository(ctx2);
        var engine2 = CreateEngine(repo2);

        // Act: Start engine (triggers RecoverStaleInProgressItemsAsync)
        var recovered = await engine2.RecoverStaleInProgressItemsAsync();

        // Assert
        Assert.Equal(1, recovered);

        var recoveredItem = await repo2.GetByIdAsync(item.QueueItemId);
        Assert.NotNull(recoveredItem);
        Assert.Equal(SyncStatus.Pending, recoveredItem.Status);

        // Verify it can now be successfully synced
        var syncedCount = await engine2.SyncPendingAsync();
        Assert.Equal(1, syncedCount);

        var finalItem = await repo2.GetByIdAsync(item.QueueItemId);
        Assert.NotNull(finalItem);
        Assert.Equal(SyncStatus.Synced, finalItem.Status);
    }

    [Fact]
    public async Task Scenario6_Queue_Survives_Restart_When_Context_Is_Closed_And_Reopened()
    {
        // Enqueue in context 1
        var repo1 = new SyncQueueRepository(_fixture.Context);
        var item1 = CreateItem("batch-persist-1");
        var item2 = CreateItem("batch-persist-2");
        await repo1.EnqueueAsync(item1);
        await repo1.EnqueueAsync(item2);

        // Close context 1 and open context 2 pointing to the same SQLite file
        await using var ctx2 = _fixture.ReopenContext();
        var repo2 = new SyncQueueRepository(ctx2);

        var pending = await repo2.GetPendingAsync();

        Assert.Equal(2, pending.Count);
        Assert.Contains(pending, i => i.EntityId == "batch-persist-1");
        Assert.Contains(pending, i => i.EntityId == "batch-persist-2");
    }

    [Fact]
    public async Task Scenario7_Duplicate_Upload_Prevention_Should_Not_Create_Duplicate_Queue_Items()
    {
        var repo = new SyncQueueRepository(_fixture.Context);
        var stableEntityId = "batch-unique-id-999";

        var itemA = CreateItem(stableEntityId);
        var itemB = CreateItem(stableEntityId); // Same EntityId

        // Act: EnqueueIfNotExists
        var enqueuedFirst = await repo.EnqueueIfNotExistsAsync(itemA);
        var enqueuedSecond = await repo.EnqueueIfNotExistsAsync(itemB);

        // Assert
        Assert.True(enqueuedFirst);
        Assert.False(enqueuedSecond); // Duplicate rejected

        var count = await repo.CountByStatusAsync(SyncStatus.Pending);
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task Scenario8_Partial_Batch_Success_Should_Handle_Mixed_Results_And_Not_Retry_Permanent_Errors()
    {
        var repo = new SyncQueueRepository(_fixture.Context);
        var engine = CreateEngine(repo);

        var successItem = CreateItem("item-success");
        var permanentFailItem = CreateItem("item-permanent");
        var transientFailItem = CreateItem("item-transient");

        await repo.EnqueueAsync(successItem);
        await repo.EnqueueAsync(permanentFailItem);
        await repo.EnqueueAsync(transientFailItem);

        // Configure transport behavior
        _transport.PermanentErrorPredicate = item => item.EntityId == "item-permanent";
        _transport.TransientErrorPredicate = item => item.EntityId == "item-transient";

        // Act
        var syncedCount = await engine.SyncPendingAsync();

        // Assert
        Assert.Equal(1, syncedCount);

        var storedSuccess = await repo.GetByIdAsync(successItem.QueueItemId);
        var storedPermanent = await repo.GetByIdAsync(permanentFailItem.QueueItemId);
        var storedTransient = await repo.GetByIdAsync(transientFailItem.QueueItemId);

        Assert.NotNull(storedSuccess);
        Assert.NotNull(storedPermanent);
        Assert.NotNull(storedTransient);

        Assert.Equal(SyncStatus.Synced, storedSuccess.Status);

        Assert.Equal(SyncStatus.Failed, storedPermanent.Status);
        Assert.True(storedPermanent.IsPermanentFailure);
        Assert.Null(storedPermanent.NextAttemptAt); // Never scheduled for retry

        Assert.Equal(SyncStatus.Failed, storedTransient.Status);
        Assert.False(storedTransient.IsPermanentFailure);
        Assert.NotNull(storedTransient.NextAttemptAt); // Scheduled for retry
    }

    [Fact]
    public async Task Scenario9_Cancellation_During_Sync_Should_Abort_Cleanly_Without_Crashing()
    {
        var repo = new SyncQueueRepository(_fixture.Context);
        var engine = CreateEngine(repo);

        var item = CreateItem("batch-cancel");
        await repo.EnqueueAsync(item);

        using var cts = new CancellationTokenSource();
        cts.Cancel(); // Pre-cancel

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await engine.SyncPendingAsync(cts.Token);
        });
    }

    [Fact]
    public async Task Scenario10_Tracking_And_Persistence_Pipeline_Works_Offline()
    {
        // Setup SQLite repositories
        _fixture.Context.Devices.Add(new DeviceEntity
        {
            DeviceId = "dev-offline-01",
            Hostname = "host",
            OperatingSystem = "macOS",
            OsVersion = "14.5",
            AgentVersion = "1.0",
            RegisteredAt = DateTimeOffset.UtcNow,
            LastSeenAt = DateTimeOffset.UtcNow
        });
        _fixture.Context.Sessions.Add(new SessionEntity
        {
            SessionId = "session-offline-01",
            DeviceId = "dev-offline-01",
            StartedAt = DateTimeOffset.UtcNow,
            Status = "Active"
        });
        await _fixture.Context.SaveChangesAsync();

        var batchRepo = new ActivityBatchRepository(_fixture.Context);
        var queueRepo = new SyncQueueRepository(_fixture.Context);

        var coordinator = new TrackingPersistenceCoordinator(
            batchRepo,
            queueRepo,
            NullLogger<TrackingPersistenceCoordinator>.Instance);

        var batch = new ActivityBatch
        {
            BatchId = Guid.NewGuid().ToString("D"),
            DeviceId = "dev-offline-01",
            SessionId = "session-offline-01",
            StartedAt = DateTimeOffset.UtcNow.AddMinutes(-1),
            EndedAt = DateTimeOffset.UtcNow,
            KeyboardCount = 100,
            MouseCount = 50,
            ActiveDuration = TimeSpan.FromSeconds(50),
            IdleDuration = TimeSpan.FromSeconds(10),
            HasSuspiciousMouseActivity = false
        };

        // Network is completely offline
        _transport.IsOnline = false;

        // Act: Persist batch and queue it
        var enqueued = await coordinator.PersistAndEnqueueBatchAsync(batch);

        // Assert: Tracking data is successfully saved locally even when offline!
        Assert.True(enqueued);

        var savedBatches = await batchRepo.GetBySessionIdAsync("session-offline-01");
        Assert.Single(savedBatches);
        Assert.Equal(batch.BatchId, savedBatches[0].BatchId);

        var pendingQueueItems = await queueRepo.GetPendingAsync();
        Assert.Single(pendingQueueItems);
        Assert.Equal(batch.BatchId, pendingQueueItems[0].EntityId);

        // Duplicate call with same batch does not create duplicate in queue
        var duplicateResult = await coordinator.PersistAndEnqueueBatchAsync(batch);
        Assert.False(duplicateResult);

        var finalQueueCount = await queueRepo.CountByStatusAsync(SyncStatus.Pending);
        Assert.Equal(1, finalQueueCount);
    }

    public void Dispose() => _fixture.Dispose();
}
