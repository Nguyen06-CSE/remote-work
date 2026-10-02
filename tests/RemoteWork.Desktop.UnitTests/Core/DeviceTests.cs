using RemoteWork.Desktop.Core.Models;
using Xunit;

namespace RemoteWork.Desktop.UnitTests.Core;

public class DeviceTests
{
    [Fact]
    public void Device_Should_Store_Properties_Correctly()
    {
        var device = new Device
        {
            DeviceId = "dev-12345",
            Hostname = "WORK-MACBOOK-01",
            OperatingSystem = "macOS",
            OsVersion = "15.0.1",
            AgentVersion = "1.0.0-beta"
        };

        Assert.Equal("dev-12345", device.DeviceId);
        Assert.Equal("WORK-MACBOOK-01", device.Hostname);
        Assert.Equal("macOS", device.OperatingSystem);
        Assert.Equal("15.0.1", device.OsVersion);
        Assert.Equal("1.0.0-beta", device.AgentVersion);
    }
}
