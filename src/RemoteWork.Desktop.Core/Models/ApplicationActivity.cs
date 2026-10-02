namespace RemoteWork.Desktop.Core.Models;

public sealed class ApplicationActivity
{
    public required string ActivityId { get; init; }

    public required string DeviceId { get; init; }

    public string? SessionId { get; init; }

    public required DateTimeOffset Timestamp { get; init; }

    public required string ApplicationName { get; init; }

    public required string ProcessName { get; init; }

    public int ProcessId { get; init; }

    public string? WindowTitle { get; init; }

    public TimeSpan Duration { get; init; }
}
