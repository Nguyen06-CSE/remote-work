using RemoteWork.Desktop.Core.Models;

namespace RemoteWork.Desktop.Core.Interfaces;

public interface ISessionRepository
{
    Task<Session?> GetBySessionIdAsync(string sessionId, CancellationToken ct = default);
    Task<IReadOnlyList<Session>> GetByDeviceIdAsync(string deviceId, CancellationToken ct = default);
    Task SaveAsync(Session session, CancellationToken ct = default);
    Task UpdateAsync(Session session, CancellationToken ct = default);
}
