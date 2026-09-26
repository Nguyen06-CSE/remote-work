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
    public void GetOrCreateDeviceId_Should_Generate_And_Persist_DeviceId()
    {
        var store = new FileDeviceIdentityStore(_tempFilePath);

        var firstId = store.GetOrCreateDeviceId();
        Assert.False(string.IsNullOrWhiteSpace(firstId));

        var store2 = new FileDeviceIdentityStore(_tempFilePath);
        var secondId = store2.GetOrCreateDeviceId();

        Assert.Equal(firstId, secondId);
        Assert.True(File.Exists(_tempFilePath));
        Assert.Equal(firstId, File.ReadAllText(_tempFilePath).Trim());
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
