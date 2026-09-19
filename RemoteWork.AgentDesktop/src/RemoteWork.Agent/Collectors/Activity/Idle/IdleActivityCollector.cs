using Microsoft.Extensions.Options;
using RemoteWork.Agent.Configuration;
using RemoteWork.Agent.Platform.Windows.Input;

namespace RemoteWork.Agent.Collectors.Activity.Idle;

public sealed class IdleActivityCollector
{
    private readonly WindowsInputActivityProvider _inputProvider;
    private readonly AgentOptions _options;

    public IdleActivityCollector(
        WindowsInputActivityProvider inputProvider,
        IOptions<AgentOptions> options)
    {
        _inputProvider = inputProvider;
        _options = options.Value;
    }

    public bool IsUserActive()
    {
        var idleTime =
            _inputProvider.GetIdleTime();

        return idleTime.TotalSeconds <
               _options.IdleThresholdSeconds;
    }
}