using Microsoft.Extensions.Logging;
using RemoteWork.Desktop.Core.Interfaces;
using RemoteWork.Desktop.Core.Models;

namespace RemoteWork.Desktop.Application.Collectors;

public sealed class SessionCollector : ISessionCollector
{
    private readonly ILogger<SessionCollector> _logger;
    private SessionInfo? _currentSession;

    public SessionCollector(ILogger<SessionCollector> logger)
    {
        _logger = logger;
    }

    public SessionInfo StartSession(string deviceId)
    {
        if (_currentSession is not null)
        {
            throw new InvalidOperationException("A session is already active.");
        }

        var session = new SessionInfo
        {
            SessionId = Guid.NewGuid().ToString(),
            DeviceId = deviceId,
            StartedAt = DateTimeOffset.UtcNow
        };

        session.MarkActive();
        _currentSession = session;

        _logger.LogInformation(
            "Session started. SessionId: {SessionId}, DeviceId: {DeviceId}",
            session.SessionId,
            session.DeviceId);

        return session;
    }

    public void EndSession()
    {
        if (_currentSession is null)
        {
            _logger.LogWarning("Cannot end session because no active session exists.");
            return;
        }

        _currentSession.MarkEnding();

        _logger.LogInformation(
            "Ending session {SessionId}.",
            _currentSession.SessionId);

        _currentSession.MarkEnded();

        _logger.LogInformation(
            "Session ended. SessionId: {SessionId}, Duration: {Duration}",
            _currentSession.SessionId,
            _currentSession.Duration);

        _currentSession = null;
    }

    public SessionInfo? GetCurrentSession()
    {
        return _currentSession;
    }
}
