using Microsoft.Extensions.DependencyInjection;
using RemoteWork.Desktop.Platform.Abstractions;

namespace RemoteWork.Desktop.Platform.Linux;

public static class LinuxServiceCollectionExtensions
{
    public static IServiceCollection AddLinuxPlatformServices(this IServiceCollection services)
    {
        services.AddSingleton<IDeviceInfoProvider, LinuxDeviceInfoProvider>();
        services.AddSingleton<IIdleTimeProvider, LinuxIdleTimeProvider>();
        services.AddSingleton<IInputActivityProvider, LinuxInputActivityProvider>();
        services.AddSingleton<IActiveApplicationProvider, LinuxActiveApplicationProvider>();
        services.AddSingleton<IPlatformPermissionProvider, LinuxPlatformPermissionProvider>();
        services.AddSingleton<IScreenshotProvider, LinuxScreenshotProvider>();
        return services;
    }
}
