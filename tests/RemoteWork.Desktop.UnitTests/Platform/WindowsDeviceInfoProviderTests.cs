using RemoteWork.Desktop.Platform.Windows;
using Xunit;

namespace RemoteWork.Desktop.UnitTests.Platform;

public class WindowsDeviceInfoProviderTests
{
    [Fact]
    public void WindowsDeviceInfoProvider_Should_Return_DeviceInfo_With_Windows_OperatingSystem()
    {
        var provider = new WindowsDeviceInfoProvider();

        var info = provider.GetDeviceInfo("test-win-device-id", "1.0.0");

        Assert.Equal("test-win-device-id", info.DeviceId);
        Assert.Equal("1.0.0", info.AgentVersion);
        Assert.Equal("Windows", info.OperatingSystem);
        Assert.False(string.IsNullOrWhiteSpace(info.Hostname));
        Assert.False(string.IsNullOrWhiteSpace(info.OsVersion));
    }
}
