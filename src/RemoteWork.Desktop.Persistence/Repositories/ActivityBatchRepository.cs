using Microsoft.EntityFrameworkCore;
using RemoteWork.Desktop.Core.Interfaces;
using RemoteWork.Desktop.Core.Models.Activity;
using RemoteWork.Desktop.Persistence.Data;
using RemoteWork.Desktop.Persistence.Entities;

namespace RemoteWork.Desktop.Persistence.Repositories;

public sealed class ActivityBatchRepository : IActivityBatchRepository
{
    private readonly RemoteWorkDbContext _context;

    public ActivityBatchRepository(RemoteWorkDbContext context)
    {
        _context = context;
    }

    public async Task SaveAsync(ActivityBatch batch, CancellationToken ct = default)
    {
        var existing = await _context.ActivityBatches.FindAsync([batch.BatchId], ct);
        if (existing is not null)
        {
            return; // Idempotent: already saved
        }

        var entity = new ActivityBatchEntity
        {
            BatchId = batch.BatchId,
            DeviceId = batch.DeviceId,
            SessionId = batch.SessionId,
            StartedAt = batch.StartedAt,
            EndedAt = batch.EndedAt,
            KeyboardCount = batch.KeyboardCount,
            MouseCount = batch.MouseCount,
            ActiveDurationTicks = batch.ActiveDuration.Ticks,
            IdleDurationTicks = batch.IdleDuration.Ticks,
            HasSuspiciousMouseActivity = batch.HasSuspiciousMouseActivity
        };
        _context.ActivityBatches.Add(entity);
        await _context.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<ActivityBatch>> GetBySessionIdAsync(string sessionId, CancellationToken ct = default)
    {
        var entities = await _context.ActivityBatches.AsNoTracking()
            .Where(b => b.SessionId == sessionId)
            .OrderBy(b => b.StartedAt)
            .ToListAsync(ct);

        return entities.Select(Map).ToList();
    }

    public async Task<IReadOnlyList<ActivityBatch>> GetByDeviceIdAsync(string deviceId, CancellationToken ct = default)
    {
        var entities = await _context.ActivityBatches.AsNoTracking()
            .Where(b => b.DeviceId == deviceId)
            .OrderByDescending(b => b.StartedAt)
            .ToListAsync(ct);

        return entities.Select(Map).ToList();
    }

    private static ActivityBatch Map(ActivityBatchEntity entity)
    {
        return new ActivityBatch
        {
            BatchId = entity.BatchId,
            DeviceId = entity.DeviceId,
            SessionId = entity.SessionId,
            StartedAt = entity.StartedAt,
            EndedAt = entity.EndedAt,
            KeyboardCount = entity.KeyboardCount,
            MouseCount = entity.MouseCount,
            ActiveDuration = TimeSpan.FromTicks(entity.ActiveDurationTicks),
            IdleDuration = TimeSpan.FromTicks(entity.IdleDurationTicks),
            HasSuspiciousMouseActivity = entity.HasSuspiciousMouseActivity
        };
    }
}
