using RemoteWork.Agent.Core.Enums;
using RemoteWork.Agent.Core.Models;

namespace RemoteWork.Agent.Tests;

public class AgentRuntimeStateTests
{
    [Fact]
    public void Initial_Status_Should_Be_Starting()
    {
        var state = new AgentRuntimeState();

        Assert.Equal(
            AgentStatus.Starting,
            state.Status);
    }

    [Fact]
    public void MarkRunning_Should_Set_Running_Status()
    {
        var state = new AgentRuntimeState();

        state.MarkRunning();

        Assert.Equal(
            AgentStatus.Running,
            state.Status);
    }

    [Fact]
    public void MarkStopped_Should_Set_Stopped_Status()
    {
        var state = new AgentRuntimeState();

        state.MarkStopped();

        Assert.Equal(
            AgentStatus.Stopped,
            state.Status);
    }
}