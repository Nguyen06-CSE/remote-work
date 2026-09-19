using RemoteWork.Agent.Core.Models;

namespace RemoteWork.Agent.Tests;

public class DeviceInfoTests
{
    [Fact]
    public void DeviceInfo_Should_Store_Device_Data()
    {
        var device = new DeviceInfo
        {
            DeviceId = "device-001",
            Hostname = "TEST-PC",
            OperatingSystem = "Windows",
            OsVersion = "10.0",
            AgentVersion = "1.0.0"
        };

        Assert.Equal(
            "device-001",
            device.DeviceId);

        Assert.Equal(
            "TEST-PC",
            device.Hostname);

        Assert.Equal(
            "Windows",
            device.OperatingSystem);

        Assert.Equal(
            "10.0",
            device.OsVersion);

        Assert.Equal(
            "1.0.0",
            device.AgentVersion);
    }
}