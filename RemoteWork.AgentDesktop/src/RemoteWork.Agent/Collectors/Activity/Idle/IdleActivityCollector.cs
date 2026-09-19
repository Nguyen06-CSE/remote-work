using Microsoft.Extensions.Options;
using RemoteWork.Agent.Configuration;
using RemoteWork.Agent.Platform.Windows.Input;

namespace RemoteWork.Agent.Collectors.Activity.Idle;

public sealed class IdleActivityCollector
{
    private readonly WindowsIdleTimeProvider _idleProvider;
    private readonly AgentOptions _options;

    public IdleActivityCollector(
        WindowsIdleTimeProvider idleProvider,
        IOptions<AgentOptions> options)
    {
        _idleProvider = idleProvider;
        _options = options.Value;
    }

    public bool IsUserActive()
    {
        var idleTime =
            _idleProvider.GetIdleTime();

        return idleTime.TotalSeconds <
               _options.IdleThresholdSeconds;
    }
}