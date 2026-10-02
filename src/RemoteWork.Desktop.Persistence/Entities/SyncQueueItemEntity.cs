namespace RemoteWork.Desktop.Persistence.Entities;

public sealed class SyncQueueItemEntity
{
    public required string QueueItemId { get; set; }

    /// <summary>
    /// Backwards compatibility alias for ItemId.
    /// </summary>
    public string ItemId
    {
        get => QueueItemId;
        set => QueueItemId = value;
    }

    public required string EntityType { get; set; } // "Session", "ActivityBatch", "ApplicationActivity"
    public required string EntityId { get; set; }
    public required string PayloadJson { get; set; }
    public required string Status { get; set; } // "Pending", "InProgress", "Synced", "Failed"
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? LastAttemptAt { get; set; }

    /// <summary>
    /// Backwards compatibility alias for SentAt.
    /// </summary>
    public DateTimeOffset? SentAt
    {
        get => LastAttemptAt;
        set => LastAttemptAt = value;
    }

    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Backwards compatibility alias for FailureReason.
    /// </summary>
    public string? FailureReason
    {
        get => ErrorMessage;
        set => ErrorMessage = value;
    }

    public int AttemptCount { get; set; }

    /// <summary>
    /// Backwards compatibility alias for RetryCount.
    /// </summary>
    public int RetryCount
    {
        get => AttemptCount;
        set => AttemptCount = value;
    }

    public bool IsPermanentFailure { get; set; }

    public DateTimeOffset? NextAttemptAt { get; set; }
}
