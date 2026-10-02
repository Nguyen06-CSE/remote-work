using RemoteWork.Desktop.Core.Models;

namespace RemoteWork.Desktop.Core.Interfaces;

public interface IApplicationActivityRepository
{
    Task SaveAsync(ApplicationActivity activity, CancellationToken ct = default);
    Task<IReadOnlyList<ApplicationActivity>> GetBySessionIdAsync(string sessionId, CancellationToken ct = default);
    Task<IReadOnlyList<ApplicationActivity>> GetByDeviceIdAsync(string deviceId, int limit = 100, CancellationToken ct = default);
}
