using Microsoft.Extensions.Logging;
using RemoteWork.Desktop.Core.Enums;
using RemoteWork.Desktop.Core.Interfaces;
using RemoteWork.Desktop.Core.Models;

namespace RemoteWork.Desktop.Application.Collectors;

public class SessionEngine : ISessionEngine, ISessionCollector
{
    private readonly ILogger _logger;
    private readonly object _syncLock = new();
    private SessionInfo? _currentSession;

    public SessionEngine(ILogger<SessionEngine> logger)
    {
        _logger = logger;
    }

    protected SessionEngine(ILogger logger)
    {
        _logger = logger;
    }

    public SessionInfo StartSession(string deviceId)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            throw new ArgumentException("DeviceId cannot be null or whitespace.", nameof(deviceId));
        }

        lock (_syncLock)
        {
            if (_currentSession is not null &&
                (_currentSession.Status == SessionStatus.Starting || _currentSession.Status == SessionStatus.Active))
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
                "Session started. SessionId: {SessionId}, DeviceId: {DeviceId}, StartedAt: {StartedAt}",
                session.SessionId,
                session.DeviceId,
                session.StartedAt);

            return session;
        }
    }

    public SessionInfo? EndSession()
    {
        lock (_syncLock)
        {
            if (_currentSession is null)
            {
                _logger.LogWarning("Cannot end session because no active session exists.");
                return null;
            }

            var session = _currentSession;

            if (session.Status == SessionStatus.Ended)
            {
                _logger.LogWarning("Session {SessionId} is already ended.", session.SessionId);
                _currentSession = null;
                return session;
            }

            session.MarkEnding();

            _logger.LogInformation("Ending session {SessionId}.", session.SessionId);

            session.MarkEnded();

            _logger.LogInformation(
                "Session ended. SessionId: {SessionId}, Duration: {Duration}, EndedAt: {EndedAt}",
                session.SessionId,
                session.Duration,
                session.EndedAt);

            _currentSession = null;
            return session;
        }
    }

    public SessionInfo? GetCurrentSession()
    {
        lock (_syncLock)
        {
            return _currentSession;
        }
    }
}
