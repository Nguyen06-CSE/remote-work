// src/RemoteWork.Agent/Monitoring/MonitoringService.cs

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RemoteWork.Agent.Configuration;
using RemoteWork.Agent.Core.Interfaces;

namespace RemoteWork.Agent.Monitoring;

public sealed class MonitoringService : IMonitoringService
{
    private readonly IActivityCollector _activityCollector;
    private readonly IInputActivityProvider _inputProvider;
    private readonly AgentOptions _options;
    private readonly ILogger<MonitoringService> _logger;

    private Task? _monitoringTask;
    private CancellationTokenSource? _internalCts;

    public MonitoringService(
        IActivityCollector activityCollector,
        IInputActivityProvider inputProvider,
        IOptions<AgentOptions> options,
        ILogger<MonitoringService> logger)
    {
        _activityCollector = activityCollector;
        _inputProvider = inputProvider;
        _options = options.Value;
        _logger = logger;
    }

    public Task StartAsync(
        CancellationToken cancellationToken)
    {
        if (_monitoringTask is not null)
            return Task.CompletedTask;

        _inputProvider.Start();

        _internalCts =
            CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken);

        _monitoringTask =
            RunAsync(_internalCts.Token);

        return Task.CompletedTask;
    }

    public async Task StopAsync(
        CancellationToken cancellationToken)
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
                // Expected during shutdown.
            }
        }

        _monitoringTask = null;

        _internalCts.Dispose();
        _internalCts = null;

        _inputProvider.Stop();
    }

    private async Task RunAsync(
        CancellationToken cancellationToken)
    {
        using var timer =
            new PeriodicTimer(
                TimeSpan.FromSeconds(
                    _options.ActivitySamplingIntervalSeconds));

        var lastBatchTime =
            DateTimeOffset.UtcNow;

        while (await timer.WaitForNextTickAsync(
                   cancellationToken))
        {
            try
            {
                var events =
                    _activityCollector.Collect();

                foreach (var activityEvent in events)
                {
                    _logger.LogInformation(
                        "Activity: {Type} | Session: {SessionId} | Count: {Count} | Active: {Active}",
                        activityEvent.Type,
                        activityEvent.SessionId,
                        activityEvent.Count,
                        activityEvent.IsActive);
                }

                var elapsed =
                    DateTimeOffset.UtcNow -
                    lastBatchTime;

                if (elapsed.TotalSeconds >=
                    _options.ActivityBatchIntervalSeconds)
                {
                    // Dùng interface (đã có FlushBatch theo mục 25)
                    var batch =
                        _activityCollector.FlushBatch();

                    _logger.LogInformation(
                        "Activity batch created. " +
                        "BatchId={BatchId}, " +
                        "Keyboard={Keyboard}, " +
                        "Mouse={Mouse}, " +
                        "Active={Active}, " +
                        "Idle={Idle}",
                        batch.BatchId,
                        batch.KeyboardCount,
                        batch.MouseCount,
                        batch.ActiveDuration,
                        batch.IdleDuration);

                    lastBatchTime =
                        DateTimeOffset.UtcNow;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error while collecting activity.");
            }
        }
    }
}