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
        public bool ThrowOnIdle { get; set; }

        public bool IsUserActive()
        {
            if (ThrowOnIdle)
                throw new InvalidOperationException("Simulated idle hardware failure.");
            return Active;
        }
    }

    private sealed class StubCountCollector : IKeyboardActivityCollector, IMouseActivityCollector
    {
        public int NextKeyboardCount { get; set; }
        public int NextMouseCount { get; set; }
        public bool ThrowOnKeyboard { get; set; }
        public bool ThrowOnMouse { get; set; }
        public List<MouseClickSample> SamplesToDrain { get; set; } = [];

        int IKeyboardActivityCollector.Collect()
        {
            if (ThrowOnKeyboard)
                throw new InvalidOperationException("Simulated keyboard hook crash.");

            var count = NextKeyboardCount;
            NextKeyboardCount = 0;
            return count;
        }

        int IMouseActivityCollector.Collect()
        {
            if (ThrowOnMouse)
                throw new InvalidOperationException("Simulated mouse driver failure.");

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

    [Fact]
    public void StateTransitions_EmitsEventOnlyWhenStateActuallyChanges()
    {
        var sessionCollector = new SessionCollector(NullLogger<SessionCollector>.Instance);
        sessionCollector.StartSession("device-001");

        var idleCollector = new StubIdleCollector { Active = true };
        var countCollector = new StubCountCollector();
        var botDetector = new StubBotDetector();

        var collector = new ActivityCollector(
            sessionCollector,
            idleCollector,
            countCollector,
            countCollector,
            botDetector,
            NullLogger<ActivityCollector>.Instance);

        // Cycle 1: First observation with Active state -> should emit ActivityStateChanged (IsActive = true)
        var eventsCycle1 = collector.Collect();
        var stateEvents1 = eventsCycle1.Where(e => e.Type == ActivityEventType.ActivityStateChanged).ToList();
        Assert.Single(stateEvents1);
        Assert.True(stateEvents1[0].IsActive);

        // Cycle 2: Same state (Active) -> MUST NOT emit duplicate state change event
        var eventsCycle2 = collector.Collect();
        var stateEvents2 = eventsCycle2.Where(e => e.Type == ActivityEventType.ActivityStateChanged).ToList();
        Assert.Empty(stateEvents2);

        // Cycle 3: Transition to Idle -> MUST emit ActivityStateChanged (IsActive = false)
        idleCollector.Active = false;
        var eventsCycle3 = collector.Collect();
        var stateEvents3 = eventsCycle3.Where(e => e.Type == ActivityEventType.ActivityStateChanged).ToList();
        Assert.Single(stateEvents3);
        Assert.False(stateEvents3[0].IsActive);

        // Cycle 4: Same state (Idle) -> MUST NOT emit duplicate state change event
        var eventsCycle4 = collector.Collect();
        var stateEvents4 = eventsCycle4.Where(e => e.Type == ActivityEventType.ActivityStateChanged).ToList();
        Assert.Empty(stateEvents4);

        // Cycle 5: Transition back to Active -> MUST emit ActivityStateChanged (IsActive = true)
        idleCollector.Active = true;
        var eventsCycle5 = collector.Collect();
        var stateEvents5 = eventsCycle5.Where(e => e.Type == ActivityEventType.ActivityStateChanged).ToList();
        Assert.Single(stateEvents5);
        Assert.True(stateEvents5[0].IsActive);
    }

    [Fact]
    public void Collect_ZeroActivity_DoesNotEmitInputEvents()
    {
        var sessionCollector = new SessionCollector(NullLogger<SessionCollector>.Instance);
        sessionCollector.StartSession("device-001");

        var idleCollector = new StubIdleCollector { Active = true };
        var countCollector = new StubCountCollector
        {
            NextKeyboardCount = 0,
            NextMouseCount = 0
        };
        var botDetector = new StubBotDetector();

        var collector = new ActivityCollector(
            sessionCollector,
            idleCollector,
            countCollector,
            countCollector,
            botDetector,
            NullLogger<ActivityCollector>.Instance);

        // Initialize state
        collector.Collect();

        // Sample with 0 activity
        var events = collector.Collect();

        // Should have zero keyboard or mouse events
        Assert.DoesNotContain(events, e => e.Type == ActivityEventType.KeyboardActivity);
        Assert.DoesNotContain(events, e => e.Type == ActivityEventType.MouseActivity);
        Assert.Empty(events);
    }

    [Fact]
    public void Collect_WithInputActivity_EmitsIndividualCounts()
    {
        var sessionCollector = new SessionCollector(NullLogger<SessionCollector>.Instance);
        sessionCollector.StartSession("device-001");

        var idleCollector = new StubIdleCollector { Active = true };
        var countCollector = new StubCountCollector();
        var botDetector = new StubBotDetector();

        var collector = new ActivityCollector(
            sessionCollector,
            idleCollector,
            countCollector,
            countCollector,
            botDetector,
            NullLogger<ActivityCollector>.Instance);

        // Warm up initial state
        collector.Collect();

        // Supply counts
        countCollector.NextKeyboardCount = 42;
        countCollector.NextMouseCount = 18;

        var events = collector.Collect();

        var kbEvent = Assert.Single(events, e => e.Type == ActivityEventType.KeyboardActivity);
        Assert.Equal(42, kbEvent.Count);

        var mouseEvent = Assert.Single(events, e => e.Type == ActivityEventType.MouseActivity);
        Assert.Equal(18, mouseEvent.Count);
    }

    [Fact]
    public void ActivityAggregation_AggregatesCountsAcrossSamples_AndFlushBatchResets()
    {
        var sessionCollector = new SessionCollector(NullLogger<SessionCollector>.Instance);
        var session = sessionCollector.StartSession("device-001");

        var idleCollector = new StubIdleCollector { Active = true };
        var countCollector = new StubCountCollector();
        var botDetector = new StubBotDetector();

        var collector = new ActivityCollector(
            sessionCollector,
            idleCollector,
            countCollector,
            countCollector,
            botDetector,
            NullLogger<ActivityCollector>.Instance);

        // Sample 1: 15 kb, 5 mouse
        countCollector.NextKeyboardCount = 15;
        countCollector.NextMouseCount = 5;
        collector.Collect();

        // Sample 2: 25 kb, 10 mouse
        countCollector.NextKeyboardCount = 25;
        countCollector.NextMouseCount = 10;
        collector.Collect();

        // Flush Batch
        var batch = collector.FlushBatch();

        Assert.NotNull(batch);
        Assert.Equal(session.SessionId, batch.SessionId);
        Assert.Equal("device-001", batch.DeviceId);
        Assert.Equal(40, batch.KeyboardCount); // 15 + 25
        Assert.Equal(15, batch.MouseCount);    // 5 + 10
        Assert.False(batch.HasSuspiciousMouseActivity);

        // Second Flush immediately after should have 0 counts (counter reset verification)
        var batch2 = collector.FlushBatch();
        Assert.Equal(0, batch2.KeyboardCount);
        Assert.Equal(0, batch2.MouseCount);
    }

    [Fact]
    public void BatchDuration_SumOfActiveAndIdleDuration_ApproximatelyEqualsBatchDuration()
    {
        var sessionCollector = new SessionCollector(NullLogger<SessionCollector>.Instance);
        sessionCollector.StartSession("device-001");

        var idleCollector = new StubIdleCollector { Active = true };
        var countCollector = new StubCountCollector();
        var botDetector = new StubBotDetector();

        var collector = new ActivityCollector(
            sessionCollector,
            idleCollector,
            countCollector,
            countCollector,
            botDetector,
            NullLogger<ActivityCollector>.Instance);

        collector.Collect(); // Initialize start
        Thread.Sleep(30);

        idleCollector.Active = false;
        collector.Collect(); // Transition to idle
        Thread.Sleep(30);

        var batch = collector.FlushBatch();

        var totalBatchDuration = batch.EndedAt - batch.StartedAt;
        var sumOfDurations = batch.ActiveDuration + batch.IdleDuration;

        // Ensure ActiveDuration + IdleDuration is approximately equal to EndedAt - StartedAt
        var difference = Math.Abs((totalBatchDuration - sumOfDurations).TotalMilliseconds);
        Assert.True(difference < 15, $"Difference between batch duration and sum was {difference} ms (expected < 15 ms).");
        Assert.True(batch.ActiveDuration > TimeSpan.Zero);
        Assert.True(batch.IdleDuration > TimeSpan.Zero);
    }

    [Fact]
    public void BatchBoundaries_MaintainSequentialTimestamps()
    {
        var sessionCollector = new SessionCollector(NullLogger<SessionCollector>.Instance);
        sessionCollector.StartSession("device-001");

        var idleCollector = new StubIdleCollector { Active = true };
        var countCollector = new StubCountCollector();
        var botDetector = new StubBotDetector();

        var collector = new ActivityCollector(
            sessionCollector,
            idleCollector,
            countCollector,
            countCollector,
            botDetector,
            NullLogger<ActivityCollector>.Instance);

        collector.Collect();
        Thread.Sleep(20);
        var batch1 = collector.FlushBatch();

        Thread.Sleep(20);
        collector.Collect();
        var batch2 = collector.FlushBatch();

        Assert.True(batch1.StartedAt <= batch1.EndedAt);
        Assert.True(batch2.StartedAt <= batch2.EndedAt);
        Assert.True(batch2.StartedAt >= batch1.EndedAt);
    }

    [Fact]
    public void Batch_IsAssociatedWithCurrentSessionAndDeviceId()
    {
        var sessionCollector = new SessionCollector(NullLogger<SessionCollector>.Instance);
        var session = sessionCollector.StartSession("device-unique-999");

        var idleCollector = new StubIdleCollector { Active = true };
        var countCollector = new StubCountCollector();
        var botDetector = new StubBotDetector();

        var collector = new ActivityCollector(
            sessionCollector,
            idleCollector,
            countCollector,
            countCollector,
            botDetector,
            NullLogger<ActivityCollector>.Instance);

        collector.Collect();
        var batch = collector.FlushBatch();

        Assert.Equal("device-unique-999", batch.DeviceId);
        Assert.Equal(session.SessionId, batch.SessionId);
        Assert.False(string.IsNullOrWhiteSpace(batch.BatchId));
    }

    [Fact]
    public void Collect_WhenKeyboardCollectorFails_IsolatesErrorAndCollectsMouse()
    {
        var sessionCollector = new SessionCollector(NullLogger<SessionCollector>.Instance);
        sessionCollector.StartSession("device-001");

        var idleCollector = new StubIdleCollector { Active = true };
        var countCollector = new StubCountCollector
        {
            ThrowOnKeyboard = true,
            NextMouseCount = 7
        };
        var botDetector = new StubBotDetector();

        var collector = new ActivityCollector(
            sessionCollector,
            idleCollector,
            countCollector,
            countCollector,
            botDetector,
            NullLogger<ActivityCollector>.Instance);

        // Does not throw despite keyboard failure
        var events = collector.Collect();

        // Mouse activity was successfully gathered despite keyboard failure
        Assert.Contains(events, e => e.Type == ActivityEventType.MouseActivity && e.Count == 7);

        var batch = collector.FlushBatch();
        Assert.Equal(0, batch.KeyboardCount);
        Assert.Equal(7, batch.MouseCount);
    }

    [Fact]
    public void Collect_WhenMouseCollectorFails_IsolatesErrorAndCollectsKeyboard()
    {
        var sessionCollector = new SessionCollector(NullLogger<SessionCollector>.Instance);
        sessionCollector.StartSession("device-001");

        var idleCollector = new StubIdleCollector { Active = true };
        var countCollector = new StubCountCollector
        {
            ThrowOnMouse = true,
            NextKeyboardCount = 14
        };
        var botDetector = new StubBotDetector();

        var collector = new ActivityCollector(
            sessionCollector,
            idleCollector,
            countCollector,
            countCollector,
            botDetector,
            NullLogger<ActivityCollector>.Instance);

        // Does not throw despite mouse failure
        var events = collector.Collect();

        // Keyboard activity was successfully gathered despite mouse failure
        Assert.Contains(events, e => e.Type == ActivityEventType.KeyboardActivity && e.Count == 14);

        var batch = collector.FlushBatch();
        Assert.Equal(14, batch.KeyboardCount);
        Assert.Equal(0, batch.MouseCount);
    }

    [Fact]
    public void Collect_WhenIdleCollectorFails_IsolatesErrorAndCollectsInput()
    {
        var sessionCollector = new SessionCollector(NullLogger<SessionCollector>.Instance);
        sessionCollector.StartSession("device-001");

        var idleCollector = new StubIdleCollector
        {
            ThrowOnIdle = true
        };
        var countCollector = new StubCountCollector
        {
            NextKeyboardCount = 8,
            NextMouseCount = 4
        };
        var botDetector = new StubBotDetector();

        var collector = new ActivityCollector(
            sessionCollector,
            idleCollector,
            countCollector,
            countCollector,
            botDetector,
            NullLogger<ActivityCollector>.Instance);

        // Does not throw; falls back safely to Active
        var events = collector.Collect();

        Assert.Contains(events, e => e.Type == ActivityEventType.KeyboardActivity && e.Count == 8);
        Assert.Contains(events, e => e.Type == ActivityEventType.MouseActivity && e.Count == 4);

        var batch = collector.FlushBatch();
        Assert.Equal(8, batch.KeyboardCount);
        Assert.Equal(4, batch.MouseCount);
    }
}