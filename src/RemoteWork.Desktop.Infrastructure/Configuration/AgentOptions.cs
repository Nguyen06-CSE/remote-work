namespace RemoteWork.Desktop.Infrastructure.Configuration;

public sealed class AgentOptions
{
    public const string SectionName = "Agent";

    public string AgentVersion { get; set; } = "1.0.0";

    public string Environment { get; set; } = "Development";

    public int HeartbeatIntervalSeconds { get; set; } = 60;

    public string BackendBaseUrl { get; set; } = "http://localhost:8000";

    public int ActivitySamplingIntervalSeconds { get; set; } = 10;

    public int IdleThresholdSeconds { get; set; } = 300;

    public int ActivityBatchIntervalSeconds { get; set; } = 60;
}
