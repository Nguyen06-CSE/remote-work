using Microsoft.Extensions.DependencyInjection;
using RemoteWork.Desktop.Platform.Abstractions;

namespace RemoteWork.Desktop.Platform.Linux;

public static class LinuxServiceCollectionExtensions
{
    public static IServiceCollection AddLinuxPlatformServices(this IServiceCollection services)
    {
        services.AddSingleton<IDeviceInfoProvider, LinuxDeviceInfoProvider>();
        services.AddSingleton<LinuxIdleTimeProvider>();
        services.AddSingleton<IIdleTimeProvider>(sp => sp.GetRequiredService<LinuxIdleTimeProvider>());
        services.AddSingleton<IIdleProvider>(sp => sp.GetRequiredService<LinuxIdleTimeProvider>());
        services.AddSingleton<IInputActivityProvider, LinuxInputActivityProvider>();
        services.AddSingleton<LinuxActiveApplicationProvider>();
        services.AddSingleton<IApplicationActivityProvider>(sp => sp.GetRequiredService<LinuxActiveApplicationProvider>());
        services.AddSingleton<IActiveApplicationProvider>(sp => sp.GetRequiredService<LinuxActiveApplicationProvider>());
        services.AddSingleton<IPlatformPermissionProvider, LinuxPlatformPermissionProvider>();
        services.AddSingleton<IScreenshotProvider, LinuxScreenshotProvider>();
        return services;
    }
}
