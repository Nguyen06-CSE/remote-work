using Microsoft.Extensions.DependencyInjection;
using RemoteWork.Desktop.Platform.Abstractions;

namespace RemoteWork.Desktop.Platform.MacOS;

public static class MacOsServiceCollectionExtensions
{
    public static IServiceCollection AddMacOsPlatformServices(this IServiceCollection services)
    {
        services.AddSingleton<IDeviceInfoProvider, MacOsDeviceInfoProvider>();
        services.AddSingleton<IIdleTimeProvider, MacOsIdleTimeProvider>();
        services.AddSingleton<IInputActivityProvider, MacOsInputActivityProvider>();
        return services;
    }
}
