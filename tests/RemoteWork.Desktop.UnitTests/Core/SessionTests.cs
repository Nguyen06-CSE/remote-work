using RemoteWork.Desktop.Core.Enums;
using RemoteWork.Desktop.Core.Models;
using Xunit;

namespace RemoteWork.Desktop.UnitTests.Core;

public class SessionTests
{
    [Fact]
    public void Session_Initial_Status_Should_Be_Starting()
    {
        var session = new Session
        {
            SessionId = "session-100",
            DeviceId = "dev-12345",
            StartedAt = DateTimeOffset.UtcNow
        };

        Assert.Equal(SessionStatus.Starting, session.Status);
        Assert.Null(session.EndedAt);
        Assert.Null(session.Duration);
    }

    [Fact]
    public void MarkActive_Should_Transition_To_Active()
    {
        var session = new Session
        {
            SessionId = "session-100",
            DeviceId = "dev-12345",
            StartedAt = DateTimeOffset.UtcNow
        };

        session.MarkActive();

        Assert.Equal(SessionStatus.Active, session.Status);
    }

    [Fact]
    public void MarkEnded_Should_Set_EndedAt_And_Calculate_Duration()
    {
        var startTime = DateTimeOffset.UtcNow.AddMinutes(-30);
        var session = new Session
        {
            SessionId = "session-100",
            DeviceId = "dev-12345",
            StartedAt = startTime
        };

        session.MarkActive();
        session.MarkEnded();

        Assert.Equal(SessionStatus.Ended, session.Status);
        Assert.NotNull(session.EndedAt);
        Assert.NotNull(session.Duration);
        Assert.True(session.Duration.Value.TotalMinutes >= 29);
    }
}
