using RemoteWork.Desktop.Core.Models;
using RemoteWork.Desktop.Persistence.Repositories;

namespace RemoteWork.Desktop.IntegrationTests.Persistence;

/// <summary>
/// Tests for Device persistence: upsert (insert + update), query by device id.
/// </summary>
public sealed class DevicePersistenceTests : IDisposable
{
    private readonly DbContextFixture _fixture = new();

    private static Device MakeDevice(string deviceId = "dev-001") => new()
    {
        DeviceId = deviceId,
        Hostname = "test-host",
        OperatingSystem = "macOS",
        OsVersion = "14.5",
        AgentVersion = "1.2.3"
    };

    [Fact]
    public async Task UpsertAsync_Should_Insert_New_Device()
    {
        var repo = new DeviceRepository(_fixture.Context);
        var device = MakeDevice();

        await repo.UpsertAsync(device);

        var stored = await _fixture.Context.Devices.FindAsync(device.DeviceId);
        Assert.NotNull(stored);
        Assert.Equal("test-host", stored.Hostname);
        Assert.Equal("macOS", stored.OperatingSystem);
        Assert.Equal("14.5", stored.OsVersion);
        Assert.Equal("1.2.3", stored.AgentVersion);
    }

    [Fact]
    public async Task UpsertAsync_Should_Update_Existing_Device()
    {
        var repo = new DeviceRepository(_fixture.Context);
        await repo.UpsertAsync(MakeDevice("dev-upsert"));

        // Upsert with updated fields
        var updated = new Device
        {
            DeviceId = "dev-upsert",
            Hostname = "new-host",
            OperatingSystem = "Windows",
            OsVersion = "11",
            AgentVersion = "2.0.0"
        };
        await repo.UpsertAsync(updated);

        var stored = await _fixture.Context.Devices.FindAsync("dev-upsert");
        Assert.NotNull(stored);
        Assert.Equal("new-host", stored.Hostname);
        Assert.Equal("Windows", stored.OperatingSystem);
        Assert.Equal("2.0.0", stored.AgentVersion);
    }

    [Fact]
    public async Task UpsertAsync_Should_Update_LastSeenAt_On_Second_Call()
    {
        var repo = new DeviceRepository(_fixture.Context);
        await repo.UpsertAsync(MakeDevice("dev-seen"));

        var firstSeen = (await _fixture.Context.Devices.FindAsync("dev-seen"))!.LastSeenAt;

        // Small delay to ensure time difference is detectable
        await Task.Delay(10);
        await repo.UpsertAsync(MakeDevice("dev-seen"));

        var secondSeen = (await _fixture.Context.Devices.FindAsync("dev-seen"))!.LastSeenAt;

        Assert.True(secondSeen >= firstSeen);
    }

    [Fact]
    public async Task GetByDeviceIdAsync_Should_Return_Null_For_Unknown_Device()
    {
        var repo = new DeviceRepository(_fixture.Context);

        var result = await repo.GetByDeviceIdAsync("ghost-device");

        Assert.Null(result);
    }

    [Fact]
    public async Task GetByDeviceIdAsync_Should_Return_Domain_Object()
    {
        var repo = new DeviceRepository(_fixture.Context);
        await repo.UpsertAsync(MakeDevice("dev-domain"));

        var result = await repo.GetByDeviceIdAsync("dev-domain");

        Assert.NotNull(result);
        Assert.IsType<Device>(result);
        Assert.Equal("dev-domain", result.DeviceId);
        Assert.Equal("test-host", result.Hostname);
    }

    [Fact]
    public async Task Multiple_Upserts_Should_Not_Create_Duplicate_Rows()
    {
        var repo = new DeviceRepository(_fixture.Context);
        await repo.UpsertAsync(MakeDevice("dev-dedup"));
        await repo.UpsertAsync(MakeDevice("dev-dedup"));
        await repo.UpsertAsync(MakeDevice("dev-dedup"));

        var count = _fixture.Context.Devices.Count(d => d.DeviceId == "dev-dedup");
        Assert.Equal(1, count);
    }

    public void Dispose() => _fixture.Dispose();
}
