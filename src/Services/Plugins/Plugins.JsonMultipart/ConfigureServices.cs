using LayeredTemplate.Plugins.JsonMultipart.Options;
using Microsoft.Extensions.DependencyInjection;

namespace LayeredTemplate.Plugins.JsonMultipart;

public static class ConfigureServices
{
    /// <summary>
    /// Registers the OpenAPI operation transformer that rewrites bodies of endpoints whose
    /// request DTOs use <c>[FromJson]</c> + <c>IFormFile</c> into proper <c>multipart/form-data</c>
    /// schemas (with <c>contentType: application/json</c> encoding hints on the JSON-typed parts).
    /// </summary>
    /// <remarks>
    /// Binding itself is performed by <c>IJsonMultipartRequest&lt;TSelf&gt;</c> on the DTO and does
    /// not need DI registration — Minimal API discovers <c>BindAsync</c> via the interface.
    /// </remarks>
    public static IServiceCollection AddPluginJsonMultipart(this IServiceCollection services)
    {
        services.ConfigureOptions<ConfigureOpenApiOptions>();
        return services;
    }
}
