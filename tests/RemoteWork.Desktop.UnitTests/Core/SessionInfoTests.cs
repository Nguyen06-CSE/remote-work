using RemoteWork.Desktop.Core.Enums;
using RemoteWork.Desktop.Core.Models;
using Xunit;

namespace RemoteWork.Desktop.UnitTests.Core;

public class SessionInfoTests
{
    [Fact]
    public void Session_Should_Start_As_Starting()
    {
        var session = new SessionInfo
        {
            SessionId = "session-001",
            DeviceId = "device-001",
            StartedAt = DateTimeOffset.UtcNow
        };

        Assert.Equal(SessionStatus.Starting, session.Status);
    }

    [Fact]
    public void MarkActive_Should_Set_Active_Status()
    {
        var session = new SessionInfo
        {
            SessionId = "session-001",
            DeviceId = "device-001",
            StartedAt = DateTimeOffset.UtcNow
        };

        session.MarkActive();

        Assert.Equal(SessionStatus.Active, session.Status);
    }

    [Fact]
    public void MarkEnding_Should_Set_Ending_Status()
    {
        var session = new SessionInfo
        {
            SessionId = "session-001",
            DeviceId = "device-001",
            StartedAt = DateTimeOffset.UtcNow
        };

        session.MarkEnding();

        Assert.Equal(SessionStatus.Ending, session.Status);
    }

    [Fact]
    public void MarkEnded_Should_Set_End_Time()
    {
        var session = new SessionInfo
        {
            SessionId = "session-001",
            DeviceId = "device-001",
            StartedAt = DateTimeOffset.UtcNow.AddMinutes(-10)
        };

        session.MarkActive();
        session.MarkEnded();

        Assert.Equal(SessionStatus.Ended, session.Status);
        Assert.NotNull(session.EndedAt);
        Assert.NotNull(session.Duration);
    }

    [Fact]
    public void MarkError_Should_Set_Error_Status()
    {
        var session = new SessionInfo
        {
            SessionId = "session-001",
            DeviceId = "device-001",
            StartedAt = DateTimeOffset.UtcNow
        };

        session.MarkError();

        Assert.Equal(SessionStatus.Error, session.Status);
    }
}
