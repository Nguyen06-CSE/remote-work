using RemoteWork.Desktop.Platform.Abstractions;

namespace RemoteWork.Desktop.Platform.Linux;

public sealed class LinuxActiveApplicationProvider : IActiveApplicationProvider
{
    public ActiveApplicationInfo? GetActiveApplication() => null; // Linux not in scope for this spike
}
