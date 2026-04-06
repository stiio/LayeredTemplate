using LayeredTemplate.Plugins.StartupRunner.HostedServices;
using LayeredTemplate.Plugins.StartupRunner.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.Internal;

namespace LayeredTemplate.Plugins.StartupRunner;

public static class ConfigureServices
{
    public static IServiceCollection AddPluginStartupRunner(this IServiceCollection services)
    {
        return services
            .AddSingleton<IHostApplicationLifetime, ApplicationLifetime>()
            .AddScoped<IStartupRunner, Impl.StartupRunner>()
            .AddHostedService<StartupRunnerHostedService>();
    }

    public static IServiceCollection AddStartupTask<T>(this IServiceCollection services)
        where T : class, IStartupTask
        => services
            .AddScoped<T>()
            .AddTransient<IStartupTask, T>(sp => sp.GetRequiredService<T>());
}