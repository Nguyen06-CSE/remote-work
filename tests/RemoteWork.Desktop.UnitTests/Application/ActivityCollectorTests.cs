using Microsoft.Extensions.Logging.Abstractions;
using RemoteWork.Desktop.Application.Collectors;
using RemoteWork.Desktop.Core.Enums;
using RemoteWork.Desktop.Core.Interfaces;
using RemoteWork.Desktop.Core.Models.Activity;
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
        public List<MouseClickSample> SamplesToDrain { get; set; } = [];

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

        IReadOnlyList<MouseClickSample> IMouseActivityCollector.DrainSamples()
        {
            var drained = SamplesToDrain.ToArray();
            SamplesToDrain.Clear();
            return drained;
        }
    }

    private sealed class StubBotDetector : IMouseBotDetector
    {
        public bool ReturnSuspicious { get; set; }

        public BotDetectionResult Analyze(IReadOnlyList<MouseClickSample> samples)
        {
            return new BotDetectionResult
            {
                IsSuspicious = ReturnSuspicious,
                Timestamp = DateTimeOffset.UtcNow
            };
        }
    }

    [Fact]
    public void Collect_Without_Active_Session_Should_Return_Empty()
    {
        var sessionCollector = new SessionCollector(NullLogger<SessionCollector>.Instance);
        var idleCollector = new StubIdleCollector();
        var countCollector = new StubCountCollector();
        var botDetector = new StubBotDetector();

        var collector = new ActivityCollector(
            sessionCollector,
            idleCollector,
            countCollector,
            countCollector,
            botDetector,
            NullLogger<ActivityCollector>.Instance);

        var events = collector.Collect();

        Assert.Empty(events);
    }

    [Fact]
    public void Collect_With_Suspicious_Bot_Activity_Should_Emit_Event_And_Flag_Batch()
    {
        var sessionCollector = new SessionCollector(NullLogger<SessionCollector>.Instance);
        sessionCollector.StartSession("device-100");

        var idleCollector = new StubIdleCollector { Active = true };
        var countCollector = new StubCountCollector
        {
            NextKeyboardCount = 10,
            NextMouseCount = 5,
            SamplesToDrain = [new MouseClickSample(1000, 100, 100)]
        };

        var botDetector = new StubBotDetector { ReturnSuspicious = true };

        var collector = new ActivityCollector(
            sessionCollector,
            idleCollector,
            countCollector,
            countCollector,
            botDetector,
            NullLogger<ActivityCollector>.Instance);

        var events = collector.Collect();

        Assert.NotEmpty(events);
        Assert.Contains(events, e => e.Type == ActivityEventType.SuspiciousActivityDetected);

        var batch = collector.FlushBatch();

        Assert.NotNull(batch);
        Assert.True(batch.HasSuspiciousMouseActivity);
        Assert.Equal(10, batch.KeyboardCount);
        Assert.Equal(5, batch.MouseCount);
    }
}