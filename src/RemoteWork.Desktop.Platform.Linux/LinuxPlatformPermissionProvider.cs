using RemoteWork.Desktop.Platform.Abstractions;

namespace RemoteWork.Desktop.Platform.Linux;

public sealed class LinuxPlatformPermissionProvider : IPlatformPermissionProvider
{
    public IReadOnlyList<PermissionInfo> GetPermissions() => []; // Linux not in scope for this spike
}
