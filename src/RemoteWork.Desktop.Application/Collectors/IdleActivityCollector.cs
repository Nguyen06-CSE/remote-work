using RemoteWork.Desktop.Core.Interfaces;
using RemoteWork.Desktop.Platform.Abstractions;

namespace RemoteWork.Desktop.Application.Collectors;

public sealed class IdleActivityCollector : IIdleActivityCollector
{
    private readonly IIdleProvider _idleProvider;
    private readonly int _idleThresholdSeconds;

    public IdleActivityCollector(
        IIdleProvider idleProvider,
        int idleThresholdSeconds = 300)
    {
        _idleProvider = idleProvider;
        _idleThresholdSeconds = idleThresholdSeconds;
    }

    public bool IsUserActive()
    {
        var idleTime = _idleProvider.GetIdleTime();
        return idleTime.TotalSeconds < _idleThresholdSeconds;
    }
}
