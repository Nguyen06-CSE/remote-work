namespace RemoteWork.Desktop.Persistence.Entities;

public sealed class ApplicationActivityEntity
{
    public required string ActivityId { get; set; }
    public required string DeviceId { get; set; }
    public string? SessionId { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    public required string ApplicationName { get; set; }
    public required string ProcessName { get; set; }
    public int ProcessId { get; set; }
    public string? WindowTitle { get; set; }
    public long DurationTicks { get; set; }
    
    // Navigation
    public DeviceEntity? Device { get; set; }
    public SessionEntity? Session { get; set; }
}
