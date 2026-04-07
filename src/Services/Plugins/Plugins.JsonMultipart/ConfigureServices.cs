using LayeredTemplate.Plugins.JsonMultipart.Options;
using Microsoft.Extensions.DependencyInjection;

namespace LayeredTemplate.Plugins.JsonMultipart;

public static class ConfigureServices
{
    public static IServiceCollection AddPluginJsonMultipart(this IServiceCollection services)
    {
        services.ConfigureOptions<ConfigureMvcOptions>();
        services.ConfigureOptions<ConfigureOpenApiOptions>();

        return services;
    }
}