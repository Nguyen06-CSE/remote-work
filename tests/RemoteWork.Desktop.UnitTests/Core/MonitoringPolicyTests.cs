using RemoteWork.Desktop.Core.Models;
using Xunit;

namespace RemoteWork.Desktop.UnitTests.Core;

public class MonitoringPolicyTests
{
    [Fact]
    public void CreateDefault_Should_Have_Sensible_Defaults()
    {
        var policy = MonitoringPolicy.CreateDefault();

        Assert.Equal("default", policy.PolicyId);
        Assert.Equal(TimeSpan.FromSeconds(10), policy.ActivitySamplingInterval);
        Assert.Equal(TimeSpan.FromMinutes(5), policy.IdleThreshold);
        Assert.Equal(TimeSpan.FromMinutes(10), policy.ScreenshotInterval);
        Assert.True(policy.EnableScreenshots);
        Assert.True(policy.EnableApplicationTracking);
        Assert.True(policy.EnableInputTracking);
    }

    [Fact]
    public void MonitoringPolicy_Custom_Settings_Can_Be_Applied()
    {
        var policy = new MonitoringPolicy
        {
            PolicyId = "strict-policy",
            ActivitySamplingInterval = TimeSpan.FromSeconds(5),
            IdleThreshold = TimeSpan.FromMinutes(2),
            ScreenshotInterval = TimeSpan.FromMinutes(5),
            EnableScreenshots = false,
            EnableApplicationTracking = true,
            EnableInputTracking = true
        };

        Assert.Equal("strict-policy", policy.PolicyId);
        Assert.Equal(TimeSpan.FromSeconds(5), policy.ActivitySamplingInterval);
        Assert.Equal(TimeSpan.FromMinutes(2), policy.IdleThreshold);
        Assert.False(policy.EnableScreenshots);
    }
}
