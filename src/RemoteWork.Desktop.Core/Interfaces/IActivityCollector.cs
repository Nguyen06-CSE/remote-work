using RemoteWork.Desktop.Core.Models.Activity;

namespace RemoteWork.Desktop.Core.Interfaces;

public interface IActivityCollector
{
    IReadOnlyList<ActivityEvent> Collect();

    ActivityBatch FlushBatch();
}
