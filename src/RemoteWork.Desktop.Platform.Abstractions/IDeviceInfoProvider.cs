using RemoteWork.Desktop.Core.Models;

namespace RemoteWork.Desktop.Platform.Abstractions;

public interface IDeviceInfoProvider
{
    DeviceInfo GetDeviceInfo(string deviceId, string agentVersion);
}
