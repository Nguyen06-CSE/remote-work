using RemoteWork.Desktop.Core.Models;
using RemoteWork.Desktop.Persistence.Entities;
using RemoteWork.Desktop.Persistence.Repositories;

namespace RemoteWork.Desktop.IntegrationTests.Persistence;

/// <summary>
/// Tests for ApplicationActivity persistence: save, query by session, query by device.
/// </summary>
public sealed class ApplicationActivityPersistenceTests : IDisposable
{
    private readonly DbContextFixture _fixture = new();
    private const string DeviceId = "device-app-001";
    private const string SessionId = "session-app-001";

    public ApplicationActivityPersistenceTests()
    {
        SeedDeviceAndSession().GetAwaiter().GetResult();
    }

    private async Task SeedDeviceAndSession()
    {
        _fixture.Context.Devices.Add(new DeviceEntity
        {
            DeviceId = DeviceId,
            Hostname = "app-host",
            OperatingSystem = "macOS",
            OsVersion = "14.5",
            AgentVersion = "1.0.0",
            RegisteredAt = DateTimeOffset.UtcNow,
            LastSeenAt = DateTimeOffset.UtcNow
        });
        _fixture.Context.Sessions.Add(new SessionEntity
        {
            SessionId = SessionId,
            DeviceId = DeviceId,
            StartedAt = DateTimeOffset.UtcNow.AddHours(-2),
            Status = "Active"
        });
        await _fixture.Context.SaveChangesAsync();
    }

    private static ApplicationActivity MakeActivity(string activityId, string? sessionId = SessionId)
        => new()
        {
            ActivityId = activityId,
            DeviceId = DeviceId,
            SessionId = sessionId,
            Timestamp = DateTimeOffset.UtcNow,
            ApplicationName = "Visual Studio Code",
            ProcessName = "code",
            ProcessId = 12345,
            WindowTitle = "Program.cs - remotework",
            Duration = TimeSpan.FromSeconds(30)
        };

    [Fact]
    public async Task SaveAsync_Should_Persist_ApplicationActivity()
    {
        var repo = new ApplicationActivityRepository(_fixture.Context);
        var activity = MakeActivity(Guid.NewGuid().ToString());

        await repo.SaveAsync(activity);

        var stored = await _fixture.Context.ApplicationActivities.FindAsync(activity.ActivityId);
        Assert.NotNull(stored);
        Assert.Equal("Visual Studio Code", stored.ApplicationName);
        Assert.Equal("code", stored.ProcessName);
        Assert.Equal(12345, stored.ProcessId);
        Assert.Equal("Program.cs - remotework", stored.WindowTitle);
    }

    [Fact]
    public async Task GetBySessionIdAsync_Should_Return_Activities_In_Timestamp_Order()
    {
        var repo = new ApplicationActivityRepository(_fixture.Context);
        var t = DateTimeOffset.UtcNow;

        await _fixture.Context.ApplicationActivities.AddRangeAsync(
            new ApplicationActivityEntity { ActivityId = "a3", DeviceId = DeviceId, SessionId = SessionId, Timestamp = t.AddMinutes(2), ApplicationName = "C", ProcessName = "c" },
            new ApplicationActivityEntity { ActivityId = "a1", DeviceId = DeviceId, SessionId = SessionId, Timestamp = t, ApplicationName = "A", ProcessName = "a" },
            new ApplicationActivityEntity { ActivityId = "a2", DeviceId = DeviceId, SessionId = SessionId, Timestamp = t.AddMinutes(1), ApplicationName = "B", ProcessName = "b" }
        );
        await _fixture.Context.SaveChangesAsync();

        var activities = await repo.GetBySessionIdAsync(SessionId);

        Assert.Equal(3, activities.Count);
        Assert.Equal("a1", activities[0].ActivityId);
        Assert.Equal("a2", activities[1].ActivityId);
        Assert.Equal("a3", activities[2].ActivityId);
    }

    [Fact]
    public async Task GetByDeviceIdAsync_Should_Return_Activities_Newest_First()
    {
        var repo = new ApplicationActivityRepository(_fixture.Context);
        var t = DateTimeOffset.UtcNow;

        for (var i = 0; i < 5; i++)
        {
            var act = new ApplicationActivity
            {
                ActivityId = $"dev-act-{i}",
                DeviceId = DeviceId,
                SessionId = SessionId,
                Timestamp = t.AddMinutes(-i),
                ApplicationName = $"App{i}",
                ProcessName = $"proc{i}",
                Duration = TimeSpan.FromSeconds(10)
            };
            await repo.SaveAsync(act);
        }

        var results = await repo.GetByDeviceIdAsync(DeviceId, limit: 3);

        // Should get 3 most recent
        Assert.Equal(3, results.Count);
        // Verify newest first
        Assert.True(results[0].Timestamp >= results[1].Timestamp);
        Assert.True(results[1].Timestamp >= results[2].Timestamp);
    }

    [Fact]
    public async Task Activity_With_Null_SessionId_Should_Persist_Without_Error()
    {
        var repo = new ApplicationActivityRepository(_fixture.Context);
        var activity = MakeActivity(Guid.NewGuid().ToString(), sessionId: null);

        await repo.SaveAsync(activity);

        var stored = await _fixture.Context.ApplicationActivities.FindAsync(activity.ActivityId);
        Assert.NotNull(stored);
        Assert.Null(stored.SessionId);
    }

    [Fact]
    public async Task Activity_With_Null_WindowTitle_Should_Persist_Without_Error()
    {
        var repo = new ApplicationActivityRepository(_fixture.Context);
        var activity = new ApplicationActivity
        {
            ActivityId = Guid.NewGuid().ToString(),
            DeviceId = DeviceId,
            SessionId = SessionId,
            Timestamp = DateTimeOffset.UtcNow,
            ApplicationName = "Terminal",
            ProcessName = "Terminal",
            WindowTitle = null,
            Duration = TimeSpan.FromSeconds(5)
        };

        await repo.SaveAsync(activity);

        var stored = await _fixture.Context.ApplicationActivities.FindAsync(activity.ActivityId);
        Assert.NotNull(stored);
        Assert.Null(stored.WindowTitle);
    }

    [Fact]
    public async Task Duration_Should_Roundtrip_Via_Ticks()
    {
        var repo = new ApplicationActivityRepository(_fixture.Context);
        var duration = TimeSpan.FromMinutes(1).Add(TimeSpan.FromSeconds(23)).Add(TimeSpan.FromMilliseconds(456));
        var activityId = Guid.NewGuid().ToString();
        var activity = new ApplicationActivity
        {
            ActivityId = activityId,
            DeviceId = DeviceId,
            SessionId = SessionId,
            Timestamp = DateTimeOffset.UtcNow,
            ApplicationName = "Visual Studio Code",
            ProcessName = "code",
            ProcessId = 12345,
            WindowTitle = "Program.cs - remotework",
            Duration = duration
        };

        await repo.SaveAsync(activity);
        var loaded = await repo.GetBySessionIdAsync(SessionId);
        var found = loaded.First(a => a.ActivityId == activity.ActivityId);

        Assert.Equal(duration, found.Duration);
    }

    public void Dispose() => _fixture.Dispose();
}
