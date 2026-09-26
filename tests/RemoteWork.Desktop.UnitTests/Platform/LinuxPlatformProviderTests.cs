using RemoteWork.Desktop.Platform.Linux;
using Xunit;

namespace RemoteWork.Desktop.UnitTests.Platform;

public sealed class LinuxPlatformProviderTests
{
    [Fact]
    public void LinuxActiveApplicationProvider_ReturnsNullGracefully()
    {
        var provider = new LinuxActiveApplicationProvider();
        var app = provider.GetActiveApplication();
        Assert.Null(app);
    }

    [Fact]
    public void LinuxPlatformPermissionProvider_ReturnsEmptyList()
    {
        var provider = new LinuxPlatformPermissionProvider();
        var permissions = provider.GetPermissions();
        Assert.NotNull(permissions);
        Assert.Empty(permissions);
    }

    [Fact]
    public void LinuxScreenshotProvider_ReturnsNotImplementedFailure()
    {
        var provider = new LinuxScreenshotProvider();
        var result = provider.CaptureScreen("/tmp/test.png");
        Assert.False(result.Success);
        Assert.Contains("not implemented", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }
}
