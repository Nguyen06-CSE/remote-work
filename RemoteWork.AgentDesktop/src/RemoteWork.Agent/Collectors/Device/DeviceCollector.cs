using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RemoteWork.Agent.Configuration;
using RemoteWork.Agent.Core.Interfaces;
using RemoteWork.Agent.Core.Models;

namespace RemoteWork.Agent.Collectors.Device;

public sealed class DeviceCollector
{
    private readonly IDeviceIdentityStore _identityStore;
    private readonly IDeviceInfoProvider _deviceInfoProvider;
    private readonly AgentOptions _options;
    private readonly ILogger<DeviceCollector> _logger;

    public DeviceCollector(
        IDeviceIdentityStore identityStore,
        IDeviceInfoProvider deviceInfoProvider,
        IOptions<AgentOptions> options,
        ILogger<DeviceCollector> logger)
    {
        _identityStore = identityStore;
        _deviceInfoProvider = deviceInfoProvider;
        _options = options.Value;
        _logger = logger;
    }

    public DeviceInfo Collect()
    {
        var deviceId =
            _identityStore.GetOrCreateDeviceId();

        var deviceInfo =
            _deviceInfoProvider.GetDeviceInfo(
                deviceId,
                _options.AgentVersion);

        _logger.LogInformation(
            "Device detected: {DeviceId}, Hostname: {Hostname}, OS: {OS}",
            deviceInfo.DeviceId,
            deviceInfo.Hostname,
            deviceInfo.OperatingSystem);

        return deviceInfo;
    }
}