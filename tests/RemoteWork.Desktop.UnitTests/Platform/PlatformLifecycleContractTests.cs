using System.Runtime.InteropServices;
using RemoteWork.Desktop.Platform.MacOS;
using RemoteWork.Desktop.Platform.Windows;
using Xunit;

namespace RemoteWork.Desktop.UnitTests.Platform;

public sealed class PlatformLifecycleContractTests
{
    [Fact]
    public void MacOsInputActivityProvider_Lifecycle_IdempotencyAndNoException()
    {
        using var provider = new MacOsInputActivityProvider();

        // Repeated Start should be idempotent
        provider.Start();
        provider.Start();

        // Read and reset
        var kb = provider.GetKeyboardCount();
        var mouse = provider.GetMouseCount();
        var samples = provider.DrainMouseSamples();

        Assert.True(kb >= 0);
        Assert.True(mouse >= 0);
        Assert.NotNull(samples);

        // Repeated Stop should be idempotent
        provider.Stop();
        provider.Stop();

        // Dispose should not throw
        provider.Dispose();
        provider.Dispose();
    }

    [Fact]
    public void WindowsInputActivityProvider_Lifecycle_IdempotencyAndNoException()
    {
        using var provider = new WindowsInputActivityProvider();

        provider.Start();
        provider.Start();

        var kb = provider.GetKeyboardCount();
        var mouse = provider.GetMouseCount();
        var samples = provider.DrainMouseSamples();

        Assert.True(kb >= 0);
        Assert.True(mouse >= 0);
        Assert.NotNull(samples);

        provider.Stop();
        provider.Stop();

        provider.Dispose();
        provider.Dispose();
    }
}