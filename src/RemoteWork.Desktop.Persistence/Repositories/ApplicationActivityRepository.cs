using Microsoft.EntityFrameworkCore;
using RemoteWork.Desktop.Core.Interfaces;
using RemoteWork.Desktop.Core.Models;
using RemoteWork.Desktop.Persistence.Data;
using RemoteWork.Desktop.Persistence.Entities;

namespace RemoteWork.Desktop.Persistence.Repositories;

public sealed class ApplicationActivityRepository : IApplicationActivityRepository
{
    private readonly RemoteWorkDbContext _context;

    public ApplicationActivityRepository(RemoteWorkDbContext context)
    {
        _context = context;
    }

    public async Task SaveAsync(ApplicationActivity activity, CancellationToken ct = default)
    {
        var entity = new ApplicationActivityEntity
        {
            ActivityId = activity.ActivityId,
            DeviceId = activity.DeviceId,
            SessionId = activity.SessionId,
            Timestamp = activity.Timestamp,
            ApplicationName = activity.ApplicationName,
            ProcessName = activity.ProcessName,
            ProcessId = activity.ProcessId,
            WindowTitle = activity.WindowTitle,
            DurationTicks = activity.Duration.Ticks
        };
        _context.ApplicationActivities.Add(entity);
        await _context.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<ApplicationActivity>> GetBySessionIdAsync(string sessionId, CancellationToken ct = default)
    {
        var entities = await _context.ApplicationActivities.AsNoTracking()
            .Where(a => a.SessionId == sessionId)
            .OrderBy(a => a.Timestamp)
            .ToListAsync(ct);

        return entities.Select(Map).ToList();
    }

    public async Task<IReadOnlyList<ApplicationActivity>> GetByDeviceIdAsync(string deviceId, int limit = 100, CancellationToken ct = default)
    {
        var entities = await _context.ApplicationActivities.AsNoTracking()
            .Where(a => a.DeviceId == deviceId)
            .OrderByDescending(a => a.Timestamp)
            .Take(limit)
            .ToListAsync(ct);

        return entities.Select(Map).ToList();
    }

    private static ApplicationActivity Map(ApplicationActivityEntity entity)
    {
        return new ApplicationActivity
        {
            ActivityId = entity.ActivityId,
            DeviceId = entity.DeviceId,
            SessionId = entity.SessionId,
            Timestamp = entity.Timestamp,
            ApplicationName = entity.ApplicationName,
            ProcessName = entity.ProcessName,
            ProcessId = entity.ProcessId,
            WindowTitle = entity.WindowTitle,
            Duration = TimeSpan.FromTicks(entity.DurationTicks)
        };
    }
}
