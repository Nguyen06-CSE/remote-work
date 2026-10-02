using RemoteWork.Desktop.Persistence;
using Xunit;

namespace RemoteWork.Desktop.IntegrationTests.Persistence;

public class FileDeviceIdentityStoreTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly string _tempFilePath;

    public FileDeviceIdentityStoreTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "RemoteWorkTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDirectory);
        _tempFilePath = Path.Combine(_tempDirectory, "device-id.txt");
    }

    [Fact]
    public void FirstRun_Should_Generate_And_Persist_Valid_Guid_DeviceId()
    {
        var store = new FileDeviceIdentityStore(_tempFilePath);

        var deviceId = store.GetOrCreateDeviceId();

        Assert.False(string.IsNullOrWhiteSpace(deviceId));
        Assert.True(Guid.TryParse(deviceId, out _));
        Assert.True(File.Exists(_tempFilePath));
        Assert.Equal(deviceId, File.ReadAllText(_tempFilePath).Trim());
    }

    [Fact]
    public void RepeatedLoad_Should_Return_Same_DeviceId()
    {
        var store1 = new FileDeviceIdentityStore(_tempFilePath);
        var firstId = store1.GetOrCreateDeviceId();

        var store2 = new FileDeviceIdentityStore(_tempFilePath);
        var secondId = store2.GetOrCreateDeviceId();

        Assert.Equal(firstId, secondId);
    }

    [Fact]
    public void Corrupted_File_Content_Should_Regenerate_Valid_DeviceId()
    {
        File.WriteAllText(_tempFilePath, "invalid-non-guid-content");

        var store = new FileDeviceIdentityStore(_tempFilePath);
        var newId = store.GetOrCreateDeviceId();

        Assert.False(string.IsNullOrWhiteSpace(newId));
        Assert.True(Guid.TryParse(newId, out _));
        Assert.NotEqual("invalid-non-guid-content", newId);
    }

    [Fact]
    public void DefaultDirectoryPath_Should_Be_Valid()
    {
        var dirPath = FileDeviceIdentityStore.GetDefaultIdentityDirectory();
        Assert.False(string.IsNullOrWhiteSpace(dirPath));
        Assert.Contains("RemoteWork", dirPath);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDirectory))
            {
                Directory.Delete(_tempDirectory, true);
            }
        }
        catch
        {
            // Ignored in test teardown
        }
    }
}
