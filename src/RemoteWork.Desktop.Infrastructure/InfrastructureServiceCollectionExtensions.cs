using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using RemoteWork.Desktop.Core.Configuration;
using RemoteWork.Desktop.Infrastructure.Configuration;

namespace RemoteWork.Desktop.Infrastructure;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSingleton<IValidateOptions<DesktopConfiguration>, DesktopConfigurationValidator>();

        services.AddOptions<DesktopConfiguration>()
            .Bind(configuration.GetSection(DesktopConfiguration.SectionName))
            .ValidateOnStart();

        services.AddOptions<AgentOptions>()
            .Bind(configuration.GetSection(AgentOptions.SectionName))
            .ValidateOnStart();

        return services;
    }
}
