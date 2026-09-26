using Microsoft.Extensions.DependencyInjection;
using RemoteWork.Desktop.Platform.Abstractions;

namespace RemoteWork.Desktop.Platform.Windows;

public static class WindowsServiceCollectionExtensions
{
    public static IServiceCollection AddWindowsPlatformServices(this IServiceCollection services)
    {
        services.AddSingleton<IDeviceInfoProvider, WindowsDeviceInfoProvider>();
        services.AddSingleton<IIdleTimeProvider, WindowsIdleTimeProvider>();
        services.AddSingleton<IInputActivityProvider, WindowsInputActivityProvider>();
        services.AddSingleton<IActiveApplicationProvider, WindowsActiveApplicationProvider>();
        services.AddSingleton<IPlatformPermissionProvider, WindowsPlatformPermissionProvider>();
        services.AddSingleton<IScreenshotProvider, WindowsScreenshotProvider>();
        return services;
    }
}
