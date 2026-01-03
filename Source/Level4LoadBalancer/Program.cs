using Level4LoadBalancer;
using Level4LoadBalancer.Configuration;
using Level4LoadBalancer.LoadBalancingStrategy;
using Level4LoadBalancer.Services;
using Level4LoadBalancer.TcpAbstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

builder.Configuration
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

builder.Services.Configure<List<BackendServer>>(
    builder.Configuration.GetSection(
        key: "BackendServers"));

builder.Services.Configure<LoadBalancerSettings>(
    builder.Configuration.GetSection(
        key: nameof(LoadBalancerSettings)));

builder.Services.Configure<HealthcheckSettings>(
    builder.Configuration.GetSection(
        key: nameof(HealthcheckSettings)));

builder.Services.AddSingleton<BackendServerRegister>();
builder.Services.AddSingleton<IBackendServerRegister>(x => x.GetRequiredService<BackendServerRegister>());
builder.Services.AddSingleton<IHealthyBackendServerRegister>(x => x.GetRequiredService<BackendServerRegister>());

builder.Services.AddSingleton<ITcpClientFactory, TcpClientFactory>();
builder.Services.AddSingleton<ITcpListenerFactory, TcpListenerFactory>();

builder.Services.AddSingleton<IStreamCopier, StreamCopier>();
builder.Services.AddSingleton<ILoadBalancingStrategy, RandomLoadBalancingStrategy>();

builder.Services.AddHostedService<LoadBalancerService>();
builder.Services.AddHostedService<BackendHealthcheckService>();

var host = builder.Build();

await host.RunAsync();
