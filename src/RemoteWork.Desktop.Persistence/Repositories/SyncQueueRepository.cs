using Microsoft.EntityFrameworkCore;
using RemoteWork.Desktop.Core.Enums;
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
        var entity = MapToEntity(item);
        _context.SyncQueue.Add(entity);
        await _context.SaveChangesAsync(ct);
    }

    public async Task<bool> EnqueueIfNotExistsAsync(SyncQueueItem item, CancellationToken ct = default)
    {
        var exists = await _context.SyncQueue.AsNoTracking().AnyAsync(
            q => q.EntityType == item.EntityType && q.EntityId == item.EntityId,
            ct);

        if (exists)
        {
            return false;
        }

        var entity = MapToEntity(item);
        _context.SyncQueue.Add(entity);
        await _context.SaveChangesAsync(ct);
        return true;
    }

    public async Task<SyncQueueItem?> GetByIdAsync(string queueItemId, CancellationToken ct = default)
    {
        var entity = await _context.SyncQueue.AsNoTracking()
            .FirstOrDefaultAsync(q => q.QueueItemId == queueItemId, ct);

        return entity is null ? null : MapToDomain(entity);
    }

    public async Task<SyncQueueItem?> GetByEntityIdAsync(string entityType, string entityId, CancellationToken ct = default)
    {
        var entity = await _context.SyncQueue.AsNoTracking()
            .FirstOrDefaultAsync(q => q.EntityType == entityType && q.EntityId == entityId, ct);

        return entity is null ? null : MapToDomain(entity);
    }

    public async Task<IReadOnlyList<SyncQueueItem>> GetPendingAsync(int limit = 50, CancellationToken ct = default)
    {
        var entities = await _context.SyncQueue.AsNoTracking()
            .Where(q => q.Status == nameof(SyncStatus.Pending))
            .OrderBy(q => q.CreatedAt)
            .Take(limit)
            .ToListAsync(ct);

        return entities.Select(MapToDomain).ToList();
    }

    public async Task<IReadOnlyList<SyncQueueItem>> GetEligibleForSyncAsync(DateTimeOffset now, int limit = 50, CancellationToken ct = default)
    {
        var pendingStatus = nameof(SyncStatus.Pending);
        var failedStatus = nameof(SyncStatus.Failed);

        // An item is eligible if:
        // 1. Status is Pending
        // 2. Status is Failed, NOT permanent, and NextAttemptAt has arrived (null or <= now)
        var entities = await _context.SyncQueue.AsNoTracking()
            .Where(q => q.Status == pendingStatus ||
                        (q.Status == failedStatus && !q.IsPermanentFailure && (q.NextAttemptAt == null || q.NextAttemptAt <= now)))
            .OrderBy(q => q.CreatedAt)
            .Take(limit)
            .ToListAsync(ct);

        return entities.Select(MapToDomain).ToList();
    }

    public async Task<IReadOnlyList<SyncQueueItem>> GetByStatusAsync(SyncStatus status, int limit = 50, CancellationToken ct = default)
    {
        var statusStr = status.ToString();
        var entities = await _context.SyncQueue.AsNoTracking()
            .Where(q => q.Status == statusStr)
            .OrderBy(q => q.CreatedAt)
            .Take(limit)
            .ToListAsync(ct);

        return entities.Select(MapToDomain).ToList();
    }

    public async Task UpdateAsync(SyncQueueItem item, CancellationToken ct = default)
    {
        var entity = await _context.SyncQueue.FirstOrDefaultAsync(q => q.QueueItemId == item.QueueItemId, ct);
        if (entity is not null)
        {
            entity.Status = item.Status.ToString();
            entity.AttemptCount = item.AttemptCount;
            entity.LastAttemptAt = item.LastAttemptAt;
            entity.ErrorMessage = item.ErrorMessage;
            entity.IsPermanentFailure = item.IsPermanentFailure;
            entity.NextAttemptAt = item.NextAttemptAt;

            await _context.SaveChangesAsync(ct);
        }
    }

    public async Task<int> ResetInProgressToPendingAsync(CancellationToken ct = default)
    {
        var inProgressStatus = nameof(SyncStatus.InProgress);
        var pendingStatus = nameof(SyncStatus.Pending);

        var staleItems = await _context.SyncQueue
            .Where(q => q.Status == inProgressStatus)
            .ToListAsync(ct);

        if (staleItems.Count == 0)
        {
            return 0;
        }

        foreach (var item in staleItems)
        {
            item.Status = pendingStatus;
        }

        await _context.SaveChangesAsync(ct);
        return staleItems.Count;
    }

    public async Task MarkSentAsync(string itemId, CancellationToken ct = default)
    {
        var entity = await _context.SyncQueue.FirstOrDefaultAsync(q => q.QueueItemId == itemId, ct);
        if (entity is not null)
        {
            entity.Status = "Sent";
            entity.LastAttemptAt = DateTimeOffset.UtcNow;
            entity.ErrorMessage = null;
            entity.NextAttemptAt = null;
            await _context.SaveChangesAsync(ct);
        }
    }

    public async Task MarkFailedAsync(string itemId, string reason, CancellationToken ct = default)
    {
        var entity = await _context.SyncQueue.FirstOrDefaultAsync(q => q.QueueItemId == itemId, ct);
        if (entity is not null)
        {
            entity.Status = nameof(SyncStatus.Failed);
            entity.ErrorMessage = reason;
            entity.AttemptCount++;
            entity.LastAttemptAt = DateTimeOffset.UtcNow;
            await _context.SaveChangesAsync(ct);
        }
    }

    public async Task DeleteSentAsync(CancellationToken ct = default)
    {
        var syncedStatus = nameof(SyncStatus.Synced);
        var sentItems = await _context.SyncQueue
            .Where(q => q.Status == syncedStatus || q.Status == "Sent")
            .ToListAsync(ct);

        if (sentItems.Count > 0)
        {
            _context.SyncQueue.RemoveRange(sentItems);
            await _context.SaveChangesAsync(ct);
        }
    }

    public async Task<int> CountByStatusAsync(SyncStatus status, CancellationToken ct = default)
    {
        var statusStr = status.ToString();
        return await _context.SyncQueue.AsNoTracking().CountAsync(q => q.Status == statusStr, ct);
    }

    private static SyncQueueItemEntity MapToEntity(SyncQueueItem item) => new()
    {
        QueueItemId = item.QueueItemId,
        EntityType = item.EntityType,
        EntityId = item.EntityId,
        PayloadJson = item.PayloadJson,
        Status = item.Status.ToString(),
        CreatedAt = item.CreatedAt,
        LastAttemptAt = item.LastAttemptAt,
        ErrorMessage = item.ErrorMessage,
        AttemptCount = item.AttemptCount,
        IsPermanentFailure = item.IsPermanentFailure,
        NextAttemptAt = item.NextAttemptAt
    };

    private static SyncQueueItem MapToDomain(SyncQueueItemEntity entity)
    {
        Enum.TryParse<SyncStatus>(entity.Status, ignoreCase: true, out var status);

        // Normalize legacy "Sent" to Synced
        if (string.Equals(entity.Status, "Sent", StringComparison.OrdinalIgnoreCase))
        {
            status = SyncStatus.Synced;
        }

        return new SyncQueueItem(
            queueItemId: entity.QueueItemId,
            entityType: entity.EntityType,
            entityId: entity.EntityId,
            payloadJson: entity.PayloadJson,
            createdAt: entity.CreatedAt,
            attemptCount: entity.AttemptCount,
            lastAttemptAt: entity.LastAttemptAt,
            status: status,
            errorMessage: entity.ErrorMessage,
            isPermanentFailure: entity.IsPermanentFailure,
            nextAttemptAt: entity.NextAttemptAt);
    }
}
