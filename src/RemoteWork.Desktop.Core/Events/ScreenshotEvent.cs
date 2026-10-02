namespace RemoteWork.Desktop.Core.Events;

public sealed class ScreenshotEvent : TrackingEvent
{
    public required string FilePath { get; init; }

    public int Width { get; init; }

    public int Height { get; init; }

    public long FileSize { get; init; }
}
