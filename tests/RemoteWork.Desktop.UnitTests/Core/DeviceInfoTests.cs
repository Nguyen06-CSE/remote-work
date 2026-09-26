using RemoteWork.Desktop.Core.Models;
using Xunit;

namespace RemoteWork.Desktop.UnitTests.Core;

public class DeviceInfoTests
{
    [Fact]
    public void DeviceInfo_Should_Store_Device_Data()
    {
        var device = new DeviceInfo
        {
            DeviceId = "device-001",
            Hostname = "TEST-PC",
            OperatingSystem = "macOS",
            OsVersion = "15.0",
            AgentVersion = "1.0.0"
        };

        Assert.Equal("device-001", device.DeviceId);
        Assert.Equal("TEST-PC", device.Hostname);
        Assert.Equal("macOS", device.OperatingSystem);
        Assert.Equal("15.0", device.OsVersion);
        Assert.Equal("1.0.0", device.AgentVersion);
    }
}
