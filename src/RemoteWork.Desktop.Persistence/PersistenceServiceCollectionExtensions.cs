using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using RemoteWork.Desktop.Core.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using RemoteWork.Desktop.Persistence.Data;
using RemoteWork.Desktop.Persistence.Repositories;

namespace RemoteWork.Desktop.Persistence;

public static class PersistenceServiceCollectionExtensions
{
    public static IServiceCollection AddPersistenceServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<DatabaseOptions>(configuration.GetSection(DatabaseOptions.SectionName));
        services.AddSingleton<DatabasePathResolver>();
        
        services.AddDbContext<RemoteWorkDbContext>((provider, options) =>
        {
            var resolver = provider.GetRequiredService<DatabasePathResolver>();
            var dbPath = resolver.ResolveDatabasePath();
            options.UseSqlite($"Data Source={dbPath}");
        });

        services.AddScoped<IDeviceRepository, DeviceRepository>();
        services.AddScoped<ISessionRepository, SessionRepository>();
        services.AddScoped<IActivityBatchRepository, ActivityBatchRepository>();
        services.AddScoped<IApplicationActivityRepository, ApplicationActivityRepository>();
        services.AddScoped<ISyncQueueRepository, SyncQueueRepository>();
        services.AddScoped<DatabaseInitializer>();

        services.AddSingleton<IDeviceIdentityStore, FileDeviceIdentityStore>();
        return services;
    }
}
