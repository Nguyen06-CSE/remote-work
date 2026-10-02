using RemoteWork.Desktop.Core.Enums;

namespace RemoteWork.Desktop.Core.Models;

/// <summary>
/// Domain model for an offline sync queue item.
/// Backed by persistent local storage (SQLite) and manages explicit state transitions:
/// Pending -> InProgress -> Synced | Failed.
/// </summary>
public sealed class SyncQueueItem
{
    public string QueueItemId { get; init; } = Guid.NewGuid().ToString("D");

    /// <summary>
    /// Backwards compatibility alias for QueueItemId.
    /// </summary>
    public string ItemId
    {
        get => QueueItemId;
        init => QueueItemId = value;
    }

    /// <summary>
    /// Logical entity type (e.g., "Session", "ActivityBatch", "ApplicationActivity").
    /// </summary>
    public required string EntityType { get; init; }

    /// <summary>
    /// Stable unique ID of the tracked entity for idempotency (e.g., BatchId, SessionId).
    /// </summary>
    public required string EntityId { get; init; }

    /// <summary>
    /// Serialized payload to send to remote transport.
    /// </summary>
    public required string PayloadJson { get; init; }

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    public int AttemptCount { get; private set; }

    /// <summary>
    /// Backwards compatibility alias for AttemptCount.
    /// </summary>
    public int RetryCount => AttemptCount;

    public DateTimeOffset? LastAttemptAt { get; private set; }

    public SyncStatus Status { get; private set; } = SyncStatus.Pending;

    public string? ErrorMessage { get; private set; }

    public bool IsPermanentFailure { get; private set; }

    public DateTimeOffset? NextAttemptAt { get; private set; }

    public SyncQueueItem()
    {
    }

    [System.Diagnostics.CodeAnalysis.SetsRequiredMembers]
    public SyncQueueItem(
        string queueItemId,
        string entityType,
        string entityId,
        string payloadJson,
        DateTimeOffset createdAt,
        int attemptCount = 0,
        DateTimeOffset? lastAttemptAt = null,
        SyncStatus status = SyncStatus.Pending,
        string? errorMessage = null,
        bool isPermanentFailure = false,
        DateTimeOffset? nextAttemptAt = null)
    {
        QueueItemId = queueItemId;
        EntityType = entityType;
        EntityId = entityId;
        PayloadJson = payloadJson;
        CreatedAt = createdAt;
        AttemptCount = attemptCount;
        LastAttemptAt = lastAttemptAt;
        Status = status;
        ErrorMessage = errorMessage;
        IsPermanentFailure = isPermanentFailure;
        NextAttemptAt = nextAttemptAt;
    }

    public void MarkInProgress(DateTimeOffset attemptTime)
    {
        EnsureCanTransitionTo(SyncStatus.InProgress);
        Status = SyncStatus.InProgress;
        AttemptCount++;
        LastAttemptAt = attemptTime;
    }

    public void MarkSynced(DateTimeOffset syncedTime)
    {
        EnsureCanTransitionTo(SyncStatus.Synced);
        Status = SyncStatus.Synced;
        LastAttemptAt = syncedTime;
        ErrorMessage = null;
        NextAttemptAt = null;
    }

    public void MarkFailed(string error, DateTimeOffset attemptTime, TimeSpan? retryDelay = null, bool isPermanent = false)
    {
        EnsureCanTransitionTo(SyncStatus.Failed);
        Status = SyncStatus.Failed;
        LastAttemptAt = attemptTime;
        ErrorMessage = error;
        IsPermanentFailure = isPermanent;
        NextAttemptAt = isPermanent || retryDelay is null ? null : attemptTime + retryDelay.Value;
    }

    public void ResetToPending()
    {
        EnsureCanTransitionTo(SyncStatus.Pending);
        Status = SyncStatus.Pending;
    }

    private void EnsureCanTransitionTo(SyncStatus newStatus)
    {
        if (!IsValidTransition(Status, newStatus))
        {
            throw new InvalidOperationException(
                $"Invalid sync status transition from '{Status}' to '{newStatus}'.");
        }
    }

    public static bool IsValidTransition(SyncStatus current, SyncStatus target)
    {
        if (current == target)
            return true;

        return current switch
        {
            SyncStatus.Pending => target is SyncStatus.InProgress,
            SyncStatus.InProgress => target is SyncStatus.Synced or SyncStatus.Failed or SyncStatus.Pending,
            SyncStatus.Failed => target is SyncStatus.InProgress or SyncStatus.Pending,
            SyncStatus.Synced => false,
            _ => false
        };
    }
}
