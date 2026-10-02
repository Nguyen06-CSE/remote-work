using RemoteWork.Desktop.Core.Models;

namespace RemoteWork.Desktop.Core.Interfaces;

/// <summary>
/// Transport abstraction responsible for transmitting sync queue items to the remote backend.
/// Allows swapping between mock, in-memory, and actual HTTP/FastAPI implementations.
/// </summary>
public interface ISyncTransport
{
    /// <summary>
    /// Sends a single item to the backend.
    /// </summary>
    Task<SyncResult> SendAsync(SyncQueueItem item, CancellationToken ct = default);

    /// <summary>
    /// Sends a batch of items to the backend, returning individual results per item.
    /// Supports partial batch success.
    /// </summary>
    Task<SyncBatchResult> SendBatchAsync(IReadOnlyList<SyncQueueItem> items, CancellationToken ct = default);

    /// <summary>
    /// Checks connectivity to the backend service.
    /// </summary>
    Task<bool> CheckConnectivityAsync(CancellationToken ct = default);
}
