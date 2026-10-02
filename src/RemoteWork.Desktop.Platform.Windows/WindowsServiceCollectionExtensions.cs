using Microsoft.Extensions.DependencyInjection;
using RemoteWork.Desktop.Platform.Abstractions;

namespace RemoteWork.Desktop.Platform.Windows;

public static class WindowsServiceCollectionExtensions
{
    public static IServiceCollection AddWindowsPlatformServices(this IServiceCollection services)
    {
        services.AddSingleton<IDeviceInfoProvider, WindowsDeviceInfoProvider>();
        services.AddSingleton<WindowsIdleTimeProvider>();
        services.AddSingleton<IIdleTimeProvider>(sp => sp.GetRequiredService<WindowsIdleTimeProvider>());
        services.AddSingleton<IIdleProvider>(sp => sp.GetRequiredService<WindowsIdleTimeProvider>());
        services.AddSingleton<IInputActivityProvider, WindowsInputActivityProvider>();
        services.AddSingleton<WindowsActiveApplicationProvider>();
        services.AddSingleton<IApplicationActivityProvider>(sp => sp.GetRequiredService<WindowsActiveApplicationProvider>());
        services.AddSingleton<IActiveApplicationProvider>(sp => sp.GetRequiredService<WindowsActiveApplicationProvider>());
        services.AddSingleton<IPlatformPermissionProvider, WindowsPlatformPermissionProvider>();
        services.AddSingleton<IScreenshotProvider, WindowsScreenshotProvider>();
        return services;
    }
}
