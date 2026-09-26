namespace RemoteWork.Desktop.Platform.Abstractions;

public sealed class ScreenshotResult
{
    public bool Success { get; init; }
    public string? FilePath { get; init; }
    public int Width { get; init; }
    public int Height { get; init; }
    public string? ErrorMessage { get; init; }
}

public interface IScreenshotProvider
{
    ScreenshotResult CaptureScreen(string outputPath);
}
