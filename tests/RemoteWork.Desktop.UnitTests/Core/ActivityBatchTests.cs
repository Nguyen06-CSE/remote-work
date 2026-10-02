using RemoteWork.Desktop.Core.Models;
using Xunit;

namespace RemoteWork.Desktop.UnitTests.Core;

public class ActivityBatchTests
{
    [Fact]
    public void ActivityBatch_Should_Maintain_Aggregate_Properties()
    {
        var start = DateTimeOffset.UtcNow.AddMinutes(-10);
        var end = DateTimeOffset.UtcNow;
        var batch = new ActivityBatch
        {
            BatchId = "batch-555",
            DeviceId = "dev-12345",
            SessionId = "session-100",
            StartedAt = start,
            EndedAt = end,
            KeyboardCount = 350,
            MouseCount = 120,
            ActiveDuration = TimeSpan.FromMinutes(8),
            IdleDuration = TimeSpan.FromMinutes(2),
            HasSuspiciousMouseActivity = false
        };

        Assert.Equal("batch-555", batch.BatchId);
        Assert.Equal("dev-12345", batch.DeviceId);
        Assert.Equal("session-100", batch.SessionId);
        Assert.Equal(350, batch.KeyboardCount);
        Assert.Equal(120, batch.MouseCount);
        Assert.Equal(TimeSpan.FromMinutes(8), batch.ActiveDuration);
        Assert.Equal(TimeSpan.FromMinutes(2), batch.IdleDuration);
        Assert.False(batch.HasSuspiciousMouseActivity);
    }
}
