using System.Reflection;
using LayeredTemplate.Shared.Options;
using LayeredTemplate.Web.ConfigureOptions;

namespace LayeredTemplate.Web.Extensions;

public static class PolymorphismExtensions
{
    public static void AddPolymorphismSupport(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<JsonPolymorphismSettings>(configuration.GetSection(nameof(JsonPolymorphismSettings)));
        services.Configure<JsonPolymorphismSettings>(opts =>
        {
            opts.Assemblies = opts.AssemblyNames.Select(Assembly.Load).ToArray();
        });

        services.ConfigureOptions<ConfigurePolymorphismOptions>();
    }
}