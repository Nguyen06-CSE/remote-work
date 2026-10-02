using RemoteWork.Desktop.Application.Options;
using RemoteWork.Desktop.Application.Sync;
using RemoteWork.Desktop.Core.Enums;
using RemoteWork.Desktop.Core.Models;
using Xunit;

namespace RemoteWork.Desktop.UnitTests.Core;

public class SyncQueueItemStateTransitionTests
{
    private static SyncQueueItem CreateTestItem() => new()
    {
        QueueItemId = Guid.NewGuid().ToString("D"),
        EntityType = "ActivityBatch",
        EntityId = Guid.NewGuid().ToString("D"),
        PayloadJson = """{"keyboardCount":10,"mouseCount":5}""",
        CreatedAt = DateTimeOffset.UtcNow
    };

    [Fact]
    public void Initial_Status_Should_Be_Pending()
    {
        var item = CreateTestItem();

        Assert.Equal(SyncStatus.Pending, item.Status);
        Assert.Equal(0, item.AttemptCount);
        Assert.Null(item.LastAttemptAt);
        Assert.Null(item.ErrorMessage);
        Assert.False(item.IsPermanentFailure);
    }

    [Fact]
    public void MarkInProgress_From_Pending_Should_Succeed_And_Increment_AttemptCount()
    {
        var item = CreateTestItem();
        var now = DateTimeOffset.UtcNow;

        item.MarkInProgress(now);

        Assert.Equal(SyncStatus.InProgress, item.Status);
        Assert.Equal(1, item.AttemptCount);
        Assert.Equal(now, item.LastAttemptAt);
    }

    [Fact]
    public void MarkSynced_From_InProgress_Should_Succeed()
    {
        var item = CreateTestItem();
        var now = DateTimeOffset.UtcNow;
        item.MarkInProgress(now);

        item.MarkSynced(now.AddSeconds(1));

        Assert.Equal(SyncStatus.Synced, item.Status);
        Assert.Equal(now.AddSeconds(1), item.LastAttemptAt);
        Assert.Null(item.ErrorMessage);
    }

    [Fact]
    public void MarkFailed_From_InProgress_Should_Succeed_With_Backoff_Delay()
    {
        var item = CreateTestItem();
        var now = DateTimeOffset.UtcNow;
        item.MarkInProgress(now);

        var retryDelay = TimeSpan.FromSeconds(5);
        item.MarkFailed("Server 500 error", now.AddSeconds(1), retryDelay, isPermanent: false);

        Assert.Equal(SyncStatus.Failed, item.Status);
        Assert.Equal("Server 500 error", item.ErrorMessage);
        Assert.False(item.IsPermanentFailure);
        Assert.NotNull(item.NextAttemptAt);
        Assert.Equal(now.AddSeconds(1) + retryDelay, item.NextAttemptAt);
    }

    [Fact]
    public void MarkFailed_Permanent_Should_Set_IsPermanentFailure_True_And_Null_NextAttemptAt()
    {
        var item = CreateTestItem();
        var now = DateTimeOffset.UtcNow;
        item.MarkInProgress(now);

        item.MarkFailed("400 Bad Request: Schema validation failed", now.AddSeconds(1), retryDelay: TimeSpan.FromSeconds(10), isPermanent: true);

        Assert.Equal(SyncStatus.Failed, item.Status);
        Assert.True(item.IsPermanentFailure);
        Assert.Null(item.NextAttemptAt);
    }

    [Fact]
    public void ResetToPending_From_InProgress_Should_Succeed_For_Crash_Recovery()
    {
        var item = CreateTestItem();
        item.MarkInProgress(DateTimeOffset.UtcNow);

        item.ResetToPending();

        Assert.Equal(SyncStatus.Pending, item.Status);
    }

    [Fact]
    public void Invalid_Transition_From_Pending_To_Synced_Should_Throw()
    {
        var item = CreateTestItem();

        Assert.Throws<InvalidOperationException>(() => item.MarkSynced(DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Invalid_Transition_From_Synced_To_InProgress_Should_Throw()
    {
        var item = CreateTestItem();
        item.MarkInProgress(DateTimeOffset.UtcNow);
        item.MarkSynced(DateTimeOffset.UtcNow);

        Assert.Throws<InvalidOperationException>(() => item.MarkInProgress(DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Exponential_Backoff_Calculation_Should_Scale_Correctly_And_Cap_At_Max()
    {
        var options = new SyncOptions
        {
            InitialRetryDelaySeconds = 2,
            BackoffMultiplier = 2.0,
            MaxRetryIntervalSeconds = 60
        };

        // Attempt 1: Initial delay = 2s
        Assert.Equal(TimeSpan.FromSeconds(2), SyncEngine.CalculateBackoff(1, options));

        // Attempt 2: 2 * 2^1 = 4s
        Assert.Equal(TimeSpan.FromSeconds(4), SyncEngine.CalculateBackoff(2, options));

        // Attempt 3: 2 * 2^2 = 8s
        Assert.Equal(TimeSpan.FromSeconds(8), SyncEngine.CalculateBackoff(3, options));

        // Attempt 4: 2 * 2^3 = 16s
        Assert.Equal(TimeSpan.FromSeconds(16), SyncEngine.CalculateBackoff(4, options));

        // Attempt 10: 2 * 2^9 = 1024s -> capped at 60s
        Assert.Equal(TimeSpan.FromSeconds(60), SyncEngine.CalculateBackoff(10, options));
    }
}
