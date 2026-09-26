using Microsoft.Extensions.Logging;
using RemoteWork.Desktop.Core.Interfaces;
using RemoteWork.Desktop.Platform.Abstractions;

namespace RemoteWork.Desktop.Application.Monitoring;

public sealed class MonitoringService : IMonitoringService
{
    private readonly IActivityCollector _activityCollector;
    private readonly IInputActivityProvider _inputProvider;
    private readonly int _samplingIntervalSeconds;
    private readonly int _batchIntervalSeconds;
    private readonly ILogger<MonitoringService> _logger;

    private Task? _monitoringTask;
    private CancellationTokenSource? _internalCts;

    public MonitoringService(
        IActivityCollector activityCollector,
        IInputActivityProvider inputProvider,
        int samplingIntervalSeconds,
        int batchIntervalSeconds,
        ILogger<MonitoringService> logger)
    {
        _activityCollector = activityCollector;
        _inputProvider = inputProvider;
        _samplingIntervalSeconds = samplingIntervalSeconds > 0 ? samplingIntervalSeconds : 10;
        _batchIntervalSeconds = batchIntervalSeconds > 0 ? batchIntervalSeconds : 60;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (_monitoringTask is not null)
            return Task.CompletedTask;

        _inputProvider.Start();
        _logger.LogInformation("Input hooks installed.");

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
        }

        _inputProvider.Stop();
        _logger.LogInformation("Input hooks removed.");

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
