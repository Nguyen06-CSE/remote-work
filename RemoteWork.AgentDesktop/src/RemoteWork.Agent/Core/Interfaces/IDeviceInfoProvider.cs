using RemoteWork.Agent.Core.Models;

namespace RemoteWork.Agent.Core.Interfaces;

public interface IDeviceInfoProvider
{
    DeviceInfo GetDeviceInfo(string deviceId, string agentVersion);
}