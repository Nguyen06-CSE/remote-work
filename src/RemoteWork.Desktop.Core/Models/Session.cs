using RemoteWork.Desktop.Core.Enums;

namespace RemoteWork.Desktop.Core.Models;

public class Session
{
    public required string SessionId { get; init; }

    public required string DeviceId { get; init; }

    public DateTimeOffset StartedAt { get; init; }

    public DateTimeOffset? EndedAt { get; protected set; }

    public SessionStatus Status { get; protected set; } = SessionStatus.Starting;

public TimeSpan? Duration =>
        EndedAt.HasValue
            ? (EndedAt.Value >= StartedAt ? EndedAt.Value - StartedAt : TimeSpan.Zero)
            : null;

    public void MarkActive()
    {
        EnsureCanTransitionTo(SessionStatus.Active);
        Status = SessionStatus.Active;
    }

    public void MarkEnding()
    {
        EnsureCanTransitionTo(SessionStatus.Ending);
        Status = SessionStatus.Ending;
    }

    public void MarkEnded(DateTimeOffset? endedAt = null)
    {
        EnsureCanTransitionTo(SessionStatus.Ended);
        Status = SessionStatus.Ended;
        EndedAt = endedAt ?? DateTimeOffset.UtcNow;
    }

    public void MarkError()
    {
        EnsureCanTransitionTo(SessionStatus.Error);
        Status = SessionStatus.Error;
    }

    private void EnsureCanTransitionTo(SessionStatus newStatus)
    {
        if (!IsValidTransition(Status, newStatus))
        {
            throw new InvalidOperationException(
                $"Invalid session status transition from '{Status}' to '{newStatus}'.");
        }
    }

    public static bool IsValidTransition(SessionStatus current, SessionStatus target)
    {
        if (current == target)
            return true;

        return current switch
        {
            SessionStatus.Starting => target is SessionStatus.Active or SessionStatus.Ending or SessionStatus.Ended or SessionStatus.Error,
            SessionStatus.Active => target is SessionStatus.Ending or SessionStatus.Ended or SessionStatus.Error,
            SessionStatus.Ending => target is SessionStatus.Ended or SessionStatus.Error,
            SessionStatus.Ended => false,
            SessionStatus.Error => false,
            _ => false
        };
    }
}
