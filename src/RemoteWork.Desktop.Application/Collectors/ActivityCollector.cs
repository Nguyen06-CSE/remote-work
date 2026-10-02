using Microsoft.Extensions.Logging;
using RemoteWork.Desktop.Core.Enums;
using RemoteWork.Desktop.Core.Interfaces;
using RemoteWork.Desktop.Core.Models.Activity;

namespace RemoteWork.Desktop.Application.Collectors;

/// <summary>
/// Orchestrates input and idle activity sampling, state transition detection, and aggregation.
/// Principles:
/// - Error Isolation: Failure in one collector (keyboard, mouse, or idle) does not crash the pipeline.
/// - Only emits ActivityStateChanged when the state changes (Active <-> Idle).
/// - Never emits redundant Active state events every cycle.
/// - Accumulates keyboard and mouse counts into an ActivityAccumulator.
/// - Flushes periodic ActivityBatch objects representing aggregated intervals.
/// - Guarantees ActiveDuration + IdleDuration is approximately equal to EndedAt - StartedAt.
/// </summary>
public sealed class ActivityCollector : IActivityCollector
{
    private readonly ISessionCollector _sessionCollector;
    private readonly IIdleActivityCollector _idleCollector;
    private readonly IKeyboardActivityCollector _keyboardCollector;
    private readonly IMouseActivityCollector _mouseCollector;
    private readonly IMouseBotDetector _mouseBotDetector;
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
        IMouseBotDetector mouseBotDetector,
        ILogger<ActivityCollector> logger)
    {
        _sessionCollector = sessionCollector;
        _idleCollector = idleCollector;
        _keyboardCollector = keyboardCollector;
        _mouseCollector = mouseCollector;
        _mouseBotDetector = mouseBotDetector;
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

        // 1. Error-isolated idle collection
        var currentIsActive = true;
        try
        {
            currentIsActive = _idleCollector.IsUserActive();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to collect idle status from idle provider. Assuming active as safe fallback.");
        }

        var currentActivityState = currentIsActive ? ActivityState.Active : ActivityState.Idle;

        if (_lastTimestamp is null)
        {
            _lastTimestamp = now;
        }
        else
        {
            var stateForElapsed = _previousState ?? currentActivityState;
            UpdateDuration(stateForElapsed, now);
        }

        var events = new List<ActivityEvent>();

        // Only emit state changed when state transitions between Active and Idle
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

        // 2. Error-isolated keyboard collection
        var keyboardCount = 0;
        try
        {
            keyboardCount = _keyboardCollector.Collect();
            _accumulator.AddKeyboard(keyboardCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to collect keyboard activity. Continuing with other collectors.");
        }

        // 3. Error-isolated mouse collection
        var mouseCount = 0;
        try
        {
            mouseCount = _mouseCollector.Collect();
            _accumulator.AddMouse(mouseCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to collect mouse activity. Continuing with other collectors.");
        }

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

        // 4. Error-isolated ephemeral bot detection
        try
        {
            var mouseSamples = _mouseCollector.DrainSamples();
            if (mouseSamples.Count > 0)
            {
                var detectionResult = _mouseBotDetector.Analyze(mouseSamples);
                if (detectionResult.IsSuspicious)
                {
                    _accumulator.MarkSuspiciousMouseActivity();
                    events.Add(new ActivityEvent
                    {
                        EventId = Guid.NewGuid().ToString(),
                        DeviceId = session.DeviceId,
                        SessionId = session.SessionId,
                        Timestamp = detectionResult.Timestamp,
                        Type = ActivityEventType.SuspiciousActivityDetected
                    });
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process mouse samples for bot detection.");
        }

        return events;
    }

    public ActivityBatch FlushBatch()
    {
        var session = _sessionCollector.GetCurrentSession()
            ?? throw new InvalidOperationException("No active session.");

        var now = DateTimeOffset.UtcNow;
        var startedAt = _batchStartedAt ?? now;

        // If time has elapsed since the last sampled timestamp, attribute it to current state
        if (_lastTimestamp is not null && now > _lastTimestamp.Value)
        {
            var currentState = _previousState ?? ActivityState.Active;
            UpdateDuration(currentState, now);
        }

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
            IdleDuration = _accumulator.IdleDuration,
            HasSuspiciousMouseActivity = _accumulator.HasSuspiciousMouseActivity
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
        if (elapsed < TimeSpan.Zero)
            elapsed = TimeSpan.Zero;

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