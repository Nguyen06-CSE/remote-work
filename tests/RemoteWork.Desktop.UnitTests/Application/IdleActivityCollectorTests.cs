using RemoteWork.Desktop.Application.Collectors;
using RemoteWork.Desktop.Platform.Abstractions;
using Xunit;

namespace RemoteWork.Desktop.UnitTests.Application;

public class IdleActivityCollectorTests
{
    private sealed class StubIdleTimeProvider : IIdleTimeProvider
    {
        public TimeSpan IdleTime { get; set; }
        public TimeSpan GetIdleTime() => IdleTime;
    }

    [Fact]
    public void IsUserActive_Should_Return_True_When_IdleTime_Is_Under_Threshold()
    {
        var stubProvider = new StubIdleTimeProvider { IdleTime = TimeSpan.FromSeconds(50) };
        var collector = new IdleActivityCollector(stubProvider, idleThresholdSeconds: 300);

        Assert.True(collector.IsUserActive());
    }

    [Fact]
    public void IsUserActive_Should_Return_False_When_IdleTime_Exceeds_Threshold()
    {
        var stubProvider = new StubIdleTimeProvider { IdleTime = TimeSpan.FromSeconds(350) };
        var collector = new IdleActivityCollector(stubProvider, idleThresholdSeconds: 300);

        Assert.False(collector.IsUserActive());
    }
}
