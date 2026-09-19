using RemoteWork.Agent.Core.Enums;
using RemoteWork.Agent.Core.Models;

namespace RemoteWork.Agent.Tests;

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
            // Không gán Status nữa — mặc định đã là Starting
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
    public void MarkEnded_Should_Set_End_Time()
    {
        var session = new SessionInfo
        {
            SessionId = "session-001",
            DeviceId = "device-001",
            StartedAt = DateTimeOffset.UtcNow.AddMinutes(-10)
        };

        session.MarkActive();  // Chuyển sang Active trước
        session.MarkEnded();

        Assert.Equal(SessionStatus.Ended, session.Status);
        Assert.NotNull(session.EndedAt);
        Assert.NotNull(session.Duration);
    }
}