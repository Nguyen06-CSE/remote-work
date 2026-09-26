using System.Runtime.InteropServices;
using RemoteWork.Desktop.Platform.Abstractions;

namespace RemoteWork.Desktop.Platform.MacOS;

/// <summary>
/// Checks macOS permissions using native APIs.
/// - Accessibility: AXIsProcessTrusted() — required for CGEventTap input monitoring.
/// - Screen Recording: CGPreflightScreenCaptureAccess() — required for screenshots/screen capture.
/// Permission: None required to CHECK permission status.
/// How to grant: System Preferences → Security & Privacy → Privacy → Accessibility / Screen Recording.
/// </summary>
public sealed class MacOsPlatformPermissionProvider : IPlatformPermissionProvider
{
    public IReadOnlyList<PermissionInfo> GetPermissions()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return [];

        return
        [
            new PermissionInfo
            {
                CapabilityName = "Idle Time",
                IsRequired = false,
                Status = PermissionStatus.NotRequired,
                HowToGrant = "No permission required — uses IOKit HIDIdleTime"
            },
            new PermissionInfo
            {
                CapabilityName = "Active Application",
                IsRequired = false,
                Status = PermissionStatus.NotRequired,
                HowToGrant = "No permission required — uses NSWorkspace.frontmostApplication"
            },
            new PermissionInfo
            {
                CapabilityName = "Accessibility (Input Monitoring)",
                IsRequired = true,
                Status = CheckAccessibility(),
                HowToGrant = "System Preferences → Security & Privacy → Privacy → Accessibility → Add this application"
            },
            new PermissionInfo
            {
                CapabilityName = "Screen Recording",
                IsRequired = true,
                Status = CheckScreenRecording(),
                HowToGrant = "System Preferences → Security & Privacy → Privacy → Screen Recording → Add this application"
            },
        ];
    }

    private static PermissionStatus CheckAccessibility()
    {
        try
        {
            return AXIsProcessTrusted() ? PermissionStatus.Granted : PermissionStatus.Denied;
        }
        catch
        {
            return PermissionStatus.Unknown;
        }
    }

    private static PermissionStatus CheckScreenRecording()
    {
        try
        {
            return CGPreflightScreenCaptureAccess() ? PermissionStatus.Granted : PermissionStatus.Denied;
        }
        catch
        {
            return PermissionStatus.Unknown;
        }
    }

    [DllImport("/System/Library/Frameworks/ApplicationServices.framework/ApplicationServices")]
    private static extern bool AXIsProcessTrusted();

    [DllImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    private static extern bool CGPreflightScreenCaptureAccess();
}
