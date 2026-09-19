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

using RemoteWork.Agent.Collectors.Activity;
using RemoteWork.Agent.Collectors.Activity.Idle;
using RemoteWork.Agent.Platform.Windows.Input;

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

builder.Services.AddSingleton<WindowsInputActivityProvider>();

builder.Services.AddSingleton<IdleActivityCollector>();

builder.Services.AddSingleton<IActivityCollector, ActivityCollector>();



var host = builder.Build();

await host.RunAsync();