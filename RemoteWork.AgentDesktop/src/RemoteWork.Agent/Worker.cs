using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RemoteWork.Agent.Configuration;
using RemoteWork.Agent.Core.Models;
using RemoteWork.Agent;

namespace RemoteWork.Agent;

public sealed class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly AgentOptions _options;
    private readonly AgentRuntimeState _state;

    public Worker(
        ILogger<Worker> logger,
        IOptions<AgentOptions> options,
        AgentRuntimeState state)
    {
        _logger = logger;
        _options = options.Value;
        _state = state;
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