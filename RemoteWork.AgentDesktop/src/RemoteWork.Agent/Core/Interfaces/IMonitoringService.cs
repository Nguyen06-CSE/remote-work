namespace RemoteWork.Agent.Core.Interfaces;

public interface IMonitoringService
{
    Task StartAsync(
        CancellationToken cancellationToken);

    Task StopAsync(
        CancellationToken cancellationToken);
}