using System.Runtime.InteropServices;
using RemoteWork.Agent.Core.Interfaces;
using RemoteWork.Agent.Core.Models;

namespace RemoteWork.Agent.Platform.Windows;

public sealed class WindowsDeviceInfoProvider : IDeviceInfoProvider
{
    public DeviceInfo GetDeviceInfo(
        string deviceId,
        string agentVersion)
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