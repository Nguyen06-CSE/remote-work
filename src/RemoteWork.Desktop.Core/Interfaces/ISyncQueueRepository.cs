using RemoteWork.Desktop.Core.Models;

namespace RemoteWork.Desktop.Core.Interfaces;

public interface ISyncQueueRepository
{
    Task EnqueueAsync(SyncQueueItem item, CancellationToken ct = default);
    Task<IReadOnlyList<SyncQueueItem>> GetPendingAsync(int limit = 50, CancellationToken ct = default);
    Task MarkSentAsync(string itemId, CancellationToken ct = default);
    Task MarkFailedAsync(string itemId, string reason, CancellationToken ct = default);
    Task DeleteSentAsync(CancellationToken ct = default);
}
