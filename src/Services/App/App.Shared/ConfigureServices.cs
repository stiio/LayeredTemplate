using LayeredTemplate.App.Shared.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LayeredTemplate.App.Shared;

public static class ConfigureServices
{
    public static void AddPluginOptions(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<SmtpSettings>(configuration.GetSection(nameof(SmtpSettings)));
        services.Configure<AppSettings>(configuration.GetSection(nameof(AppSettings)));
    }
}