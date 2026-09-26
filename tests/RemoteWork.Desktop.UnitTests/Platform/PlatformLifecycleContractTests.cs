using System.Runtime.InteropServices;
using RemoteWork.Desktop.Platform.MacOS;
using RemoteWork.Desktop.Platform.Windows;
using Xunit;

namespace RemoteWork.Desktop.UnitTests.Platform;

public sealed class PlatformLifecycleContractTests
{
    [Fact]
    public void MacOsInputActivityProvider_Lifecycle_IdempotencyAndNoException()
    {
        using var provider = new MacOsInputActivityProvider();

        // Repeated Start should be idempotent
        provider.Start();
        provider.Start();

        // Read and reset
        var kb = provider.GetKeyboardCount();
        var mouse = provider.GetMouseCount();
        Assert.True(kb >= 0);
        Assert.True(mouse >= 0);

        // Repeated Stop should be idempotent
        provider.Stop();
        provider.Stop();

        // Dispose should not throw
        provider.Dispose();
        provider.Dispose();
    }

    [Fact]
    public void WindowsInputActivityProvider_Lifecycle_IdempotencyAndNoException()
    {
        using var provider = new WindowsInputActivityProvider();

        // When run on non-Windows (e.g. macOS), should gracefully do nothing and not throw
        provider.Start();
        provider.Start();

        var kb = provider.GetKeyboardCount();
        var mouse = provider.GetMouseCount();
        Assert.True(kb >= 0);
        Assert.True(mouse >= 0);

        provider.Stop();
        provider.Stop();

        provider.Dispose();
        provider.Dispose();
    }

    [Fact]
    public void WindowsPlatformProviders_GracefulDegradation_OnNonWindows()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return;

        var activeAppProvider = new WindowsActiveApplicationProvider();
        var app = activeAppProvider.GetActiveApplication();
        Assert.Null(app);

        var permissionProvider = new WindowsPlatformPermissionProvider();
        var permissions = permissionProvider.GetPermissions();
        Assert.NotNull(permissions);

        var screenshotProvider = new WindowsScreenshotProvider();
        var result = screenshotProvider.CaptureScreen("/tmp/win_test.png");
        Assert.False(result.Success);
    }
}
