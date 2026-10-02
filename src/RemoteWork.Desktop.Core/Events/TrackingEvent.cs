namespace RemoteWork.Desktop.Core.Events;

public abstract class TrackingEvent
{
    public required string EventId { get; init; }

    public required string DeviceId { get; init; }

    public required DateTimeOffset Timestamp { get; init; }

    public required string Type { get; init; }

    public string? SessionId { get; init; }
}
