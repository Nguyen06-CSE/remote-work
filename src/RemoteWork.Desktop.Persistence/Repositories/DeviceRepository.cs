using Microsoft.EntityFrameworkCore;
using RemoteWork.Desktop.Core.Interfaces;
using RemoteWork.Desktop.Core.Models;
using RemoteWork.Desktop.Persistence.Data;
using RemoteWork.Desktop.Persistence.Entities;

namespace RemoteWork.Desktop.Persistence.Repositories;

public sealed class DeviceRepository : IDeviceRepository
{
    private readonly RemoteWorkDbContext _context;

    public DeviceRepository(RemoteWorkDbContext context)
    {
        _context = context;
    }

    public async Task<Device?> GetByDeviceIdAsync(string deviceId, CancellationToken ct = default)
    {
        var entity = await _context.Devices.AsNoTracking().FirstOrDefaultAsync(d => d.DeviceId == deviceId, ct);
        if (entity == null) return null;

        return new Device
        {
            DeviceId = entity.DeviceId,
            Hostname = entity.Hostname,
            OperatingSystem = entity.OperatingSystem,
            OsVersion = entity.OsVersion,
            AgentVersion = entity.AgentVersion
        };
    }

    public async Task UpsertAsync(Device device, CancellationToken ct = default)
    {
        var entity = await _context.Devices.FirstOrDefaultAsync(d => d.DeviceId == device.DeviceId, ct);
        if (entity == null)
        {
            entity = new DeviceEntity
            {
                DeviceId = device.DeviceId,
                Hostname = device.Hostname,
                OperatingSystem = device.OperatingSystem,
                OsVersion = device.OsVersion,
                AgentVersion = device.AgentVersion,
                RegisteredAt = DateTimeOffset.UtcNow,
                LastSeenAt = DateTimeOffset.UtcNow
            };
            _context.Devices.Add(entity);
        }
        else
        {
            entity.Hostname = device.Hostname;
            entity.OperatingSystem = device.OperatingSystem;
            entity.OsVersion = device.OsVersion;
            entity.AgentVersion = device.AgentVersion;
            entity.LastSeenAt = DateTimeOffset.UtcNow;
        }

        await _context.SaveChangesAsync(ct);
    }
}
