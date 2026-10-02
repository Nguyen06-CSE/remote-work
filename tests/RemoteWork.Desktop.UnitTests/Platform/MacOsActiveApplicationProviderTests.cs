using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging.Abstractions;
using RemoteWork.Desktop.Platform.MacOS;
using Xunit;

namespace RemoteWork.Desktop.UnitTests.Platform;

public sealed class MacOsActiveApplicationProviderTests
{
    private static bool IsMacOs => RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

    [Fact]
    public void WhenAppKitInitializationFails_ProviderReturnsNullWithoutCrashing()
    {
        if (!IsMacOs)
            return;

        // Injected loader returns IntPtr.Zero to simulate dlopen failure
        var provider = new MacOsActiveApplicationProvider(
            NullLogger<MacOsActiveApplicationProvider>.Instance,
            dlopenFunc: (path, mode) => IntPtr.Zero,
            getClassFunc: null);

        var app = provider.GetActiveApplication();

        Assert.Null(app);
    }

    [Fact]
    public void WhenClassLookupFails_ProviderReturnsNullWithoutCrashing()
    {
        if (!IsMacOs)
            return;

        // Injected class resolver returns IntPtr.Zero to simulate missing NSWorkspace
        var provider = new MacOsActiveApplicationProvider(
            NullLogger<MacOsActiveApplicationProvider>.Instance,
            dlopenFunc: (path, mode) => new IntPtr(12345),
            getClassFunc: name => IntPtr.Zero);

        var app = provider.GetActiveApplication();

        Assert.Null(app);
    }

    [Fact]
    public void LiveProvider_OnMacOs_RetrievesActiveApplicationMetadata()
    {
        if (!IsMacOs)
            return;

        var provider = new MacOsActiveApplicationProvider(NullLogger<MacOsActiveApplicationProvider>.Instance);
        var app = provider.GetActiveApplication();

        Assert.NotNull(app);
        Assert.True(app.ProcessId > 0, $"Expected PID > 0, got {app.ProcessId}");
        Assert.False(string.IsNullOrWhiteSpace(app.ApplicationName), "ApplicationName must not be empty");
        Assert.False(string.IsNullOrWhiteSpace(app.ProcessName), "ProcessName must not be empty");
        Assert.Empty(app.WindowTitle); // Zero window title per privacy policy
        Assert.True(app.Timestamp <= DateTimeOffset.UtcNow);
    }
}
