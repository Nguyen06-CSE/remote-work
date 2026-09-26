using System.Runtime.InteropServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RemoteWork.Desktop.Application;
using RemoteWork.Desktop.Host;
using RemoteWork.Desktop.Infrastructure;
using RemoteWork.Desktop.Infrastructure.Configuration;
using RemoteWork.Desktop.Persistence;
using RemoteWork.Desktop.Platform.Linux;
using RemoteWork.Desktop.Platform.MacOS;
using RemoteWork.Desktop.Platform.Windows;

var builder = Host.CreateApplicationBuilder(args);

// 1. Infrastructure (Configuration & Technical Services)
builder.Services.AddInfrastructureServices(builder.Configuration);

// 2. Persistence Layer
builder.Services.AddPersistenceServices();

// 3. Platform Detection & Services
if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
{
    builder.Services.AddWindowsPlatformServices();
}
else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
{
    builder.Services.AddMacOsPlatformServices();
}
else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
{
    builder.Services.AddLinuxPlatformServices();
}
else
{
    throw new PlatformNotSupportedException($"Unsupported operating system: {RuntimeInformation.OSDescription}");
}

// 4. Application Layer
var agentSection = builder.Configuration.GetSection(AgentOptions.SectionName);
builder.Services.AddApplicationServices(options =>
{
    options.ActivitySamplingIntervalSeconds = agentSection.GetValue("ActivitySamplingIntervalSeconds", 10);
    options.IdleThresholdSeconds = agentSection.GetValue("IdleThresholdSeconds", 300);
    options.ActivityBatchIntervalSeconds = agentSection.GetValue("ActivityBatchIntervalSeconds", 60);
});

// 5. Host Runtime Worker
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
await host.RunAsync();
