namespace RemoteWork.Desktop.Persistence.Entities;

public sealed class DeviceEntity
{
    public required string DeviceId { get; set; }
    public required string Hostname { get; set; }
    public required string OperatingSystem { get; set; }
    public required string OsVersion { get; set; }
    public required string AgentVersion { get; set; }
    public DateTimeOffset RegisteredAt { get; set; }
    public DateTimeOffset LastSeenAt { get; set; }
    
    // Navigation
    public ICollection<SessionEntity> Sessions { get; set; } = [];
    public ICollection<ActivityBatchEntity> ActivityBatches { get; set; } = [];
    public ICollection<ApplicationActivityEntity> ApplicationActivities { get; set; } = [];
}
