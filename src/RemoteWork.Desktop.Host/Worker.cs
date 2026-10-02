using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RemoteWork.Desktop.Application.Collectors;
using RemoteWork.Desktop.Application.Monitoring;
using RemoteWork.Desktop.Application.Sync;
using RemoteWork.Desktop.Core.Interfaces;
using RemoteWork.Desktop.Core.Models;
using RemoteWork.Desktop.Infrastructure.Configuration;

namespace RemoteWork.Desktop.Host;

public sealed class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly AgentOptions _options;
    private readonly AgentRuntimeState _state;
    private readonly DeviceCollector _deviceCollector;
    private readonly ISessionCollector _sessionCollector;
    private readonly MonitoringService _monitoringService;
    private readonly ISyncEngine _syncEngine;
    private readonly IServiceScopeFactory _scopeFactory;

    public Worker(
        ILogger<Worker> logger,
        IOptions<AgentOptions> options,
        AgentRuntimeState state,
        DeviceCollector deviceCollector,
        ISessionCollector sessionCollector,
        MonitoringService monitoringService,
        ISyncEngine syncEngine,
        IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _options = options.Value;
        _state = state;
        _deviceCollector = deviceCollector;
        _sessionCollector = sessionCollector;
        _monitoringService = monitoringService;
        _syncEngine = syncEngine;
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("RemoteWork Desktop Host starting...");
        _logger.LogInformation("Agent version: {Version}", _options.AgentVersion);
        _logger.LogInformation("Environment: {Environment}", _options.Environment);
        _logger.LogInformation("Backend: {Backend}", _options.BackendBaseUrl);

        try
        {
            _state.MarkStarting();

            // 1. Thu thập thông tin thiết bị
            var device = _deviceCollector.Collect(_options.AgentVersion);

            _logger.LogInformation("Device ID: {DeviceId}", device.DeviceId);
            _logger.LogInformation("Hostname: {Hostname}", device.Hostname);
            _logger.LogInformation("Operating System: {OperatingSystem}", device.OperatingSystem);
            _logger.LogInformation("OS Version: {OsVersion}", device.OsVersion);
            _logger.LogInformation("Agent Version: {AgentVersion}", device.AgentVersion);

            // 2. Bắt đầu session
            var session = _sessionCollector.StartSession(device.DeviceId);
            _logger.LogInformation("Current session: {SessionId}", session.SessionId);

            // Persist Device and Session to SQLite so that foreign keys in ActivityBatches are satisfied
            using (var scope = _scopeFactory.CreateScope())
            {
                var deviceRepo = scope.ServiceProvider.GetRequiredService<IDeviceRepository>();
                var sessionRepo = scope.ServiceProvider.GetRequiredService<ISessionRepository>();
                await deviceRepo.UpsertAsync(device, stoppingToken);
                await sessionRepo.SaveAsync(session, stoppingToken);
                _logger.LogInformation("Persisted initial Device {DeviceId} and Session {SessionId} to SQLite.", device.DeviceId, session.SessionId);
            }

            // 3. Chuyển state sang Running
            _state.MarkRunning();
            _logger.LogInformation("Agent status: {Status}", _state.Status);

            // 4. Kết nối pipeline: Collector -> SQLite -> SyncQueue
            _monitoringService.OnBatchGenerated += batch =>
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        using var scope = _scopeFactory.CreateScope();
                        var coordinator = scope.ServiceProvider.GetRequiredService<TrackingPersistenceCoordinator>();
                        await coordinator.PersistAndEnqueueBatchAsync(batch);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to persist and queue ActivityBatch {BatchId}.", batch.BatchId);
                    }
                });
            };

            // 5. Khởi động MonitoringService và SyncEngine
            await _monitoringService.StartAsync(stoppingToken);
            await _syncEngine.StartAsync(stoppingToken);

            // 6. Giữ Worker chạy cho tới khi có tín hiệu shutdown
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Shutdown requested.");
        }
        catch (Exception ex)
        {
            _state.MarkError();
            _logger.LogError(ex, "Agent execution failed.");
            throw;
        }
        finally
        {
            _state.MarkStopping();
            _logger.LogInformation("Agent status: {Status}", _state.Status);

            await _syncEngine.StopAsync(CancellationToken.None);
            await _monitoringService.StopAsync(CancellationToken.None);
            var endedSession = _sessionCollector.EndSession();

            if (endedSession != null)
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var sessionRepo = scope.ServiceProvider.GetRequiredService<ISessionRepository>();
                    await sessionRepo.UpdateAsync(endedSession, CancellationToken.None);
                    _logger.LogInformation("Updated ended Session {SessionId} in SQLite.", endedSession.SessionId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to update ended Session {SessionId} in SQLite.", endedSession.SessionId);
                }
            }

            _state.MarkStopped();
            _logger.LogInformation("Agent status: {Status}", _state.Status);
            _logger.LogInformation("RemoteWork Desktop Host stopped.");
        }
    }
}
