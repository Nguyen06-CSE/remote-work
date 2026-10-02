using RemoteWork.Desktop.Core.Enums;
using RemoteWork.Desktop.Core.Models;

namespace RemoteWork.Desktop.Core.Interfaces;

/// <summary>
/// Repository interface for persisting and querying sync queue items.
/// </summary>
public interface ISyncQueueRepository
{
    /// <summary>
    /// Enqueues a sync item.
    /// </summary>
    Task EnqueueAsync(SyncQueueItem item, CancellationToken ct = default);

    /// <summary>
    /// Idempotently enqueues an item: if an item with the same EntityType and EntityId
    /// already exists, it is NOT re-inserted, preventing duplicate records.
    /// Returns true if enqueued; false if duplicate existed.
    /// </summary>
    Task<bool> EnqueueIfNotExistsAsync(SyncQueueItem item, CancellationToken ct = default);

    /// <summary>
    /// Gets a sync queue item by its QueueItemId.
    /// </summary>
    Task<SyncQueueItem?> GetByIdAsync(string queueItemId, CancellationToken ct = default);

    /// <summary>
    /// Gets a sync queue item by EntityType and EntityId.
    /// </summary>
    Task<SyncQueueItem?> GetByEntityIdAsync(string entityType, string entityId, CancellationToken ct = default);

    /// <summary>
    /// Gets items with Pending status up to the specified limit, ordered oldest first.
    /// </summary>
    Task<IReadOnlyList<SyncQueueItem>> GetPendingAsync(int limit = 50, CancellationToken ct = default);

    /// <summary>
    /// Gets items eligible for sync: either Pending or Failed (non-permanent) whose NextAttemptAt is null or in the past.
    /// </summary>
    Task<IReadOnlyList<SyncQueueItem>> GetEligibleForSyncAsync(DateTimeOffset now, int limit = 50, CancellationToken ct = default);

    /// <summary>
    /// Gets items matching a given status.
    /// </summary>
    Task<IReadOnlyList<SyncQueueItem>> GetByStatusAsync(SyncStatus status, int limit = 50, CancellationToken ct = default);

    /// <summary>
    /// Updates an existing sync queue item in the database.
    /// </summary>
    Task UpdateAsync(SyncQueueItem item, CancellationToken ct = default);

    /// <summary>
    /// Recovers from an abnormal shutdown/crash by resetting any items stuck in 'InProgress' back to 'Pending'.
    /// Returns the number of items reset.
    /// </summary>
    Task<int> ResetInProgressToPendingAsync(CancellationToken ct = default);

    /// <summary>
    /// Marks an item as sent (legacy compatibility).
    /// </summary>
    Task MarkSentAsync(string itemId, CancellationToken ct = default);

    /// <summary>
    /// Marks an item as failed (legacy compatibility).
    /// </summary>
    Task MarkFailedAsync(string itemId, string reason, CancellationToken ct = default);

    /// <summary>
    /// Deletes sent/synced items.
    /// </summary>
    Task DeleteSentAsync(CancellationToken ct = default);

    /// <summary>
    /// Counts items with the specified status.
    /// </summary>
    Task<int> CountByStatusAsync(SyncStatus status, CancellationToken ct = default);
}
