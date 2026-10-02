namespace RemoteWork.Desktop.Core.Models;

public sealed class ScreenshotMetadata
{
    public required string ScreenshotId { get; init; }

    public required string DeviceId { get; init; }

    public string? SessionId { get; init; }

    public required DateTimeOffset Timestamp { get; init; }

    public required string FilePath { get; init; }

    public int Width { get; init; }

    public int Height { get; init; }

    public long FileSize { get; init; }
}
