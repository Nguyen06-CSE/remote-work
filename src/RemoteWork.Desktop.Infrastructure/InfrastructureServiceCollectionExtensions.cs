using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RemoteWork.Desktop.Infrastructure.Configuration;

namespace RemoteWork.Desktop.Infrastructure;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<AgentOptions>()
            .Bind(configuration.GetSection(AgentOptions.SectionName))
            .ValidateOnStart();

        return services;
    }
}
