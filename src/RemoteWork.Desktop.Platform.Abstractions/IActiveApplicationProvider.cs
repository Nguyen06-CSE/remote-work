using RemoteWork.Desktop.Core.Models;

namespace RemoteWork.Desktop.Platform.Abstractions;

public sealed class ActiveApplicationInfo
{
    public string ApplicationName { get; init; } = string.Empty;
    public string ProcessName { get; init; } = string.Empty;
    public int ProcessId { get; init; }
    public string WindowTitle { get; init; } = string.Empty;
    public DateTimeOffset Timestamp { get; init; }
}

public interface IActiveApplicationProvider : IApplicationActivityProvider
{
    ActiveApplicationInfo? GetActiveApplication();

    ApplicationActivity? IApplicationActivityProvider.GetActiveApplicationActivity()
    {
        var app = GetActiveApplication();
        if (app is null) return null;

        return new ApplicationActivity
        {
            ActivityId = Guid.NewGuid().ToString(),
            DeviceId = string.Empty,
            Timestamp = app.Timestamp,
            ApplicationName = app.ApplicationName,
            ProcessName = app.ProcessName,
            ProcessId = app.ProcessId,
            WindowTitle = app.WindowTitle
        };
    }
}
