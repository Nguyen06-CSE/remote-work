namespace RemoteWork.Agent.Core.Interfaces;

public interface IInputActivityProvider : IDisposable
{
    int GetKeyboardCount();

    int GetMouseCount();

    void Start();

    void Stop();
}