using Microsoft.Extensions.Logging;
using RemoteWork.Desktop.Core.Interfaces;
using RemoteWork.Desktop.Core.Models.Activity;
using RemoteWork.Desktop.Platform.Abstractions;

namespace RemoteWork.Desktop.Application.Monitoring;

/// <summary>
/// Background runtime orchestrating periodic activity sampling, batch aggregation, and provider lifecycles.
/// Responsibilities:
/// - Coordinates ActivityCollector, Session, and native platform providers.
/// - Controls explicit Start / Stop lifecycles with CancellationToken support.
/// - Graceful Shutdown: flushes pending activity upon termination so no sampled work is lost.
/// - Emits OnBatchGenerated whenever an ActivityBatch is created.
/// - Error Isolation: Exceptions during individual ticks are logged without crashing the host process.
/// </summary>
public sealed class MonitoringService : IMonitoringService
{
    private readonly IActivityCollector _activityCollector;
    private readonly IInputActivityProvider _inputProvider;
    private readonly ISessionCollector _sessionCollector;
    private readonly int _samplingIntervalSeconds;
    private readonly int _batchIntervalSeconds;
    private readonly ILogger<MonitoringService> _logger;

    private Task? _monitoringTask;
    private CancellationTokenSource? _internalCts;

    public event Action<ActivityBatch>? OnBatchGenerated;

    public MonitoringService(
        IActivityCollector activityCollector,
        IInputActivityProvider inputProvider,
        ISessionCollector sessionCollector,
        int samplingIntervalSeconds,
        int batchIntervalSeconds,
        ILogger<MonitoringService> logger)
    {
        _activityCollector = activityCollector;
        _inputProvider = inputProvider;
        _sessionCollector = sessionCollector;
        _samplingIntervalSeconds = samplingIntervalSeconds > 0 ? samplingIntervalSeconds : 10;
        _batchIntervalSeconds = batchIntervalSeconds > 0 ? batchIntervalSeconds : 60;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (_monitoringTask is not null)
            return Task.CompletedTask;

        try
        {
            _inputProvider.Start();
            _logger.LogInformation("Input activity provider started.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start input activity provider.");
        }

        _internalCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _monitoringTask = RunAsync(_internalCts.Token);

        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_internalCts is null)
            return;

        await _internalCts.CancelAsync();

        if (_monitoringTask is not null)
        {
            try
            {
                await _monitoringTask;
            }
            catch (OperationCanceledException)
            {
                // Expected when canceling
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while awaiting monitoring background loop termination.");
            }
        }

        // Graceful shutdown: flush any pending aggregated activity
        try
        {
            var session = _sessionCollector.GetCurrentSession();
            if (session is not null)
            {
                _activityCollector.Collect(); // Final sample
                var finalBatch = _activityCollector.FlushBatch();

                _logger.LogInformation(
                    "Final activity batch flushed on shutdown. BatchId={BatchId}, Keyboard={Keyboard}, Mouse={Mouse}, Active={Active}, Idle={Idle}",
                    finalBatch.BatchId,
                    finalBatch.KeyboardCount,
                    finalBatch.MouseCount,
                    finalBatch.ActiveDuration,
                    finalBatch.IdleDuration);

                OnBatchGenerated?.Invoke(finalBatch);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to flush final activity batch during graceful shutdown.");
        }

        try
        {
            _inputProvider.Stop();
            _logger.LogInformation("Input activity provider stopped.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while stopping input activity provider.");
        }

        _monitoringTask = null;
        _internalCts.Dispose();
        _internalCts = null;
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(_samplingIntervalSeconds));
        var lastBatchTime = DateTimeOffset.UtcNow;

        while (await timer.WaitForNextTickAsync(cancellationToken))
        {
            try
            {
                var session = _sessionCollector.GetCurrentSession();
                if (session is null)
                {
                    _logger.LogTrace("Skipping activity collection tick: no active session.");
                    continue;
                }

                var events = _activityCollector.Collect();

                foreach (var activityEvent in events)
                {
                    _logger.LogInformation(
                        "Activity: {Type} | Session: {SessionId} | Count: {Count} | Active: {Active}",
                        activityEvent.Type,
                        activityEvent.SessionId,
                        activityEvent.Count,
                        activityEvent.IsActive);
                }

                var elapsed = DateTimeOffset.UtcNow - lastBatchTime;

                if (elapsed.TotalSeconds >= _batchIntervalSeconds)
                {
                    var batch = _activityCollector.FlushBatch();

                    _logger.LogInformation(
                        "Activity batch created. BatchId={BatchId}, Keyboard={Keyboard}, Mouse={Mouse}, Active={Active}, Idle={Idle}",
                        batch.BatchId,
                        batch.KeyboardCount,
                        batch.MouseCount,
                        batch.ActiveDuration,
                        batch.IdleDuration);

                    OnBatchGenerated?.Invoke(batch);

                    lastBatchTime = DateTimeOffset.UtcNow;
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while collecting activity.");
            }
        }
    }
}
