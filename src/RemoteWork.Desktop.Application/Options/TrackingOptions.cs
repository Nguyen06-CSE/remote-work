namespace RemoteWork.Desktop.Application.Options;

public sealed class TrackingOptions
{
    public int ActivitySamplingIntervalSeconds { get; set; } = 10;

    public int IdleThresholdSeconds { get; set; } = 300;

    public int ActivityBatchIntervalSeconds { get; set; } = 60;
}
