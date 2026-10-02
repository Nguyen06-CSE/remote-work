using RemoteWork.Desktop.Core.Enums;
using RemoteWork.Desktop.Core.Events;
using Xunit;

namespace RemoteWork.Desktop.UnitTests.Core;

public class EventModelTests
{
    [Fact]
    public void SessionEvent_Should_Inherit_TrackingEvent_And_Include_Status()
    {
        var timestamp = DateTimeOffset.UtcNow;
        var evt = new SessionEvent
        {
            EventId = "evt-001",
            DeviceId = "dev-123",
            SessionId = "sess-456",
            Timestamp = timestamp,
            Type = "session.started",
            Status = SessionStatus.Active
        };

        Assert.Equal("evt-001", evt.EventId);
        Assert.Equal("dev-123", evt.DeviceId);
        Assert.Equal("sess-456", evt.SessionId);
        Assert.Equal(timestamp, evt.Timestamp);
        Assert.Equal("session.started", evt.Type);
        Assert.Equal(SessionStatus.Active, evt.Status);
    }

    [Fact]
    public void ActivityTrackingEvent_Should_Support_Work_Tracking_Session_Reference()
    {
        var timestamp = DateTimeOffset.UtcNow;
        var evt = new ActivityTrackingEvent
        {
            EventId = "evt-002",
            DeviceId = "dev-123",
            SessionId = "sess-456",
            Timestamp = timestamp,
            Type = "activity.sample",
            KeyboardCount = 15,
            MouseCount = 5,
            ActiveDuration = TimeSpan.FromSeconds(50),
            IdleDuration = TimeSpan.FromSeconds(10),
            IsActive = true
        };

        Assert.Equal("evt-002", evt.EventId);
        Assert.Equal("dev-123", evt.DeviceId);
        Assert.Equal("sess-456", evt.SessionId);
        Assert.True(evt.IsActive);
    }

    [Fact]
    public void ApplicationActivityEvent_Should_Record_Application_Info()
    {
        var timestamp = DateTimeOffset.UtcNow;
        var evt = new ApplicationActivityEvent
        {
            EventId = "evt-003",
            DeviceId = "dev-123",
            SessionId = "sess-456",
            Timestamp = timestamp,
            Type = "application.changed",
            ApplicationName = "Slack",
            ProcessName = "slack",
            ProcessId = 5678,
            WindowTitle = "General - Slack"
        };

        Assert.Equal("evt-003", evt.EventId);
        Assert.Equal("Slack", evt.ApplicationName);
        Assert.Equal("slack", evt.ProcessName);
        Assert.Equal(5678, evt.ProcessId);
    }

    [Fact]
    public void ScreenshotEvent_Should_Contain_Image_Metadata()
    {
        var timestamp = DateTimeOffset.UtcNow;
        var evt = new ScreenshotEvent
        {
            EventId = "evt-004",
            DeviceId = "dev-123",
            SessionId = "sess-456",
            Timestamp = timestamp,
            Type = "screenshot.captured",
            FilePath = "/tmp/screenshot.png",
            Width = 1920,
            Height = 1080,
            FileSize = 500000
        };

        Assert.Equal("evt-004", evt.EventId);
        Assert.Equal("/tmp/screenshot.png", evt.FilePath);
        Assert.Equal(1920, evt.Width);
        Assert.Equal(1080, evt.Height);
    }
}
