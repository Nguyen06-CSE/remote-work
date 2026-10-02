using System.Runtime.InteropServices;
using RemoteWork.Desktop.Platform.Abstractions;
using RemoteWork.Desktop.Platform.Windows;
using Xunit;

namespace RemoteWork.Desktop.IntegrationTests.Platform;

public sealed class WindowsPlatformIntegrationTests
{
    private static bool IsWindows => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

    [Fact]
    public void WindowsIdleTimeProvider_ReturnsValidIdleDuration_OrZeroNonWindows()
    {
        var provider = new WindowsIdleTimeProvider();
        var idleTime = provider.GetIdleTime();

        if (IsWindows)
        {
            Assert.True(idleTime >= TimeSpan.Zero, $"Idle time on Windows should be non-negative, got: {idleTime}");
        }
        else
        {
            Assert.Equal(TimeSpan.Zero, idleTime);
        }
    }

    [Fact]
    public void WindowsInputActivityProvider_Lifecycle_OperatesWithoutCrash()
    {
        using var provider = new WindowsInputActivityProvider();
        provider.Start();

        var kb = provider.GetKeyboardCount();
        var mouse = provider.GetMouseCount();
        var samples = provider.DrainMouseSamples();

        Assert.True(kb >= 0);
        Assert.True(mouse >= 0);
        Assert.Empty(samples); // Under privacy policy, no coordinates are stored

        // Reset check
        var kb2 = provider.GetKeyboardCount();
        var mouse2 = provider.GetMouseCount();
        Assert.Equal(0, kb2);
        Assert.Equal(0, mouse2);

        provider.Stop();
    }

    [Fact]
    public void WindowsPlatformPermissionProvider_ReturnsNotRequired()
    {
        var provider = new WindowsPlatformPermissionProvider();
        var permissions = provider.GetPermissions();

        Assert.NotNull(permissions);
        Assert.Equal(4, permissions.Count);
        Assert.All(permissions, p => Assert.False(p.IsRequired));
    }

    [Fact]
    public void WindowsActiveApplicationProvider_OperatesWithoutCrash()
    {
        var provider = new WindowsActiveApplicationProvider();
        var app = provider.GetActiveApplication();

        if (IsWindows)
        {
            if (app is not null)
            {
                Assert.True(app.ProcessId > 0);
                Assert.False(string.IsNullOrWhiteSpace(app.ApplicationName));
            }
        }
        else
        {
            Assert.Null(app);
        }
    }
}
