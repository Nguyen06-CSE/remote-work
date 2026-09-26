using RemoteWork.Desktop.Platform.Abstractions;

namespace RemoteWork.Desktop.Platform.MacOS;

public sealed class MacOsIdleTimeProvider : IIdleTimeProvider
{
    public TimeSpan GetIdleTime()
    {
        // Safe default for macOS scaffold; native CoreGraphics/IOKit hooks will be wired in macOS platform phase
        return TimeSpan.Zero;
    }
}
