namespace RemoteWork.Desktop.Persistence.Entities;

public sealed class SessionEntity
{
    public required string SessionId { get; set; }
    public required string DeviceId { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? EndedAt { get; set; }
    public required string Status { get; set; } // stored as string
    
    // Navigation
    public DeviceEntity? Device { get; set; }
    public ICollection<ActivityBatchEntity> ActivityBatches { get; set; } = [];
    public ICollection<ApplicationActivityEntity> ApplicationActivities { get; set; } = [];
}
