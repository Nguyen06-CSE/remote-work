using Microsoft.Extensions.Logging.Abstractions;
using RemoteWork.Desktop.Application.Collectors;
using RemoteWork.Desktop.Core.Enums;
using RemoteWork.Desktop.Core.Interfaces;
using Xunit;

namespace RemoteWork.Desktop.UnitTests.Application;

public class ActivityCollectorTests
{
    private sealed class StubIdleCollector : IIdleActivityCollector
    {
        public bool Active { get; set; } = true;
        public bool IsUserActive() => Active;
    }

    private sealed class StubCountCollector : IKeyboardActivityCollector, IMouseActivityCollector
    {
        public int NextKeyboardCount { get; set; }
        public int NextMouseCount { get; set; }

        int IKeyboardActivityCollector.Collect()
        {
            var count = NextKeyboardCount;
            NextKeyboardCount = 0;
            return count;
        }

        int IMouseActivityCollector.Collect()
        {
            var count = NextMouseCount;
            NextMouseCount = 0;
            return count;
        }
    }

    [Fact]
    public void Collect_Without_Active_Session_Should_Return_Empty()
    {
        var sessionCollector = new SessionCollector(NullLogger<SessionCollector>.Instance);
        var idleCollector = new StubIdleCollector();
        var countCollector = new StubCountCollector();

        var collector = new ActivityCollector(
            sessionCollector,
            idleCollector,
            countCollector,
            countCollector,
            NullLogger<ActivityCollector>.Instance);

        var events = collector.Collect();

        Assert.Empty(events);
    }

    [Fact]
    public void Collect_With_Active_Session_Should_Generate_Events_And_Batch()
    {
        var sessionCollector = new SessionCollector(NullLogger<SessionCollector>.Instance);
        sessionCollector.StartSession("device-100");

        var idleCollector = new StubIdleCollector { Active = true };
        var countCollector = new StubCountCollector
        {
            NextKeyboardCount = 10,
            NextMouseCount = 5
        };

        var collector = new ActivityCollector(
            sessionCollector,
            idleCollector,
            countCollector,
            countCollector,
            NullLogger<ActivityCollector>.Instance);

        var events = collector.Collect();

        Assert.NotEmpty(events);
        Assert.Contains(events, e => e.Type == ActivityEventType.ActivityStateChanged);
        Assert.Contains(events, e => e.Type == ActivityEventType.KeyboardActivity && e.Count == 10);
        Assert.Contains(events, e => e.Type == ActivityEventType.MouseActivity && e.Count == 5);

        var batch = collector.FlushBatch();

        Assert.NotNull(batch);
        Assert.Equal("device-100", batch.DeviceId);
        Assert.Equal(10, batch.KeyboardCount);
        Assert.Equal(5, batch.MouseCount);
    }
}
