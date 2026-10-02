using RemoteWork.Desktop.Core.Models;

namespace RemoteWork.Desktop.Core.Interfaces;

public interface ISessionCollector : ISessionEngine
{
    new SessionInfo? EndSession();
}
