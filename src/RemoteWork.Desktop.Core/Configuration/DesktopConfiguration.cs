namespace RemoteWork.Desktop.Core.Configuration;

public sealed class DesktopConfiguration
{
    public const string SectionName = "Desktop";

    public string ApplicationVersion { get; set; } = "1.0.0";

    public string BackendBaseUrl { get; set; } = "http://localhost:8000";

    public string LocalStoragePath { get; set; } = string.Empty;

    public int HeartbeatIntervalSeconds { get; set; } = 60;

    public int ActivitySamplingIntervalSeconds { get; set; } = 10;

    public int ActivityBatchIntervalSeconds { get; set; } = 60;

    public LoggingSettings Logging { get; set; } = new();

    public ScreenshotSettings ScreenshotDefaults { get; set; } = new();
}

public sealed class LoggingSettings
{
    public string MinimumLevel { get; set; } = "Information";

    public string LogFilePath { get; set; } = string.Empty;

    public bool EnableConsole { get; set; } = true;
}

public sealed class ScreenshotSettings
{
    public bool Enabled { get; set; } = true;

    public int IntervalSeconds { get; set; } = 600;

    public int Quality { get; set; } = 80;
}
