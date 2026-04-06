using LayeredTemplate.Plugins.JsonMultipart.Integrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace LayeredTemplate.Plugins.JsonMultipart.Options;

internal class ConfigureSwaggerGenOptions : IConfigureOptions<SwaggerGenOptions>
{
    public void Configure(SwaggerGenOptions options)
    {
        options.OperationFilter<MultiPartJsonOperationFilter>();
    }
}