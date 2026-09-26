using RemoteWork.Desktop.Platform.Abstractions;

namespace RemoteWork.Desktop.Platform.Linux;

public sealed class LinuxInputActivityProvider : IInputActivityProvider
{
    private readonly object _lock = new();
    private int _keyboardCount;
    private int _mouseCount;
    private volatile bool _started;

    public void Start()
    {
        _started = true;
    }

    public void Stop()
    {
        _started = false;
    }

    public int GetKeyboardCount()
    {
        if (!_started)
            return 0;

        lock (_lock)
        {
            var count = _keyboardCount;
            _keyboardCount = 0;
            return count;
        }
    }

    public int GetMouseCount()
    {
        if (!_started)
            return 0;

        lock (_lock)
        {
            var count = _mouseCount;
            _mouseCount = 0;
            return count;
        }
    }

    public void Dispose()
    {
        Stop();
    }
}
