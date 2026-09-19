using RemoteWork.Agent.Core.Models.Activity;

namespace RemoteWork.Agent.Core.Interfaces;

public interface IActivityCollector
{
    IReadOnlyList<ActivityEvent> Collect();

    ActivityBatch FlushBatch();
}