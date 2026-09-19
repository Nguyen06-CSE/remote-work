using RemoteWork.Agent.Core.Interfaces;

namespace RemoteWork.Agent.Collectors.Activity.Keyboard;

public sealed class KeyboardActivityCollector
{
    private readonly IInputActivityProvider _inputProvider;

    public KeyboardActivityCollector(
        IInputActivityProvider inputProvider)
    {
        _inputProvider = inputProvider;
    }

    public int Collect()
    {
        return _inputProvider.GetKeyboardCount();
    }
}