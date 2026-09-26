using RemoteWork.Desktop.Platform.Abstractions;

namespace RemoteWork.Desktop.Platform.Linux;

public sealed class LinuxIdleTimeProvider : IIdleTimeProvider
{
    public TimeSpan GetIdleTime()
    {
        // Safe default for Linux scaffold; X11/Wayland idle provider will be implemented in Linux platform phase
        return TimeSpan.Zero;
    }
}
