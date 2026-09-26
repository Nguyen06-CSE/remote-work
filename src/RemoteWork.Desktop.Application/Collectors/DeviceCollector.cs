using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RemoteWork.Desktop.Core.Interfaces;
using RemoteWork.Desktop.Core.Models;
using RemoteWork.Desktop.Platform.Abstractions;

namespace RemoteWork.Desktop.Application.Collectors;

public sealed class DeviceCollector
{
    private readonly IDeviceIdentityStore _identityStore;
    private readonly IDeviceInfoProvider _deviceInfoProvider;
    private readonly ILogger<DeviceCollector> _logger;

    public DeviceCollector(
        IDeviceIdentityStore identityStore,
        IDeviceInfoProvider deviceInfoProvider,
        ILogger<DeviceCollector> logger)
    {
        _identityStore = identityStore;
        _deviceInfoProvider = deviceInfoProvider;
        _logger = logger;
    }

    public DeviceInfo Collect(string agentVersion)
    {
        var deviceId = _identityStore.GetOrCreateDeviceId();
        var deviceInfo = _deviceInfoProvider.GetDeviceInfo(deviceId, agentVersion);

        _logger.LogInformation(
            "Device detected: {DeviceId}, Hostname: {Hostname}, OS: {OS}",
            deviceInfo.DeviceId,
            deviceInfo.Hostname,
            deviceInfo.OperatingSystem);

        return deviceInfo;
    }
}
