using RemoteWork.Agent.Core.Enums;

namespace RemoteWork.Agent.Core.Models;

public sealed class SessionInfo
{
    public required string SessionId { get; init; }

    public required string DeviceId { get; init; }

    // public SessionStatus Status { get; private set; }

    public DateTimeOffset StartedAt { get; init; }

    public DateTimeOffset? EndedAt { get; private set; }


    public SessionStatus Status { get; private set; } = SessionStatus.Starting;


    public TimeSpan? Duration =>
        EndedAt.HasValue
            ? EndedAt.Value - StartedAt
            : null;

    public void MarkActive()
    {
        Status = SessionStatus.Active;
    }

    public void MarkEnding()
    {
        Status = SessionStatus.Ending;
    }

    public void MarkEnded()
    {
        Status = SessionStatus.Ended;
        EndedAt = DateTimeOffset.UtcNow;
    }

    public void MarkError()
    {
        Status = SessionStatus.Error;
    }
}