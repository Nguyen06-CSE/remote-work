using System.Text.Json;
using Microsoft.Extensions.Logging;
using RemoteWork.Desktop.Core.Interfaces;
using RemoteWork.Desktop.Core.Models;
using ActivityBatch = RemoteWork.Desktop.Core.Models.Activity.ActivityBatch;

namespace RemoteWork.Desktop.Application.Sync;

/// <summary>
/// Coordinates saving raw tracking data locally to SQLite and enqueuing items into the offline SyncQueue.
/// Ensures the tracking system is 100% offline-first and never depends on network connectivity.
/// </summary>
public sealed class TrackingPersistenceCoordinator
{
    private readonly IActivityBatchRepository _batchRepository;
    private readonly ISyncQueueRepository _syncQueueRepository;
    private readonly IApplicationActivityRepository? _appActivityRepository;
    private readonly ILogger<TrackingPersistenceCoordinator> _logger;

    public TrackingPersistenceCoordinator(
        IActivityBatchRepository batchRepository,
        ISyncQueueRepository syncQueueRepository,
        ILogger<TrackingPersistenceCoordinator> logger)
        : this(batchRepository, syncQueueRepository, null, logger)
    {
    }

    public TrackingPersistenceCoordinator(
        IActivityBatchRepository batchRepository,
        ISyncQueueRepository syncQueueRepository,
        IApplicationActivityRepository? appActivityRepository,
        ILogger<TrackingPersistenceCoordinator> logger)
    {
        _batchRepository = batchRepository;
        _syncQueueRepository = syncQueueRepository;
        _appActivityRepository = appActivityRepository;
        _logger = logger;
    }

    /// <summary>
    /// Persists an ActivityBatch to local SQLite and enqueues it for background synchronization.
    /// Idempotent: repeated calls with the same BatchId will not create duplicate queue items.
    /// </summary>
    public async Task<bool> PersistAndEnqueueBatchAsync(ActivityBatch batch, CancellationToken ct = default)
    {
        // 1. Persist to local SQLite storage
        await _batchRepository.SaveAsync(batch, ct);
        _logger.LogInformation("Saved ActivityBatch {BatchId} to local SQLite.", batch.BatchId);

        // 2. Enqueue into persistent SyncQueue with stable unique BatchId
        var payload = JsonSerializer.Serialize(batch);
        var queueItem = new SyncQueueItem
        {
            QueueItemId = Guid.NewGuid().ToString("D"),
            EntityType = "ActivityBatch",
            EntityId = batch.BatchId,
            PayloadJson = payload,
            CreatedAt = DateTimeOffset.UtcNow
        };

        var enqueued = await _syncQueueRepository.EnqueueIfNotExistsAsync(queueItem, ct);
        if (enqueued)
        {
            _logger.LogInformation("Enqueued ActivityBatch {BatchId} to sync queue.", batch.BatchId);
        }
        else
        {
            _logger.LogInformation("ActivityBatch {BatchId} was already enqueued. Skipped duplicate.", batch.BatchId);
        }

        return enqueued;
    }

    /// <summary>
    /// Persists a Session to local SQLite and enqueues it for background synchronization.
    /// </summary>
    public async Task<bool> PersistAndEnqueueSessionAsync(ISessionRepository sessionRepository, Session session, CancellationToken ct = default)
    {
        await sessionRepository.SaveAsync(session, ct);
        _logger.LogInformation("Saved Session {SessionId} to local SQLite.", session.SessionId);

        var payload = JsonSerializer.Serialize(session);
        var queueItem = new SyncQueueItem
        {
            QueueItemId = Guid.NewGuid().ToString("D"),
            EntityType = "Session",
            EntityId = session.SessionId,
            PayloadJson = payload,
            CreatedAt = DateTimeOffset.UtcNow
        };

        return await _syncQueueRepository.EnqueueIfNotExistsAsync(queueItem, ct);
    }

    /// <summary>
    /// Persists an ApplicationActivity to local SQLite and enqueues it for background synchronization.
    /// </summary>
    public async Task<bool> PersistAndEnqueueApplicationActivityAsync(ApplicationActivity activity, CancellationToken ct = default)
    {
        if (_appActivityRepository is not null)
        {
            await _appActivityRepository.SaveAsync(activity, ct);
            _logger.LogInformation("Saved ApplicationActivity {ActivityId} ({AppName}) to local SQLite.", activity.ActivityId, activity.ApplicationName);
        }

        var payload = JsonSerializer.Serialize(activity);
        var queueItem = new SyncQueueItem
        {
            QueueItemId = Guid.NewGuid().ToString("D"),
            EntityType = "ApplicationActivity",
            EntityId = activity.ActivityId,
            PayloadJson = payload,
            CreatedAt = DateTimeOffset.UtcNow
        };

        var enqueued = await _syncQueueRepository.EnqueueIfNotExistsAsync(queueItem, ct);
        if (enqueued)
        {
            _logger.LogInformation("Enqueued ApplicationActivity {ActivityId} ({AppName}) to sync queue.", activity.ActivityId, activity.ApplicationName);
        }

        return enqueued;
    }
}
