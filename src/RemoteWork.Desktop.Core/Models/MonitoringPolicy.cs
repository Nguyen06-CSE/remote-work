namespace RemoteWork.Desktop.Core.Models;

public sealed class MonitoringPolicy
{
    public string PolicyId { get; init; } = "default";

    public TimeSpan ActivitySamplingInterval { get; init; } = TimeSpan.FromSeconds(10);

    public TimeSpan IdleThreshold { get; init; } = TimeSpan.FromMinutes(5);

    public TimeSpan ScreenshotInterval { get; init; } = TimeSpan.FromMinutes(10);

    public bool EnableScreenshots { get; init; } = true;

    public bool EnableApplicationTracking { get; init; } = true;

    public bool EnableInputTracking { get; init; } = true;

    public static MonitoringPolicy CreateDefault() => new();
}
