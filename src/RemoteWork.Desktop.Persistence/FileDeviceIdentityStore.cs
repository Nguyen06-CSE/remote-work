using RemoteWork.Desktop.Core.Interfaces;

namespace RemoteWork.Desktop.Persistence;

public sealed class FileDeviceIdentityStore : IDeviceIdentityStore
{
    private readonly string _filePath;

    public FileDeviceIdentityStore()
    {
        var appDirectory = GetDefaultIdentityDirectory();
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
            try
            {
                var existingId = File.ReadAllText(_filePath).Trim();
                if (!string.IsNullOrWhiteSpace(existingId) && Guid.TryParse(existingId, out var parsedGuid))
                {
                    return parsedGuid.ToString("D");
                }
            }
            catch
            {
                // Fallback to generating a fresh DeviceId if file read fails or file is corrupted
            }
        }

        var newDeviceId = Guid.NewGuid().ToString("D");
        SaveDeviceId(newDeviceId);
        return newDeviceId;
    }

    private void SaveDeviceId(string deviceId)
    {
        var tempFilePath = $"{_filePath}.tmp";
        File.WriteAllText(tempFilePath, deviceId);

        if (File.Exists(_filePath))
        {
            File.Delete(_filePath);
        }

        File.Move(tempFilePath, _filePath);
    }

    public static string GetDefaultIdentityDirectory()
    {
        var localDataFolder = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrEmpty(localDataFolder))
        {
            localDataFolder = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        }

        return Path.Combine(localDataFolder, "RemoteWork", "Agent");
    }
}
