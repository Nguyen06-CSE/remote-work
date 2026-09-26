using RemoteWork.Desktop.Core.Models;

namespace RemoteWork.Desktop.Core.Interfaces;

public interface ISessionCollector
{
    SessionInfo StartSession(string deviceId);

    void EndSession();

    SessionInfo? GetCurrentSession();
}
