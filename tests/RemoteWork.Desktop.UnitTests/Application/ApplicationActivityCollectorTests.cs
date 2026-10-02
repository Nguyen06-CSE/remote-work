using Microsoft.Extensions.Logging.Abstractions;
using RemoteWork.Desktop.Application.Collectors;
using RemoteWork.Desktop.Platform.Abstractions;
using Xunit;

namespace RemoteWork.Desktop.UnitTests.Application;

public sealed class ApplicationActivityCollectorTests
{
    private sealed class FakeApplicationActivityProvider : IApplicationActivityProvider
    {
        public ActiveApplicationInfo? CurrentApp { get; set; }
        public bool ShouldThrow { get; set; }

        public ActiveApplicationInfo? GetActiveApplication()
        {
            if (ShouldThrow)
            {
                throw new InvalidOperationException("Simulated platform API failure.");
            }
            return CurrentApp;
        }
    }

    private readonly FakeApplicationActivityProvider _provider = new();
    private readonly ApplicationActivityCollector _collector;

    public ApplicationActivityCollectorTests()
    {
        _collector = new ApplicationActivityCollector(_provider, NullLogger<ApplicationActivityCollector>.Instance);
    }

    [Fact]
    public void Initial_Sample_Should_Not_Emit_Activity()
    {
        _provider.CurrentApp = new ActiveApplicationInfo
        {
            ApplicationName = "Visual Studio Code",
            ProcessName = "Code",
            ProcessId = 1234
        };

        var activity = _collector.Sample("device-1", "session-1", DateTimeOffset.UtcNow);

        Assert.Null(activity);
    }

    [Fact]
    public void Same_Application_Remains_Active_Should_Not_Generate_Redundant_Records()
    {
        var startTime = DateTimeOffset.UtcNow;
        _provider.CurrentApp = new ActiveApplicationInfo
        {
            ApplicationName = "Visual Studio Code",
            ProcessName = "Code",
            ProcessId = 1234
        };

        _collector.Sample("device-1", "session-1", startTime);

        // Poll 10 times with the same active application
        for (int i = 1; i <= 10; i++)
        {
            var result = _collector.Sample("device-1", "session-1", startTime.AddSeconds(i));
            Assert.Null(result);
        }
    }

    [Fact]
    public void Application_Change_Should_Emit_Previous_Application_With_Accurate_WallClock_Duration()
    {
        var t0 = new DateTimeOffset(2026, 10, 2, 10, 0, 0, TimeSpan.Zero);
        var t1 = t0.AddSeconds(45); // VS Code active for 45s

        _provider.CurrentApp = new ActiveApplicationInfo
        {
            ApplicationName = "Visual Studio Code",
            ProcessName = "Code",
            ProcessId = 1234
        };
        _collector.Sample("device-1", "session-1", t0);

        // Transition to Chrome at t1
        _provider.CurrentApp = new ActiveApplicationInfo
        {
            ApplicationName = "Google Chrome",
            ProcessName = "chrome",
            ProcessId = 5678
        };
        var activity = _collector.Sample("device-1", "session-1", t1);

        Assert.NotNull(activity);
        Assert.Equal("Visual Studio Code", activity.ApplicationName);
        Assert.Equal("Code", activity.ProcessName);
        Assert.Equal(1234, activity.ProcessId);
        Assert.Equal(t0, activity.Timestamp);
        Assert.Equal(TimeSpan.FromSeconds(45), activity.Duration);
        Assert.Equal("device-1", activity.DeviceId);
        Assert.Equal("session-1", activity.SessionId);
        Assert.Null(activity.WindowTitle); // Zero content leak
    }

    [Fact]
    public void Subsequent_Application_Transition_Should_Track_Next_Application_Duration()
    {
        var t0 = new DateTimeOffset(2026, 10, 2, 10, 0, 0, TimeSpan.Zero);
        var t1 = t0.AddSeconds(30); // VS Code for 30s
        var t2 = t1.AddSeconds(60); // Chrome for 60s

        // 1. VS Code starts
        _provider.CurrentApp = new ActiveApplicationInfo { ApplicationName = "Code", ProcessName = "Code", ProcessId = 1 };
        _collector.Sample("device-1", "session-1", t0);

        // 2. Switch to Chrome at t1
        _provider.CurrentApp = new ActiveApplicationInfo { ApplicationName = "Chrome", ProcessName = "chrome", ProcessId = 2 };
        var codeActivity = _collector.Sample("device-1", "session-1", t1);
        Assert.NotNull(codeActivity);
        Assert.Equal("Code", codeActivity.ApplicationName);
        Assert.Equal(TimeSpan.FromSeconds(30), codeActivity.Duration);

        // 3. Switch to Postman at t2
        _provider.CurrentApp = new ActiveApplicationInfo { ApplicationName = "Postman", ProcessName = "postman", ProcessId = 3 };
        var chromeActivity = _collector.Sample("device-1", "session-1", t2);
        Assert.NotNull(chromeActivity);
        Assert.Equal("Chrome", chromeActivity.ApplicationName);
        Assert.Equal(TimeSpan.FromSeconds(60), chromeActivity.Duration);
    }

    [Fact]
    public void Focus_Lost_To_Null_Should_Finalize_Active_Application()
    {
        var t0 = new DateTimeOffset(2026, 10, 2, 10, 0, 0, TimeSpan.Zero);
        var t1 = t0.AddSeconds(20);

        _provider.CurrentApp = new ActiveApplicationInfo { ApplicationName = "Code", ProcessName = "Code", ProcessId = 1 };
        _collector.Sample("device-1", "session-1", t0);

        // Focus lost (e.g. desktop clicked or lock screen)
        _provider.CurrentApp = null;
        var activity = _collector.Sample("device-1", "session-1", t1);

        Assert.NotNull(activity);
        Assert.Equal("Code", activity.ApplicationName);
        Assert.Equal(TimeSpan.FromSeconds(20), activity.Duration);

        // Subsequent sample while still null produces nothing
        var nextActivity = _collector.Sample("device-1", "session-1", t1.AddSeconds(5));
        Assert.Null(nextActivity);
    }

    [Fact]
    public void Flush_On_Shutdown_Should_Emit_In_Progress_Application_And_Reset_State()
    {
        var t0 = new DateTimeOffset(2026, 10, 2, 10, 0, 0, TimeSpan.Zero);
        var tShutdown = t0.AddSeconds(15);

        _provider.CurrentApp = new ActiveApplicationInfo { ApplicationName = "Terminal", ProcessName = "zsh", ProcessId = 99 };
        _collector.Sample("device-1", "session-1", t0);

        var flushed = _collector.Flush("device-1", "session-1", tShutdown);

        Assert.NotNull(flushed);
        Assert.Equal("Terminal", flushed.ApplicationName);
        Assert.Equal("zsh", flushed.ProcessName);
        Assert.Equal(99, flushed.ProcessId);
        Assert.Equal(TimeSpan.FromSeconds(15), flushed.Duration);

        // Second flush returns null
        Assert.Null(_collector.Flush("device-1", "session-1", tShutdown.AddSeconds(1)));
    }

    [Fact]
    public void Missing_Application_Metadata_Should_Fallback_Safely()
    {
        var t0 = new DateTimeOffset(2026, 10, 2, 10, 0, 0, TimeSpan.Zero);
        var t1 = t0.AddSeconds(10);

        // Empty app name falls back to process name
        _provider.CurrentApp = new ActiveApplicationInfo { ApplicationName = "   ", ProcessName = "my-daemon", ProcessId = 100 };
        _collector.Sample("device-1", "session-1", t0);

        _provider.CurrentApp = new ActiveApplicationInfo { ApplicationName = "", ProcessName = "", ProcessId = -1 };
        var act1 = _collector.Sample("device-1", "session-1", t1);

        Assert.NotNull(act1);
        Assert.Equal("my-daemon", act1.ApplicationName);
        Assert.Equal("my-daemon", act1.ProcessName);
        Assert.Equal(100, act1.ProcessId);

        // Both empty falls back to defaults and pid >= 0
        var t2 = t1.AddSeconds(10);
        _provider.CurrentApp = null;
        var act2 = _collector.Sample("device-1", "session-1", t2);

        Assert.NotNull(act2);
        Assert.Equal("Unknown Application", act2.ApplicationName);
        Assert.Equal("Unknown Process", act2.ProcessName);
        Assert.Equal(0, act2.ProcessId);
    }

    [Fact]
    public void Provider_Failure_Should_Be_Handled_Gracefully_Without_Crashing()
    {
        var t0 = new DateTimeOffset(2026, 10, 2, 10, 0, 0, TimeSpan.Zero);
        var t1 = t0.AddSeconds(10);

        _provider.CurrentApp = new ActiveApplicationInfo { ApplicationName = "Code", ProcessName = "Code", ProcessId = 1 };
        _collector.Sample("device-1", "session-1", t0);

        // Provider throws exception
        _provider.ShouldThrow = true;
        var act = _collector.Sample("device-1", "session-1", t1);

        // Should finalize "Code" because focus was lost, without throwing
        Assert.NotNull(act);
        Assert.Equal("Code", act.ApplicationName);

        // Provider recovers
        _provider.ShouldThrow = false;
        _provider.CurrentApp = new ActiveApplicationInfo { ApplicationName = "Slack", ProcessName = "slack", ProcessId = 2 };
        var next = _collector.Sample("device-1", "session-1", t1.AddSeconds(5));
        Assert.Null(next); // Slack starts
    }
}
