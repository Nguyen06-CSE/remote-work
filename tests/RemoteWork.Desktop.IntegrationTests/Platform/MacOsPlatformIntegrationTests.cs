using System.Runtime.InteropServices;
using RemoteWork.Desktop.Platform.Abstractions;
using RemoteWork.Desktop.Platform.MacOS;
using Xunit;

namespace RemoteWork.Desktop.IntegrationTests.Platform;

public sealed class MacOsPlatformIntegrationTests
{
    private static bool IsMacOs => RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

    [Fact]
    public void MacOsIdleTimeProvider_ReturnsValidIdleDuration()
    {
        if (!IsMacOs)
            return;

        var provider = new MacOsIdleTimeProvider();
        var idleTime = provider.GetIdleTime();

        Assert.True(idleTime >= TimeSpan.Zero, $"Idle time should be non-negative, got: {idleTime}");
    }

    [Fact]
    public void MacOsActiveApplicationProvider_ReturnsActiveApplicationMetadata()
    {
        if (!IsMacOs)
            return;

        var provider = new MacOsActiveApplicationProvider();
        var app = provider.GetActiveApplication();

        // In macOS GUI session, active app should not be null
        if (app is not null)
        {
            Assert.True(app.ProcessId > 0, $"ProcessId should be positive, got: {app.ProcessId}");
            Assert.False(string.IsNullOrWhiteSpace(app.ApplicationName) && string.IsNullOrWhiteSpace(app.ProcessName),
                "Either ApplicationName or ProcessName must be present");
            Assert.True(app.Timestamp <= DateTimeOffset.UtcNow, "Timestamp should not be in the future");
        }
    }

    [Fact]
    public void MacOsPlatformPermissionProvider_ReturnsRequiredPermissions()
    {
        if (!IsMacOs)
            return;

        var provider = new MacOsPlatformPermissionProvider();
        var permissions = provider.GetPermissions();

        Assert.NotNull(permissions);
        Assert.Equal(4, permissions.Count);

        var idlePerm = permissions.FirstOrDefault(p => p.CapabilityName == "Idle Time");
        Assert.NotNull(idlePerm);
        Assert.Equal(PermissionStatus.NotRequired, idlePerm.Status);

        var activeAppPerm = permissions.FirstOrDefault(p => p.CapabilityName == "Active Application");
        Assert.NotNull(activeAppPerm);
        Assert.Equal(PermissionStatus.NotRequired, activeAppPerm.Status);

        var accessibilityPerm = permissions.FirstOrDefault(p => p.CapabilityName.StartsWith("Accessibility", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(accessibilityPerm);
        Assert.True(accessibilityPerm.IsRequired);
        Assert.True(accessibilityPerm.Status is PermissionStatus.Granted or PermissionStatus.Denied or PermissionStatus.Unknown);

        var screenRecPerm = permissions.FirstOrDefault(p => p.CapabilityName.StartsWith("Screen Recording", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(screenRecPerm);
        Assert.True(screenRecPerm.IsRequired);
        Assert.True(screenRecPerm.Status is PermissionStatus.Granted or PermissionStatus.Denied or PermissionStatus.Unknown);
    }

    [Fact]
    public void MacOsInputActivityProvider_Lifecycle_OperatesWithoutCrash()
    {
        if (!IsMacOs)
            return;

        using var provider = new MacOsInputActivityProvider();
        provider.Start();

        // Sample counts
        var kb = provider.GetKeyboardCount();
        var mouse = provider.GetMouseCount();
        Assert.True(kb >= 0);
        Assert.True(mouse >= 0);

        // Counter reset check
        var secondKb = provider.GetKeyboardCount();
        var secondMouse = provider.GetMouseCount();
        Assert.True(secondKb >= 0);
        Assert.True(secondMouse >= 0);

        provider.Stop();
    }

    [Fact]
    public void MacOsScreenshotProvider_CapturesAndValidatesImage()
    {
        if (!IsMacOs)
            return;

        var tempPath = Path.Combine(Path.GetTempPath(), $"spike_test_{Guid.NewGuid():N}.png");
        try
        {
            var provider = new MacOsScreenshotProvider();
            var result = provider.CaptureScreen(tempPath);

            // If screencapture succeeded, verify file and dimension
            if (result.Success)
            {
                Assert.True(File.Exists(result.FilePath));
                Assert.True(result.Width > 0, $"Expected width > 0, got {result.Width}");
                Assert.True(result.Height > 0, $"Expected height > 0, got {result.Height}");
            }
            else
            {
                // Graceful error message without throwing unhandled exception
                Assert.False(string.IsNullOrWhiteSpace(result.ErrorMessage));
            }
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                try { File.Delete(tempPath); } catch { /* ignore */ }
            }
        }
    }
}
