namespace RemoteWork.Desktop.Core.Models.Activity;

public sealed class BotDetectionResult
{
    public bool IsSuspicious { get; init; }
    public DateTimeOffset Timestamp { get; init; }
}