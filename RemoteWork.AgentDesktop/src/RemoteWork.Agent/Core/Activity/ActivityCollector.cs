using Microsoft.Extensions.Logging;
using RemoteWork.Agent.Core.Interfaces;
using RemoteWork.Agent.Core.Models.Activity;

namespace RemoteWork.Agent.Collectors.Activity;

public sealed class ActivityCollector : IActivityCollector
{
    private readonly ISessionCollector _sessionCollector;
    private readonly ILogger<ActivityCollector> _logger;

    private ActivityState? _previousState;

    public ActivityCollector(
        ISessionCollector sessionCollector,
        ILogger<ActivityCollector> logger)
    {
        _sessionCollector = sessionCollector;
        _logger = logger;
    }

    public IReadOnlyList<ActivityEvent> Collect()
    {
        var session =
            _sessionCollector.GetCurrentSession();

        if (session is null)
        {
            _logger.LogWarning(
                "Cannot collect activity because there is no active session.");

            return [];
        }

        return [];
    }
}