using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RemoteWork.Agent.Configuration;
using RemoteWork.Agent.Core.Models;
using RemoteWork.Agent;

var builder = Host.CreateApplicationBuilder(args);

builder.Services
    .AddOptions<AgentOptions>()
    .Bind(builder.Configuration.GetSection("Agent"))
    .ValidateOnStart();

builder.Services.AddSingleton<AgentRuntimeState>();

builder.Services.AddHostedService<Worker>();

var host = builder.Build();

await host.RunAsync();