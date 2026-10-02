using RemoteWork.Desktop.Core.Models;

namespace RemoteWork.Desktop.Core.Interfaces;

/// <summary>
/// Engine orchestrating the offline-first sync lifecycle:
/// Reading eligible items from SQLite, sending through ISyncTransport,
/// updating state transitions, managing exponential backoff retries,
/// and recovering from crashes/restarts.
/// </summary>
public interface ISyncEngine
{
    /// <summary>
    /// Gets whether the remote backend is currently considered reachable.
    /// </summary>
    bool IsOnline { get; }

    /// <summary>
    /// Event fired when backend connectivity state changes.
    /// </summary>
    event Action<bool>? ConnectivityChanged;

    /// <summary>
    /// Event fired when a queue item is successfully synced.
    /// </summary>
    event Action<SyncQueueItem>? ItemSynced;

    /// <summary>
    /// Event fired when a queue item fails to sync.
    /// </summary>
    event Action<SyncQueueItem, string>? ItemFailed;

    /// <summary>
    /// Synchronizes eligible pending or retryable items in the queue once.
    /// Returns the number of items successfully synced.
    /// </summary>
    Task<int> SyncPendingAsync(CancellationToken ct = default);

    /// <summary>
    /// Starts the background sync engine loop.
    /// </summary>
    Task StartAsync(CancellationToken ct = default);

    /// <summary>
    /// Stops the background sync engine loop gracefully.
    /// </summary>
    Task StopAsync(CancellationToken ct = default);
}
