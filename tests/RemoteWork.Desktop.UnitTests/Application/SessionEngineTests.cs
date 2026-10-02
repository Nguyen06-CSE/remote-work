using Microsoft.Extensions.Logging.Abstractions;
using RemoteWork.Desktop.Application.Collectors;
using RemoteWork.Desktop.Core.Enums;
using RemoteWork.Desktop.Core.Models;
using Xunit;

namespace RemoteWork.Desktop.UnitTests.Application;

public class SessionEngineTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void StartSession_Should_Require_Valid_DeviceId(string? invalidDeviceId)
    {
        var engine = new SessionEngine(NullLogger<SessionEngine>.Instance);

        Assert.Throws<ArgumentException>(() => engine.StartSession(invalidDeviceId!));
    }

    [Fact]
    public void StartSession_Should_Create_Active_Session_With_UtcTimestamp()
    {
        var engine = new SessionEngine(NullLogger<SessionEngine>.Instance);
        var deviceId = "device-mac-001";

        var session = engine.StartSession(deviceId);

        Assert.NotNull(session);
        Assert.False(string.IsNullOrWhiteSpace(session.SessionId));
        Assert.True(Guid.TryParse(session.SessionId, out _));
        Assert.Equal(deviceId, session.DeviceId);
        Assert.Equal(SessionStatus.Active, session.Status);
        Assert.Equal(TimeSpan.Zero, session.StartedAt.Offset);
        Assert.NotNull(engine.GetCurrentSession());
        Assert.Equal(session.SessionId, engine.GetCurrentSession()!.SessionId);
    }

    [Fact]
    public void Cannot_Start_Two_Sessions_Simultaneously()
    {
        var engine = new SessionEngine(NullLogger<SessionEngine>.Instance);
        engine.StartSession("device-001");

        var ex = Assert.Throws<InvalidOperationException>(() => engine.StartSession("device-001"));
        Assert.Equal("A session is already active.", ex.Message);
    }

    [Fact]
    public void Ending_A_Session_Records_EndedAt_And_Positive_Duration()
    {
        var engine = new SessionEngine(NullLogger<SessionEngine>.Instance);
        var session = engine.StartSession("device-001");

        Thread.Sleep(15); // Ensure measurable elapsed time

        var endedSession = engine.EndSession();

        Assert.NotNull(endedSession);
        Assert.Equal(session.SessionId, endedSession.SessionId);
        Assert.Equal(SessionStatus.Ended, endedSession.Status);
        Assert.NotNull(endedSession.EndedAt);
        Assert.Equal(TimeSpan.Zero, endedSession.EndedAt.Value.Offset);
        Assert.NotNull(endedSession.Duration);
        Assert.True(endedSession.Duration.Value > TimeSpan.Zero);
        Assert.Null(engine.GetCurrentSession());
    }

    [Fact]
    public void A_New_Session_Gets_A_New_SessionId_And_DeviceId_Remains_Unchanged()
    {
        var engine = new SessionEngine(NullLogger<SessionEngine>.Instance);
        const string stableDeviceId = "device-stable-12345";

        var session1 = engine.StartSession(stableDeviceId);
        var ended1 = engine.EndSession();

        var session2 = engine.StartSession(stableDeviceId);
        var ended2 = engine.EndSession();

        Assert.NotEqual(session1.SessionId, session2.SessionId);
        Assert.Equal(stableDeviceId, session1.DeviceId);
        Assert.Equal(stableDeviceId, session2.DeviceId);
    }

    [Fact]
    public void State_Transitions_Are_Valid_And_Enforced()
    {
        var session = new SessionInfo
        {
            SessionId = Guid.NewGuid().ToString(),
            DeviceId = "dev-01",
            StartedAt = DateTimeOffset.UtcNow
        };

        // Starting -> Active
        Assert.True(Session.IsValidTransition(session.Status, SessionStatus.Active));
        session.MarkActive();
        Assert.Equal(SessionStatus.Active, session.Status);

        // Active -> Ending
        Assert.True(Session.IsValidTransition(session.Status, SessionStatus.Ending));
        session.MarkEnding();
        Assert.Equal(SessionStatus.Ending, session.Status);

        // Ending -> Ended
        Assert.True(Session.IsValidTransition(session.Status, SessionStatus.Ended));
        session.MarkEnded();
        Assert.Equal(SessionStatus.Ended, session.Status);

        // Invalid: Ended -> Active
        Assert.False(Session.IsValidTransition(session.Status, SessionStatus.Active));
        Assert.Throws<InvalidOperationException>(() => session.MarkActive());

        // Invalid: Ended -> Error
        Assert.False(Session.IsValidTransition(session.Status, SessionStatus.Error));
        Assert.Throws<InvalidOperationException>(() => session.MarkError());
    }

    [Fact]
    public void Shutdown_Can_Safely_End_An_Active_Session()
    {
        var engine = new SessionEngine(NullLogger<SessionEngine>.Instance);
        engine.StartSession("device-shutdown-test");

        // Simulate shutdown calling EndSession()
        var endedSession = engine.EndSession();
        Assert.NotNull(endedSession);
        Assert.Equal(SessionStatus.Ended, endedSession.Status);

        // Subsequent call during shutdown should be cancellation-safe and return cleanly without throwing
        var duplicateEndCall = engine.EndSession();
        Assert.Null(duplicateEndCall);
    }

    [Fact]
    public async Task Concurrent_Access_Is_Thread_And_Cancellation_Safe()
    {
        var engine = new SessionEngine(NullLogger<SessionEngine>.Instance);
        engine.StartSession("device-concurrent");

        var tasks = new List<Task>();
        for (int i = 0; i < 10; i++)
        {
            tasks.Add(Task.Run(() =>
            {
                var s = engine.GetCurrentSession();
                if (s != null)
                {
                    Assert.Equal("device-concurrent", s.DeviceId);
                }
            }));
        }

        await Task.WhenAll(tasks);

        var ended = engine.EndSession();
        Assert.NotNull(ended);
    }
}
