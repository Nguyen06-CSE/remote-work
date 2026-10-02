using Microsoft.EntityFrameworkCore;
using RemoteWork.Desktop.Core.Interfaces;
using RemoteWork.Desktop.Core.Models;
using RemoteWork.Desktop.Persistence.Data;
using RemoteWork.Desktop.Persistence.Entities;

namespace RemoteWork.Desktop.Persistence.Repositories;

public sealed class SyncQueueRepository : ISyncQueueRepository
{
    private readonly RemoteWorkDbContext _context;

    public SyncQueueRepository(RemoteWorkDbContext context)
    {
        _context = context;
    }

    public async Task EnqueueAsync(SyncQueueItem item, CancellationToken ct = default)
    {
        var entity = new SyncQueueItemEntity
        {
            ItemId = item.ItemId,
            EntityType = item.EntityType,
            EntityId = item.EntityId,
            PayloadJson = item.PayloadJson,
            Status = "Pending",
            CreatedAt = item.CreatedAt,
            RetryCount = 0
        };
        _context.SyncQueue.Add(entity);
        await _context.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<SyncQueueItem>> GetPendingAsync(int limit = 50, CancellationToken ct = default)
    {
        var entities = await _context.SyncQueue.AsNoTracking()
            .Where(q => q.Status == "Pending")
            .OrderBy(q => q.CreatedAt)
            .Take(limit)
            .ToListAsync(ct);

        return entities.Select(e => new SyncQueueItem
        {
            ItemId = e.ItemId,
            EntityType = e.EntityType,
            EntityId = e.EntityId,
            PayloadJson = e.PayloadJson,
            CreatedAt = e.CreatedAt
        }).ToList();
    }

    public async Task MarkSentAsync(string itemId, CancellationToken ct = default)
    {
        var entity = await _context.SyncQueue.FirstOrDefaultAsync(q => q.ItemId == itemId, ct);
        if (entity != null)
        {
            entity.Status = "Sent";
            entity.SentAt = DateTimeOffset.UtcNow;
            await _context.SaveChangesAsync(ct);
        }
    }

    public async Task MarkFailedAsync(string itemId, string reason, CancellationToken ct = default)
    {
        var entity = await _context.SyncQueue.FirstOrDefaultAsync(q => q.ItemId == itemId, ct);
        if (entity != null)
        {
            entity.Status = "Failed";
            entity.FailureReason = reason;
            entity.RetryCount++;
            await _context.SaveChangesAsync(ct);
        }
    }

    public async Task DeleteSentAsync(CancellationToken ct = default)
    {
        var sentItems = await _context.SyncQueue
            .Where(q => q.Status == "Sent")
            .ToListAsync(ct);
            
        if (sentItems.Any())
        {
            _context.SyncQueue.RemoveRange(sentItems);
            await _context.SaveChangesAsync(ct);
        }
    }
}
