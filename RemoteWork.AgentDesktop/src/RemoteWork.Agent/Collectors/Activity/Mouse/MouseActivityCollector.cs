using RemoteWork.Agent.Core.Interfaces;

namespace RemoteWork.Agent.Collectors.Activity.Mouse;

public sealed class MouseActivityCollector
{
    private readonly IInputActivityProvider _inputProvider;

    public MouseActivityCollector(
        IInputActivityProvider inputProvider)
    {
        _inputProvider = inputProvider;
    }

    public int Collect()
    {
        return _inputProvider.GetMouseCount();
    }
}