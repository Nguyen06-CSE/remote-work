namespace RemoteWork.Desktop.Core.Interfaces;

public interface IMonitoringService
{
    Task StartAsync(CancellationToken cancellationToken);

    Task StopAsync(CancellationToken cancellationToken);
}
