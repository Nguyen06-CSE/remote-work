using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RemoteWork.Desktop.Application.Options;
using RemoteWork.Desktop.Core.Interfaces;
using RemoteWork.Desktop.Core.Models;

namespace RemoteWork.Desktop.Application.Sync;

/// <summary>
/// Core implementation of the offline-first sync engine.
/// Coordinates reading eligible items from SQLite, sending to ISyncTransport,
/// managing explicit state transitions, backoff retry calculations, and
/// recovering stale items left in progress after crashes or restarts.
/// </summary>
public sealed class SyncEngine : ISyncEngine, IDisposable
{
    private readonly ISyncQueueRepository? _directRepository;
    private readonly IServiceScopeFactory? _scopeFactory;
    private readonly ISyncTransport _transport;
    private readonly SyncOptions _options;
    private readonly ILogger<SyncEngine> _logger;

    private Task? _backgroundTask;
    private CancellationTokenSource? _cts;
    private bool _isOnline = true;
    private readonly object _stateLock = new();

    public bool IsOnline
    {
        get
        {
            lock (_stateLock)
            {
                return _isOnline;
            }
        }
        private set
        {
            bool changed = false;
            lock (_stateLock)
            {
                if (_isOnline != value)
                {
                    _isOnline = value;
                    changed = true;
                }
            }

            if (changed)
            {
                _logger.LogInformation("Sync engine connectivity changed. IsOnline={IsOnline}", value);
                ConnectivityChanged?.Invoke(value);
            }
        }
    }

    public event Action<bool>? ConnectivityChanged;
    public event Action<SyncQueueItem>? ItemSynced;
    public event Action<SyncQueueItem, string>? ItemFailed;

    public SyncEngine(
        ISyncQueueRepository queueRepository,
        ISyncTransport transport,
        IOptions<SyncOptions> options,
        ILogger<SyncEngine> logger)
    {
        _directRepository = queueRepository;
        _transport = transport;
        _options = options.Value;
        _logger = logger;
    }

    [ActivatorUtilitiesConstructor]
    public SyncEngine(
        IServiceScopeFactory scopeFactory,
        ISyncTransport transport,
        IOptions<SyncOptions> options,
        ILogger<SyncEngine> logger)
    {
        _scopeFactory = scopeFactory;
        _transport = transport;
        _options = options.Value;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken ct = default)
    {
        if (_backgroundTask is not null)
            return;

        _logger.LogInformation("Starting offline-first SyncEngine...");

        // Restart recovery: recover any items left InProgress from previous session/crash
        await RecoverStaleInProgressItemsAsync(ct);

        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _backgroundTask = RunLoopAsync(_cts.Token);
    }

    public async Task StopAsync(CancellationToken ct = default)
    {
        if (_cts is null)
            return;

        _logger.LogInformation("Stopping SyncEngine...");
        await _cts.CancelAsync();

        if (_backgroundTask is not null)
        {
            try
            {
                await _backgroundTask;
            }
            catch (OperationCanceledException)
            {
                // Expected on cancellation
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while awaiting sync engine background loop completion.");
            }
        }

        _backgroundTask = null;
        _cts.Dispose();
        _cts = null;

        _logger.LogInformation("SyncEngine stopped.");
    }

    /// <summary>
    /// Synchronizes a single batch of eligible items from the queue.
    /// Returns the number of items successfully synced.
    /// </summary>
    public async Task<int> SyncPendingAsync(CancellationToken ct = default)
    {
        var (repository, scope) = ResolveRepository();
        try
        {
            var now = DateTimeOffset.UtcNow;
            var eligibleItems = await repository.GetEligibleForSyncAsync(now, _options.BatchSize, ct);

            if (eligibleItems.Count == 0)
            {
                return 0;
            }

            _logger.LogInformation("Processing {Count} eligible sync queue items...", eligibleItems.Count);

            // Step 1: Transition all eligible items to InProgress
            foreach (var item in eligibleItems)
            {
                item.MarkInProgress(now);
                await repository.UpdateAsync(item, ct);
            }

            // Step 2: Send batch to transport
            SyncBatchResult batchResult;
            try
            {
                batchResult = await _transport.SendBatchAsync(eligibleItems, ct);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Transport exception encountered while uploading batch.");
                batchResult = SyncBatchResult.TransportFailure(eligibleItems, ex.Message);
            }

            // Step 3: Handle results and transition states
            if (batchResult.IsTransportFailure)
            {
                IsOnline = false;
                _logger.LogWarning("Transport failed during sync. Marking system offline: {Message}", batchResult.TransportErrorMessage);

                foreach (var item in eligibleItems)
                {
                    var backoffDelay = CalculateBackoff(item.AttemptCount, _options);
                    bool isPermanent = item.AttemptCount >= _options.MaxRetryAttempts;
                    string error = batchResult.TransportErrorMessage ?? "Transport failure";

                    item.MarkFailed(error, DateTimeOffset.UtcNow, backoffDelay, isPermanent);
                    await repository.UpdateAsync(item, ct);
                    ItemFailed?.Invoke(item, error);
                }

                return 0;
            }

            // Transport succeeded -> we are online
            IsOnline = true;
            int syncedCount = 0;

            var resultsMap = batchResult.ItemResults.ToDictionary(r => r.QueueItemId);

            foreach (var item in eligibleItems)
            {
                if (resultsMap.TryGetValue(item.QueueItemId, out var itemResult) && itemResult.IsSuccess)
                {
                    item.MarkSynced(DateTimeOffset.UtcNow);
                    await repository.UpdateAsync(item, ct);
                    ItemSynced?.Invoke(item);
                    syncedCount++;
                }
                else
                {
                    var backoffDelay = CalculateBackoff(item.AttemptCount, _options);
                    bool isPermanent = (itemResult?.IsPermanentError == true) || item.AttemptCount >= _options.MaxRetryAttempts;
                    string error = itemResult?.ErrorMessage ?? "Item upload rejected";

                    item.MarkFailed(error, DateTimeOffset.UtcNow, backoffDelay, isPermanent);
                    await repository.UpdateAsync(item, ct);
                    ItemFailed?.Invoke(item, error);

                    if (isPermanent)
                    {
                        _logger.LogWarning("Item {QueueItemId} failed permanently: {Error}. Will not retry.", item.QueueItemId, error);
                    }
                    else
                    {
                        _logger.LogInformation("Item {QueueItemId} failed temporarily. Backoff delay: {Delay}s", item.QueueItemId, backoffDelay.TotalSeconds);
                    }
                }
            }

            _logger.LogInformation("Sync batch completed: {SyncedCount}/{Total} synced successfully.", syncedCount, eligibleItems.Count);
            return syncedCount;
        }
        finally
        {
            scope?.Dispose();
        }
    }

    /// <summary>
    /// Recovers any items stuck in 'InProgress' status (e.g. from a previous application crash or restart).
    /// </summary>
    public async Task<int> RecoverStaleInProgressItemsAsync(CancellationToken ct = default)
    {
        var (repository, scope) = ResolveRepository();
        try
        {
            var recoveredCount = await repository.ResetInProgressToPendingAsync(ct);
            if (recoveredCount > 0)
            {
                _logger.LogInformation("Restart recovery: reset {Count} in-progress items back to Pending.", recoveredCount);
            }
            return recoveredCount;
        }
        finally
        {
            scope?.Dispose();
        }
    }

    private async Task RunLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                // Attempt to sync pending items
                var synced = await SyncPendingAsync(ct);

                // If items were synced, check immediately if more remain without waiting full interval
                if (synced > 0)
                {
                    await Task.Delay(100, ct);
                    continue;
                }

                // If offline or queue was empty, wait the configured interval (or backoff if offline)
                var delaySeconds = IsOnline
                    ? _options.SyncIntervalSeconds
                    : Math.Min(_options.SyncIntervalSeconds * 2, _options.MaxRetryIntervalSeconds);

                await Task.Delay(TimeSpan.FromSeconds(Math.Max(1, delaySeconds)), ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in SyncEngine background loop. Continuing after backoff.");
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(_options.InitialRetryDelaySeconds), ct);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }

    public static TimeSpan CalculateBackoff(int attemptCount, SyncOptions options)
    {
        if (attemptCount <= 1)
        {
            return TimeSpan.FromSeconds(options.InitialRetryDelaySeconds);
        }

        var delaySeconds = options.InitialRetryDelaySeconds * Math.Pow(options.BackoffMultiplier, attemptCount - 1);
        var cappedSeconds = Math.Min(delaySeconds, options.MaxRetryIntervalSeconds);
        return TimeSpan.FromSeconds(cappedSeconds);
    }

    private (ISyncQueueRepository repo, IServiceScope? scope) ResolveRepository()
    {
        if (_directRepository is not null)
        {
            return (_directRepository, null);
        }

        if (_scopeFactory is not null)
        {
            var scope = _scopeFactory.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<ISyncQueueRepository>();
            return (repo, scope);
        }

        throw new InvalidOperationException("No repository or scope factory configured for SyncEngine.");
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
    }
}
