using RemoteWork.Desktop.Core.Models;
using RemoteWork.Desktop.Persistence.Repositories;

namespace RemoteWork.Desktop.IntegrationTests.Persistence;

/// <summary>
/// Tests for the SyncQueue: enqueue, get pending, mark sent, mark failed, delete sent.
/// </summary>
public sealed class SyncQueuePersistenceTests : IDisposable
{
    private readonly DbContextFixture _fixture = new();

    private static SyncQueueItem MakeItem(string? itemId = null) => new()
    {
        ItemId = itemId ?? Guid.NewGuid().ToString(),
        EntityType = "ActivityBatch",
        EntityId = Guid.NewGuid().ToString(),
        PayloadJson = """{"batchId":"test"}""",
        CreatedAt = DateTimeOffset.UtcNow
    };

    [Fact]
    public async Task EnqueueAsync_Should_Add_Item_As_Pending()
    {
        var repo = new SyncQueueRepository(_fixture.Context);
        var item = MakeItem();

        await repo.EnqueueAsync(item);

        var stored = await _fixture.Context.SyncQueue.FindAsync(item.ItemId);
        Assert.NotNull(stored);
        Assert.Equal("Pending", stored.Status);
        Assert.Equal(0, stored.RetryCount);
        Assert.Null(stored.SentAt);
        Assert.Null(stored.FailureReason);
    }

    [Fact]
    public async Task GetPendingAsync_Should_Return_Only_Pending_Items()
    {
        var repo = new SyncQueueRepository(_fixture.Context);

        await repo.EnqueueAsync(MakeItem("p1"));
        await repo.EnqueueAsync(MakeItem("p2"));
        await repo.EnqueueAsync(MakeItem("p3"));

        // Mark p2 as sent
        await repo.MarkSentAsync("p2");

        var pending = await repo.GetPendingAsync();

        Assert.Equal(2, pending.Count);
        Assert.DoesNotContain(pending, p => p.ItemId == "p2");
    }

    [Fact]
    public async Task GetPendingAsync_Should_Respect_Limit()
    {
        var repo = new SyncQueueRepository(_fixture.Context);
        for (var i = 0; i < 10; i++)
            await repo.EnqueueAsync(MakeItem());

        var pending = await repo.GetPendingAsync(limit: 3);

        Assert.Equal(3, pending.Count);
    }

    [Fact]
    public async Task GetPendingAsync_Should_Return_Oldest_Items_First()
    {
        var repo = new SyncQueueRepository(_fixture.Context);
        var t = DateTimeOffset.UtcNow;

        await repo.EnqueueAsync(new SyncQueueItem { ItemId = "old", EntityType = "ActivityBatch", EntityId = "e1", PayloadJson = "{}", CreatedAt = t.AddMinutes(-10) });
        await repo.EnqueueAsync(new SyncQueueItem { ItemId = "new", EntityType = "ActivityBatch", EntityId = "e2", PayloadJson = "{}", CreatedAt = t });

        var pending = await repo.GetPendingAsync(limit: 2);

        Assert.Equal("old", pending[0].ItemId);
        Assert.Equal("new", pending[1].ItemId);
    }

    [Fact]
    public async Task MarkSentAsync_Should_Update_Status_And_SentAt()
    {
        var repo = new SyncQueueRepository(_fixture.Context);
        var item = MakeItem("to-send");
        await repo.EnqueueAsync(item);

        var before = DateTimeOffset.UtcNow;
        await repo.MarkSentAsync("to-send");

        var stored = await _fixture.Context.SyncQueue.FindAsync("to-send");
        Assert.NotNull(stored);
        Assert.Equal("Sent", stored.Status);
        Assert.NotNull(stored.SentAt);
        Assert.True(stored.SentAt >= before);
    }

    [Fact]
    public async Task MarkFailedAsync_Should_Update_Status_Reason_And_RetryCount()
    {
        var repo = new SyncQueueRepository(_fixture.Context);
        var item = MakeItem("to-fail");
        await repo.EnqueueAsync(item);

        await repo.MarkFailedAsync("to-fail", "connection timeout");
        await repo.MarkFailedAsync("to-fail", "connection timeout");

        var stored = await _fixture.Context.SyncQueue.FindAsync("to-fail");
        Assert.NotNull(stored);
        Assert.Equal("Failed", stored.Status);
        Assert.Equal("connection timeout", stored.FailureReason);
        Assert.Equal(2, stored.RetryCount);
    }

    [Fact]
    public async Task DeleteSentAsync_Should_Remove_All_Sent_Items()
    {
        var repo = new SyncQueueRepository(_fixture.Context);
        await repo.EnqueueAsync(MakeItem("sent-1"));
        await repo.EnqueueAsync(MakeItem("sent-2"));
        await repo.EnqueueAsync(MakeItem("pending-1"));

        await repo.MarkSentAsync("sent-1");
        await repo.MarkSentAsync("sent-2");

        await repo.DeleteSentAsync();

        var remaining = await repo.GetPendingAsync();
        Assert.Single(remaining);
        Assert.Equal("pending-1", remaining[0].ItemId);

        var allItems = _fixture.Context.SyncQueue.ToList();
        Assert.DoesNotContain(allItems, i => i.ItemId == "sent-1");
        Assert.DoesNotContain(allItems, i => i.ItemId == "sent-2");
    }

    [Fact]
    public async Task DeleteSentAsync_On_Empty_Queue_Should_Not_Throw()
    {
        var repo = new SyncQueueRepository(_fixture.Context);

        var ex = await Record.ExceptionAsync(() => repo.DeleteSentAsync());

        Assert.Null(ex);
    }

    public void Dispose() => _fixture.Dispose();
}
