using Microsoft.Extensions.DependencyInjection;
using RemoteWork.Desktop.Platform.Abstractions;

namespace RemoteWork.Desktop.Platform.MacOS;

public static class MacOsServiceCollectionExtensions
{
    public static IServiceCollection AddMacOsPlatformServices(this IServiceCollection services)
    {
        services.AddSingleton<IDeviceInfoProvider, MacOsDeviceInfoProvider>();
        services.AddSingleton<MacOsIdleTimeProvider>();
        services.AddSingleton<IIdleTimeProvider>(sp => sp.GetRequiredService<MacOsIdleTimeProvider>());
        services.AddSingleton<IIdleProvider>(sp => sp.GetRequiredService<MacOsIdleTimeProvider>());
        services.AddSingleton<IInputActivityProvider, MacOsInputActivityProvider>();
        services.AddSingleton<MacOsActiveApplicationProvider>();
        services.AddSingleton<IApplicationActivityProvider>(sp => sp.GetRequiredService<MacOsActiveApplicationProvider>());
        services.AddSingleton<IActiveApplicationProvider>(sp => sp.GetRequiredService<MacOsActiveApplicationProvider>());
        services.AddSingleton<IPlatformPermissionProvider, MacOsPlatformPermissionProvider>();
        services.AddSingleton<IScreenshotProvider, MacOsScreenshotProvider>();
        return services;
    }
}
