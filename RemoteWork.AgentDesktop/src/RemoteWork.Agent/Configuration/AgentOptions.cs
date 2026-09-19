namespace RemoteWork.Agent.Configuration;

public sealed class AgentOptions
{
    public string AgentVersion { get; set; } = "1.0.0";

    public string Environment { get; set; } = "Development";

    public int HeartbeatIntervalSeconds { get; set; } = 60;

    public string BackendBaseUrl { get; set; } =
        "http://localhost:8000";
}