using RemoteWork.Desktop.Core.Models.Activity;
namespace RemoteWork.Desktop.Platform.Abstractions;


public interface IInputActivityProvider : IDisposable
{
    int GetKeyboardCount();

    int GetMouseCount();

    IReadOnlyList<MouseClickSample> DrainMouseSamples();

    void Start();

    void Stop();
}
