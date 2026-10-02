namespace RemoteWork.Desktop.Core.Events;

public sealed class ApplicationActivityEvent : TrackingEvent
{
    public required string ApplicationName { get; init; }

    public required string ProcessName { get; init; }

    public int ProcessId { get; init; }

    public string? WindowTitle { get; init; }

    public TimeSpan Duration { get; init; }
}
