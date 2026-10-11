using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NeX_Drop.Service.Monitors;
using NeX_Drop.Service.Hooks;
using NeX_Drop.Service.Registry;

var host = Host.CreateDefaultBuilder(args)
    .UseWindowsService(options => options.ServiceName = "NeX_Drop_KillSwitch")
    .ConfigureServices((_, services) =>
    {
        services.AddHostedService<KillSwitchMonitor>();
        services.AddHostedService<KeyboardHookService>();
        services.AddHostedService<RegistryLockdownService>();
        services.AddHostedService<ShellWatchdogService>();
    })
    .Build();

await host.RunAsync();
