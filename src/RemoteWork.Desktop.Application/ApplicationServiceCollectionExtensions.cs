using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RemoteWork.Desktop.Application.Collectors;
using RemoteWork.Desktop.Application.Monitoring;
using RemoteWork.Desktop.Application.Options;
using RemoteWork.Desktop.Core.Interfaces;
using RemoteWork.Desktop.Core.Models;
using RemoteWork.Desktop.Platform.Abstractions;

namespace RemoteWork.Desktop.Application;

public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationServices(
        this IServiceCollection services,
        Action<TrackingOptions>? configureOptions = null)
    {
        if (configureOptions is not null)
        {
            services.Configure(configureOptions);
        }

        services.AddSingleton<AgentRuntimeState>();
        services.AddSingleton<DeviceCollector>();
        services.AddSingleton<ISessionCollector, SessionCollector>();

        services.AddSingleton<IIdleActivityCollector>(sp =>
        {
            var idleProvider = sp.GetRequiredService<IIdleTimeProvider>();
            var options = sp.GetService<IOptions<TrackingOptions>>()?.Value ?? new TrackingOptions();
            return new IdleActivityCollector(idleProvider, options.IdleThresholdSeconds);
        });

        services.AddSingleton<IKeyboardActivityCollector, KeyboardActivityCollector>();
        services.AddSingleton<IMouseActivityCollector, MouseActivityCollector>();
        services.AddSingleton<IActivityCollector, ActivityCollector>();

        services.AddSingleton<IMonitoringService>(sp =>
        {
            var activityCollector = sp.GetRequiredService<IActivityCollector>();
            var inputProvider = sp.GetRequiredService<IInputActivityProvider>();
            var options = sp.GetService<IOptions<TrackingOptions>>()?.Value ?? new TrackingOptions();
            var logger = sp.GetRequiredService<ILogger<MonitoringService>>();

            return new MonitoringService(
                activityCollector,
                inputProvider,
                options.ActivitySamplingIntervalSeconds,
                options.ActivityBatchIntervalSeconds,
                logger);
        });

        return services;
    }
}
