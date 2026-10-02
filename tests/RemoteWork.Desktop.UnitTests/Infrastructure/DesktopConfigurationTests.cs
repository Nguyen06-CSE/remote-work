using System.Text.Json;
using RemoteWork.Desktop.Core.Configuration;
using RemoteWork.Desktop.Infrastructure.Configuration;
using Xunit;

namespace RemoteWork.Desktop.UnitTests.Infrastructure;

public class DesktopConfigurationTests
{
    [Fact]
    public void Default_Configuration_Values_Should_Be_Valid()
    {
        var config = new DesktopConfiguration();
        var validator = new DesktopConfigurationValidator();

        var result = validator.Validate(null, config);

        Assert.True(result.Succeeded);
        Assert.Equal("1.0.0", config.ApplicationVersion);
        Assert.Equal("http://localhost:8000", config.BackendBaseUrl);
        Assert.Equal(60, config.HeartbeatIntervalSeconds);
        Assert.Equal(10, config.ActivitySamplingIntervalSeconds);
        Assert.Equal(60, config.ActivityBatchIntervalSeconds);
        Assert.True(config.ScreenshotDefaults.Enabled);
        Assert.Equal(80, config.ScreenshotDefaults.Quality);
    }

    [Fact]
    public void Configuration_Serialization_And_Deserialization_Should_Preserve_Values()
    {
        var original = new DesktopConfiguration
        {
            ApplicationVersion = "2.1.0",
            BackendBaseUrl = "https://api.remotework.internal",
            LocalStoragePath = "/data/remotework",
            HeartbeatIntervalSeconds = 30,
            ActivitySamplingIntervalSeconds = 5,
            ActivityBatchIntervalSeconds = 30,
            Logging = new LoggingSettings
            {
                MinimumLevel = "Debug",
                LogFilePath = "/data/logs/agent.log",
                EnableConsole = false
            },
            ScreenshotDefaults = new ScreenshotSettings
            {
                Enabled = true,
                IntervalSeconds = 300,
                Quality = 90
            }
        };

        var json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<DesktopConfiguration>(json);

        Assert.NotNull(deserialized);
        Assert.Equal("2.1.0", deserialized.ApplicationVersion);
        Assert.Equal("https://api.remotework.internal", deserialized.BackendBaseUrl);
        Assert.Equal("/data/remotework", deserialized.LocalStoragePath);
        Assert.Equal(30, deserialized.HeartbeatIntervalSeconds);
        Assert.Equal(5, deserialized.ActivitySamplingIntervalSeconds);
        Assert.Equal(30, deserialized.ActivityBatchIntervalSeconds);
        Assert.Equal("Debug", deserialized.Logging.MinimumLevel);
        Assert.False(deserialized.Logging.EnableConsole);
        Assert.Equal(90, deserialized.ScreenshotDefaults.Quality);
    }

    [Theory]
    [InlineData("", "http://localhost:8000", 60, 10, 60, 80)]
    [InlineData("1.0.0", "invalid-url", 60, 10, 60, 80)]
    [InlineData("1.0.0", "ftp://invalid-scheme", 60, 10, 60, 80)]
    [InlineData("1.0.0", "http://localhost:8000", -1, 10, 60, 80)]
    [InlineData("1.0.0", "http://localhost:8000", 60, 0, 60, 80)]
    [InlineData("1.0.0", "http://localhost:8000", 60, 10, 5, 80)]
    [InlineData("1.0.0", "http://localhost:8000", 60, 10, 60, 105)]
    [InlineData("1.0.0", "http://localhost:8000", 60, 10, 60, 0)]
    public void Invalid_Configuration_Should_Fail_Validation(
        string appVersion,
        string backendUrl,
        int heartbeatInterval,
        int sampleInterval,
        int batchInterval,
        int screenshotQuality)
    {
        var config = new DesktopConfiguration
        {
            ApplicationVersion = appVersion,
            BackendBaseUrl = backendUrl,
            HeartbeatIntervalSeconds = heartbeatInterval,
            ActivitySamplingIntervalSeconds = sampleInterval,
            ActivityBatchIntervalSeconds = batchInterval,
            ScreenshotDefaults = new ScreenshotSettings
            {
                Enabled = true,
                IntervalSeconds = 600,
                Quality = screenshotQuality
            }
        };

        var validator = new DesktopConfigurationValidator();
        var result = validator.Validate(null, config);

        Assert.True(result.Failed);
        Assert.NotEmpty(result.Failures);
    }
}
