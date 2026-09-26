using System.Runtime.InteropServices;
using RemoteWork.Desktop.Core.Models;
using RemoteWork.Desktop.Platform.Abstractions;

namespace RemoteWork.Desktop.Platform.Windows;

public sealed class WindowsDeviceInfoProvider : IDeviceInfoProvider
{
    public DeviceInfo GetDeviceInfo(string deviceId, string agentVersion)
    {
        return new DeviceInfo
        {
            DeviceId = deviceId,
            Hostname = Environment.MachineName,
            OperatingSystem = RuntimeInformation.OSDescription,
            OsVersion = Environment.OSVersion.Version.ToString(),
            AgentVersion = agentVersion
        };
    }
}
