using Microsoft.Extensions.Logging;

namespace RemoteWork.Desktop.Application.Collectors;

public class SessionCollector : SessionEngine
{
    public SessionCollector(ILogger<SessionCollector> logger)
        : base(logger)
    {
    }
}
