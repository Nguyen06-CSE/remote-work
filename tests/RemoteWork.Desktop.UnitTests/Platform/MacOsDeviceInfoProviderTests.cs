using RemoteWork.Desktop.Platform.MacOS;
using Xunit;

namespace RemoteWork.Desktop.UnitTests.Platform;

public class MacOsDeviceInfoProviderTests
{
    [Fact]
    public void MacOsDeviceInfoProvider_Should_Return_DeviceInfo_With_MacOs_OperatingSystem()
    {
        var provider = new MacOsDeviceInfoProvider();

        var info = provider.GetDeviceInfo("test-mac-device-id", "1.0.0");

        Assert.Equal("test-mac-device-id", info.DeviceId);
        Assert.Equal("1.0.0", info.AgentVersion);
        Assert.Equal("macOS", info.OperatingSystem);
        Assert.False(string.IsNullOrWhiteSpace(info.Hostname));
        Assert.False(string.IsNullOrWhiteSpace(info.OsVersion));
    }
}
