namespace RemoteWork.Desktop.Platform.Abstractions;

public interface IInputActivityProvider : IDisposable
{
    int GetKeyboardCount();

    int GetMouseCount();

    void Start();

    void Stop();
}
