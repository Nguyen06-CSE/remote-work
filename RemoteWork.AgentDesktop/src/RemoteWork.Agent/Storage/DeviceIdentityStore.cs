using RemoteWork.Agent.Core.Interfaces;

namespace RemoteWork.Agent.Storage;

public sealed class DeviceIdentityStore : IDeviceIdentityStore
{
    private readonly string _filePath;

    public DeviceIdentityStore()
    {
        var appDirectory = Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData),
            "RemoteWork",
            "Agent");

        Directory.CreateDirectory(appDirectory);

        _filePath = Path.Combine(
            appDirectory,
            "device-id.txt");
    }

    public string GetOrCreateDeviceId()
    {
        if (File.Exists(_filePath))
        {
            var existingId = File.ReadAllText(_filePath).Trim();

            if (!string.IsNullOrWhiteSpace(existingId))
            {
                return existingId;
            }
        }

        var deviceId = Guid.NewGuid().ToString();

        File.WriteAllText(
            _filePath,
            deviceId);

        return deviceId;
    }
}