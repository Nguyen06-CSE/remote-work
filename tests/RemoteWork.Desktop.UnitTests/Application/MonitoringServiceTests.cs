using Microsoft.Extensions.Logging.Abstractions;
using RemoteWork.Desktop.Application.Collectors;
using RemoteWork.Desktop.Application.Monitoring;
using RemoteWork.Desktop.Core.Enums;
using RemoteWork.Desktop.Core.Interfaces;
using RemoteWork.Desktop.Core.Models.Activity;
using RemoteWork.Desktop.Platform.Abstractions;
using Xunit;

namespace RemoteWork.Desktop.UnitTests.Application;

public class MonitoringServiceTests
{
    private sealed class MockInputActivityProvider : IInputActivityProvider
    {
        public bool IsStarted { get; private set; }
        public int StartCallCount { get; private set; }
        public int StopCallCount { get; private set; }

        public void Start()
        {
            IsStarted = true;
            StartCallCount++;
        }

        public void Stop()
        {
            IsStarted = false;
            StopCallCount++;
        }

        public int GetKeyboardCount() => 0;
        public int GetMouseCount() => 0;
        public IReadOnlyList<MouseClickSample> DrainMouseSamples() => Array.Empty<MouseClickSample>();
        public void Dispose() => Stop();
    }

    private sealed class MockActivityCollector : IActivityCollector
    {
        public int CollectCallCount { get; private set; }
        public int FlushBatchCallCount { get; private set; }
        public bool ThrowOnCollect { get; set; }

        public IReadOnlyList<ActivityEvent> Collect()
        {
            CollectCallCount++;
            if (ThrowOnCollect)
                throw new InvalidOperationException("Simulated collection error.");

            return [];
        }

        public ActivityBatch FlushBatch()
        {
            FlushBatchCallCount++;
            return new ActivityBatch
            {
                BatchId = Guid.NewGuid().ToString(),
                DeviceId = "test-device-id",
                SessionId = "test-session-id",
                StartedAt = DateTimeOffset.UtcNow.AddSeconds(-60),
                EndedAt = DateTimeOffset.UtcNow,
                KeyboardCount = 20,
                MouseCount = 10,
                ActiveDuration = TimeSpan.FromSeconds(50),
                IdleDuration = TimeSpan.FromSeconds(10),
                HasSuspiciousMouseActivity = false
            };
        }
    }

    [Fact]
    public async Task Lifecycle_StartsAndStopsInputProviderCleanly()
    {
        var inputProvider = new MockInputActivityProvider();
        var activityCollector = new MockActivityCollector();
        var sessionCollector = new SessionCollector(NullLogger<SessionCollector>.Instance);

        var monitoringService = new MonitoringService(
            activityCollector,
            inputProvider,
            sessionCollector,
            samplingIntervalSeconds: 1,
            batchIntervalSeconds: 2,
            NullLogger<MonitoringService>.Instance);

        Assert.False(inputProvider.IsStarted);

        using var cts = new CancellationTokenSource();
        await monitoringService.StartAsync(cts.Token);
        Assert.True(inputProvider.IsStarted);
        Assert.Equal(1, inputProvider.StartCallCount);

        // Multiple starts should be idempotent
        await monitoringService.StartAsync(cts.Token);
        Assert.Equal(1, inputProvider.StartCallCount);

        await monitoringService.StopAsync(CancellationToken.None);
        Assert.False(inputProvider.IsStarted);
        Assert.Equal(1, inputProvider.StopCallCount);

        // Multiple stops should be idempotent
        await monitoringService.StopAsync(CancellationToken.None);
        Assert.Equal(1, inputProvider.StopCallCount);
    }

    [Fact]
    public async Task GracefulShutdown_FlushesFinalActivityBatch_WhenSessionIsActive()
    {
        var inputProvider = new MockInputActivityProvider();
        var activityCollector = new MockActivityCollector();
        var sessionCollector = new SessionCollector(NullLogger<SessionCollector>.Instance);
        sessionCollector.StartSession("device-test-1");

        var monitoringService = new MonitoringService(
            activityCollector,
            inputProvider,
            sessionCollector,
            samplingIntervalSeconds: 10,
            batchIntervalSeconds: 60,
            NullLogger<MonitoringService>.Instance);

        ActivityBatch? receivedBatch = null;
        monitoringService.OnBatchGenerated += batch => receivedBatch = batch;

        using var cts = new CancellationTokenSource();
        await monitoringService.StartAsync(cts.Token);

        // Stop without waiting for normal timer interval
        await monitoringService.StopAsync(CancellationToken.None);

        Assert.NotNull(receivedBatch);
        Assert.Equal("test-device-id", receivedBatch.DeviceId);
        Assert.Equal("test-session-id", receivedBatch.SessionId);
        Assert.True(activityCollector.FlushBatchCallCount >= 1);
    }

    [Fact]
    public async Task StopAsync_DoesNotFlushBatch_WhenNoActiveSession()
    {
        var inputProvider = new MockInputActivityProvider();
        var activityCollector = new MockActivityCollector();
        var sessionCollector = new SessionCollector(NullLogger<SessionCollector>.Instance);
        // Note: No session started

        var monitoringService = new MonitoringService(
            activityCollector,
            inputProvider,
            sessionCollector,
            samplingIntervalSeconds: 10,
            batchIntervalSeconds: 60,
            NullLogger<MonitoringService>.Instance);

        var batchCount = 0;
        monitoringService.OnBatchGenerated += _ => batchCount++;

        using var cts = new CancellationTokenSource();
        await monitoringService.StartAsync(cts.Token);
        await monitoringService.StopAsync(CancellationToken.None);

        Assert.Equal(0, batchCount);
        Assert.Equal(0, activityCollector.FlushBatchCallCount);
    }

    [Fact]
    public async Task Cancellation_TerminatesMonitoringTaskCleanly()
    {
        var inputProvider = new MockInputActivityProvider();
        var activityCollector = new MockActivityCollector();
        var sessionCollector = new SessionCollector(NullLogger<SessionCollector>.Instance);

        var monitoringService = new MonitoringService(
            activityCollector,
            inputProvider,
            sessionCollector,
            samplingIntervalSeconds: 1,
            batchIntervalSeconds: 2,
            NullLogger<MonitoringService>.Instance);

        using var cts = new CancellationTokenSource();
        await monitoringService.StartAsync(cts.Token);

        // Cancel token directly
        await cts.CancelAsync();

        // Stop should complete quickly without throwing
        await monitoringService.StopAsync(CancellationToken.None);
        Assert.False(inputProvider.IsStarted);
    }

    [Fact]
    public async Task ErrorIsolation_CollectorFailureDoesNotCrashRuntime()
    {
        var inputProvider = new MockInputActivityProvider();
        var activityCollector = new MockActivityCollector { ThrowOnCollect = true };
        var sessionCollector = new SessionCollector(NullLogger<SessionCollector>.Instance);
        sessionCollector.StartSession("device-test-error");

        var monitoringService = new MonitoringService(
            activityCollector,
            inputProvider,
            sessionCollector,
            samplingIntervalSeconds: 1,
            batchIntervalSeconds: 1,
            NullLogger<MonitoringService>.Instance);

        using var cts = new CancellationTokenSource();
        await monitoringService.StartAsync(cts.Token);

        // Allow at least 1-2 ticks to occur
        await Task.Delay(1100);

        // Verify runtime is still running and stops gracefully
        await monitoringService.StopAsync(CancellationToken.None);

        Assert.True(activityCollector.CollectCallCount >= 1);
        Assert.False(inputProvider.IsStarted);
    }
}
