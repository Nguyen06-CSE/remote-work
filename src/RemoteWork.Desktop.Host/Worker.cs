using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RemoteWork.Desktop.Application.Collectors;
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
    private readonly IMonitoringService _monitoringService;

    public Worker(
        ILogger<Worker> logger,
        IOptions<AgentOptions> options,
        AgentRuntimeState state,
        DeviceCollector deviceCollector,
        ISessionCollector sessionCollector,
        IMonitoringService monitoringService)
    {
        _logger = logger;
        _options = options.Value;
        _state = state;
        _deviceCollector = deviceCollector;
        _sessionCollector = sessionCollector;
        _monitoringService = monitoringService;
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

            // 3. Chuyển state sang Running
            _state.MarkRunning();
            _logger.LogInformation("Agent status: {Status}", _state.Status);

            // 4. Khởi động MonitoringService
            await _monitoringService.StartAsync(stoppingToken);

            // 5. Giữ Worker chạy cho tới khi có tín hiệu shutdown
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

            await _monitoringService.StopAsync(CancellationToken.None);
            _sessionCollector.EndSession();

            _state.MarkStopped();
            _logger.LogInformation("Agent status: {Status}", _state.Status);
            _logger.LogInformation("RemoteWork Desktop Host stopped.");
        }
    }
}
