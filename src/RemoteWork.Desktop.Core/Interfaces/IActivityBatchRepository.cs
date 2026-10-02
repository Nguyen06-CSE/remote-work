using RemoteWork.Desktop.Core.Models.Activity;

namespace RemoteWork.Desktop.Core.Interfaces;

public interface IActivityBatchRepository
{
    Task SaveAsync(ActivityBatch batch, CancellationToken ct = default);
    Task<IReadOnlyList<ActivityBatch>> GetBySessionIdAsync(string sessionId, CancellationToken ct = default);
    Task<IReadOnlyList<ActivityBatch>> GetByDeviceIdAsync(string deviceId, CancellationToken ct = default);
}
