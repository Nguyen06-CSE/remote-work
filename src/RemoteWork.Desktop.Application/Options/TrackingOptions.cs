namespace RemoteWork.Desktop.Application.Options;

public sealed class TrackingOptions
{
    public int ActivitySamplingIntervalSeconds { get; set; } = 10;

    public int IdleThresholdSeconds { get; set; } = 300;

    public int ActivityBatchIntervalSeconds { get; set; } = 60;

    // Bot detection options
    public bool MouseBotDetectionEnabled { get; set; } = true;

    public int MouseSampleBufferSize { get; set; } = 200;

    public double PeriodicityThresholdCv { get; set; } = 0.15;

    public int PeriodicityMinSamples { get; set; } = 30;

    public int PeriodicityMinIntervalMs { get; set; } = 5000;

    public int PeriodicityMaxIntervalMs { get; set; } = 300000;

    public int FixedAreaMaxPixels { get; set; } = 10000;

    public int FixedAreaMinDurationMs { get; set; } = 60000;

    public int MultiAreaMinClusters { get; set; } = 2;

    public int MultiAreaMaxClusters { get; set; } = 5;
}