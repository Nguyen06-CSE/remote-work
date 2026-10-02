namespace RemoteWork.Desktop.Core.Models;

/// <summary>
/// Result of an individual sync item transport operation.
/// </summary>
public sealed class SyncResult
{
    public required bool IsSuccess { get; init; }
    public string? ErrorMessage { get; init; }
    public bool IsPermanentError { get; init; }
    public int? StatusCode { get; init; }

    public static SyncResult Success() => new() { IsSuccess = true };

    public static SyncResult TransientFailure(string errorMessage, int? statusCode = null) => new()
    {
        IsSuccess = false,
        ErrorMessage = errorMessage,
        IsPermanentError = false,
        StatusCode = statusCode
    };

    public static SyncResult PermanentFailure(string errorMessage, int? statusCode = 400) => new()
    {
        IsSuccess = false,
        ErrorMessage = errorMessage,
        IsPermanentError = true,
        StatusCode = statusCode
    };
}

/// <summary>
/// Result of a single item in a batch operation.
/// </summary>
public sealed class SyncItemResult
{
    public required string QueueItemId { get; init; }
    public required bool IsSuccess { get; init; }
    public string? ErrorMessage { get; init; }
    public bool IsPermanentError { get; init; }

    public static SyncItemResult Success(string queueItemId) => new()
    {
        QueueItemId = queueItemId,
        IsSuccess = true
    };

    public static SyncItemResult TransientFailure(string queueItemId, string errorMessage) => new()
    {
        QueueItemId = queueItemId,
        IsSuccess = false,
        ErrorMessage = errorMessage,
        IsPermanentError = false
    };

    public static SyncItemResult PermanentFailure(string queueItemId, string errorMessage) => new()
    {
        QueueItemId = queueItemId,
        IsSuccess = false,
        ErrorMessage = errorMessage,
        IsPermanentError = true
    };
}

/// <summary>
/// Result of a batch transport operation, supporting partial success.
/// </summary>
public sealed class SyncBatchResult
{
    public required IReadOnlyList<SyncItemResult> ItemResults { get; init; }
    public bool HasFailures => ItemResults.Any(r => !r.IsSuccess);
    public bool AllSucceeded => ItemResults.Count > 0 && ItemResults.All(r => r.IsSuccess);
    public bool IsTransportFailure { get; init; }
    public string? TransportErrorMessage { get; init; }

    public static SyncBatchResult FromItemResults(IReadOnlyList<SyncItemResult> itemResults) => new()
    {
        ItemResults = itemResults
    };

    public static SyncBatchResult TransportFailure(IReadOnlyList<SyncQueueItem> items, string errorMessage) => new()
    {
        ItemResults = items.Select(i => SyncItemResult.TransientFailure(i.QueueItemId, errorMessage)).ToList(),
        IsTransportFailure = true,
        TransportErrorMessage = errorMessage
    };
}
