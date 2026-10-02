namespace RemoteWork.Desktop.Persistence.Entities;

public sealed class ActivityBatchEntity
{
    public required string BatchId { get; set; }
    public required string DeviceId { get; set; }
    public required string SessionId { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset EndedAt { get; set; }
    public int KeyboardCount { get; set; }
    public int MouseCount { get; set; }
    public long ActiveDurationTicks { get; set; }
    public long IdleDurationTicks { get; set; }
    public bool HasSuspiciousMouseActivity { get; set; }
    
    // Navigation
    public DeviceEntity? Device { get; set; }
    public SessionEntity? Session { get; set; }
}
