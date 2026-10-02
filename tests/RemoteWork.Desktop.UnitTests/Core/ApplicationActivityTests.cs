using RemoteWork.Desktop.Core.Models;
using Xunit;

namespace RemoteWork.Desktop.UnitTests.Core;

public class ApplicationActivityTests
{
    [Fact]
    public void ApplicationActivity_Should_Contain_Valid_Metadata()
    {
        var timestamp = DateTimeOffset.UtcNow;
        var appActivity = new ApplicationActivity
        {
            ActivityId = "appact-001",
            DeviceId = "dev-12345",
            SessionId = "session-100",
            Timestamp = timestamp,
            ApplicationName = "Visual Studio Code",
            ProcessName = "Code",
            ProcessId = 1234,
            WindowTitle = "SessionTests.cs - remote-gent",
            Duration = TimeSpan.FromSeconds(30)
        };

        Assert.Equal("appact-001", appActivity.ActivityId);
        Assert.Equal("dev-12345", appActivity.DeviceId);
        Assert.Equal("session-100", appActivity.SessionId);
        Assert.Equal(timestamp, appActivity.Timestamp);
        Assert.Equal("Visual Studio Code", appActivity.ApplicationName);
        Assert.Equal("Code", appActivity.ProcessName);
        Assert.Equal(1234, appActivity.ProcessId);
        Assert.Equal("SessionTests.cs - remote-gent", appActivity.WindowTitle);
        Assert.Equal(TimeSpan.FromSeconds(30), appActivity.Duration);
    }
}
