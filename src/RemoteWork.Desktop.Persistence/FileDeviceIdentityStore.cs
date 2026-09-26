using RemoteWork.Desktop.Core.Interfaces;

namespace RemoteWork.Desktop.Persistence;

public sealed class FileDeviceIdentityStore : IDeviceIdentityStore
{
    private readonly string _filePath;

    public FileDeviceIdentityStore()
    {
        var appDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RemoteWork",
            "Agent");

        Directory.CreateDirectory(appDirectory);
        _filePath = Path.Combine(appDirectory, "device-id.txt");
    }

    public FileDeviceIdentityStore(string customFilePath)
    {
        _filePath = customFilePath;
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
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
        File.WriteAllText(_filePath, deviceId);
        return deviceId;
    }
}
