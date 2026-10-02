namespace RemoteWork.Desktop.Core.Events;

public sealed class ActivityTrackingEvent : TrackingEvent
{
    public int KeyboardCount { get; init; }

    public int MouseCount { get; init; }

    public TimeSpan ActiveDuration { get; init; }

    public TimeSpan IdleDuration { get; init; }

    public bool IsActive { get; init; }
}
