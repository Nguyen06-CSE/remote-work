namespace RemoteWork.Desktop.Core.Models;

public sealed class ActivitySample
{
    public required string SampleId { get; init; }

    public required string DeviceId { get; init; }

    public required string SessionId { get; init; }

    public required DateTimeOffset Timestamp { get; init; }

    public int KeyboardCount { get; init; }

    public int MouseCount { get; init; }

    public TimeSpan ActiveDuration { get; init; }

    public TimeSpan IdleDuration { get; init; }

    public bool IsActive { get; init; }
}
