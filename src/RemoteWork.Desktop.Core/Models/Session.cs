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
