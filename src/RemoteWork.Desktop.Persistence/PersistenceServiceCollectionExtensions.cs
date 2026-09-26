using Microsoft.Extensions.DependencyInjection;
using RemoteWork.Desktop.Core.Interfaces;

namespace RemoteWork.Desktop.Persistence;

public static class PersistenceServiceCollectionExtensions
{
    public static IServiceCollection AddPersistenceServices(this IServiceCollection services)
    {
        services.AddSingleton<IDeviceIdentityStore, FileDeviceIdentityStore>();
        return services;
    }
}
