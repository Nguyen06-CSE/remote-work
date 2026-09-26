namespace RemoteWork.Desktop.Core.Models.Activity;

public sealed class ActivityBatch
{
    public required string BatchId { get; init; }

    public required string DeviceId { get; init; }

    public required string SessionId { get; init; }

    public required DateTimeOffset StartedAt { get; init; }

    public required DateTimeOffset EndedAt { get; init; }

    public int KeyboardCount { get; init; }

    public int MouseCount { get; init; }

    public TimeSpan ActiveDuration { get; init; }

    public TimeSpan IdleDuration { get; init; }
}
