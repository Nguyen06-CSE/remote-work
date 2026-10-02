namespace RemoteWork.Desktop.Core.Models;

public sealed class SyncQueueItem
{
    public required string ItemId { get; init; }
    public required string EntityType { get; init; }
    public required string EntityId { get; init; }
    public required string PayloadJson { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}
