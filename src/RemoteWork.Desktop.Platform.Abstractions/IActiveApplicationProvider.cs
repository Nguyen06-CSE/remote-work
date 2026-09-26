namespace RemoteWork.Desktop.Platform.Abstractions;

public sealed class ActiveApplicationInfo
{
    public string ApplicationName { get; init; } = string.Empty;
    public string ProcessName { get; init; } = string.Empty;
    public int ProcessId { get; init; }
    public string WindowTitle { get; init; } = string.Empty;
    public DateTimeOffset Timestamp { get; init; }
}

public interface IActiveApplicationProvider
{
    ActiveApplicationInfo? GetActiveApplication();
}
