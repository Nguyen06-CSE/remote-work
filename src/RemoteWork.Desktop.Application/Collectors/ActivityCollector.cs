using Microsoft.Extensions.Logging;
using RemoteWork.Desktop.Core.Enums;
using RemoteWork.Desktop.Core.Interfaces;
using RemoteWork.Desktop.Core.Models.Activity;

namespace RemoteWork.Desktop.Application.Collectors;

public sealed class ActivityCollector : IActivityCollector
{
    private readonly ISessionCollector _sessionCollector;
    private readonly IIdleActivityCollector _idleCollector;
    private readonly IKeyboardActivityCollector _keyboardCollector;
    private readonly IMouseActivityCollector _mouseCollector;
    private readonly ILogger<ActivityCollector> _logger;

    private readonly ActivityAccumulator _accumulator = new();

    private ActivityState? _previousState;
    private DateTimeOffset? _batchStartedAt;
    private DateTimeOffset? _lastTimestamp;

    public ActivityCollector(
        ISessionCollector sessionCollector,
        IIdleActivityCollector idleCollector,
        IKeyboardActivityCollector keyboardCollector,
        IMouseActivityCollector mouseCollector,
        ILogger<ActivityCollector> logger)
    {
        _sessionCollector = sessionCollector;
        _idleCollector = idleCollector;
        _keyboardCollector = keyboardCollector;
        _mouseCollector = mouseCollector;
        _logger = logger;
    }

    public IReadOnlyList<ActivityEvent> Collect()
    {
        var session = _sessionCollector.GetCurrentSession();

        if (session is null)
        {
            _logger.LogWarning("Cannot collect activity because no session is active.");
            return [];
        }

        var now = DateTimeOffset.UtcNow;
        _batchStartedAt ??= now;

        if (_lastTimestamp is null)
        {
            _lastTimestamp = now;
        }
        else
        {
            var isActive = _idleCollector.IsUserActive();
            var currentState = isActive ? ActivityState.Active : ActivityState.Idle;
            UpdateDuration(currentState, now);
        }

        var currentIsActive = _idleCollector.IsUserActive();
        var currentActivityState = currentIsActive ? ActivityState.Active : ActivityState.Idle;

        var events = new List<ActivityEvent>();

        if (_previousState != currentActivityState)
        {
            events.Add(new ActivityEvent
            {
                EventId = Guid.NewGuid().ToString(),
                DeviceId = session.DeviceId,
                SessionId = session.SessionId,
                Timestamp = now,
                Type = ActivityEventType.ActivityStateChanged,
                IsActive = currentIsActive
            });

            _previousState = currentActivityState;
        }

        var keyboardCount = _keyboardCollector.Collect();
        var mouseCount = _mouseCollector.Collect();

        _accumulator.AddKeyboard(keyboardCount);
        _accumulator.AddMouse(mouseCount);

        if (keyboardCount > 0)
        {
            events.Add(new ActivityEvent
            {
                EventId = Guid.NewGuid().ToString(),
                DeviceId = session.DeviceId,
                SessionId = session.SessionId,
                Timestamp = now,
                Type = ActivityEventType.KeyboardActivity,
                Count = keyboardCount
            });
        }

        if (mouseCount > 0)
        {
            events.Add(new ActivityEvent
            {
                EventId = Guid.NewGuid().ToString(),
                DeviceId = session.DeviceId,
                SessionId = session.SessionId,
                Timestamp = now,
                Type = ActivityEventType.MouseActivity,
                Count = mouseCount
            });
        }

        return events;
    }

    public ActivityBatch FlushBatch()
    {
        var session = _sessionCollector.GetCurrentSession()
            ?? throw new InvalidOperationException("No active session.");

        var now = DateTimeOffset.UtcNow;
        var startedAt = _batchStartedAt ?? now;

        var batch = new ActivityBatch
        {
            BatchId = Guid.NewGuid().ToString(),
            DeviceId = session.DeviceId,
            SessionId = session.SessionId,
            StartedAt = startedAt,
            EndedAt = now,
            KeyboardCount = _accumulator.KeyboardCount,
            MouseCount = _accumulator.MouseCount,
            ActiveDuration = _accumulator.ActiveDuration,
            IdleDuration = _accumulator.IdleDuration
        };

        _accumulator.Reset();
        _batchStartedAt = now;
        _lastTimestamp = now;

        return batch;
    }

    private void UpdateDuration(ActivityState currentState, DateTimeOffset now)
    {
        if (_lastTimestamp is null)
            return;

        var elapsed = now - _lastTimestamp.Value;

        if (currentState == ActivityState.Active)
        {
            _accumulator.AddActiveDuration(elapsed);
        }
        else
        {
            _accumulator.AddIdleDuration(elapsed);
        }

        _lastTimestamp = now;
    }
}
