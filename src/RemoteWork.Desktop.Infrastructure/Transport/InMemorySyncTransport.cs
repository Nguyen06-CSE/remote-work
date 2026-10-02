using RemoteWork.Desktop.Core.Interfaces;
using RemoteWork.Desktop.Core.Models;

namespace RemoteWork.Desktop.Infrastructure.Transport;

/// <summary>
/// In-memory transport implementation of ISyncTransport for testing and offline simulation.
/// Allows fine-grained control over network availability, transient and permanent errors,
/// batch partial failures, and idempotency inspection.
/// </summary>
public sealed class InMemorySyncTransport : ISyncTransport
{
    private readonly object _lock = new();
    private readonly List<SyncQueueItem> _sentItems = [];
    private readonly List<string> _sentEntityIds = [];

    public bool IsOnline { get; set; } = true;
    public bool SimulateNetworkFailure { get; set; }
    public string NetworkErrorMessage { get; set; } = "Connection refused: backend unavailable";
    public TimeSpan Latency { get; set; } = TimeSpan.Zero;
    public int SendCallCount { get; private set; }

    /// <summary>
    /// Custom predicate determining whether a specific item should fail with a permanent error.
    /// </summary>
    public Func<SyncQueueItem, bool>? PermanentErrorPredicate { get; set; }

    /// <summary>
    /// Custom predicate determining whether a specific item should fail with a transient error.
    /// </summary>
    public Func<SyncQueueItem, bool>? TransientErrorPredicate { get; set; }

    /// <summary>
    /// Optional hook invoked on each send attempt (e.g., to inject cancellations or inspect timing).
    /// </summary>
    public Func<SyncQueueItem, CancellationToken, Task>? OnBeforeSendAsync { get; set; }

    public IReadOnlyList<SyncQueueItem> SentItems
    {
        get
        {
            lock (_lock)
            {
                return _sentItems.ToList();
            }
        }
    }

    public IReadOnlyList<string> SentEntityIds
    {
        get
        {
            lock (_lock)
            {
                return _sentEntityIds.ToList();
            }
        }
    }

    public async Task<SyncResult> SendAsync(SyncQueueItem item, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        if (Latency > TimeSpan.Zero)
        {
            await Task.Delay(Latency, ct);
        }

        if (OnBeforeSendAsync is not null)
        {
            await OnBeforeSendAsync(item, ct);
        }

        lock (_lock)
        {
            SendCallCount++;
        }

        if (!IsOnline || SimulateNetworkFailure)
        {
            return SyncResult.TransientFailure(NetworkErrorMessage);
        }

        if (PermanentErrorPredicate?.Invoke(item) == true)
        {
            return SyncResult.PermanentFailure($"Permanent validation failure for item {item.QueueItemId}");
        }

        if (TransientErrorPredicate?.Invoke(item) == true)
        {
            return SyncResult.TransientFailure($"Transient failure for item {item.QueueItemId}");
        }

        lock (_lock)
        {
            _sentItems.Add(item);
            _sentEntityIds.Add(item.EntityId);
        }

        return SyncResult.Success();
    }

    public async Task<SyncBatchResult> SendBatchAsync(IReadOnlyList<SyncQueueItem> items, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        if (items.Count == 0)
        {
            return SyncBatchResult.FromItemResults([]);
        }

        if (Latency > TimeSpan.Zero)
        {
            await Task.Delay(Latency, ct);
        }

        lock (_lock)
        {
            SendCallCount++;
        }

        // Entire transport failure (e.g. offline or DNS failure)
        if (!IsOnline || SimulateNetworkFailure)
        {
            return SyncBatchResult.TransportFailure(items, NetworkErrorMessage);
        }

        var results = new List<SyncItemResult>();

        foreach (var item in items)
        {
            if (OnBeforeSendAsync is not null)
            {
                await OnBeforeSendAsync(item, ct);
            }

            if (PermanentErrorPredicate?.Invoke(item) == true)
            {
                results.Add(SyncItemResult.PermanentFailure(item.QueueItemId, $"Validation error: payload rejected for {item.EntityId}"));
                continue;
            }

            if (TransientErrorPredicate?.Invoke(item) == true)
            {
                results.Add(SyncItemResult.TransientFailure(item.QueueItemId, $"Temporary error for {item.EntityId}"));
                continue;
            }

            lock (_lock)
            {
                _sentItems.Add(item);
                _sentEntityIds.Add(item.EntityId);
            }

            results.Add(SyncItemResult.Success(item.QueueItemId));
        }

        return SyncBatchResult.FromItemResults(results);
    }

    public Task<bool> CheckConnectivityAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(IsOnline && !SimulateNetworkFailure);
    }

    public void Reset()
    {
        lock (_lock)
        {
            _sentItems.Clear();
            _sentEntityIds.Clear();
            SendCallCount = 0;
            IsOnline = true;
            SimulateNetworkFailure = false;
            PermanentErrorPredicate = null;
            TransientErrorPredicate = null;
            OnBeforeSendAsync = null;
            Latency = TimeSpan.Zero;
        }
    }
}
