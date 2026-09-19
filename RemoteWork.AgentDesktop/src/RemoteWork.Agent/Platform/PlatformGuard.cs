using System.Runtime.InteropServices;

namespace RemoteWork.Agent.Platform;

public static class PlatformGuard
{
    public static bool IsWindows =>
        RuntimeInformation.IsOSPlatform(
            OSPlatform.Windows);
}