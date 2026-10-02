using RemoteWork.Desktop.Core.Models;
using Xunit;

namespace RemoteWork.Desktop.UnitTests.Core;

public class ScreenshotMetadataTests
{
    [Fact]
    public void ScreenshotMetadata_Should_Store_Captures_Information()
    {
        var timestamp = DateTimeOffset.UtcNow;
        var metadata = new ScreenshotMetadata
        {
            ScreenshotId = "shot-999",
            DeviceId = "dev-12345",
            SessionId = "session-100",
            Timestamp = timestamp,
            FilePath = "/tmp/screenshots/shot-999.png",
            Width = 1920,
            Height = 1080,
            FileSize = 204800
        };

        Assert.Equal("shot-999", metadata.ScreenshotId);
        Assert.Equal("dev-12345", metadata.DeviceId);
        Assert.Equal("session-100", metadata.SessionId);
        Assert.Equal(timestamp, metadata.Timestamp);
        Assert.Equal("/tmp/screenshots/shot-999.png", metadata.FilePath);
        Assert.Equal(1920, metadata.Width);
        Assert.Equal(1080, metadata.Height);
        Assert.Equal(204800, metadata.FileSize);
    }
}
