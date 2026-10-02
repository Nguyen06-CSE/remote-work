using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RemoteWork.Desktop.Application.Collectors;
using RemoteWork.Desktop.Application.Monitoring;
using RemoteWork.Desktop.Application.Options;
using RemoteWork.Desktop.Application.Sync;
using RemoteWork.Desktop.Core.Interfaces;
using RemoteWork.Desktop.Core.Models;
using RemoteWork.Desktop.Platform.Abstractions;

namespace RemoteWork.Desktop.Application;

public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationServices(
        this IServiceCollection services,
        Action<TrackingOptions>? configureOptions = null,
        Action<SyncOptions>? configureSyncOptions = null)
    {
        if (configureOptions is not null)
        {
            services.Configure(configureOptions);
        }

        if (configureSyncOptions is not null)
        {
            services.Configure(configureSyncOptions);
        }
        else
        {
            services.AddOptions<SyncOptions>();
        }

        services.AddSingleton<AgentRuntimeState>();
        services.AddSingleton<DeviceCollector>();
        services.AddSingleton<SessionEngine>();
        services.AddSingleton<ISessionEngine>(sp => sp.GetRequiredService<SessionEngine>());
        services.AddSingleton<ISessionCollector>(sp => sp.GetRequiredService<SessionEngine>());

        services.AddSingleton<IIdleActivityCollector>(sp =>
        {
            var idleProvider = sp.GetRequiredService<IIdleProvider>();
            var options = sp.GetService<IOptions<TrackingOptions>>()?.Value ?? new TrackingOptions();
            return new IdleActivityCollector(idleProvider, options.IdleThresholdSeconds);
        });

        services.AddSingleton<IKeyboardActivityCollector, KeyboardActivityCollector>();
        services.AddSingleton<IMouseActivityCollector, MouseActivityCollector>();
        services.AddSingleton<IMouseBotDetector, MouseBotDetector>();
        services.AddSingleton<IActivityCollector, ActivityCollector>();
        services.AddSingleton<IApplicationActivityCollector, ApplicationActivityCollector>();

        services.AddSingleton<IMonitoringService>(sp =>
        {
            var activityCollector = sp.GetRequiredService<IActivityCollector>();
            var inputProvider = sp.GetRequiredService<IInputActivityProvider>();
            var sessionCollector = sp.GetRequiredService<ISessionCollector>();
            var appCollector = sp.GetService<IApplicationActivityCollector>();
            var options = sp.GetService<IOptions<TrackingOptions>>()?.Value ?? new TrackingOptions();
            var logger = sp.GetRequiredService<ILogger<MonitoringService>>();

            return new MonitoringService(
                activityCollector,
                inputProvider,
                sessionCollector,
                options.ActivitySamplingIntervalSeconds,
                options.ActivityBatchIntervalSeconds,
                logger,
                appCollector);
        });

        services.AddSingleton<MonitoringService>(sp => (MonitoringService)sp.GetRequiredService<IMonitoringService>());

        // Offline-first Sync and Persistence services
        services.AddScoped<TrackingPersistenceCoordinator>();
        services.AddSingleton<ISyncEngine>(sp => new SyncEngine(
            sp.GetRequiredService<IServiceScopeFactory>(),
            sp.GetRequiredService<ISyncTransport>(),
            sp.GetRequiredService<IOptions<SyncOptions>>(),
            sp.GetRequiredService<ILogger<SyncEngine>>()));
        services.AddSingleton<SyncEngine>(sp => (SyncEngine)sp.GetRequiredService<ISyncEngine>());

        return services;
    }
}