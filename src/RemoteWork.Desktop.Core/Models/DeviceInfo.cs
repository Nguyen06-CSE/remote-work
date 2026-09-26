namespace RemoteWork.Desktop.Core.Models;

public sealed class DeviceInfo
{
    public required string DeviceId { get; init; }

    public required string Hostname { get; init; }

    public required string OperatingSystem { get; init; }

    public required string OsVersion { get; init; }

    public required string AgentVersion { get; init; }
}
