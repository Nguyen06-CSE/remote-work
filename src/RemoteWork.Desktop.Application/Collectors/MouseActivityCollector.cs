using RemoteWork.Desktop.Core.Interfaces;
using RemoteWork.Desktop.Platform.Abstractions;

namespace RemoteWork.Desktop.Application.Collectors;

public sealed class MouseActivityCollector : IMouseActivityCollector
{
    private readonly IInputActivityProvider _inputProvider;

    public MouseActivityCollector(IInputActivityProvider inputProvider)
    {
        _inputProvider = inputProvider;
    }

    public int Collect()
    {
        return _inputProvider.GetMouseCount();
    }
}
