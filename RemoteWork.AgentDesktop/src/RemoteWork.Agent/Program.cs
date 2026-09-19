using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RemoteWork.Agent.Configuration;
using RemoteWork.Agent.Core.Models;
using RemoteWork.Agent;

using RemoteWork.Agent.Collectors.Device;
using RemoteWork.Agent.Core.Interfaces;
using RemoteWork.Agent.Platform.Windows;
using RemoteWork.Agent.Storage;

using RemoteWork.Agent.Collectors.Session;

var builder = Host.CreateApplicationBuilder(args);

builder.Services
    .AddOptions<AgentOptions>()
    .Bind(builder.Configuration.GetSection("Agent"))
    .ValidateOnStart();

builder.Services.AddSingleton<AgentRuntimeState>();

builder.Services.AddSingleton<IDeviceIdentityStore, DeviceIdentityStore>();

builder.Services.AddSingleton<IDeviceInfoProvider, WindowsDeviceInfoProvider>();

builder.Services.AddSingleton<DeviceCollector>();

builder.Services.AddSingleton<ISessionCollector, SessionCollector>();

builder.Services.AddHostedService<Worker>();



var host = builder.Build();

await host.RunAsync();