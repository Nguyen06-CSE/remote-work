using RemoteWork.Desktop.Core.Models;

namespace RemoteWork.Desktop.Platform.Abstractions;

public interface IDeviceProvider
{
    Device GetDevice(string deviceId, string agentVersion);
}
