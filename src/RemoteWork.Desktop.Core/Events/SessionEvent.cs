using RemoteWork.Desktop.Core.Enums;

namespace RemoteWork.Desktop.Core.Events;

public sealed class SessionEvent : TrackingEvent
{
    public required SessionStatus Status { get; init; }
}
