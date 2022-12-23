using LayeredTemplate.Shared.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LayeredTemplate.Shared;

public static class ConfigureServices
{
    public static void RegisterSharedOptions(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<CognitoSettings>(configuration.GetSection(nameof(CognitoSettings)));
    }
}