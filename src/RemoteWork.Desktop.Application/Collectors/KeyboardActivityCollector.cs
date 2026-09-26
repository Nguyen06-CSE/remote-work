using RemoteWork.Desktop.Core.Interfaces;
using RemoteWork.Desktop.Platform.Abstractions;

namespace RemoteWork.Desktop.Application.Collectors;

public sealed class KeyboardActivityCollector : IKeyboardActivityCollector
{
    private readonly IInputActivityProvider _inputProvider;

    public KeyboardActivityCollector(IInputActivityProvider inputProvider)
    {
        _inputProvider = inputProvider;
    }

    public int Collect()
    {
        return _inputProvider.GetKeyboardCount();
    }
}
