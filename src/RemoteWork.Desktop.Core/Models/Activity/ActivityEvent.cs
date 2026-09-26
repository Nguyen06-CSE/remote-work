using RemoteWork.Desktop.Core.Enums;

namespace RemoteWork.Desktop.Core.Models.Activity;

public sealed class ActivityEvent
{
    public required string EventId { get; init; }

    public required string DeviceId { get; init; }

    public required string SessionId { get; init; }

    public required DateTimeOffset Timestamp { get; init; }

    public required ActivityEventType Type { get; init; }

    public int? Count { get; init; }

    public bool? IsActive { get; init; }
}
