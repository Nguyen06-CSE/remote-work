using RemoteWork.Desktop.Core.Models;
using Xunit;

namespace RemoteWork.Desktop.UnitTests.Core;

public class ActivitySampleTests
{
    [Fact]
    public void ActivitySample_Should_Store_Metrics_Correctly()
    {
        var timestamp = DateTimeOffset.UtcNow;
        var sample = new ActivitySample
        {
            SampleId = "sample-001",
            DeviceId = "dev-12345",
            SessionId = "session-100",
            Timestamp = timestamp,
            KeyboardCount = 42,
            MouseCount = 15,
            ActiveDuration = TimeSpan.FromSeconds(50),
            IdleDuration = TimeSpan.FromSeconds(10),
            IsActive = true
        };

        Assert.Equal("sample-001", sample.SampleId);
        Assert.Equal("dev-12345", sample.DeviceId);
        Assert.Equal("session-100", sample.SessionId);
        Assert.Equal(timestamp, sample.Timestamp);
        Assert.Equal(42, sample.KeyboardCount);
        Assert.Equal(15, sample.MouseCount);
        Assert.Equal(TimeSpan.FromSeconds(50), sample.ActiveDuration);
        Assert.Equal(TimeSpan.FromSeconds(10), sample.IdleDuration);
        Assert.True(sample.IsActive);
    }
}
