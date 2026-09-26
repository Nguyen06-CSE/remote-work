using RemoteWork.Desktop.Platform.Abstractions;

namespace RemoteWork.Desktop.Platform.Linux;

public sealed class LinuxScreenshotProvider : IScreenshotProvider
{
    public ScreenshotResult CaptureScreen(string outputPath) =>
        new() { Success = false, ErrorMessage = "Linux screenshot not implemented" };
}
