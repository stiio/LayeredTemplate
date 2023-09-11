using LayeredTemplate.Shared.HostedServices;
using LayeredTemplate.Shared.Interfaces;
using LayeredTemplate.Shared.Options;
using LayeredTemplate.Shared.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.Internal;

namespace LayeredTemplate.Shared;

public static class ConfigureServices
{
    public static void RegisterSharedOptions(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<CognitoSettings>(configuration.GetSection(nameof(CognitoSettings)));
        services.Configure<SmtpSettings>(configuration.GetSection(nameof(SmtpSettings)));
    }

    public static IServiceCollection AddStartupRunner(this IServiceCollection services)
    {
        return services
            .AddSingleton<IHostApplicationLifetime, ApplicationLifetime>()
            .AddScoped<IStartupRunner, StartupRunner>()
            .AddHostedService<StartupRunnerHostedService>();
    }

    public static IServiceCollection AddStartupTask<T>(this IServiceCollection services)
        where T : class, IStartupTask
        => services
            .AddScoped<T>()
            .AddTransient<IStartupTask, T>(sp => sp.GetRequiredService<T>());
}