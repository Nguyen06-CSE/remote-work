using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RemoteWork.Agent.Configuration;
using RemoteWork.Agent.Core.Models;
using RemoteWork.Agent;
using RemoteWork.Agent.Collectors.Device;
using RemoteWork.Agent.Core.Interfaces;

namespace RemoteWork.Agent;

public sealed class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly AgentOptions _options;
    private readonly AgentRuntimeState _state;
    private readonly DeviceCollector _deviceCollector;

    private readonly ISessionCollector _sessionCollector;

    public Worker(
    ILogger<Worker> logger,
    IOptions<AgentOptions> options,
    AgentRuntimeState state,
    DeviceCollector deviceCollector,
    ISessionCollector sessionCollector)
    {
        _logger = logger;
        _options = options.Value;
        _state = state;
        _deviceCollector = deviceCollector;
        _sessionCollector = sessionCollector;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "RemoteWork Agent starting...");

        _logger.LogInformation(
            "Agent version: {Version}",
            _options.AgentVersion);

        _logger.LogInformation(
            "Environment: {Environment}",
            _options.Environment);

        _logger.LogInformation(
            "Backend: {Backend}",
            _options.BackendBaseUrl);

        _state.MarkRunning();

        var deviceInfo = _deviceCollector.Collect();

        var session = _sessionCollector.StartSession(
    deviceInfo.DeviceId);

        _logger.LogInformation(
            "Current session: {SessionId}",
            session.SessionId);

        _logger.LogInformation(
            "Device ID: {DeviceId}",
            deviceInfo.DeviceId);

        _logger.LogInformation(
            "Hostname: {Hostname}",
            deviceInfo.Hostname);

        _logger.LogInformation(
            "Operating System: {OperatingSystem}",
            deviceInfo.OperatingSystem);

        _logger.LogInformation(
            "OS Version: {OsVersion}",
            deviceInfo.OsVersion);

        _logger.LogInformation(
            "Agent Version: {AgentVersion}",
            deviceInfo.AgentVersion);


        _logger.LogInformation(
            "Agent status: {Status}",
            _state.Status);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogDebug(
                    "Agent heartbeat at {Time}",
                    DateTimeOffset.UtcNow);

                await Task.Delay(
                    TimeSpan.FromSeconds(30),
                    stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation(
                "Shutdown requested.");
        }
        catch (Exception ex)
        {
            _state.MarkError();

            _logger.LogError(
                ex,
                "Agent encountered an unexpected error.");

            throw;
        }
        finally
        {
            _state.MarkStopping();

            _sessionCollector.EndSession();

            _logger.LogInformation(
                "Agent status: {Status}",
                _state.Status);

            _state.MarkStopped();

            _logger.LogInformation(
                "Agent status: {Status}",
                _state.Status);

            _logger.LogInformation(
                "RemoteWork Agent stopped.");
        }
    }
}