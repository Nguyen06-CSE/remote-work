using RemoteWork.Desktop.Core.Models;

namespace RemoteWork.Desktop.Platform.Abstractions;

public interface IDeviceInfoProvider : IDeviceProvider
{
    DeviceInfo GetDeviceInfo(string deviceId, string agentVersion);

    Device IDeviceProvider.GetDevice(string deviceId, string agentVersion) => GetDeviceInfo(deviceId, agentVersion);
}
