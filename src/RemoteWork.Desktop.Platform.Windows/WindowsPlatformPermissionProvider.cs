using RemoteWork.Desktop.Platform.Abstractions;

namespace RemoteWork.Desktop.Platform.Windows;

/// <summary>
/// Windows does not require special permissions for idle time, active app, input monitoring, or screenshots.
/// All capabilities are available by default to any desktop application.
/// </summary>
public sealed class WindowsPlatformPermissionProvider : IPlatformPermissionProvider
{
    public IReadOnlyList<PermissionInfo> GetPermissions() =>
    [
        new() { CapabilityName = "Idle Time", IsRequired = false, Status = PermissionStatus.NotRequired, HowToGrant = "No permission required on Windows" },
        new() { CapabilityName = "Active Application", IsRequired = false, Status = PermissionStatus.NotRequired, HowToGrant = "No permission required on Windows" },
        new() { CapabilityName = "Input Monitoring", IsRequired = false, Status = PermissionStatus.NotRequired, HowToGrant = "No permission required on Windows" },
        new() { CapabilityName = "Screen Capture", IsRequired = false, Status = PermissionStatus.NotRequired, HowToGrant = "No permission required on Windows" },
    ];
}
