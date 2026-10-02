using RemoteWork.Desktop.Core.Models;

namespace RemoteWork.Desktop.Core.Interfaces;

public interface IDeviceRepository
{
    Task<Device?> GetByDeviceIdAsync(string deviceId, CancellationToken ct = default);
    Task UpsertAsync(Device device, CancellationToken ct = default);
}
