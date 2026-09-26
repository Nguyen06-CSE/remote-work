using Microsoft.Extensions.Logging.Abstractions;
using RemoteWork.Desktop.Application.Collectors;
using RemoteWork.Desktop.Core.Enums;
using Xunit;

namespace RemoteWork.Desktop.UnitTests.Application;

public class SessionCollectorTests
{
    [Fact]
    public void StartSession_Should_Create_Active_Session()
    {
        var collector = new SessionCollector(NullLogger<SessionCollector>.Instance);

        var session = collector.StartSession("device-001");

        Assert.NotNull(session);
        Assert.Equal("device-001", session.DeviceId);
        Assert.Equal(SessionStatus.Active, session.Status);
        Assert.NotNull(collector.GetCurrentSession());
    }

    [Fact]
    public void EndSession_Should_Clear_Current_Session()
    {
        var collector = new SessionCollector(NullLogger<SessionCollector>.Instance);

        collector.StartSession("device-001");
        collector.EndSession();

        Assert.Null(collector.GetCurrentSession());
    }

    [Fact]
    public void StartSession_Should_Not_Allow_Two_Active_Sessions()
    {
        var collector = new SessionCollector(NullLogger<SessionCollector>.Instance);

        collector.StartSession("device-001");

        Assert.Throws<InvalidOperationException>(() => collector.StartSession("device-001"));
    }

    [Fact]
    public void Should_Allow_New_Session_After_Previous_Session_Ended()
    {
        var collector = new SessionCollector(NullLogger<SessionCollector>.Instance);

        var first = collector.StartSession("device-001");
        collector.EndSession();

        var second = collector.StartSession("device-001");

        Assert.NotEqual(first.SessionId, second.SessionId);
        Assert.Equal(SessionStatus.Active, second.Status);
    }
}
