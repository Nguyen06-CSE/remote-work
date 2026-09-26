using RemoteWork.Desktop.Platform.Abstractions;
using RemoteWork.Desktop.Platform.Linux;
using Xunit;

namespace RemoteWork.Desktop.UnitTests.Platform;

public sealed class PlatformModelTests
{
    [Fact]
    public void ActiveApplicationInfo_InitializesCorrectly()
    {
        var now = DateTimeOffset.UtcNow;
        var appInfo = new ActiveApplicationInfo
        {
            ApplicationName = "Visual Studio Code",
            ProcessName = "Code",
            ProcessId = 12345,
            WindowTitle = "main.cs - remote-gent",
            Timestamp = now
        };

        Assert.Equal("Visual Studio Code", appInfo.ApplicationName);
        Assert.Equal("Code", appInfo.ProcessName);
        Assert.Equal(12345, appInfo.ProcessId);
        Assert.Equal("main.cs - remote-gent", appInfo.WindowTitle);
        Assert.Equal(now, appInfo.Timestamp);
    }

    [Fact]
    public void PermissionInfo_InitializesCorrectly()
    {
        var perm = new PermissionInfo
        {
            CapabilityName = "Accessibility",
            IsRequired = true,
            Status = PermissionStatus.Granted,
            HowToGrant = "System Settings"
        };

        Assert.Equal("Accessibility", perm.CapabilityName);
        Assert.True(perm.IsRequired);
        Assert.Equal(PermissionStatus.Granted, perm.Status);
        Assert.Equal("System Settings", perm.HowToGrant);
    }

    [Fact]
    public void ScreenshotResult_InitializesCorrectly()
    {
        var result = new ScreenshotResult
        {
            Success = true,
            FilePath = "/tmp/screenshot.png",
            Width = 1920,
            Height = 1080,
            ErrorMessage = null
        };

        Assert.True(result.Success);
        Assert.Equal("/tmp/screenshot.png", result.FilePath);
        Assert.Equal(1920, result.Width);
        Assert.Equal(1080, result.Height);
        Assert.Null(result.ErrorMessage);
    }
}
