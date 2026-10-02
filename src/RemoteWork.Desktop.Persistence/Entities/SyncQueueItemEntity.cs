namespace RemoteWork.Desktop.Persistence.Entities;

public sealed class SyncQueueItemEntity
{
    public required string ItemId { get; set; }
    public required string EntityType { get; set; } // "Session", "ActivityBatch", "ApplicationActivity"
    public required string EntityId { get; set; }
    public required string PayloadJson { get; set; }
    public required string Status { get; set; } // "Pending", "Sent", "Failed"
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? SentAt { get; set; }
    public string? FailureReason { get; set; }
    public int RetryCount { get; set; }
}
