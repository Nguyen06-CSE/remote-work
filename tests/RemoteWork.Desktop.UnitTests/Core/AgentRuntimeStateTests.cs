using RemoteWork.Desktop.Core.Enums;
using RemoteWork.Desktop.Core.Models;
using Xunit;

namespace RemoteWork.Desktop.UnitTests.Core;

public class AgentRuntimeStateTests
{
    [Fact]
    public void Initial_Status_Should_Be_Starting()
    {
        var state = new AgentRuntimeState();

        Assert.Equal(AgentStatus.Starting, state.Status);
    }

    [Fact]
    public void MarkRunning_Should_Set_Running_Status()
    {
        var state = new AgentRuntimeState();

        state.MarkRunning();

        Assert.Equal(AgentStatus.Running, state.Status);
    }

    [Fact]
    public void MarkStopped_Should_Set_Stopped_Status()
    {
        var state = new AgentRuntimeState();

        state.MarkStopped();

        Assert.Equal(AgentStatus.Stopped, state.Status);
    }

    [Fact]
    public void MarkStopping_Should_Set_Stopping_Status()
    {
        var state = new AgentRuntimeState();

        state.MarkStopping();

        Assert.Equal(AgentStatus.Stopping, state.Status);
    }

    [Fact]
    public void MarkError_Should_Set_Error_Status()
    {
        var state = new AgentRuntimeState();

        state.MarkError();

        Assert.Equal(AgentStatus.Error, state.Status);
    }
}
