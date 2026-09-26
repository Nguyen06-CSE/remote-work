using RemoteWork.Desktop.Core.Enums;

namespace RemoteWork.Desktop.Core.Models;

public sealed class AgentRuntimeState
{
    public AgentStatus Status { get; private set; } = AgentStatus.Starting;

    public DateTimeOffset StartedAt { get; private set; }

    public void MarkStarting()
    {
        Status = AgentStatus.Starting;
    }

    public void MarkRunning()
    {
        Status = AgentStatus.Running;
        StartedAt = DateTimeOffset.UtcNow;
    }

    public void MarkStopping()
    {
        Status = AgentStatus.Stopping;
    }

    public void MarkStopped()
    {
        Status = AgentStatus.Stopped;
    }

    public void MarkError()
    {
        Status = AgentStatus.Error;
    }
}
